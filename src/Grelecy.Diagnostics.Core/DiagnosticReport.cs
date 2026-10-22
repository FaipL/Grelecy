// SPDX-License-Identifier: MPL-2.0
namespace Grelecy.Diagnostics;

public enum UiLanguage { French, English }

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
