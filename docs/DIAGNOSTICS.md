<!-- SPDX-License-Identifier: CC-BY-4.0 -->

# GRELECY Diagnostics — prototype 0.2

## Français

Une application Windows avec interface graphique pour partager un problème, un essai réussi ou une fiche de configuration et participer à GRELECY. Aucun terminal, compte ou accès administrateur n'est nécessaire pour préparer et enregistrer le rapport. L'interface est disponible en français et en anglais.

### Démarrer

1. Télécharger l'archive **GRELECY-Diagnostics-0.2.0-preview.1-win-x64.zip** jointe à une construction du workflow **Windows diagnostics** et extraire son contenu dans un dossier.
2. Ouvrir **GRELECY-Diagnostics.exe** par double-clic. Le runtime .NET est inclus ; aucune installation de .NET n'est nécessaire.
3. Choisir **Signaler un problème**, **Partager un résultat qui fonctionne**, ou **Lire ma configuration** pour un relevé seul.
4. Dans les parcours guidés, indiquer le logiciel, puis raconter l'essai. Sa version et la carte utilisée peuvent rester inconnues. La carte n'est jamais choisie automatiquement parmi les GPU détectés.
5. À la dernière étape, vérifier l'aperçu et ajouter la configuration de ce PC si souhaité. Les choix de la fenêtre principale sont conservés. Les cases concernent uniquement le relevé automatique ; le récit et la carte que vous avez indiquée restent inclus, comme le montre l'aperçu.
6. Utiliser **Copier le rapport** pour le coller dans un message ou **Enregistrer…** pour garder un fichier Markdown lisible. Le format JSON est proposé dans la liste des types de fichiers.
7. **Ouvrir le formulaire GitHub** ouvre le formulaire correspondant au problème ou à l'essai réussi. Coller le rapport si souhaité. Aucune réponse n'est transmise dans l'adresse du navigateur. Les nouveaux modèles de formulaire deviennent disponibles quand les changements sont fusionnés sur la branche principale ; auparavant, utiliser la copie ou le fichier pour conserver son rapport. Le bouton **Participer sur GitHub** de la fenêtre principale reste disponible pour une présentation libre.

Pour un problème, décrire au minimum ce qui se passe ; les étapes, le résultat attendu, les réglages et les solutions déjà tentées peuvent être ajoutés. Pour un essai réussi, décrire ce qui a effectivement fonctionné ; préciser si possible la durée, les réglages et ce qui n'a pas été essayé. Un succès concerne cet essai précis, pas une garantie générale de compatibilité.

Il est possible de participer sans relevé automatique, sans GPU ciblé et sans savoir programmer. Le récit reste partageable si la détection échoue ou dépasse son délai ; une lecture encore en cours n'est pas relancée en parallèle dans cette fenêtre et son résultat tardif est écarté. Si les rubriques ou la langue changent après une copie ou un enregistrement, recopier ou enregistrer le nouveau rapport : les anciennes copies conservent leur contenu. Publier sur GitHub rend la contribution publique et nécessite un compte GitHub.

### Ce que l'outil relève

- L'édition, la version, le build et l'architecture de Windows.
- Les noms des cartes graphiques et la version Windows de leurs pilotes, y compris sur un PC avec plusieurs GPU. Ce numéro peut différer du numéro commercial du pilote NVIDIA.
- Sur demande dans le rapport : les codes PCI du fabricant, du modèle, du sous-système et de la révision. Ils décrivent le matériel, pas un appareil unique.

La collecte s'effectue localement avec WMI, en lecture seule. Les rubriques sont lues en mémoire, puis les cases déterminent ce qui apparaît dans l'aperçu et les exports. Aucun nom de compte, nom de PC, numéro de série, chemin d'instance PNP complet ou journal brut n'est conservé dans le modèle du rapport. Les messages d'erreur système bruts ne sont pas exportés. L'application n'a ni télémétrie ni envoi automatique ; seul le bouton GitHub ouvre une page web dans le navigateur.

Le récit saisi par l'utilisateur est distinct du relevé automatique et reste visible dans l'aperçu. Ne pas y inclure d'informations personnelles à publier involontairement. Les notes acceptent jusqu'à 4 000 caractères par zone ; le nom du logiciel, sa version et le nom de carte sont limités à 256 caractères. La date de préparation du rapport ne représente pas la date de l'essai et n'est pas présentée comme telle.

Ce relevé **ne teste pas les performances, les jeux ou la compatibilité**. Il ne modifie aucun pilote et ne déduit pas automatiquement l'architecture Kepler, Maxwell ou Pascal à partir du nom commercial. Des informations peuvent manquer : cela est signalé et n'empêche pas de participer. La quantité de mémoire vidéo n'est pas relevée, car les informations WMI correspondantes peuvent être inexactes.

### État du prototype et premier essai

La cible est Windows 10/11 **x64**. Les PC ARM64 et Windows 32 bits ne font pas partie de ce premier paquet. Le prototype n'est pas signé numériquement ; Windows peut donc afficher un avertissement d'éditeur inconnu. Respecter les protections de Windows. Une distribution publique signée reste un travail distinct.

Les tests automatisés vérifient le format, le filtrage des données et les parcours avec des configurations simulées, ainsi que la collecte Windows sur le serveur de test. Le collecteur initial a été essayé avec succès par l'initiateur. Les nouveaux parcours, l'accessibilité et les variantes d'affichage restent à essayer sur de vraies machines. Voir la liste de vérification ci-dessous. Il n'existe pas encore de version déclarée stable ou de pilote GRELECY.

## English

GRELECY Diagnostics is a graphical Windows prototype for preparing a problem report, a successful-test report or a local configuration report. It works without a terminal, administrator rights, a .NET installation or an account. French and English are available in the same application.

1. Download **GRELECY-Diagnostics-0.2.0-preview.1-win-x64.zip** from a **Windows diagnostics** workflow build, extract it and double-click **GRELECY-Diagnostics.exe**.
2. Choose **Report a problem**, **Share a successful test** or **Read my configuration**.
3. The guided paths ask for the application and what happened or what you successfully tried. Versions, reproduction steps and the tested GPU can remain unknown. A detected GPU is never automatically assumed to be the card used for the test.
4. Review your statement. Optionally add this PC's configuration and select the automatic categories to include. Optional PCI details are off by default; the main window's exclusions carry over. Your own statement, including a card you name, stays in the report.
5. **Copy the report** or **Save…** as Markdown or JSON. After changing the selection or language, copy/save again; earlier copies keep their original content.
6. **Open the GitHub form** opens the corresponding problem/working-test form. Paste the report if you want to share it. These templates are available once this change is merged to the default branch. Before then, save or copy the report. **Join on GitHub** in the main window opens the existing introduction form. GitHub submissions are public and require an account.

Nothing is uploaded automatically, and no report data is added to browser URLs. You can contribute without collected hardware information: a failed or timed-out collection does not block a manual observation. User-entered notes are distinct from automatically read information. Review them for personal information. A successful-test statement applies only to the scenario described. Report preparation time is not presented as the time of the test.

The tool reads Windows edition/version/build/architecture, graphics card names and Windows driver versions using local, read-only WMI queries. Model-level PCI identifiers are optional in exports. It does not retain account names, PC names, serial numbers, full PNP instance paths or raw system errors in the report. There is no telemetry or automatic upload. It does not measure performance or compatibility, change drivers, infer GPU architectures or report potentially misleading WMI video-memory values.

The initial package targets Windows 10/11 **x64**, with .NET included. ARM64 and 32-bit Windows are not part of this package. This prototype is unsigned; Windows may show an unknown-publisher warning. Respect Windows protections. Automated checks do not replace a real PC usability/collection test. A signed public release and stable support remain future work.

## Developer workflow

Install the .NET SDK pinned in [`global.json`](../global.json). Run commands from the repository root. Source is MPL-2.0; this guide is CC-BY-4.0. The package includes notices for the bundled Microsoft .NET components.

```sh
dotnet run --project tests/Grelecy.Diagnostics.Tests --configuration Release
dotnet build src/Grelecy.Diagnostics.Windows --configuration Release
```

On Windows, also run the UI smoke checks and launch the application:

```sh
dotnet run --project tests/Grelecy.Diagnostics.Windows.SmokeTests --configuration Release
dotnet run --project src/Grelecy.Diagnostics.Windows --configuration Release
```

On Linux, the core tests run and the Windows target can be compiled/published; WinForms and WMI cannot run. Do not describe a cross-compilation as a Windows runtime test.

To produce the portable x64 archive on Windows or Linux (Python 3.12+):

```sh
python scripts/package_diagnostics.py
```

Output: `artifacts/GRELECY-Diagnostics-<version>-win-x64.zip` and its SHA-256 file. The script accepts `--dotnet /path/to/dotnet` for a locally installed SDK. It builds a self-contained executable and includes the usage guide, GRELECY licenses and Microsoft notices. It does not sign, upload or release the archive.

Configuration-only JSON retains schema version 1. Guided reports use schema version 2 with a `user_reported` observation, a report `createdAt` time, and an optional nested, filtered configuration using schema 1. A missing configuration is `null`, with no invented collection timestamp. There is no global `compatible` or `supported` verdict. The application does not maintain or publish a compatibility database.

### Windows acceptance checklist

- Open the extracted program by double-click as a standard user on a PC without a separately installed .NET runtime.
- Confirm Windows and GPU/driver values against Windows Settings and Device Manager, including both GPUs on a hybrid laptop when available.
- Switch FR/EN before and after collection; keep results and options.
- Complete both guided paths, go back and change language; verify every entered field survives and appears in the report. A successful test must describe its scope and untested areas when known.
- Open a guided path with Windows or graphics excluded in the main window; ensure those exclusions carry over. Check a manually entered card name remains distinct from collected GPU data.
- Use a guided report with no collection, a failed collection and a timed-out collection; its manual statement must still be exportable. Late data must not change a report after timeout.
- Uncheck each category; verify both the displayed report and the saved Markdown/JSON omit it. Enable hardware details and confirm no instance suffix or serial is present.
- Save both formats, cancel the file dialog, try a non-writable destination, and copy/paste into a text editor. Confirm the selected file type matches its extension/content.
- Navigate by keyboard only (Tab, Shift+Tab, Space and button shortcuts). Check Narrator announces results/errors, high contrast, a small screen and 150–200% display scaling.
- Check partial/unavailable WMI results and retry behavior. A long-running WMI query must not freeze the UI or start duplicate background scans. Closing the window should always work.
- Open the GitHub form separately and verify that no report content was added to the URL or submitted automatically.

Record the actual Windows build, GPU and observed results. Mark untested checks explicitly; do not replace them with the automated runner's outcome.
