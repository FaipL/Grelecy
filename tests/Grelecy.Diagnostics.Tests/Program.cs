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
    }),
    ("Problem observations validate the user's evidence and retain the real collection timestamp", () =>
    {
        var observation = ObservationSample(ObservationKind.Problem);
        Equal(0, ObservationFormatter.Validate(observation with { Steps = "", Expected = "", TestedGpu = "" }).Count);
        var errors = ObservationFormatter.Validate(observation with { SoftwareName = " \u202E", Observed = "\r\n\0" });
        Check(errors.SequenceEqual(new[] { "software_required", "observed_required" }), "problem validation must require software and an observation, not reproduction steps");
        Check(ObservationFormatter.Validate(observation with { Kind = (ObservationKind)999 }).Contains("kind_invalid"), "invalid observation kinds must be reported");
        var report = new ObservationReport(new DateTimeOffset(2026, 10, 23, 13, 0, 0, TimeSpan.Zero), observation, Sample(null));
        using var json = JsonDocument.Parse(ObservationFormatter.ToJson(report, new()));
        Equal(2, json.RootElement.GetProperty("schemaVersion").GetInt32());
        var declared = json.RootElement.GetProperty("observation");
        Equal("problem", declared.GetProperty("kind").GetString());
        Equal("user_reported", declared.GetProperty("source").GetString());
        Equal("visual_issue", declared.GetProperty("category").GetString());
        Equal("The screen remains black", declared.GetProperty("observed").GetString());
        var configuration = json.RootElement.GetProperty("configuration");
        Equal(1, configuration.GetProperty("schemaVersion").GetInt32());
        Equal("1970-01-01T00:00:00.0000000+00:00", configuration.GetProperty("collectedAt").GetString());
        Equal("2026-10-23T13:00:00.0000000+00:00", json.RootElement.GetProperty("createdAt").GetString());
    }),
    ("The two observation routes exclude stale fields and scope positive results to the described test", () =>
    {
        var working = ObservationSample(ObservationKind.Working) with
        {
            Expected = "STALE-PROBLEM-EXPECTED", Observed = "STALE-PROBLEM-OBSERVED", Problem = ProblemCategory.Crash
        };
        Equal(0, ObservationFormatter.Validate(working with { Observed = "", Duration = "", Limitations = "" }).Count);
        Check(ObservationFormatter.Validate(working with { Steps = "\n\u202E" }).SequenceEqual(new[] { "steps_required" }), "a working report must describe the actual test");
        var report = new ObservationReport(DateTimeOffset.UnixEpoch, working, null);
        foreach (var format in ObservationFormats(report, new()))
        {
            DoesNotContain("STALE-PROBLEM", format);
            Contains("1080p", format);
        }
        using var json = JsonDocument.Parse(ObservationFormatter.ToJson(report, new()));
        var declared = json.RootElement.GetProperty("observation");
        Equal("working", declared.GetProperty("kind").GetString());
        Equal("worked_in_described_test", declared.GetProperty("resultScope").GetString());
        foreach (var field in new[] { "expected", "observed", "category", "compatible", "supported" })
            Check(!declared.TryGetProperty(field, out _), $"working reports must omit {field}");
        Contains("ne garantit pas la compatibilité générale", ObservationFormatter.ToPlainText(report, UiLanguage.French, new()));
        Contains("does not guarantee general", ObservationFormatter.ToPlainText(report, UiLanguage.English, new()));
        var problem = working with { Kind = ObservationKind.Problem, Observed = "Image missing", Duration = "STALE-WORKING-DURATION", Settings = "Reduced shadows", Limitations = "Tried windowed mode" };
        foreach (var format in ObservationFormats(report with { Observation = problem }, new()))
        {
            DoesNotContain("STALE-WORKING", format);
            Contains("Reduced shadows", format);
            Contains("Tried windowed mode", format);
        }
        using var problemJson = JsonDocument.Parse(ObservationFormatter.ToJson(report with { Observation = problem }, new()));
        var problemDeclared = problemJson.RootElement.GetProperty("observation");
        Equal("Reduced shadows", problemDeclared.GetProperty("settings").GetString());
        Equal("Tried windowed mode", problemDeclared.GetProperty("limitations").GetString());
        Check(!problemDeclared.TryGetProperty("duration", out _), "problem reports must omit duration");
        Contains("Autres essais ou solutions tentées", ObservationFormatter.ToPlainText(report with { Observation = problem }, UiLanguage.French, new()));
        Contains("Other tests or attempted solutions", ObservationFormatter.ToPlainText(report with { Observation = problem }, UiLanguage.English, new()));
    }),
    ("Collection checkboxes filter only automatic data and never erase the user's statement", () =>
    {
        var report = new ObservationReport(DateTimeOffset.UnixEpoch, ObservationSample(ObservationKind.Problem) with { TestedGpu = "My manually stated GPU" }, Sample(new("10DE", "1C03", "85AE1043", "A1")));
        foreach (var format in ObservationFormats(report, new(false, false, true)))
        {
            Contains("My manually stated GPU", format);
            Contains("The screen remains black", format);
            DoesNotContain("Windows 11 Home", format);
            DoesNotContain("Intel UHD", format);
            DoesNotContain("85AE1043", format);
        }
        using var none = JsonDocument.Parse(ObservationFormatter.ToJson(report, new(false, false, true)));
        Equal(JsonValueKind.Null, none.RootElement.GetProperty("configuration").ValueKind);
        using var gpuOnly = JsonDocument.Parse(ObservationFormatter.ToJson(report, new(false, true, false)));
        var collected = gpuOnly.RootElement.GetProperty("configuration");
        Check(!collected.TryGetProperty("windows", out _), "Windows must be absent");
        Equal(2, collected.GetProperty("graphics").GetArrayLength());
        Check(!collected.GetProperty("graphics")[0].TryGetProperty("hardware", out _), "hardware IDs must remain opt-in");
    }),
    ("Manual reports preserve unknown values without inventing a collection or test date", () =>
    {
        var report = new ObservationReport(new DateTimeOffset(2026, 10, 23, 15, 0, 0, TimeSpan.FromHours(2)),
            ObservationSample(ObservationKind.Working) with { SoftwareVersion = "", TestedGpu = "", Duration = "", Settings = "", Limitations = "" }, null);
        using var json = JsonDocument.Parse(ObservationFormatter.ToJson(report, new()));
        Equal("2026-10-23T13:00:00.0000000+00:00", json.RootElement.GetProperty("createdAt").GetString());
        Equal(JsonValueKind.Null, json.RootElement.GetProperty("configuration").ValueKind);
        DoesNotContain("collectedAt", json.RootElement.GetRawText());
        DoesNotContain("testedAt", json.RootElement.GetRawText());
        var declared = json.RootElement.GetProperty("observation");
        Equal(JsonValueKind.Null, declared.GetProperty("software").GetProperty("version").ValueKind);
        Equal(JsonValueKind.Null, declared.GetProperty("testedGpu").ValueKind);
        Equal(JsonValueKind.Null, declared.GetProperty("duration").ValueKind);
        Contains("Aucun relevé automatique joint", ObservationFormatter.ToPlainText(report, UiLanguage.French, new()));
        Contains("No automatic configuration is attached", ObservationFormatter.ToPlainText(report, UiLanguage.English, new()));
    }),
    ("Every user note preserves paragraphs but neutralizes Markdown, HTML, controls and excessive length", () =>
    {
        const string note = "First step\r\n\r\n[link](https://example.invalid)\r\n# heading\n<script>\u202E\0";
        const string normalized = "First step\n\n[link](https://example.invalid)\n# heading\n<script>";
        Equal(normalized, ObservationText.Narrative(note));
        Equal(4000, ObservationText.Narrative(new string('x', 5000)).Length);
        Equal(3999, ObservationText.Narrative(new string('x', 3999) + "😀").Length);
        Equal(256, ObservationText.SingleLine(new string('x', 5000)).Length);
        foreach (var kind in new[] { ObservationKind.Problem, ObservationKind.Working })
        {
            var observation = new UserObservation(kind, "[App](bad)", "<version>", "[GPU]", ProblemCategory.Other, note, note, note, note, note, note);
            var report = new ObservationReport(DateTimeOffset.UnixEpoch, observation, null);
            foreach (var language in new[] { UiLanguage.French, UiLanguage.English })
            {
                var markdown = ObservationFormatter.ToMarkdown(report, language, new());
                Contains("> First step", markdown);
                Contains("> \\[link\\]", markdown);
                Contains("> \\# heading", markdown);
                Contains("> \\<script\\>", markdown);
                DoesNotContain("[App](bad)", markdown);
                DoesNotContain("<version>", markdown);
                DoesNotContain("\u202E", markdown);
                Contains(normalized.Replace("\n", Environment.NewLine, StringComparison.Ordinal), ObservationFormatter.ToPlainText(report, language, new()));
            }
            using var json = JsonDocument.Parse(ObservationFormatter.ToJson(report, new()));
            var declared = json.RootElement.GetProperty("observation");
            Equal(normalized, declared.GetProperty("steps").GetString());
            foreach (var field in kind == ObservationKind.Problem ? new[] { "expected", "observed", "settings", "limitations" } : new[] { "duration", "settings", "limitations" })
                Equal(normalized, declared.GetProperty(field).GetString());
        }
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

static UserObservation ObservationSample(ObservationKind kind) => new(kind, "Example game", "1.2", "NVIDIA GTX 1060",
    ProblemCategory.VisualIssue, "Open the game\nLoad the saved scene", "The scene should appear", "The screen remains black",
    "15 minutes", "1080p, medium", "Only the first scene was tested");

static IEnumerable<string> ObservationFormats(ObservationReport report, ReportOptions options)
{
    yield return ObservationFormatter.ToJson(report, options);
    foreach (var language in new[] { UiLanguage.French, UiLanguage.English })
    {
        yield return ObservationFormatter.ToMarkdown(report, language, options);
        yield return ObservationFormatter.ToPlainText(report, language, options);
    }
}

static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
static void Equal<T>(T expected, T actual) => Check(Equals(expected, actual), $"expected {expected}; got {actual}");
static void Contains(string expected, string actual) => Check(actual.Contains(expected, StringComparison.Ordinal), $"missing {expected}");
static void DoesNotContain(string excluded, string actual) => Check(!actual.Contains(excluded, StringComparison.Ordinal), $"unexpected {excluded}");
