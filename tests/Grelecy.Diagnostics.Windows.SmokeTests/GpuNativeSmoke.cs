// SPDX-License-Identifier: MPL-2.0
using System.Text.Json;
using Grelecy.Diagnostics;
using Grelecy.Diagnostics.Windows;

/// <summary>
/// Windows CI exercises the real D3D11 ABI, HLSL compiler, dispatch and complete
/// readback using Microsoft's software device. This is not a hardware benchmark.
/// </summary>
internal static class GpuNativeSmoke
{
    public static int Run() => RunAsync().GetAwaiter().GetResult();

    private static async Task<int> RunAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("FAIL: the native GPU smoke check requires Windows.");
            return 1;
        }
        try
        {
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(55));
            var report = await new GpuProbeRunner().RunWarpForTestingAsync(deadline.Token);
            Check(report.Adapter is { IsSoftware: true, Index: -1 },
                "WARP is explicitly reported as a software device");
            Check(report.Direct3D11.Status == GpuTestStatus.Passed &&
                report.Direct3D11.FeatureLevel is "11_0" or "11_1",
                "The isolated worker creates an actual D3D11 WARP device");
            Check(report.ComputeShader.Status == GpuTestStatus.Passed &&
                report.ComputeShader.Code == "compute_verified",
                "The compiled uint shader executes and every readback element matches");
            Check(report.Cuda.Status == GpuTestStatus.NotRun,
                "The WARP test does not claim a CUDA or AI execution test");
            Check(report.Timing.Status == GpuTestStatus.NotRun &&
                report.Timing.SamplesMilliseconds.Count == 0,
                "Software execution never produces hardware timing samples");

            var exported = GpuTestReportFormatter.ToJson(report, new GpuTestReportOptions(true, true));
            using var json = JsonDocument.Parse(exported);
            var root = json.RootElement;
            Check(root.GetProperty("execution").GetString() == "software" &&
                root.GetProperty("timing").GetProperty("status").GetString() == "not_run" &&
                !root.GetProperty("timing").TryGetProperty("measurements", out _),
                "The shared JSON preserves software status and omits performance measurements");
            Check(!exported.Contains("SessionId", StringComparison.OrdinalIgnoreCase) &&
                !root.GetProperty("graphics").TryGetProperty("hardware", out _),
                "The WARP export contains neither session identifiers nor PCI hardware claims");
            using var cancelled = new CancellationTokenSource();
            cancelled.Cancel();
            var stopped = await new GpuProbeRunner().RunWarpForTestingAsync(cancelled.Token);
            Check(stopped.ComputeShader.Status == GpuTestStatus.Cancelled && stopped.Timing.SamplesMilliseconds.Count == 0,
                "Cancellation cannot publish a successful result or a measurement");
            var expired = await new GpuProbeRunner(TimeSpan.FromMilliseconds(1)).RunWarpForTestingAsync(deadline.Token);
            Check(expired.ComputeShader.Status == GpuTestStatus.TimedOut && expired.Timing.SamplesMilliseconds.Count == 0,
                "A process deadline stops the worker without exporting partial measurements");
            Console.WriteLine("PASS: native WARP smoke checks completed; physical GPU performance was not tested.");
            return 0;
        }
        catch (SmokeFailure error)
        {
            // Only the fixed assertion description is printed, never a driver exception or machine path.
            Console.Error.WriteLine("FAIL: " + error.Message);
            return 1;
        }
        catch (Exception)
        {
            Console.Error.WriteLine("FAIL: the native WARP smoke check did not complete.");
            return 1;
        }
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new SmokeFailure(description);
        Console.WriteLine("PASS: " + description);
    }

    private sealed class SmokeFailure(string message) : Exception(message);
}
