// SPDX-License-Identifier: MPL-2.0
using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Grelecy.Diagnostics.Windows;

public sealed record GpuAdapterScan(IReadOnlyList<GpuTestAdapter> Adapters, GpuTestStatus Status, string Code);

internal sealed record GpuWorkerRequest(int Protocol, string Operation, GpuTestAdapter? Adapter = null);
internal sealed record GpuWorkerResponse(int Protocol, GpuAdapterScan? Scan = null, GpuTestReport? Report = null);

/// <summary>Runs only this application's fixed probes in an isolated, cancellable child process.</summary>
public sealed class GpuProbeRunner
{
    public static readonly TimeSpan MaximumDuration = TimeSpan.FromSeconds(45);
    private readonly TimeSpan duration;
    internal static readonly JsonSerializerOptions WireOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 12,
        Converters = { new JsonStringEnumConverter() },
    };

    public GpuProbeRunner(TimeSpan? maximumDuration = null)
    {
        duration = maximumDuration ?? MaximumDuration;
        if (duration <= TimeSpan.Zero || duration > MaximumDuration)
            throw new ArgumentOutOfRangeException(nameof(maximumDuration));
    }

    public async Task<GpuAdapterScan> EnumerateAsync(CancellationToken cancellationToken)
    {
        try
        {
            var response = await ExecuteAsync(new(GpuTestProtocol.Version, "enumerate"), cancellationToken).ConfigureAwait(false);
            if (response.Scan is not { } scan || scan.Adapters is null || scan.Adapters.Count > 64 ||
                scan.Adapters.Any(adapter => !ValidAdapter(adapter)))
                return new([], GpuTestStatus.Inconclusive, "worker_invalid_response");
            return scan;
        }
        catch (ProbeException error) { return new([], error.Status, error.Code); }
    }

    public Task<GpuTestReport> RunAsync(GpuTestAdapter adapter, CancellationToken cancellationToken) =>
        RunAsync(adapter, "run", cancellationToken);

    /// <summary>CI only: verifies the native code on WARP; never substitutes for a user's hardware.</summary>
    public Task<GpuTestReport> RunWarpForTestingAsync(CancellationToken cancellationToken) =>
        RunAsync(new(-1, "WARP", 0, 0, 0, true), "warp", cancellationToken);

    private async Task<GpuTestReport> RunAsync(GpuTestAdapter adapter, string operation, CancellationToken cancellationToken)
    {
        try
        {
            if (operation != "warp" && (!ValidAdapter(adapter) || adapter.IsSoftware))
                return GpuTestProtocol.WithoutResults(GpuTestStatus.NotRun, "adapter_not_selected");
            var response = await ExecuteAsync(new(GpuTestProtocol.Version, operation, adapter), cancellationToken).ConfigureAwait(false);
            if (response.Report is not { } report || report.Direct3D11 is null || report.ComputeShader is null ||
                report.Cuda is null || report.Timing is null || report.Timing.SamplesMilliseconds is null ||
                report.Timing.SamplesMilliseconds.Count > GpuTestProtocol.SampleCount ||
                (report.Adapter is not null && operation != "warp" && report.Adapter != adapter) ||
                (operation == "warp" && report.Adapter is { IsSoftware: false }))
                return GpuTestProtocol.WithoutResults(GpuTestStatus.Inconclusive, "worker_invalid_response", adapter);
            return report;
        }
        catch (ProbeException error) { return GpuTestProtocol.WithoutResults(error.Status, error.Code, adapter); }
    }

    private static bool ValidAdapter(GpuTestAdapter? adapter) => adapter is not null && adapter.Index is >= 0 and < 64 &&
        !string.IsNullOrWhiteSpace(adapter.Name) && adapter.Name.Length <= 256 &&
        (adapter.DriverVersion is null || adapter.DriverVersion.Length <= 64) &&
        adapter.SessionId is { Length: 16 } && adapter.SessionId.All(Uri.IsHexDigit);

    private async Task<GpuWorkerResponse> ExecuteAsync(GpuWorkerRequest request, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested) throw new ProbeException(GpuTestStatus.Cancelled, "worker_cancelled");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(duration);
        using var process = new Process();
        var started = false;
        Task<string>? output = null;
        Task<string>? errors = null;
        CancellationTokenRegistration stopOnCancellation = default;
        try
        {
            process.StartInfo = CreateStartInfo();
            started = process.Start();
            if (!started) throw new ProbeException(GpuTestStatus.Inconclusive, "worker_start_failed");
            // Cancellation can occur while the UI message loop is closing. Stop the child
            // immediately rather than relying on a continuation scheduled back to that loop.
            stopOnCancellation = timeout.Token.Register(() =>
            {
                try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
                catch (Exception error) when (error is Win32Exception or InvalidOperationException or NotSupportedException) { }
            });
            output = ReadBoundedAsync(process.StandardOutput, 65536, timeout.Token);
            errors = ReadBoundedAsync(process.StandardError, 8192, timeout.Token);
            var input = JsonSerializer.Serialize(request, WireOptions);
            await process.StandardInput.WriteAsync(input.AsMemory(), timeout.Token).ConfigureAwait(false);
            process.StandardInput.Close();
            await Task.WhenAll(output, errors, process.WaitForExitAsync(timeout.Token)).WaitAsync(timeout.Token).ConfigureAwait(false);
            timeout.Token.ThrowIfCancellationRequested();
            if (process.ExitCode != 0) throw new ProbeException(GpuTestStatus.Inconclusive, "worker_failed");
            var response = JsonSerializer.Deserialize<GpuWorkerResponse>(await output.ConfigureAwait(false), WireOptions);
            if (response?.Protocol != GpuTestProtocol.Version)
                throw new ProbeException(GpuTestStatus.Inconclusive, "worker_invalid_response");
            return response;
        }
        catch (OperationCanceledException)
        {
            throw cancellationToken.IsCancellationRequested
                ? new ProbeException(GpuTestStatus.Cancelled, "worker_cancelled")
                : new ProbeException(GpuTestStatus.TimedOut, "worker_timeout");
        }
        catch (Exception error) when (error is IOException or JsonException or Win32Exception or InvalidOperationException
            or UnauthorizedAccessException or System.Security.SecurityException or NotSupportedException)
        {
            // System exception messages can contain paths and account names. They never cross this boundary.
            if (timeout.IsCancellationRequested)
                throw cancellationToken.IsCancellationRequested
                    ? new ProbeException(GpuTestStatus.Cancelled, "worker_cancelled")
                    : new ProbeException(GpuTestStatus.TimedOut, "worker_timeout");
            throw new ProbeException(GpuTestStatus.Inconclusive, "worker_failed");
        }
        finally
        {
            await stopOnCancellation.DisposeAsync().ConfigureAwait(false);
            if (started)
            {
                try
                {
                    if (!process.HasExited) process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(3)).ConfigureAwait(false);
                }
                catch (Exception error) when (error is Win32Exception or InvalidOperationException or TimeoutException
                    or NotSupportedException) { /* The parent remains responsive if Windows cannot reap the child. */ }
            }
            // Observe stream tasks even after cancellation; never retain their raw text in a report.
            if (output is not null) _ = output.ContinueWith(task => _ = task.Exception,
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
            if (errors is not null) _ = errors.ContinueWith(task => _ = task.Exception,
                CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted, TaskScheduler.Default);
        }
    }

    private static ProcessStartInfo CreateStartInfo()
    {
        // Assembly.Location is empty in the portable single-file build. Only then use the running apphost.
        // In smoke tests the process is the test runner, while this assembly still identifies the application.
        var assemblyPath = GetAssemblyPath();
        var appHost = string.IsNullOrEmpty(assemblyPath) ? Environment.ProcessPath : Path.ChangeExtension(assemblyPath, ".exe");
        var start = new ProcessStartInfo
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
            StandardInputEncoding = new UTF8Encoding(false), StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (!string.IsNullOrEmpty(appHost) && File.Exists(appHost)) start.FileName = appHost;
        else
        {
            // Development builds launched with dotnet use the known host location, not an arbitrary command.
            var host = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
            if (string.IsNullOrEmpty(host) && string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath),
                "dotnet", StringComparison.OrdinalIgnoreCase)) host = Environment.ProcessPath;
            if (string.IsNullOrEmpty(host) || !Path.IsPathFullyQualified(host) || !File.Exists(host) ||
                string.IsNullOrEmpty(assemblyPath)) throw new InvalidOperationException();
            start.FileName = host;
            start.ArgumentList.Add(assemblyPath);
        }
        start.ArgumentList.Add("--gpu-worker");
        return start;
    }

    [System.Diagnostics.CodeAnalysis.UnconditionalSuppressMessage("SingleFile", "IL3000",
        Justification = "An empty Assembly.Location explicitly selects the single-file apphost.")]
    private static string GetAssemblyPath() => typeof(GpuProbeRunner).Assembly.Location;

    internal static async Task<string> ReadBoundedAsync(StreamReader reader, int maximum, CancellationToken token)
    {
        var result = new StringBuilder();
        var buffer = new char[2048];
        while (true)
        {
            var count = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false);
            if (count == 0) return result.ToString();
            if (result.Length + count > maximum) throw new InvalidDataException();
            result.Append(buffer, 0, count);
        }
    }

    private sealed class ProbeException(GpuTestStatus status, string code) : Exception
    {
        public GpuTestStatus Status { get; } = status;
        public string Code { get; } = code;
    }
}

internal static class GpuProbeWorker
{
    internal static int Run()
    {
        // The worker has no UI and accepts only the fixed operations below, not commands or shader source.
        // A second deadline also bounds its life if the parent terminates unexpectedly.
        using var watchdog = new System.Threading.Timer(_ => Environment.Exit(4), null,
            GpuProbeRunner.MaximumDuration, Timeout.InfiniteTimeSpan);
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var input = GpuProbeRunner.ReadBoundedAsync(new StreamReader(Console.OpenStandardInput(), Encoding.UTF8),
                4096, deadline.Token).GetAwaiter().GetResult();
            var request = JsonSerializer.Deserialize<GpuWorkerRequest>(input, GpuProbeRunner.WireOptions);
            if (request?.Protocol != GpuTestProtocol.Version) return 2;
            GpuWorkerResponse response;
            if (request.Operation == "enumerate")
            {
                var adapters = NativeGpuProbe.EnumerateAdapters();
                response = new(GpuTestProtocol.Version, new(adapters, adapters.Count == 0
                    ? GpuTestStatus.NotAvailable : GpuTestStatus.Passed, adapters.Count == 0 ? "no_adapters" : "adapters_found"));
            }
            else if (request.Operation is "run" or "warp" && request.Adapter is { } adapter)
                response = new(GpuTestProtocol.Version, Report: NativeGpuProbe.Run(adapter, request.Operation == "warp"));
            else return 2;
            // Explicit standard handles work for both the WinExe apphost and the packaged executable.
            using var writer = new StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false));
            writer.Write(JsonSerializer.Serialize(response, GpuProbeRunner.WireOptions));
            return 0;
        }
        catch (Exception) { return 3; }
    }
}
