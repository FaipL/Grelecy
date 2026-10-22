// SPDX-License-Identifier: MPL-2.0
namespace Grelecy.Diagnostics.Windows;

internal static class UiText
{
    private static readonly Dictionary<string, (string French, string English)> Texts = new()
    {
        ["windowTitle"] = ("GRELECY — Ma configuration", "GRELECY — My configuration"),
        ["language"] = ("L&angue", "&Language"),
        ["languageName"] = ("Langue de l’application", "Application language"),
        ["introduction"] = ("Participez au projet, à votre rythme. Préparez une fiche de votre PC pour aider la communauté à mieux connaître le matériel disponible.", "Join the project at your own pace. Prepare a PC configuration report to help the community understand the hardware available."),
        ["stage1"] = ("1. Lire ma configuration", "1. Read my configuration"),
        ["stage2"] = ("2. Vérifier les informations · 3. Choisir comment participer", "2. Review the information · 3. Choose how to participate"),
        ["scan"] = ("&Lire ma configuration", "&Read my configuration"),
        ["scanAgain"] = ("&Relire ma configuration", "&Read my configuration again"),
        ["reading"] = ("Lecture en cours…", "Reading…"),
        ["ready"] = ("Lecture locale de Windows, des cartes graphiques et de leurs pilotes. Aucun compte ni installation de pilote nécessaire.", "Windows, graphics cards and their drivers are read locally. No account or driver installation is needed."),
        ["readingHelp"] = ("Windows prépare les informations. Vous pouvez continuer à utiliser cette fenêtre.", "Windows is gathering the information. You can continue to use this window."),
        ["options"] = ("Informations à inclure dans l’aperçu et le rapport :", "Information to include in the preview and report:"),
        ["windows"] = ("&Windows", "&Windows"),
        ["graphics"] = ("Cartes &graphiques et pilotes", "&Graphics cards and drivers"),
        ["ids"] = ("&Détails matériels (facultatif)", "Hardware &details (optional)"),
        ["idsHelp"] = ("Codes du modèle de carte pour les chercheurs. Aucun numéro de série ni identifiant unique du PC.", "Card model codes for researchers. No serial number or unique PC identifier."),
        ["emptyPreview"] = ("Votre rapport apparaîtra ici.\r\n\r\nVous pourrez choisir les rubriques à partager avant de l’enregistrer ou de le copier.\r\n\r\nCet outil ne relève ni nom de compte, ni nom du PC, ni numéro de série. Il n’envoie aucune donnée.\r\n\r\nIl décrit la configuration : il ne teste pas les performances et ne garantit pas la compatibilité des jeux ou applications.\r\n\r\nVous pouvez aussi rejoindre le projet sans rapport, avec le bouton « Participer sur GitHub ».", "Your report will appear here.\r\n\r\nYou can choose which sections to share before saving or copying it.\r\n\r\nThis tool does not collect account names, PC names or serial numbers. It sends no data.\r\n\r\nIt describes the configuration: it does not test performance or guarantee game or application compatibility.\r\n\r\nYou can also join without a report using the Join on GitHub button."),
        ["previewName"] = ("Aperçu du rapport, en lecture seule", "Report preview, read only"),
        ["complete"] = ("La lecture est terminée. Vérifiez votre fiche et choisissez ce que vous souhaitez partager.", "Reading is complete. Review your report and choose what you would like to share."),
        ["partial"] = ("Certaines informations sont indisponibles. Le rapport indique ce qui a pu être lu ; vous pouvez tout de même participer.", "Some information is unavailable. The report shows what could be read; you can still participate."),
        ["failed"] = ("Windows n’a pas pu fournir les informations. Réessayez ou rejoignez le projet sans rapport.", "Windows could not provide the information. Try again or join the project without a report."),
        ["timeout"] = ("Windows met plus de temps que prévu. Vous pouvez fermer l’outil ou participer sans rapport. Une nouvelle lecture sera possible quand Windows aura terminé.", "Windows is taking longer than expected. You can close the tool or join without a report. You can retry when Windows finishes."),
        ["retry"] = ("La lecture trop longue a été écartée. Vous pouvez réessayer ou participer sans rapport.", "The delayed reading has been discarded. You can retry or join without a report."),
        ["save"] = ("&Enregistrer le rapport…", "&Save the report…"),
        ["copy"] = ("&Copier le rapport", "&Copy the report"),
        ["join"] = ("&Participer sur GitHub", "&Join on GitHub"),
        ["shareHelp"] = ("Le formulaire s’ouvre dans votre navigateur. Vous pouvez y coller le rapport copié. Rien n’est envoyé automatiquement. Les contributions GitHub sont publiques et nécessitent un compte GitHub.", "The form opens in your browser. You can paste the copied report there. Nothing is sent automatically. GitHub contributions are public and require a GitHub account."),
        ["footer"] = ("Prototype 0.1 · Communauté indépendante · Kepler, Maxwell et Pascal bienvenus, ainsi que les autres contributeurs.", "Prototype 0.1 · Independent community · Kepler, Maxwell and Pascal owners and other contributors are welcome."),
        ["saveTitle"] = ("Enregistrer mon rapport", "Save my report"),
        ["fileFilter"] = ("Rapport lisible Markdown (*.md)|*.md|Données structurées JSON (*.json)|*.json", "Readable Markdown report (*.md)|*.md|Structured JSON data (*.json)|*.json"),
        ["saved"] = ("Rapport enregistré :", "Report saved:"),
        ["copied"] = ("Rapport copié. Vous pouvez le coller dans le formulaire GitHub ou dans un message de votre choix.", "Report copied. You can paste it in the GitHub form or a message of your choice."),
        ["updated"] = ("Rapport modifié. Copiez ou enregistrez cette nouvelle version : les copies précédentes conservent les anciennes informations.", "Report changed. Copy or save this new version: previous copies still contain the old information."),
        ["opened"] = ("Formulaire ouvert dans le navigateur. Aucune donnée du rapport n’a été transmise.", "Form opened in your browser. No report data was transmitted."),
        ["saveError"] = ("Le rapport n’a pas pu être enregistré. Choisissez un dossier dans lequel vous pouvez écrire, ou utilisez « Copier le rapport ».", "The report could not be saved. Choose a folder you can write to, or use Copy the report."),
        ["copyError"] = ("Le presse-papiers est momentanément indisponible. Réessayez ou enregistrez le rapport.", "The clipboard is temporarily unavailable. Try again or save the report."),
        ["extensionError"] = ("Choisissez un fichier .md pour un rapport lisible ou .json pour les données structurées.", "Choose a .md file for a readable report or .json for structured data."),
        ["browserError"] = ("Le navigateur n’a pas pu être ouvert. Vous pouvez utiliser cette adresse :", "The browser could not be opened. You can use this address:"),
    };

    public static string Get(string key, UiLanguage language) => language == UiLanguage.French
        ? Texts[key].French : Texts[key].English;
}
