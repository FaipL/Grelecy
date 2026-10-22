// SPDX-License-Identifier: MPL-2.0
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Grelecy.Diagnostics;

public static class ReportFormatter
{
    public const string ApplicationVersion = "0.1.0-preview.1";
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
        var french = language == UiLanguage.French;
        string T(string fr, string en) => french ? fr : en;
        string Value(string? value)
        {
            var normalized = SafeText.Normalize(value);
            if (normalized.Length == 0) return T("Information indisponible", "Information unavailable");
            return markdown ? EscapeMarkdown(normalized) : normalized;
        }
        var output = new StringBuilder();
        void Heading(string heading) => output.AppendLine((markdown ? "## " : "") + heading).AppendLine();
        void Field(string label, string? value) => output.AppendLine($"{(markdown ? "- " : "")}{label} : {Value(value)}");

        output.AppendLine((markdown ? "# " : "") + T("GRELECY — Rapport de configuration", "GRELECY — Configuration report"));
        output.AppendLine();
        output.AppendLine(T("Créé par GRELECY Diagnostics ", "Created by GRELECY Diagnostics ") + ApplicationVersion);
        output.AppendLine(T("Collecté le ", "Collected at ")
            + report.CollectedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
        output.AppendLine(T("Ce relevé décrit la configuration. Il ne mesure ni les performances ni la compatibilité.",
            "This report describes the configuration. It does not measure performance or compatibility."));
        output.AppendLine();
        if (options.IncludeWindows)
        {
            Heading("Windows");
            if (report.Windows is { } os)
            {
                Field(T("Édition", "Edition"), os.Edition);
                Field("Version", os.Version);
                Field(T("Numéro de build", "Build number"), os.Build);
                Field("Architecture", os.Architecture);
            }
            else output.AppendLine(T("Les informations Windows n’ont pas pu être relevées.", "Windows information could not be read."));
            output.AppendLine();
        }
        if (options.IncludeGraphics)
        {
            Heading(T("Carte(s) graphique(s)", "Graphics card(s)"));
            if (report.Graphics.Count == 0)
                output.AppendLine(T("Aucune carte graphique n’a été relevée. Cela ne signifie pas que le PC n’en possède pas.",
                    "No graphics card was reported. This does not mean the PC has no graphics hardware."));
            for (var index = 0; index < report.Graphics.Count; index++)
            {
                var gpu = report.Graphics[index];
                Field(T("Carte ", "Card ") + (index + 1), gpu.Name);
                Field(T("Version Windows du pilote", "Windows driver version"), gpu.DriverVersion);
                if (options.IncludeHardwareIds)
                {
                    var pci = HardwareIdentity.Validate(gpu.Hardware);
                    Field(T("Identifiant du fabricant (PCI)", "Vendor ID (PCI)"), pci?.VendorId);
                    Field(T("Identifiant du modèle (PCI)", "Device ID (PCI)"), pci?.DeviceId);
                    Field(T("Identifiant du sous-système (PCI)", "Subsystem ID (PCI)"), pci?.SubsystemId);
                    Field(T("Révision (PCI)", "Revision (PCI)"), pci?.Revision);
                }
                output.AppendLine();
            }
            output.AppendLine(T("La version Windows du pilote peut différer du numéro utilisé sur le site du fabricant.",
                "The Windows driver version may differ from the version number used on the manufacturer's website."));
            output.AppendLine();
        }
        var notices = SelectedNotices(report, options).ToArray();
        if (notices.Length > 0)
        {
            Heading(T("Informations manquantes", "Missing information"));
            foreach (var notice in notices)
                output.AppendLine((markdown ? "- " : "") + (notice switch
                {
                    CollectionNotice.WindowsUnavailable => T("La collecte Windows est incomplète. Vous pouvez réessayer.", "Windows collection is incomplete. You can try again."),
                    CollectionNotice.GraphicsUnavailable => T("La collecte des cartes graphiques est incomplète. Vous pouvez réessayer.", "Graphics collection is incomplete. You can try again."),
                    CollectionNotice.NoGraphicsFound => T("Windows n’a signalé aucune carte graphique.", "Windows did not report any graphics cards."),
                    _ => T("Une information est indisponible.", "Some information is unavailable.")
                }));
            output.AppendLine();
        }
        if (!options.IncludeWindows && !options.IncludeGraphics)
            output.AppendLine(T("Aucune rubrique sélectionnée.", "No information categories selected."));
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
