// SPDX-License-Identifier: MPL-2.0
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Grelecy.Diagnostics;

public static class ReportFormatter
{
    public const string ApplicationVersion = "0.4.0-preview.1";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static string ToPlainText(DiagnosticReport report, UiLanguage language, ReportOptions options) =>
        Render(report, language, options, markdown: false);

    public static string ToMarkdown(DiagnosticReport report, UiLanguage language, ReportOptions options) =>
        Render(report, language, options, markdown: true);

    public static string ToJson(DiagnosticReport report, ReportOptions options)
    {
        var data = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 1,
            ["application"] = new { name = "GRELECY Diagnostics", version = ApplicationVersion },
            ["collectedAt"] = report.CollectedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)
        };
        if (options.IncludeWindows)
            data["windows"] = report.Windows is { } os ? new
            {
                edition = SafeText.Normalize(os.Edition),
                version = SafeText.Normalize(os.Version),
                build = SafeText.Normalize(os.Build),
                architecture = SafeText.Normalize(os.Architecture)
            } : null;
        if (options.IncludeGraphics)
            data["graphics"] = report.Graphics.Select(gpu =>
            {
                var card = new Dictionary<string, object?>
                {
                    ["name"] = SafeText.Normalize(gpu.Name),
                    ["windowsDriverVersion"] = SafeText.Normalize(gpu.DriverVersion)
                };
                if (options.IncludeHardwareIds)
                    card["hardware"] = HardwareIdentity.Validate(gpu.Hardware) is { } pci ? new
                    {
                        vendorId = pci.VendorId,
                        deviceId = pci.DeviceId,
                        subsystemId = pci.SubsystemId,
                        revision = pci.Revision
                    } : null;
                return card;
            }).ToArray();
        data["notices"] = SelectedNotices(report, options).Select(notice => notice switch
        {
            CollectionNotice.WindowsUnavailable => "windows_unavailable",
            CollectionNotice.GraphicsUnavailable => "graphics_unavailable",
            CollectionNotice.NoGraphicsFound => "no_graphics_found",
            _ => "information_unavailable"
        }).ToArray();
        return JsonSerializer.Serialize(data, JsonOptions) + "\n";
    }

    private static IEnumerable<CollectionNotice> SelectedNotices(DiagnosticReport report, ReportOptions options) =>
        report.Notices.Distinct().Where(notice => notice switch
        {
            CollectionNotice.WindowsUnavailable => options.IncludeWindows,
            CollectionNotice.GraphicsUnavailable or CollectionNotice.NoGraphicsFound => options.IncludeGraphics,
            _ => false
        });

    private static string Render(DiagnosticReport report, UiLanguage language, ReportOptions options, bool markdown)
    {
        string T(string key) => LocalizedText.Get(key, language);
        string Value(string? value)
        {
            var normalized = SafeText.Normalize(value);
            if (normalized.Length == 0) return T("report.informationUnavailable");
            return markdown ? EscapeMarkdown(normalized) : normalized;
        }
        var output = new StringBuilder();
        void Heading(string heading) => output.AppendLine((markdown ? "## " : "") + heading).AppendLine();
        void Field(string label, string? value) => output.AppendLine($"{(markdown ? "- " : "")}{label} : {Value(value)}");

        output.AppendLine((markdown ? "# " : "") + T("report.grelecyConfigurationReport"));
        output.AppendLine();
        output.AppendLine(T("report.createdByGrelecyDiagnostics") + ApplicationVersion);
        output.AppendLine(T("report.collectedAt")
            + report.CollectedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
        output.AppendLine(T("report.thisReportDescribesTheConfigurationItDoes"));
        output.AppendLine();
        if (options.IncludeWindows)
        {
            Heading(T("report.windows"));
            if (report.Windows is { } os)
            {
                Field(T("report.edition"), os.Edition);
                Field(T("report.version"), os.Version);
                Field(T("report.buildNumber"), os.Build);
                Field(T("report.architecture"), os.Architecture);
            }
            else output.AppendLine(T("report.windowsInformationCouldNotBeRead"));
            output.AppendLine();
        }
        if (options.IncludeGraphics)
        {
            Heading(T("report.graphicsCardS"));
            if (report.Graphics.Count == 0)
                output.AppendLine(T("report.noGraphicsCardWasReportedThisDoes"));
            for (var index = 0; index < report.Graphics.Count; index++)
            {
                var gpu = report.Graphics[index];
                Field(T("report.card") + (index + 1), gpu.Name);
                Field(T("report.windowsDriverVersion"), gpu.DriverVersion);
                if (options.IncludeHardwareIds)
                {
                    var pci = HardwareIdentity.Validate(gpu.Hardware);
                    Field(T("report.vendorIdPci"), pci?.VendorId);
                    Field(T("report.deviceIdPci"), pci?.DeviceId);
                    Field(T("report.subsystemIdPci"), pci?.SubsystemId);
                    Field(T("report.revisionPci"), pci?.Revision);
                }
                output.AppendLine();
            }
            output.AppendLine(T("report.theWindowsDriverVersionMayDifferFrom"));
            output.AppendLine();
        }
        var notices = SelectedNotices(report, options).ToArray();
        if (notices.Length > 0)
        {
            Heading(T("report.missingInformation"));
            foreach (var notice in notices)
                output.AppendLine((markdown ? "- " : "") + (notice switch
                {
                    CollectionNotice.WindowsUnavailable => T("report.windowsCollectionIsIncompleteYouCanTry"),
                    CollectionNotice.GraphicsUnavailable => T("report.graphicsCollectionIsIncompleteYouCanTry"),
                    CollectionNotice.NoGraphicsFound => T("report.windowsDidNotReportAnyGraphicsCards"),
                    _ => T("report.someInformationIsUnavailable")
                }));
            output.AppendLine();
        }
        if (!options.IncludeWindows && !options.IncludeGraphics)
            output.AppendLine(T("report.noInformationCategoriesSelected"));
        return output.ToString();
    }

    private static string EscapeMarkdown(string value)
    {
        // Escape all ASCII punctuation, including URL and HTML delimiters, before inserting WMI text.
        var result = new StringBuilder();
        foreach (var c in value)
        {
            if ((c >= '!' && c <= '/') || (c >= ':' && c <= '@') || (c >= '[' && c <= '`') || (c >= '{' && c <= '~'))
                result.Append('\\');
            result.Append(c);
        }
        return result.ToString();
    }
}
