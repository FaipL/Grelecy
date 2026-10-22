// SPDX-License-Identifier: MPL-2.0
using System.ComponentModel;
using System.Globalization;
using System.Management;
using System.Runtime.InteropServices;
using System.Security;

namespace Grelecy.Diagnostics.Windows;

/// <summary>
/// Reads an explicit allowlist of local WMI properties. This class never changes
/// device settings, launches a process, uploads data, or retains raw WMI objects.
/// </summary>
public static class WindowsCollector
{
    private static readonly TimeSpan QueryTimeout = TimeSpan.FromSeconds(15);

    public static DiagnosticReport Collect()
    {
        var collectedAt = DateTimeOffset.UtcNow;
        WindowsInfo? windows = null;
        var graphics = new List<GraphicsInfo>();
        var notices = new HashSet<CollectionNotice>();

        // Keep the queries independent: graphics information remains useful
        // when Windows information is unavailable, and vice versa.
        try
        {
            using var searcher = CreateSearcher(
                "SELECT Caption, Version, BuildNumber, OSArchitecture FROM Win32_OperatingSystem");
            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    windows = new WindowsInfo(
                        ReadText(item, "Caption", notices, CollectionNotice.WindowsUnavailable),
                        ReadText(item, "Version", notices, CollectionNotice.WindowsUnavailable),
                        ReadText(item, "BuildNumber", notices, CollectionNotice.WindowsUnavailable),
                        ReadText(item, "OSArchitecture", notices, CollectionNotice.WindowsUnavailable));
                }
                break;
            }
            if (windows is null)
                notices.Add(CollectionNotice.WindowsUnavailable);
        }
        catch (Exception error) when (IsCollectionFailure(error))
        {
            // WMI exception text can contain local machine names or paths.
            // Only a fixed, translatable notice leaves the collector.
            notices.Add(CollectionNotice.WindowsUnavailable);
        }

        try
        {
            using var searcher = CreateSearcher(
                "SELECT Name, DriverVersion, PNPDeviceID FROM Win32_VideoController");
            using var results = searcher.Get();
            foreach (ManagementObject item in results)
            {
                using (item)
                {
                    var name = ReadText(item, "Name", notices, CollectionNotice.GraphicsUnavailable);
                    var driver = ReadText(item, "DriverVersion", notices, CollectionNotice.GraphicsUnavailable);
                    PciIdentity? hardware = null;
                    try
                    {
                        // Parse directly into model identifiers. Do not retain
                        // or export the full PNP instance path or its suffix.
                        hardware = HardwareIdentity.Parse(
                            Convert.ToString(item["PNPDeviceID"], CultureInfo.InvariantCulture));
                    }
                    catch (Exception error) when (IsCollectionFailure(error))
                    {
                        notices.Add(CollectionNotice.GraphicsUnavailable);
                    }
                    graphics.Add(new GraphicsInfo(name, driver, hardware));
                }
            }
            if (graphics.Count == 0)
                notices.Add(CollectionNotice.NoGraphicsFound);
        }
        catch (Exception error) when (IsCollectionFailure(error))
        {
            // Preserve adapters already read if a later enumeration fails.
            notices.Add(CollectionNotice.GraphicsUnavailable);
        }

        return new DiagnosticReport(collectedAt, windows, graphics.ToArray(), notices.Order().ToArray());
    }

    private static ManagementObjectSearcher CreateSearcher(string query)
    {
        var scope = new ManagementScope(@"\\.\root\cimv2", new ConnectionOptions
        {
            Timeout = QueryTimeout
        });
        return new ManagementObjectSearcher(scope, new ObjectQuery(query), new System.Management.EnumerationOptions
        {
            Timeout = QueryTimeout,
            ReturnImmediately = true,
            Rewindable = false,
            BlockSize = 1
        });
    }

    private static string ReadText(
        ManagementObject item,
        string property,
        HashSet<CollectionNotice> notices,
        CollectionNotice unavailable)
    {
        try
        {
            return SafeText.Normalize(Convert.ToString(item[property], CultureInfo.InvariantCulture));
        }
        catch (Exception error) when (IsCollectionFailure(error))
        {
            notices.Add(unavailable);
            return string.Empty;
        }
    }

    private static bool IsCollectionFailure(Exception error) => error is
        ManagementException or COMException or UnauthorizedAccessException or
        SecurityException or TimeoutException or Win32Exception or
        InvalidOperationException or IOException;
}
