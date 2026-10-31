// SPDX-License-Identifier: MPL-2.0
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Grelecy.Diagnostics;

/// <summary>Explicit exports keep worker-only identifiers and native error text out of shared reports.</summary>
public static class GpuTestReportFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private static readonly HashSet<string> Codes = new(StringComparer.Ordinal)
    {
        "adapter_changed", "adapter_not_found", "dxgi_unavailable", "software_adapter",
        "d3d11_created", "d3d11_unavailable", "d3d11_not_ready", "compute_verified", "compute_failed",
        "shader_compiler_unavailable", "shader_compile_failed", "readback_mismatch", "gpu_query_timeout",
        "gpu_timing_measured", "software_timing_omitted", "gpu_clock_disjoint", "gpu_timing_invalid",
        "gpu_timing_unavailable", "cuda_driver_ready", "cuda_driver_not_found", "cuda_not_nvidia",
        "cuda_software_adapter", "cuda_device_unmatched", "cuda_initialization_failed", "cuda_no_device",
        "cuda_capability_unavailable", "cancelled", "timed_out", "worker_timeout", "worker_cancelled",
        "worker_failed", "worker_unavailable", "worker_invalid_result", "invalid_request", "not_run",
        "worker_invalid_response", "worker_start_failed", "adapter_not_selected", "no_adapters", "adapters_found",
        "information_unavailable", "measurement_invalid"
    };

    public static string ToPlainText(GpuTestReport report, UiLanguage language, GpuTestReportOptions options) =>
        Render(report, language, options, markdown: false);

    public static string ToMarkdown(GpuTestReport report, UiLanguage language, GpuTestReportOptions options) =>
        Render(report, language, options, markdown: true);

    public static string ToJson(GpuTestReport report, GpuTestReportOptions options)
    {
        var samples = ValidSamples(report);
        var timing = new Dictionary<string, object?>
        {
            ["status"] = StatusCode(TimingStatus(report, samples)),
            ["code"] = TimingCode(report, samples)
        };
        if (MatchesProtocol(report.Timing))
        {
            timing["scenario"] = GpuTestProtocol.Scenario;
            timing["elementCount"] = GpuTestProtocol.ElementCount;
            timing["iterations"] = GpuTestProtocol.Iterations;
            timing["warmupCount"] = GpuTestProtocol.WarmupCount;
            timing["plannedSampleCount"] = GpuTestProtocol.SampleCount;
        }
        if (samples is not null)
            timing["measurements"] = new
            {
                unit = "ms",
                clock = "gpu_timestamp_query",
                samples = report.Timing.SamplesMilliseconds.ToArray(),
                sampleCount = samples.Length,
                minimum = samples[0],
                median = samples[samples.Length / 2],
                maximum = samples[^1]
            };
        var cuda = new Dictionary<string, object?>
        {
            ["status"] = StatusCode(report.Cuda.Status),
            ["code"] = SafeCode(report.Cuda.Code),
            ["scope"] = "driver_api_probe_no_ai_workload"
        };
        if (options.IncludeGraphics && report.Cuda.Status == GpuTestStatus.Passed)
        {
            if (ValidCudaVersion(report.Cuda.DriverApiVersion))
                cuda["driverApiVersion"] = report.Cuda.DriverApiVersion;
            if (ValidComputeCapability(report.Cuda))
                cuda["computeCapability"] = new { major = report.Cuda.ComputeMajor, minor = report.Cuda.ComputeMinor };
        }
        var data = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["reportType"] = "automatic_gpu_tests",
            ["testVersion"] = GpuTestProtocol.Version,
            ["application"] = new { name = "GRELECY Diagnostics", version = ReportFormatter.ApplicationVersion },
            ["testedAt"] = report.TestedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["execution"] = ExecutionCode(report.Adapter),
            ["direct3D11"] = CheckData(report.Direct3D11, includeFeatureLevel: true),
            ["computeShader"] = CheckData(report.ComputeShader, includeFeatureLevel: false),
            ["cuda"] = cuda,
            ["timing"] = timing,
            ["limitations"] = new[]
            {
                "success_applies_only_to_named_tests",
                "synthetic_timing_is_not_game_fps_or_ai_throughput",
                "cuda_driver_api_version_is_not_installed_toolkit_version",
                "no_game_or_ai_model_was_executed"
            }
        };
        if (options.IncludeGraphics)
        {
            if (report.Adapter is { } adapter)
            {
                var graphics = new Dictionary<string, object?>
                {
                    ["name"] = SafeText.Normalize(adapter.Name),
                    ["windowsDriverVersion"] = SafeDriverVersion(adapter.DriverVersion),
                    ["dedicatedVideoMemoryBytes"] = adapter.DedicatedVideoMemoryBytes
                };
                if (options.IncludeHardwareIds && ValidPci(adapter))
                    graphics["hardware"] = new
                    {
                        vendorId = adapter.VendorId.ToString("X4", CultureInfo.InvariantCulture),
                        deviceId = adapter.DeviceId.ToString("X4", CultureInfo.InvariantCulture)
                    };
                data["graphics"] = graphics;
            }
            else data["graphics"] = null;
        }
        return JsonSerializer.Serialize(data, JsonOptions) + "\n";
    }

    private static Dictionary<string, object?> CheckData(GpuCheckResult check, bool includeFeatureLevel)
    {
        var data = new Dictionary<string, object?>
        {
            ["status"] = StatusCode(check.Status),
            ["code"] = SafeCode(check.Code)
        };
        if (includeFeatureLevel && check.Status == GpuTestStatus.Passed && ValidFeatureLevel(check.FeatureLevel))
            data["featureLevel"] = check.FeatureLevel;
        return data;
    }

    private static string Render(GpuTestReport report, UiLanguage language, GpuTestReportOptions options, bool markdown)
    {
        string T(string key) => LocalizedText.Get("gpu." + key, language);
        string Value(string? value)
        {
            var normalized = SafeText.Normalize(value);
            if (normalized.Length == 0) return LocalizedText.Get("report.informationUnavailable", language);
            if (!markdown) return normalized;
            var escaped = new StringBuilder();
            foreach (var c in normalized)
            {
                if ((c >= '!' && c <= '/') || (c >= ':' && c <= '@') || (c >= '[' && c <= '`') || (c >= '{' && c <= '~')) escaped.Append('\\');
                escaped.Append(c);
            }
            return escaped.ToString();
        }
        var output = new StringBuilder();
        void Field(string label, string? value) => output.AppendLine($"{(markdown ? "- " : "")}{label}: {Value(value)}");
        void Check(string label, GpuTestStatus status, string code)
        {
            Field(label, T(StatusKey(status)));
            Field(T("code"), code);
        }

        output.AppendLine((markdown ? "# " : "") + T("title")).AppendLine();
        output.AppendLine("GRELECY Diagnostics " + ReportFormatter.ApplicationVersion);
        Field(LocalizedText.Get("report.collectedAt", language).Trim().TrimEnd(':'),
            report.TestedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
        output.AppendLine(T("limitations")).AppendLine();
        Field(T("execution"), report.Adapter is null
            ? LocalizedText.Get("report.informationUnavailable", language)
            : T(report.Adapter.IsSoftware ? "software" : "hardware"));
        if (options.IncludeGraphics && report.Adapter is { } adapter)
        {
            Field(T("adapter"), adapter.Name);
            Field(LocalizedText.Get("report.windowsDriverVersion", language), SafeDriverVersion(adapter.DriverVersion));
            Field(T("memory"), (adapter.DedicatedVideoMemoryBytes / 1048576.0).ToString("0.##", CultureInfo.InvariantCulture));
            if (options.IncludeHardwareIds && ValidPci(adapter))
            {
                Field(LocalizedText.Get("report.vendorIdPci", language), adapter.VendorId.ToString("X4", CultureInfo.InvariantCulture));
                Field(LocalizedText.Get("report.deviceIdPci", language), adapter.DeviceId.ToString("X4", CultureInfo.InvariantCulture));
            }
        }
        output.AppendLine();
        Check(T("direct3D11"), report.Direct3D11.Status, SafeCode(report.Direct3D11.Code));
        if (report.Direct3D11.Status == GpuTestStatus.Passed && ValidFeatureLevel(report.Direct3D11.FeatureLevel))
            Field(T("featureLevel"), report.Direct3D11.FeatureLevel);
        Check(T("compute"), report.ComputeShader.Status, SafeCode(report.ComputeShader.Code));
        Check(T("cuda"), report.Cuda.Status, SafeCode(report.Cuda.Code));
        if (options.IncludeGraphics && report.Cuda.Status == GpuTestStatus.Passed)
        {
            if (ValidCudaVersion(report.Cuda.DriverApiVersion))
            {
                var version = report.Cuda.DriverApiVersion!.Value;
                Field(T("cudaDriver"), FormattableString.Invariant($"{version / 1000}.{version % 1000 / 10}"));
            }
            if (ValidComputeCapability(report.Cuda))
                Field(T("computeCapability"), FormattableString.Invariant($"{report.Cuda.ComputeMajor}.{report.Cuda.ComputeMinor}"));
        }
        output.AppendLine();
        var samples = ValidSamples(report);
        Check(T("timing"), TimingStatus(report, samples), TimingCode(report, samples));
        if (MatchesProtocol(report.Timing))
        {
            Field(T("scenario"), GpuTestProtocol.Scenario);
            Field(T("protocol"), FormattableString.Invariant(
                $"{GpuTestProtocol.ElementCount} × {GpuTestProtocol.Iterations}; {GpuTestProtocol.WarmupCount}"));
        }
        if (samples is not null)
        {
            Field(T("samples"), string.Join(", ", report.Timing.SamplesMilliseconds.Select(sample => sample.ToString("G17", CultureInfo.InvariantCulture))));
            Field(T("minimum"), samples[0].ToString("G17", CultureInfo.InvariantCulture));
            Field(T("median"), samples[samples.Length / 2].ToString("G17", CultureInfo.InvariantCulture));
            Field(T("maximum"), samples[^1].ToString("G17", CultureInfo.InvariantCulture));
        }
        else output.AppendLine(T(report.Adapter?.IsSoftware == true ? "softwareNote" : "metricsUnavailable"));
        return output.ToString();
    }

    private static string SafeCode(string? code) => code is not null && Codes.Contains(code) ? code : "information_unavailable";
    private static bool ValidFeatureLevel(string? level) => level is "11_0" or "11_1";
    private static bool ValidPci(GpuTestAdapter adapter) => adapter.VendorId is > 0 and <= ushort.MaxValue
        && adapter.DeviceId is > 0 and <= ushort.MaxValue && !adapter.IsSoftware;
    private static bool ValidCudaVersion(int? version) => version is >= 1000 and <= 99990 && version % 10 == 0;
    private static bool ValidComputeCapability(CudaProbeResult cuda) => cuda.ComputeMajor is >= 1 and <= 99
        && cuda.ComputeMinor is >= 0 and <= 9;
    private static string? SafeDriverVersion(string? value) => value is { Length: > 0 and <= 48 }
        && value.All(c => char.IsAsciiDigit(c) || c == '.') && value.Any(char.IsAsciiDigit) ? value : null;
    private static string ExecutionCode(GpuTestAdapter? adapter) => adapter is null ? "unknown" : adapter.IsSoftware ? "software" : "hardware";
    private static bool MatchesProtocol(GpuTimingResult timing) => timing.Scenario == GpuTestProtocol.Scenario
        && timing.ElementCount == GpuTestProtocol.ElementCount && timing.Iterations == GpuTestProtocol.Iterations
        && timing.WarmupCount == GpuTestProtocol.WarmupCount;

    private static double[]? ValidSamples(GpuTestReport report)
    {
        if (report.Adapter is not { IsSoftware: false } || report.Direct3D11.Status != GpuTestStatus.Passed
            || !ValidFeatureLevel(report.Direct3D11.FeatureLevel)
            || report.ComputeShader.Status != GpuTestStatus.Passed || report.Timing.Status != GpuTestStatus.Passed
            || !MatchesProtocol(report.Timing) || report.Timing.SamplesMilliseconds is not { Count: GpuTestProtocol.SampleCount }
            || report.Timing.SamplesMilliseconds.Any(sample => !double.IsFinite(sample) || sample <= 0)) return null;
        return report.Timing.SamplesMilliseconds.OrderBy(sample => sample).ToArray();
    }

    private static GpuTestStatus TimingStatus(GpuTestReport report, double[]? samples) => report.Adapter?.IsSoftware == true
        ? GpuTestStatus.NotRun : report.Timing.Status == GpuTestStatus.Passed && samples is null
            ? GpuTestStatus.Inconclusive : report.Timing.Status;
    private static string TimingCode(GpuTestReport report, double[]? samples) => report.Adapter?.IsSoftware == true
        ? "software_timing_omitted" : report.Timing.Status == GpuTestStatus.Passed && samples is null
            ? "measurement_invalid" : SafeCode(report.Timing.Code);

    private static string StatusCode(GpuTestStatus status) => status switch
    {
        GpuTestStatus.Passed => "passed",
        GpuTestStatus.NotAvailable => "not_available",
        GpuTestStatus.NotRun => "not_run",
        GpuTestStatus.Cancelled => "cancelled",
        GpuTestStatus.TimedOut => "timed_out",
        _ => "inconclusive"
    };
    private static string StatusKey(GpuTestStatus status) => status switch
    {
        GpuTestStatus.Passed => "passed",
        GpuTestStatus.NotAvailable => "notAvailable",
        GpuTestStatus.NotRun => "notRun",
        GpuTestStatus.Cancelled => "cancelled",
        GpuTestStatus.TimedOut => "timedOut",
        _ => "inconclusive"
    };
}
