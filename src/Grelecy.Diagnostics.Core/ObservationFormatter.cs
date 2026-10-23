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
        var french = language == UiLanguage.French;
        var problem = observation.Kind == ObservationKind.Problem;
        string T(string fr, string en) => french ? fr : en;
        string Display(string value) => value.Length == 0 ? T("Non précisé", "Not specified") : value;
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
            ? T("GRELECY — Problème signalé", "GRELECY — Problem report")
            : T("GRELECY — Essai déclaré comme fonctionnel", "GRELECY — Reported working test");
        output.AppendLine((markdown ? "# " : "") + title).AppendLine();
        output.AppendLine(T("Rapport préparé le ", "Report prepared at ")
            + report.CreatedAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture));
        output.AppendLine(T("Cette date est celle de préparation du rapport, pas celle de l’essai.",
            "This is the report preparation time, not the time of the test."));
        output.AppendLine();
        output.AppendLine(T("Les informations ci-dessous sont déclarées par l’utilisateur. Elles sont distinctes du relevé automatique.",
            "The information below is reported by the user. It is separate from automatic collection."));
        if (!problem)
            output.AppendLine(T("Le fonctionnement est déclaré uniquement pour l’essai décrit. Il ne garantit pas la compatibilité générale du logiciel ou du matériel.",
                "Working behavior is reported only for the test described. It does not guarantee general software or hardware compatibility."));
        else
            output.AppendLine(T("Cette déclaration décrit un problème observé ; elle n’en établit pas la cause.",
                "This statement describes an observed problem; it does not establish its cause."));
        output.AppendLine();
        Heading(T("Déclaration de l’utilisateur", "User statement"));
        Field(T("Logiciel ou jeu", "Application or game"), observation.SoftwareName);
        Field(T("Version du logiciel", "Software version"), observation.SoftwareVersion);
        Field(T("Carte graphique indiquée par l’utilisateur", "Graphics card stated by the user"), observation.TestedGpu);
        output.AppendLine();
        if (problem)
        {
            var category = observation.Problem switch
            {
                ProblemCategory.WontStart => T("Ne démarre pas", "Does not start"),
                ProblemCategory.VisualIssue => T("Problème d’affichage", "Visual issue"),
                ProblemCategory.Crash => T("Plantage", "Crash"),
                ProblemCategory.Slow => T("Lenteur ressentie", "Perceived slowness"),
                ProblemCategory.Other => T("Autre", "Other"),
                _ => T("Non précisé", "Not specified")
            };
            Field(T("Catégorie choisie", "Selected category"), category);
            output.AppendLine();
            Narrative(T("Étapes pour reproduire le problème", "Steps to reproduce the problem"), observation.Steps);
            Narrative(T("Résultat attendu", "Expected behavior"), observation.Expected);
            Narrative(T("Ce qui a été observé", "What was observed"), observation.Observed);
        }
        else
        {
            Narrative(T("Essai effectué", "Test performed"), observation.Steps);
            Narrative(T("Durée indiquée", "Reported duration"), observation.Duration);
        }
        Narrative(T("Réglages utilisés", "Settings used"), observation.Settings);
        Narrative(problem
            ? T("Autres essais ou solutions tentées", "Other tests or attempted solutions")
            : T("Limites de l’essai ou points non testés", "Test limitations or untested areas"), observation.Limitations);

        Heading(T("Relevé automatique joint", "Attached automatic configuration"));
        if (SelectedConfiguration(report, options) is { } collected)
        {
            output.AppendLine(T("Seules les rubriques sélectionnées sont jointes. Ce relevé ne prouve pas le résultat de l’essai déclaré.",
                "Only the selected categories are attached. This configuration does not prove the reported test result."));
            output.AppendLine();
            var configuration = markdown
                ? ReportFormatter.ToMarkdown(collected, language, options)
                : ReportFormatter.ToPlainText(collected, language, options);
            output.Append(configuration);
        }
        else
            output.AppendLine(T("Aucun relevé automatique joint. Les informations saisies restent une déclaration de l’utilisateur.",
                "No automatic configuration is attached. The information entered remains a user statement."));
        return output.ToString();
    }
}
