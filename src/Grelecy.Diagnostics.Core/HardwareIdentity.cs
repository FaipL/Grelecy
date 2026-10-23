// SPDX-License-Identifier: MPL-2.0
namespace Grelecy.Diagnostics;

public static class HardwareIdentity
{
    /// <summary>Extract model identifiers. Never retain the device-instance suffix.</summary>
    public static PciIdentity? Parse(string? fullPnpId)
    {
        if (fullPnpId is null || fullPnpId.Length > 2048) return null;
        var parts = fullPnpId.Split('\\', 3);
        if (parts.Length < 2 || !parts[0].Equals("PCI", StringComparison.OrdinalIgnoreCase)) return null;
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in parts[1].Split('&'))
        {
            var separator = token.IndexOf('_');
            if (separator <= 0) return null;
            var key = token[..separator];
            var value = token[(separator + 1)..];
            var length = key.ToUpperInvariant() switch
            {
                "VEN" or "DEV" => 4,
                "SUBSYS" => 8,
                "REV" => 2,
                _ => 0
            };
            if (length == 0) continue;
            if (value.Length != length || value.Any(c => !Uri.IsHexDigit(c)) || !fields.TryAdd(key, value.ToUpperInvariant()))
                return null;
        }
        if (!fields.TryGetValue("VEN", out var vendor) || !fields.TryGetValue("DEV", out var device)) return null;
        return new PciIdentity(vendor, device, fields.GetValueOrDefault("SUBSYS"), fields.GetValueOrDefault("REV"));
    }

    // Public records can also be constructed by callers: validate again at the export boundary.
    internal static PciIdentity? Validate(PciIdentity? identity)
    {
        if (identity is null) return null;
        static string? Hex(string? value, int length) => value?.Length == length && value.All(Uri.IsHexDigit)
            ? value.ToUpperInvariant() : null;
        var vendor = Hex(identity.VendorId, 4);
        var device = Hex(identity.DeviceId, 4);
        if (vendor is null || device is null) return null;
        return new PciIdentity(vendor, device, Hex(identity.SubsystemId, 8), Hex(identity.Revision, 2));
    }
}
