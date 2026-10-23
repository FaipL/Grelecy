// SPDX-License-Identifier: MPL-2.0
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Grelecy.Diagnostics;

public static class ObservationFormatter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static IReadOnlyList<string> Validate(UserObservation observation)
    {
        var errors = new List<string>();
        if (observation.Kind is not (ObservationKind.Problem or ObservationKind.Working)) errors.Add("kind_invalid");
        if (ObservationText.SingleLine(observation.SoftwareName).Length == 0) errors.Add("software_required");
        if (observation.Kind == ObservationKind.Problem && ObservationText.Narrative(observation.Observed).Length == 0)
            errors.Add("observed_required");
        if (observation.Kind == ObservationKind.Working && ObservationText.Narrative(observation.Steps).Length == 0)
            errors.Add("steps_required");
        return errors.AsReadOnly();
    }

    public static string ToPlainText(ObservationReport report, UiLanguage language, ReportOptions options) =>
        Render(report, language, options, markdown: false);

    public static string ToMarkdown(ObservationReport report, UiLanguage language, ReportOptions options) =>
        Render(report, language, options, markdown: true);

    public static string ToJson(ObservationReport report, ReportOptions options)
    {
        var observation = report.Observation;
        var kind = KindCode(observation.Kind);
        var declared = new Dictionary<string, object?>
        {
            ["kind"] = kind,
            ["source"] = "user_reported",
            ["software"] = new
            {
                name = Optional(ObservationText.SingleLine(observation.SoftwareName)),
                version = Optional(ObservationText.SingleLine(observation.SoftwareVersion))
            },
            ["testedGpu"] = Optional(ObservationText.SingleLine(observation.TestedGpu)),
            ["steps"] = Optional(ObservationText.Narrative(observation.Steps))
        };
        if (observation.Kind == ObservationKind.Problem)
        {
            declared["category"] = CategoryCode(observation.Problem);
            declared["expected"] = Optional(ObservationText.Narrative(observation.Expected));
            declared["observed"] = Optional(ObservationText.Narrative(observation.Observed));
        }
        else
        {
            declared["resultScope"] = "worked_in_described_test";
            declared["duration"] = Optional(ObservationText.Narrative(observation.Duration));
        }
        declared["settings"] = Optional(ObservationText.Narrative(observation.Settings));
        declared["limitations"] = Optional(ObservationText.Narrative(observation.Limitations));

        JsonElement? configuration = null;
        if (SelectedConfiguration(report, options) is { } collected)
        {
            using var document = JsonDocument.Parse(ReportFormatter.ToJson(collected, options));
            configuration = document.RootElement.Clone();
        }
        var data = new Dictionary<string, object?>
        {
            ["schemaVersion"] = 2,
            ["application"] = new { name = "GRELECY Diagnostics", version = ReportFormatter.ApplicationVersion },
            ["createdAt"] = report.CreatedAt.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            ["observation"] = declared,
            ["configuration"] = configuration
        };
        return JsonSerializer.Serialize(data, JsonOptions) + "\n";
    }

    private static DiagnosticReport? SelectedConfiguration(ObservationReport report, ReportOptions options) =>
        options.IncludeWindows || options.IncludeGraphics ? report.Configuration : null;

    private static string? Optional(string value) => value.Length == 0 ? null : value;

    private static string KindCode(ObservationKind kind) => kind switch
    {
        ObservationKind.Problem => "problem",
        ObservationKind.Working => "working",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), "Unknown observation kind")
    };

    private static string CategoryCode(ProblemCategory category) => category switch
    {
        ProblemCategory.WontStart => "wont_start",
        ProblemCategory.VisualIssue => "visual_issue",
        ProblemCategory.Crash => "crash",
        ProblemCategory.Slow => "slow",
        ProblemCategory.Other => "other",
        _ => "unspecified"
    };

    private static string Render(ObservationReport report, UiLanguage language, ReportOptions options, bool markdown)
    {
        var observation = report.Observation;
        _ = KindCode(observation.Kind);
        var problem = observation.Kind == ObservationKind.Problem;
        string T(string key) => LocalizedText.Get(key, language);
        string Display(string value) => value.Length == 0 ? T("observation.notSpecified") : value;
        var output = new StringBuilder();
        void Heading(string text) => output.AppendLine((markdown ? "## " : "") + text).AppendLine();
        void Field(string label, string value)
        {
            var clean = Display(ObservationText.SingleLine(value));
            output.AppendLine($"{(markdown ? "- " : "")}{label} : {(markdown ? ObservationText.EscapeMarkdown(clean) : clean)}");
        }
        void Narrative(string label, string value)
        {
            output.AppendLine(label + " :");
            output.AppendLine();
            var clean = Display(ObservationText.Narrative(value));
            foreach (var line in clean.Split('\n'))
                output.AppendLine(markdown ? "> " + ObservationText.EscapeMarkdown(line) : line);
            output.AppendLine();
        }

        var title = problem
            ? T("observation.grelecyProblemReport")
            : T("observation.grelecyReportedWorkingTest");
        output.AppendLine((markdown ? "# " : "") + title).AppendLine();
        output.AppendLine(T("observation.reportPreparedAt")
            + report.CreatedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
        output.AppendLine(T("observation.thisIsTheReportPreparationTimeNot"));
        output.AppendLine();
        output.AppendLine(T("observation.theInformationBelowIsReportedByThe"));
        if (!problem)
            output.AppendLine(T("observation.workingBehaviorIsReportedOnlyForThe"));
        else
            output.AppendLine(T("observation.thisStatementDescribesAnObservedProblemIt"));
        output.AppendLine();
        Heading(T("observation.userStatement"));
        Field(T("observation.applicationOrGame"), observation.SoftwareName);
        Field(T("observation.softwareVersion"), observation.SoftwareVersion);
        Field(T("observation.graphicsCardStatedByTheUser"), observation.TestedGpu);
        output.AppendLine();
        if (problem)
        {
            var category = observation.Problem switch
            {
                ProblemCategory.WontStart => T("observation.doesNotStart"),
                ProblemCategory.VisualIssue => T("observation.visualIssue"),
                ProblemCategory.Crash => T("observation.crash"),
                ProblemCategory.Slow => T("observation.perceivedSlowness"),
                ProblemCategory.Other => T("observation.other"),
                _ => T("observation.notSpecified")
            };
            Field(T("observation.selectedCategory"), category);
            output.AppendLine();
            Narrative(T("observation.stepsToReproduceTheProblem"), observation.Steps);
            Narrative(T("observation.expectedBehavior"), observation.Expected);
            Narrative(T("observation.whatWasObserved"), observation.Observed);
        }
        else
        {
            Narrative(T("observation.testPerformed"), observation.Steps);
            Narrative(T("observation.reportedDuration"), observation.Duration);
        }
        Narrative(T("observation.settingsUsed"), observation.Settings);
        Narrative(problem
            ? T("observation.otherTestsOrAttemptedSolutions")
            : T("observation.testLimitationsOrUntestedAreas"), observation.Limitations);

        Heading(T("observation.attachedAutomaticConfiguration"));
        if (SelectedConfiguration(report, options) is { } collected)
        {
            output.AppendLine(T("observation.onlyTheSelectedCategoriesAreAttachedThis"));
            output.AppendLine();
            var configuration = markdown
                ? ReportFormatter.ToMarkdown(collected, language, options)
                : ReportFormatter.ToPlainText(collected, language, options);
            output.Append(configuration);
        }
        else
            output.AppendLine(T("observation.noAutomaticConfigurationIsAttachedTheInformation"));
        return output.ToString();
    }
}
