// SPDX-License-Identifier: MPL-2.0
using System.Globalization;
using System.Text.Json;
using Grelecy.Diagnostics;

var tests = new (string Name, Action Body)[]
{
    ("PCI model IDs exclude the device-instance suffix", () =>
    {
        const string instance = @"PCI\VEN_10DE&DEV_1C03&SUBSYS_85AE1043&REV_A1\4&PRIVATE-SERIAL&0&0008";
        var pci = HardwareIdentity.Parse(instance);
        Equal(new PciIdentity("10DE", "1C03", "85AE1043", "A1"), pci);
        var report = Sample(pci);
        foreach (var format in Formats(report, new(true, true, true)))
        {
            Contains("10DE", format);
            DoesNotContain("PRIVATE-SERIAL", format);
            DoesNotContain("0008", format);
        }
    }),
    ("Malformed IDs cannot smuggle identifiers into exports", () =>
    {
        foreach (var id in new[] { @"USB\VEN_10DE&DEV_1C03", @"PCI\VEN_10DE&DEV_NOT-HEX\SERIAL", @"PCI\VEN_10DE&VEN_8086&DEV_1C03", @"PCI\VEN_10DE", @"PCI\VEN_10DE&DEV_1C03&REV_TOO-LONG" })
            Equal<PciIdentity?>(null, HardwareIdentity.Parse(id));
        var report = Sample(new PciIdentity("MY-SECRET", "MY-SECRET", "MY-SECRET", "MY-SECRET"));
        foreach (var format in Formats(report, new(true, true, true))) DoesNotContain("MY-SECRET", format);
    }),
    ("IDs are excluded by default from every format", () =>
    {
        var report = Sample(new("10DE", "1C03", "85AE1043", "A1"));
        foreach (var format in Formats(report, new()))
        {
            Contains("GTX", format);
            DoesNotContain("10DE", format);
            DoesNotContain("85AE1043", format);
        }
        using var json = JsonDocument.Parse(ReportFormatter.ToJson(report, new()));
        Check(!json.RootElement.GetProperty("graphics")[0].TryGetProperty("hardware", out _), "hardware property must be absent");
    }),
    ("Selections suppress fields and related notices in all formats", () =>
    {
        var report = Sample(null) with { Notices = [CollectionNotice.WindowsUnavailable, CollectionNotice.GraphicsUnavailable] };
        foreach (var format in Formats(report, new(false, true, true))) DoesNotContain("Windows 11 Home", format);
        foreach (var format in Formats(report, new(true, false, true)))
        {
            DoesNotContain("GTX", format);
            DoesNotContain("Intel", format);
            DoesNotContain("32.0.15.1234", format);
        }
        using var json = JsonDocument.Parse(ReportFormatter.ToJson(report, new(false, false, true)));
        Check(!json.RootElement.TryGetProperty("windows", out _), "windows property must be absent");
        Check(!json.RootElement.TryGetProperty("graphics", out _), "graphics property must be absent");
        Equal(0, json.RootElement.GetProperty("notices").GetArrayLength());
    }),
    ("Multiple GPUs survive serialization without a preferred-card guess", () =>
    {
        using var json = JsonDocument.Parse(ReportFormatter.ToJson(Sample(null), new()));
        var cards = json.RootElement.GetProperty("graphics");
        Equal(2, cards.GetArrayLength());
        Equal("NVIDIA GeForce GTX 1060", cards[0].GetProperty("name").GetString());
        Equal("Intel UHD Graphics", cards[1].GetProperty("name").GetString());
    }),
    ("Missing data remains usable and uses localized explanations", () =>
    {
        var report = new DiagnosticReport(DateTimeOffset.UnixEpoch, null, [new("", "", null)], [CollectionNotice.WindowsUnavailable]);
        Contains("Information indisponible", ReportFormatter.ToPlainText(report, UiLanguage.French, new()));
        Contains("Information unavailable", ReportFormatter.ToPlainText(report, UiLanguage.English, new()));
        using var json = JsonDocument.Parse(ReportFormatter.ToJson(report, new()));
        Equal(JsonValueKind.Null, json.RootElement.GetProperty("windows").ValueKind);
        Equal("windows_unavailable", json.RootElement.GetProperty("notices")[0].GetString());
    }),
    ("Untrusted WMI text cannot create Markdown links or HTML", () =>
    {
        var report = Sample(null) with { Graphics = [new("[click](https://example.invalid)\r\n<script>alert(1)</script>\u202E", "32.0.15.1234", null)] };
        var md = ReportFormatter.ToMarkdown(report, UiLanguage.English, new());
        DoesNotContain("[click](", md);
        DoesNotContain("<script>", md);
        DoesNotContain("\u202E", md);
        Contains("\\[click\\]", md);
        Contains("\\<script\\>", md);
        var plain = ReportFormatter.ToPlainText(report, UiLanguage.English, new());
        Contains("[click](https://example.invalid) <script>", plain);
    }),
    ("Normalization strips controls, collapses lines and bounds Unicode safely", () =>
    {
        Equal("GPU name", SafeText.Normalize("  GPU\r\n\tname\u202E\0  "));
        Equal("", SafeText.Normalize(null));
        Equal(256, SafeText.Normalize(new string('a', 400)).Length);
        var normalized = SafeText.Normalize(new string('a', 255) + "😀");
        Equal(255, normalized.Length);
        Check(!char.IsSurrogate(normalized[^1]), "do not leave an unmatched surrogate");
    }),
    ("JSON is culture-independent with a stable schema and UTC timestamp", () =>
    {
        var report = Sample(null) with { CollectedAt = new DateTimeOffset(2026, 10, 22, 22, 30, 0, TimeSpan.FromHours(2)) };
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            var fr = ReportFormatter.ToJson(report, new());
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            Equal(fr, ReportFormatter.ToJson(report, new()));
            using var json = JsonDocument.Parse(fr);
            Equal(1, json.RootElement.GetProperty("schemaVersion").GetInt32());
            Equal("2026-10-22T20:30:00.0000000+00:00", json.RootElement.GetProperty("collectedAt").GetString());
            Equal(ReportFormatter.ApplicationVersion, json.RootElement.GetProperty("application").GetProperty("version").GetString());
        }
        finally { CultureInfo.CurrentCulture = previous; }
    }),
    ("Directly constructed fields are sanitized at every export boundary", () =>
    {
        var report = Sample(null) with { Windows = new("Windows\u202E\0", "10\n0", new string('a', 400), "x64"), Notices = [(CollectionNotice)999] };
        foreach (var format in Formats(report, new()))
        {
            DoesNotContain("\u202E", format);
            DoesNotContain("999", format);
            DoesNotContain(new string('a', 257), format);
        }
        using var json = JsonDocument.Parse(ReportFormatter.ToJson(report, new()));
        Equal("Windows", json.RootElement.GetProperty("windows").GetProperty("edition").GetString());
    })
};

var failures = 0;
foreach (var (name, body) in tests)
{
    try { body(); Console.WriteLine($"PASS: {name}"); }
    catch (Exception exception) { failures++; Console.Error.WriteLine($"FAIL: {name}: {exception.Message}"); }
}
Console.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed");
return failures == 0 ? 0 : 1;

static DiagnosticReport Sample(PciIdentity? pci) => new(DateTimeOffset.UnixEpoch,
    new("Windows 11 Home", "10.0.26100", "26100", "64-bit"),
    [new("NVIDIA GeForce GTX 1060", "32.0.15.1234", pci), new("Intel UHD Graphics", "31.0.101.1234", null)], []);

static IEnumerable<string> Formats(DiagnosticReport report, ReportOptions options)
{
    yield return ReportFormatter.ToJson(report, options);
    foreach (var language in new[] { UiLanguage.French, UiLanguage.English })
    {
        yield return ReportFormatter.ToMarkdown(report, language, options);
        yield return ReportFormatter.ToPlainText(report, language, options);
    }
}

static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void Equal<T>(T expected, T actual) => Check(Equals(expected, actual), $"expected {expected}; got {actual}");
static void Contains(string expected, string actual) => Check(actual.Contains(expected, StringComparison.Ordinal), $"missing {expected}");
static void DoesNotContain(string excluded, string actual) => Check(!actual.Contains(excluded, StringComparison.Ordinal), $"unexpected {excluded}");
