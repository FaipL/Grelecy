// SPDX-License-Identifier: MPL-2.0
namespace Grelecy.Diagnostics;

public enum UiLanguage
{
    French = 0,
    English = 1,
    Italian = 2,
    Spanish = 3,
    German = 4,
    Dutch = 5,
    Portuguese = 6,
    Chinese = 7,
    Korean = 8,
    Japanese = 9,
    Arabic = 10,
    Polish = 11,
    Swedish = 12,
    Norwegian = 13
}

public enum CollectionNotice { WindowsUnavailable, GraphicsUnavailable, NoGraphicsFound }

public sealed record WindowsInfo(string Edition, string Version, string Build, string Architecture);

// These identify a hardware model, never an individual device instance.
public sealed record PciIdentity(string? VendorId, string? DeviceId, string? SubsystemId, string? Revision);

public sealed record GraphicsInfo(string Name, string DriverVersion, PciIdentity? Hardware);

public sealed record DiagnosticReport(
    DateTimeOffset CollectedAt,
    WindowsInfo? Windows,
    IReadOnlyList<GraphicsInfo> Graphics,
    IReadOnlyList<CollectionNotice> Notices);

public sealed record ReportOptions(bool IncludeWindows = true, bool IncludeGraphics = true, bool IncludeHardwareIds = false);
