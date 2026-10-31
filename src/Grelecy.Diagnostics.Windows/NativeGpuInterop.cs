// SPDX-License-Identifier: MPL-2.0
using System.Runtime.InteropServices;

namespace Grelecy.Diagnostics.Windows;

/// <summary>
/// Minimal Windows SDK ABI for the isolated GPU worker. Slots follow dxgi.h and
/// d3d11.h (including their IUnknown/IDXGIObject/ID3D11DeviceChild bases).
/// No COM pointer or adapter LUID leaves the worker's local protocol.
/// </summary>
internal static class NativeGpuInterop
{
    internal static readonly Guid Factory1Id = new("770aae78-f26f-4dba-a829-253c83d1b387");
    internal static readonly Guid DxgiDeviceId = new("54ec77fa-1377-44e6-8c32-88fd5f44c84c");
    internal const int NotFound = unchecked((int)0x887A0002);
    internal const int StillDrawing = unchecked((int)0x887A000A);
    internal const int InvalidArgument = unchecked((int)0x80070057);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("dxgi.dll", ExactSpelling = true)]
    internal static extern int CreateDXGIFactory1(in Guid iid, out nint factory);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("d3d11.dll", ExactSpelling = true)]
    internal static extern int D3D11CreateDevice(nint adapter, uint driverType,
        nint software, uint flags, [In] uint[] featureLevels, uint featureLevelCount,
        uint sdkVersion, out nint device, out uint selectedFeatureLevel, out nint context);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("d3dcompiler_47.dll", ExactSpelling = true)]
    internal static extern int D3DCompile([In] byte[] source, nuint sourceLength,
        nint sourceName, nint defines, nint include,
        [MarshalAs(UnmanagedType.LPStr)] string entryPoint,
        [MarshalAs(UnmanagedType.LPStr)] string target, uint flags1, uint flags2,
        out nint code, out nint errors);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvcuda.dll", ExactSpelling = true)]
    internal static extern int cuInit(uint flags);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvcuda.dll", ExactSpelling = true)]
    internal static extern int cuDriverGetVersion(out int version);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvcuda.dll", ExactSpelling = true)]
    internal static extern int cuDeviceGetCount(out int count);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvcuda.dll", ExactSpelling = true)]
    internal static extern int cuDeviceGet(out int device, int ordinal);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvcuda.dll", ExactSpelling = true)]
    internal static extern int cuDeviceGetAttribute(out int value, int attribute, int device);

    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [DllImport("nvcuda.dll", ExactSpelling = true)]
    internal static extern int cuDeviceGetLuid([Out] byte[] luid, out uint nodeMask, int device);

    internal static T Method<T>(nint instance, int slot) where T : Delegate =>
        Marshal.GetDelegateForFunctionPointer<T>(
            Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance), checked(slot * IntPtr.Size)));

    internal sealed class ComObject(nint pointer) : IDisposable
    {
        private nint pointer = pointer;
        internal nint Pointer => pointer;
        public void Dispose()
        {
            var previous = Interlocked.Exchange(ref pointer, 0);
            if (previous != 0) Marshal.Release(previous);
        }
    }

    internal sealed class NativeBlock : IDisposable
    {
        private nint pointer;
        internal NativeBlock(int byteCount) => pointer = Marshal.AllocHGlobal(byteCount);
        internal nint Pointer => pointer;
        public void Dispose()
        {
            var previous = Interlocked.Exchange(ref pointer, 0);
            if (previous != 0) Marshal.FreeHGlobal(previous);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    internal struct AdapterDescription
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public uint VendorId;
        public uint DeviceId;
        public uint SubsystemId;
        public uint Revision;
        public nuint DedicatedVideoMemory;
        public nuint DedicatedSystemMemory;
        public nuint SharedSystemMemory;
        public uint LuidLow;
        public int LuidHigh;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct BufferDescription
    {
        public uint ByteWidth;
        public uint Usage;
        public uint BindFlags;
        public uint CpuAccessFlags;
        public uint MiscFlags;
        public uint StructureByteStride;
    }

    // The largest UAV union member is three UINTs: preserve its full 20-byte ABI.
    [StructLayout(LayoutKind.Sequential)]
    internal struct UnorderedAccessDescription
    {
        public uint Format;
        public uint ViewDimension;
        public uint FirstElement;
        public uint ElementCount;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct QueryDescription
    {
        public uint Query;
        public uint MiscFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct TimestampDisjoint
    {
        public ulong Frequency;
        // Native BOOL is four bytes; default packing includes trailing padding.
        public int Disjoint;
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MappedSubresource
    {
        public nint Data;
        public uint RowPitch;
        public uint DepthPitch;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int EnumAdapters(nint self, uint index, out nint adapter); // factory slot 12
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int GetAdapterDescription(nint self, out AdapterDescription description); // adapter slot 10
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int CheckInterfaceSupport(nint self, in Guid iid, out long version); // adapter slot 9
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate nint BlobPointer(nint self); // blob slot 3
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate nuint BlobSize(nint self); // blob slot 4
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int CreateBuffer(nint self, in BufferDescription description,
        nint initialData, out nint buffer); // device slot 3
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int CreateUnorderedAccessView(nint self, nint resource,
        in UnorderedAccessDescription description, out nint view); // device slot 8
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int CreateComputeShader(nint self, nint bytecode, nuint length,
        nint linkage, out nint shader); // device slot 18
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int CreateQuery(nint self, in QueryDescription description, out nint query); // device slot 24
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int Map(nint self, nint resource, uint subresource, uint mapType,
        uint flags, out MappedSubresource mapped); // context slot 14
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void Unmap(nint self, nint resource, uint subresource); // context slot 15
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void QueryBoundary(nint self, nint query); // context slots 27/28
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate int GetData(nint self, nint query, nint data, uint size, uint flags); // context slot 29
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void Dispatch(nint self, uint x, uint y, uint z); // context slot 41
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void CopyResource(nint self, nint destination, nint source); // context slot 47
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void UpdateSubresource(nint self, nint destination, uint subresource,
        nint box, nint source, uint rowPitch, uint depthPitch); // context slot 48
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void SetUnorderedAccessViews(nint self, uint startSlot, uint count,
        [In] nint[] views, nint initialCounts); // context slot 68
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void SetComputeShader(nint self, nint shader,
        nint classInstances, uint count); // context slot 69
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void SetConstantBuffers(nint self, uint startSlot, uint count,
        [In] nint[] buffers); // context slot 71
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    internal delegate void ContextAction(nint self); // context slots 110/111
}
