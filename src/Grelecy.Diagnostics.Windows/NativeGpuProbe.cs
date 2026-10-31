// SPDX-License-Identifier: MPL-2.0
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using static Grelecy.Diagnostics.Windows.NativeGpuInterop;

namespace Grelecy.Diagnostics.Windows;

/// <summary>
/// Bounded, local probes for the isolated worker process. Run only after an
/// explicit user action. A successful probe describes this workload and API;
/// it does not certify a game, an AI framework, or long-term GPU stability.
/// </summary>
public static class NativeGpuProbe
{
    private const int AdapterLimit = 32;
    private const int CudaDeviceLimit = 32;
    private static readonly TimeSpan QueryDeadline = TimeSpan.FromSeconds(5);

    // d3d11-uint-mix-v1: 262144 independent uint outputs (1 MiB), 64 rounds
    // of the exact unsigned recurrence below, 256 threads/group, 1024 groups.
    // PassSeed is 1..3 for warm-up and 4..8 for measured passes. It changes the
    // output each time, so stale data cannot pass readback validation. Shader
    // compilation, constant upload, copy, readback and CPU verification are
    // outside the GPU timestamp interval. Changing this kernel or its compile
    // flags requires a new scenario identifier. This is not a FLOPS/FPS test.
    private const string ShaderSource = """
        cbuffer Parameters : register(b0)
        {
            uint PassSeed;
            uint3 Reserved;
        };
        RWStructuredBuffer<uint> Results : register(u0);
        [numthreads(256, 1, 1)]
        void main(uint3 id : SV_DispatchThreadID)
        {
            uint x = (id.x + 1u) ^ (PassSeed * 0x9E3779B9u);
            [loop]
            for (uint iteration = 0u; iteration < 64u; ++iteration)
            {
                x ^= x << 13u;
                x ^= x >> 17u;
                x ^= x << 5u;
                x += 0x9E3779B9u + iteration;
            }
            Results[id.x] = x;
        }
        """;

    public static IReadOnlyList<GpuTestAdapter> EnumerateAdapters()
    {
        if (!OperatingSystem.IsWindows()) return Array.Empty<GpuTestAdapter>();
        using var factory = CreateFactory();
        var enumerate = Method<EnumAdapters>(factory.Pointer, 12);
        var adapters = new List<GpuTestAdapter>();
        for (var index = 0; index < AdapterLimit; index++)
        {
            var hr = enumerate(factory.Pointer, (uint)index, out var pointer);
            using var adapter = new ComObject(pointer);
            if (hr == NotFound) break;
            Require(hr, "dxgi_unavailable");
            if (pointer == 0) throw new ProbeFailure("dxgi_unavailable");
            var item = DescribeAdapter(pointer, index, out _);
            if (!item.IsSoftware) adapters.Add(item);
        }
        return adapters.ToArray();
    }

    public static GpuTestReport Run(GpuTestAdapter selected, bool useWarp = false)
    {
        ArgumentNullException.ThrowIfNull(selected);
        if (!OperatingSystem.IsWindows())
            return GpuTestProtocol.WithoutResults(GpuTestStatus.NotAvailable, "dxgi_unavailable", selected);

        // WARP is available only through the explicit test hook. Hardware
        // failure never falls back to software or to the default adapter.
        if (useWarp)
            selected = new GpuTestAdapter(-1, "Microsoft WARP (software)", 0, 0, 0, true);
        else if (selected.IsSoftware)
            return GpuTestProtocol.WithoutResults(GpuTestStatus.NotRun, "software_adapter", selected);

        var testedAt = DateTimeOffset.UtcNow;
        ComObject? adapter = null;
        long adapterLuid = 0;
        try
        {
            if (!useWarp) adapter = OpenSelectedAdapter(selected, out adapterLuid);
        }
        catch (ProbeFailure error)
        {
            return GpuTestProtocol.WithoutResults(error.Status, error.Code, selected);
        }
        catch (Exception error) when (IsNativeFailure(error))
        {
            return GpuTestProtocol.WithoutResults(GpuTestStatus.NotAvailable, "dxgi_unavailable", selected);
        }

        using (adapter)
        {
            var cuda = ProbeCuda(selected, adapterLuid);
            nint devicePointer = 0;
            nint contextPointer = 0;
            uint featureLevel = 0;
            var deviceCreated = false;
            try
            {
                // An explicit IDXGIAdapter requires UNKNOWN (0), not HARDWARE.
                // WARP (5) requires a null adapter. Flags 0 retain Windows TDR.
                uint[] levels = [0xB100, 0xB000];
                var hr = D3D11CreateDevice(adapter?.Pointer ?? 0, useWarp ? 5u : 0u,
                    0, 0, levels, (uint)levels.Length, 7, out devicePointer,
                    out featureLevel, out contextPointer);
                if (hr == InvalidArgument)
                {
                    Release(ref contextPointer);
                    Release(ref devicePointer);
                    // A runtime without 11_1 rejects that entry instead of
                    // selecting 11_0. Retry the same adapter, never a fallback GPU.
                    levels = [0xB000];
                    hr = D3D11CreateDevice(adapter?.Pointer ?? 0, useWarp ? 5u : 0u,
                        0, 0, levels, 1, 7, out devicePointer,
                        out featureLevel, out contextPointer);
                }
                deviceCreated = hr >= 0 && devicePointer != 0 && contextPointer != 0 &&
                    featureLevel is 0xB000 or 0xB100;
            }
            catch (Exception error) when (IsNativeFailure(error))
            {
                // Native diagnostic text may contain machine paths; retain only a fixed code.
            }

            using var device = new ComObject(devicePointer);
            using var context = new ComObject(contextPointer);
            if (!deviceCreated)
                return new GpuTestReport(testedAt, selected,
                    new(GpuTestStatus.NotAvailable, "d3d11_unavailable"),
                    new(GpuTestStatus.NotRun, "d3d11_not_ready"), cuda,
                    Timing(GpuTestStatus.NotRun, "d3d11_not_ready"));

            var direct3D = new GpuCheckResult(GpuTestStatus.Passed, "d3d11_created",
                featureLevel == 0xB100 ? "11_1" : "11_0");
            try
            {
                var (compute, timing) = RunCompute(device.Pointer, context.Pointer, useWarp);
                return new GpuTestReport(testedAt, selected, direct3D, compute, cuda, timing);
            }
            catch (ProbeFailure error)
            {
                return new GpuTestReport(testedAt, selected, direct3D,
                    new(error.Status, error.Code), cuda,
                    Timing(useWarp ? GpuTestStatus.NotRun : error.Status,
                        useWarp ? "software_timing_omitted" : error.Code));
            }
            catch (Exception error) when (IsNativeFailure(error))
            {
                return new GpuTestReport(testedAt, selected, direct3D,
                    new(GpuTestStatus.Inconclusive, "compute_failed"), cuda,
                    Timing(useWarp ? GpuTestStatus.NotRun : GpuTestStatus.Inconclusive,
                        useWarp ? "software_timing_omitted" : "compute_failed"));
            }
        }
    }

    private static ComObject CreateFactory()
    {
        ValidateNativeLayouts();
        var hr = CreateDXGIFactory1(in Factory1Id, out var pointer);
        return Own(hr, pointer, "dxgi_unavailable");
    }

    private static ComObject OpenSelectedAdapter(GpuTestAdapter selected, out long luid)
    {
        luid = 0;
        if (selected.Index is < 0 or >= AdapterLimit)
            throw new ProbeFailure("adapter_not_found", GpuTestStatus.NotAvailable);
        using var factory = CreateFactory();
        var enumerate = Method<EnumAdapters>(factory.Pointer, 12);
        var hr = enumerate(factory.Pointer, (uint)selected.Index, out var pointer);
        var adapter = Own(hr, pointer,
            hr == NotFound ? "adapter_not_found" : "dxgi_unavailable");
        try
        {
            var actual = DescribeAdapter(pointer, selected.Index, out luid);
            // Record equality includes index, model/name, memory, software flag,
            // driver and session-local LUID. Two identical board models cannot
            // be silently substituted after hot-plug or a driver restart.
            if (selected.SessionId is null || actual != selected)
                throw new ProbeFailure("adapter_changed");
            return adapter;
        }
        catch
        {
            adapter.Dispose();
            throw;
        }
    }

    private static GpuTestAdapter DescribeAdapter(nint adapter, int index, out long luid)
    {
        Require(Method<GetAdapterDescription>(adapter, 10)(adapter, out var description), "dxgi_unavailable");
        luid = unchecked((long)(((ulong)(uint)description.LuidHigh << 32) | description.LuidLow));
        string? driver = null;
        // IDXGIDevice queries the UMD version; asking for ID3D11Device here is
        // expressly unsupported by CheckInterfaceSupport. Device creation below
        // remains the actual D3D11 test.
        if (Method<CheckInterfaceSupport>(adapter, 9)(adapter, in DxgiDeviceId, out var version) >= 0 && version != 0)
        {
            var value = unchecked((ulong)version);
            driver = string.Create(CultureInfo.InvariantCulture,
                $"{(value >> 48) & 0xFFFF}.{(value >> 32) & 0xFFFF}.{(value >> 16) & 0xFFFF}.{value & 0xFFFF}");
        }
        return new GpuTestAdapter(index, SafeText.Normalize(description.Description),
            description.VendorId, description.DeviceId, (ulong)description.DedicatedVideoMemory,
            (description.Flags & 2) != 0, driver,
            unchecked((ulong)luid).ToString("X16", CultureInfo.InvariantCulture));
    }

    private static (GpuCheckResult Compute, GpuTimingResult Timing) RunCompute(
        nint device, nint context, bool software)
    {
        ValidateNativeLayouts();
        var setShader = Method<SetComputeShader>(context, 69);
        var setViews = Method<SetUnorderedAccessViews>(context, 68);
        var setConstants = Method<SetConstantBuffers>(context, 71);
        var update = Method<UpdateSubresource>(context, 48);
        var dispatch = Method<Dispatch>(context, 41);
        var copy = Method<CopyResource>(context, 47);
        var flush = Method<ContextAction>(context, 111);
        var begin = Method<QueryBoundary>(context, 27);
        var end = Method<QueryBoundary>(context, 28);
        var clearState = Method<ContextAction>(context, 110);
        try
        {
            using var shader = CompileShader(device);
            using var output = MakeBuffer(device, new BufferDescription
            {
                ByteWidth = GpuTestProtocol.ElementCount * sizeof(uint),
                BindFlags = 0x80, MiscFlags = 0x40, StructureByteStride = sizeof(uint)
            });
            using var staging = MakeBuffer(device, new BufferDescription
            {
                ByteWidth = GpuTestProtocol.ElementCount * sizeof(uint),
                Usage = 3, CpuAccessFlags = 0x20000
            });
            using var parameters = MakeBuffer(device, new BufferDescription { ByteWidth = 16, BindFlags = 4 });
            var viewDescription = new UnorderedAccessDescription
            {
                ViewDimension = 1, ElementCount = GpuTestProtocol.ElementCount
            };
            var viewHr = Method<CreateUnorderedAccessView>(device, 8)(device, output.Pointer,
                in viewDescription, out var viewPointer);
            using var view = Own(viewHr, viewPointer, "compute_failed");
            using var parameterData = new NativeBlock(16);
            Marshal.Copy(new int[4], 0, parameterData.Pointer, 4);
            setShader(context, shader.Pointer, 0, 0);
            setConstants(context, 0, 1, [parameters.Pointer]);
            setViews(context, 0, 1, [view.Pointer], 0);

            void SetSeed(uint seed)
            {
                Marshal.WriteInt32(parameterData.Pointer, unchecked((int)seed));
                update(context, parameters.Pointer, 0, 0, parameterData.Pointer, 0, 0);
            }

            for (var pass = 1; pass <= GpuTestProtocol.WarmupCount; pass++)
            {
                SetSeed((uint)pass);
                dispatch(context, GpuTestProtocol.ElementCount / 256, 1, 1);
            }

            using var queries = software ? null : TimestampQueries.TryCreate(device);
            string? timingFailure = queries is null ? "gpu_timing_unavailable" : null;
            var samples = new List<double>(GpuTestProtocol.SampleCount);
            var readback = new int[GpuTestProtocol.ElementCount];
            // The software hook validates the identical shader/readback path
            // once after warm-up. It neither obtains nor exports GPU timings.
            var sampleCount = software ? 1 : GpuTestProtocol.SampleCount;
            for (var sample = 0; sample < sampleCount; sample++)
            {
                var clock = Stopwatch.StartNew();
                var seed = (uint)(GpuTestProtocol.WarmupCount + sample + 1);
                SetSeed(seed);
                setViews(context, 0, 1, [view.Pointer], 0);
                if (queries is not null)
                {
                    begin(context, queries.Disjoint.Pointer);
                    end(context, queries.Start.Pointer);
                }
                dispatch(context, GpuTestProtocol.ElementCount / 256, 1, 1);
                if (queries is not null)
                {
                    end(context, queries.Stop.Pointer);
                    end(context, queries.Disjoint.Pointer);
                }
                setViews(context, 0, 1, [0], 0);
                copy(context, staging.Pointer, output.Pointer);
                // GetData(DONOTFLUSH) may otherwise wait forever for queued
                // commands. Flush submits work but is not a completion fence.
                flush(context);
                if (queries is not null)
                {
                    var (milliseconds, failure) = queries.Read(context, clock);
                    if (failure is not null) timingFailure ??= failure;
                    else samples.Add(milliseconds);
                }
                ReadAndVerify(context, staging.Pointer, seed, readback, clock);
            }
            var compute = new GpuCheckResult(GpuTestStatus.Passed, "compute_verified");
            if (software) return (compute, Timing(GpuTestStatus.NotRun, "software_timing_omitted"));
            if (timingFailure is not null || samples.Count != GpuTestProtocol.SampleCount)
                return (compute, Timing(GpuTestStatus.Inconclusive, timingFailure ?? "gpu_timing_invalid"));
            return (compute, Timing(GpuTestStatus.Passed, "gpu_timing_measured", samples.ToArray()));
        }
        finally
        {
            // Drop the context's retained shader/resource references as well.
            clearState(context);
        }
    }

    private static ComObject CompileShader(nint device)
    {
        var source = Encoding.ASCII.GetBytes(ShaderSource);
        nint bytecodePointer = 0;
        nint errorsPointer = 0;
        int hr;
        try
        {
            // ENABLE_STRICTNESS | OPTIMIZATION_LEVEL3; no includes or paths.
            hr = D3DCompile(source, (nuint)source.Length, 0, 0, 0, "main", "cs_5_0",
                0x8800, 0, out bytecodePointer, out errorsPointer);
        }
        catch (Exception error) when (error is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException)
        {
            throw new ProbeFailure("shader_compiler_unavailable", GpuTestStatus.NotAvailable);
        }
        finally
        {
            // Do not copy compiler diagnostics, file paths or native text into a report.
            Release(ref errorsPointer);
        }
        using var bytecode = Own(hr, bytecodePointer, "shader_compile_failed");
        var bytes = Method<BlobPointer>(bytecode.Pointer, 3)(bytecode.Pointer);
        var length = Method<BlobSize>(bytecode.Pointer, 4)(bytecode.Pointer);
        var shaderHr = Method<CreateComputeShader>(device, 18)(device, bytes, length, 0, out var shader);
        return Own(shaderHr, shader, "compute_failed");
    }

    private static ComObject MakeBuffer(nint device, BufferDescription description)
    {
        var hr = Method<CreateBuffer>(device, 3)(device, in description, 0, out var pointer);
        return Own(hr, pointer, "compute_failed");
    }

    private static void ReadAndVerify(nint context, nint staging, uint seed, int[] readback, Stopwatch clock)
    {
        var map = Method<Map>(context, 14);
        MappedSubresource mapped;
        while (true)
        {
            // DO_NOT_WAIT preserves a bounded polling loop even for a stuck GPU.
            var hr = map(context, staging, 0, 1, 0x100000, out mapped);
            if (hr == 0) break;
            if (hr != StillDrawing) throw new ProbeFailure("compute_failed");
            WaitForGpu(clock);
        }
        try
        {
            if (mapped.Data == 0) throw new ProbeFailure("compute_failed");
            Marshal.Copy(mapped.Data, readback, 0, readback.Length);
        }
        finally
        {
            Method<Unmap>(context, 15)(context, staging, 0);
        }
        for (var index = 0; index < readback.Length; index++)
        {
            var expected = ExpectedValue((uint)index, seed);
            if (unchecked((uint)readback[index]) != expected)
                throw new ProbeFailure("readback_mismatch");
        }
    }

    private static uint ExpectedValue(uint index, uint seed)
    {
        unchecked
        {
            var value = (index + 1u) ^ (seed * 0x9E3779B9u);
            for (uint iteration = 0; iteration < GpuTestProtocol.Iterations; iteration++)
            {
                value ^= value << 13;
                value ^= value >> 17;
                value ^= value << 5;
                value += 0x9E3779B9u + iteration;
            }
            return value;
        }
    }

    private static CudaProbeResult ProbeCuda(GpuTestAdapter adapter, long selectedLuid)
    {
        if (adapter.IsSoftware) return new(GpuTestStatus.NotRun, "cuda_software_adapter");
        if (adapter.VendorId != 0x10DE) return new(GpuTestStatus.NotRun, "cuda_not_nvidia");
        int? apiVersion = null;
        try
        {
            var initialize = cuInit(0);
            if (initialize == 100) return new(GpuTestStatus.NotAvailable, "cuda_no_device");
            if (initialize != 0) return new(GpuTestStatus.Inconclusive, "cuda_initialization_failed");
            if (cuDriverGetVersion(out var version) == 0 && version > 0) apiVersion = version;
            if (cuDeviceGetCount(out var count) != 0 || count < 0 || count > CudaDeviceLimit)
                return new(GpuTestStatus.Inconclusive, "cuda_capability_unavailable", apiVersion);
            if (count == 0) return new(GpuTestStatus.NotAvailable, "cuda_no_device", apiVersion);

            var matchedDevice = -1;
            var matchCount = 0;
            for (var ordinal = 0; ordinal < count; ordinal++)
            {
                if (cuDeviceGet(out var device, ordinal) != 0) continue;
                var luid = new byte[8];
                if (cuDeviceGetLuid(luid, out var nodeMask, device) != 0 || nodeMask == 0) continue;
                if (BitConverter.ToInt64(luid, 0) != selectedLuid) continue;
                matchedDevice = device;
                matchCount++;
            }
            // Old drivers may lack LUID matching, and linked adapters may match
            // multiple CUDA devices. Never guess by name or ordinal in either case.
            if (matchCount != 1)
                return new(GpuTestStatus.Inconclusive, "cuda_device_unmatched", apiVersion);
            // CU_DEVICE_ATTRIBUTE_COMPUTE_CAPABILITY_MAJOR/MINOR are 75/76.
            if (cuDeviceGetAttribute(out var major, 75, matchedDevice) != 0 ||
                cuDeviceGetAttribute(out var minor, 76, matchedDevice) != 0 ||
                major is < 1 or > 99 || minor is < 0 or > 99)
                return new(GpuTestStatus.Inconclusive, "cuda_capability_unavailable", apiVersion);
            return new(GpuTestStatus.Passed, "cuda_driver_ready", apiVersion, major, minor);
        }
        catch (DllNotFoundException)
        {
            return new(GpuTestStatus.NotAvailable, "cuda_driver_not_found");
        }
        catch (EntryPointNotFoundException)
        {
            return new(GpuTestStatus.Inconclusive, "cuda_device_unmatched", apiVersion);
        }
        catch (Exception error) when (IsNativeFailure(error))
        {
            return new(GpuTestStatus.Inconclusive, "cuda_initialization_failed", apiVersion);
        }
    }

    private sealed class TimestampQueries(ComObject disjoint, ComObject start, ComObject stop) : IDisposable
    {
        internal ComObject Disjoint { get; } = disjoint;
        internal ComObject Start { get; } = start;
        internal ComObject Stop { get; } = stop;

        internal static TimestampQueries? TryCreate(nint device)
        {
            ComObject? disjoint = null;
            ComObject? start = null;
            ComObject? stop = null;
            try
            {
                var create = Method<CreateQuery>(device, 24);
                ComObject Query(uint kind)
                {
                    var description = new QueryDescription { Query = kind };
                    var hr = create(device, in description, out var pointer);
                    return Own(hr, pointer, "gpu_timing_unavailable");
                }
                disjoint = Query(3);
                start = Query(2);
                stop = Query(2);
                return new TimestampQueries(disjoint, start, stop);
            }
            catch (ProbeFailure)
            {
                stop?.Dispose();
                start?.Dispose();
                disjoint?.Dispose();
                return null;
            }
        }

        internal (double Milliseconds, string? Failure) Read(nint context, Stopwatch clock)
        {
            var frequency = ReadQuery<TimestampDisjoint>(context, Disjoint.Pointer, clock);
            var start = ReadQuery<ulong>(context, Start.Pointer, clock);
            var stop = ReadQuery<ulong>(context, Stop.Pointer, clock);
            if (frequency.Disjoint != 0) return (0, "gpu_clock_disjoint");
            if (frequency.Frequency == 0 || stop <= start) return (0, "gpu_timing_invalid");
            var elapsed = (stop - start) * 1000.0 / frequency.Frequency;
            return double.IsFinite(elapsed) && elapsed > 0
                ? (elapsed, null) : (0, "gpu_timing_invalid");
        }

        public void Dispose()
        {
            Stop.Dispose();
            Start.Dispose();
            Disjoint.Dispose();
        }
    }

    private static T ReadQuery<T>(nint context, nint query, Stopwatch clock) where T : struct
    {
        var size = Marshal.SizeOf<T>();
        using var buffer = new NativeBlock(size);
        var getData = Method<GetData>(context, 29);
        while (true)
        {
            var hr = getData(context, query, buffer.Pointer, (uint)size, 1);
            if (hr == 0) return Marshal.PtrToStructure<T>(buffer.Pointer);
            // S_FALSE is pending, not a ready result merely because it is positive.
            if (hr != 1) throw new ProbeFailure("gpu_timing_unavailable");
            WaitForGpu(clock);
        }
    }

    private static void WaitForGpu(Stopwatch clock)
    {
        if (clock.Elapsed >= QueryDeadline)
            throw new ProbeFailure("gpu_query_timeout", GpuTestStatus.TimedOut);
        Thread.Sleep(1);
    }

    private static GpuTimingResult Timing(GpuTestStatus status, string code,
        IReadOnlyList<double>? samples = null) => new(status, code, GpuTestProtocol.Scenario,
            GpuTestProtocol.ElementCount, GpuTestProtocol.Iterations, GpuTestProtocol.WarmupCount,
            samples ?? Array.Empty<double>());

    private static ComObject Own(int hr, nint pointer, string code)
    {
        if (hr >= 0 && pointer != 0) return new ComObject(pointer);
        if (pointer != 0) Marshal.Release(pointer);
        throw new ProbeFailure(code);
    }

    private static void Require(int hr, string code)
    {
        if (hr < 0) throw new ProbeFailure(code);
    }

    private static void Release(ref nint pointer)
    {
        var previous = pointer;
        pointer = 0;
        if (previous != 0) Marshal.Release(previous);
    }

    private static void ValidateNativeLayouts()
    {
        // Fail before native writes if a platform/runtime marshals an ABI differently.
        if (Marshal.SizeOf<AdapterDescription>() != (IntPtr.Size == 8 ? 312 : 296) ||
            Marshal.SizeOf<BufferDescription>() != 24 ||
            Marshal.SizeOf<UnorderedAccessDescription>() != 20 ||
            Marshal.SizeOf<QueryDescription>() != 8 ||
            Marshal.SizeOf<TimestampDisjoint>() != 16 ||
            Marshal.SizeOf<MappedSubresource>() != (IntPtr.Size == 8 ? 16 : 12))
            throw new ProbeFailure("compute_failed");
    }

    private static bool IsNativeFailure(Exception error) => error is DllNotFoundException or
        EntryPointNotFoundException or BadImageFormatException or COMException or
        InvalidOperationException or ArgumentException or OverflowException;

    private sealed class ProbeFailure(string code, GpuTestStatus status = GpuTestStatus.Inconclusive)
        : Exception(code)
    {
        internal string Code { get; } = code;
        internal GpuTestStatus Status { get; } = status;
    }
}
