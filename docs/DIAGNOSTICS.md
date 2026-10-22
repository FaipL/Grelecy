<!-- SPDX-License-Identifier: CC-BY-4.0 -->

# GRELECY Diagnostics — prototype 0.1

## Français

Une application Windows avec interface graphique pour préparer une fiche de configuration et participer à GRELECY. Aucun terminal, compte ou accès administrateur n'est nécessaire pour lire la configuration et enregistrer le rapport. L'interface est disponible en français et en anglais.

### Démarrer

1. Télécharger l'archive **GRELECY-Diagnostics-win-x64.zip** jointe à une construction du workflow **Windows diagnostics** et extraire son contenu dans un dossier.
2. Ouvrir **GRELECY-Diagnostics.exe** par double-clic. Le runtime .NET est inclus ; aucune installation de .NET n'est nécessaire.
3. Choisir **Lire ma configuration**, puis vérifier le rapport affiché.
4. Cocher uniquement les rubriques à partager. Les détails matériels sont facultatifs et désactivés au départ.
5. Utiliser **Copier le rapport** pour le coller dans un message ou **Enregistrer le rapport…** pour garder un fichier Markdown lisible. Le format JSON est proposé dans la liste des types de fichiers.
6. **Participer sur GitHub** ouvre le formulaire de présentation dans le navigateur. Cette action ne transmet pas le rapport : coller son contenu dans le formulaire si souhaité. La publication sur GitHub est publique et nécessite un compte GitHub.

Il est possible de participer sans rapport, sans GPU ciblé et sans savoir programmer. Si les rubriques ou la langue changent après une copie ou un enregistrement, recopier ou enregistrer le nouveau rapport : les anciennes copies conservent leur contenu.

### Ce que l'outil relève

- L'édition, la version, le build et l'architecture de Windows.
- Les noms des cartes graphiques et la version Windows de leurs pilotes, y compris sur un PC avec plusieurs GPU. Ce numéro peut différer du numéro commercial du pilote NVIDIA.
- Sur demande dans le rapport : les codes PCI du fabricant, du modèle, du sous-système et de la révision. Ils décrivent le matériel, pas un appareil unique.

La collecte s'effectue localement avec WMI, en lecture seule. Les rubriques sont lues en mémoire, puis les cases déterminent ce qui apparaît dans l'aperçu et les exports. Aucun nom de compte, nom de PC, numéro de série, chemin d'instance PNP complet ou journal brut n'est conservé dans le modèle du rapport. Les messages d'erreur système bruts ne sont pas exportés. L'application n'a ni télémétrie ni envoi automatique ; seul le bouton GitHub ouvre une page web dans le navigateur.

Ce relevé **ne teste pas les performances, les jeux ou la compatibilité**. Il ne modifie aucun pilote et ne déduit pas automatiquement l'architecture Kepler, Maxwell ou Pascal à partir du nom commercial. Des informations peuvent manquer : cela est signalé et n'empêche pas de participer. La quantité de mémoire vidéo n'est pas relevée, car les informations WMI correspondantes peuvent être inexactes.

### État du prototype et premier essai

La cible est Windows 10/11 **x64**. Les PC ARM64 et Windows 32 bits ne font pas partie de ce premier paquet. Le prototype n'est pas signé numériquement ; Windows peut donc afficher un avertissement d'éditeur inconnu. Respecter les protections de Windows. Une distribution publique signée reste un travail distinct.

Les tests automatisés vérifient le format, le filtrage des données et le parcours de l'interface avec des configurations simulées. Un essai sur une vraie machine, notamment la GTX 1060 de l'initiateur, reste nécessaire pour confirmer la collecte, l'affichage et l'usage courant. Voir la liste de vérification ci-dessous. Il n'existe pas encore de version déclarée stable ou de pilote GRELECY.

## English

GRELECY Diagnostics is a graphical Windows prototype for preparing a local configuration report. It works without a terminal, administrator rights, a .NET installation or an account. French and English are available in the same application.

1. Download **GRELECY-Diagnostics-win-x64.zip** from a **Windows diagnostics** workflow build, extract it and double-click **GRELECY-Diagnostics.exe**.
2. Choose **Read my configuration** and review the results.
3. Select which categories to include. Optional PCI hardware details are off by default.
4. **Copy the report** or **Save the report…** as Markdown or JSON. After changing the selection or language, copy/save again; earlier copies keep their original content.
5. **Join on GitHub** opens an introduction form in your browser. Nothing is uploaded automatically. Paste the report if you want to share it. GitHub submissions are public and require a GitHub account. You can join without a report or a target GPU.

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

Output: `artifacts/GRELECY-Diagnostics-win-x64.zip` and its SHA-256 file. The script accepts `--dotnet /path/to/dotnet` for a locally installed SDK. It builds a self-contained executable and includes the usage guide, GRELECY licenses and Microsoft notices. It does not sign, upload or release the archive.

### Windows acceptance checklist

- Open the extracted program by double-click as a standard user on a PC without a separately installed .NET runtime.
- Confirm Windows and GPU/driver values against Windows Settings and Device Manager, including both GPUs on a hybrid laptop when available.
- Switch FR/EN before and after collection; keep results and options.
- Uncheck each category; verify both the displayed report and the saved Markdown/JSON omit it. Enable hardware details and confirm no instance suffix or serial is present.
- Save both formats, cancel the file dialog, try a non-writable destination, and copy/paste into a text editor. Confirm the selected file type matches its extension/content.
- Navigate by keyboard only (Tab, Shift+Tab, Space and button shortcuts). Check Narrator announces results/errors, high contrast, a small screen and 150–200% display scaling.
- Check partial/unavailable WMI results and retry behavior. A long-running WMI query must not freeze the UI or start duplicate background scans. Closing the window should always work.
- Open the GitHub form separately and verify that no report content was added to the URL or submitted automatically.

Record the actual Windows build, GPU and observed results. Mark untested checks explicitly; do not replace them with the automated runner's outcome.
