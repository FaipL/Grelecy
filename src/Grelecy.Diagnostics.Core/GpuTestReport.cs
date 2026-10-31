// SPDX-License-Identifier: MPL-2.0
namespace Grelecy.Diagnostics;

/// <summary>Outcomes refer only to the named test, never to an entire game or AI application.</summary>
public enum GpuTestStatus { Passed, NotAvailable, Inconclusive, NotRun, Cancelled, TimedOut }

/// <summary>Index is a session-local DXGI selection, not a persistent hardware identifier.</summary>
public sealed record GpuTestAdapter(int Index, string Name, uint VendorId, uint DeviceId,
    ulong DedicatedVideoMemoryBytes, bool IsSoftware, string? DriverVersion = null,
    string? SessionId = null);

public sealed record GpuCheckResult(GpuTestStatus Status, string Code, string? FeatureLevel = null);

/// <summary>Driver API availability and the matched device's declared capability; no AI workload is run.</summary>
public sealed record CudaProbeResult(GpuTestStatus Status, string Code, int? DriverApiVersion = null,
    int? ComputeMajor = null, int? ComputeMinor = null);

/// <summary>GPU query durations for a fixed synthetic workload, not frame rate or AI throughput.</summary>
public sealed record GpuTimingResult(GpuTestStatus Status, string Code, string Scenario,
    int ElementCount, int Iterations, int WarmupCount, IReadOnlyList<double> SamplesMilliseconds);

public sealed record GpuTestReport(DateTimeOffset TestedAt, GpuTestAdapter? Adapter,
    GpuCheckResult Direct3D11, GpuCheckResult ComputeShader, CudaProbeResult Cuda,
    GpuTimingResult Timing);

public sealed record GpuTestReportOptions(bool IncludeGraphics = true, bool IncludeHardwareIds = false);

public static class GpuTestProtocol
{
    public const int Version = 1;
    public const string Scenario = "d3d11-uint-mix-v1";
    public const int ElementCount = 262144;
    public const int Iterations = 64;
    public const int WarmupCount = 3;
    public const int SampleCount = 5;

    public static GpuTestReport WithoutResults(GpuTestStatus status, string code, GpuTestAdapter? adapter = null) =>
        new(DateTimeOffset.UtcNow, adapter, new(status, code), new(status, code), new(status, code),
            new(status, code, Scenario, ElementCount, Iterations, WarmupCount, Array.Empty<double>()));
}
