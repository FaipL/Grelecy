// SPDX-License-Identifier: MPL-2.0
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms.Automation;

namespace Grelecy.Diagnostics.Windows;

/// <summary>A local, keyboard-accessible configuration report. No data is sent by this form.</summary>
public sealed class MainForm : Form
{
    private const string JoinUrl = "https://github.com/FaipL/Grelecy/issues/new?template=01-join.yml";
    private readonly Func<DiagnosticReport> collect;
    private readonly ComboBox languagePicker = new() { Name = "language", DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly Label languageLabel = MakeLabel("languageLabel");
    private readonly Label introduction = MakeLabel("introduction");
    private readonly Label stage = MakeLabel("stage");
    private readonly Label status = MakeLabel("status");
    private readonly Label optionsLabel = MakeLabel("optionsLabel");
    private readonly Label shareHelp = MakeLabel("shareHelp");
    private readonly Label footer = MakeLabel("footer");
    private readonly CheckBox includeWindows = MakeCheck("includeWindows", true);
    private readonly CheckBox includeGraphics = MakeCheck("includeGraphics", true);
    private readonly CheckBox includeIds = MakeCheck("includeIds", false);
    private readonly Button scan = MakeButton("scan");
    private readonly Button save = MakeButton("save");
    private readonly Button copy = MakeButton("copy");
    private readonly Button join = MakeButton("join");
    private readonly ProgressBar progress = new() { Name = "progress", Width = 130, Height = 24, Style = ProgressBarStyle.Marquee, Visible = false };
    private readonly TextBox preview = new()
    {
        Name = "preview", Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
        ScrollBars = ScrollBars.Vertical, WordWrap = true, MinimumSize = new Size(100, 120),
        BackColor = SystemColors.Window, ForeColor = SystemColors.WindowText,
    };
    private readonly List<Label> wrappingLabels = [];
    private DiagnosticReport? report;
    private bool busy;
    private bool timedOut;
    private string statusKey = "ready";
    private string savedName = "";
    private UiLanguage Language => languagePicker.SelectedIndex == 0 ? UiLanguage.French : UiLanguage.English;
    private ReportOptions Options => new(includeWindows.Checked, includeGraphics.Checked,
        includeGraphics.Checked && includeIds.Checked);

    // Collector injection allows the Windows UI smoke test to exercise success and failure paths.
    public MainForm(Func<DiagnosticReport>? collector = null)
    {
        collect = collector ?? WindowsCollector.Collect;
        Text = "GRELECY Diagnostics";
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 11F);
        MinimumSize = new Size(640, 560);
        Size = new Size(900, 740);
        StartPosition = FormStartPosition.CenterScreen;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 10,
            Padding = new Padding(22), AutoScroll = true,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < 10; i++)
            root.RowStyles.Add(new RowStyle(i == 6 ? SizeType.Percent : SizeType.AutoSize, i == 6 ? 100 : 0));

        var header = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, AutoSize = true, Margin = new Padding(0, 0, 0, 12) };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var title = MakeLabel("title");
        title.Text = "GRELECY";
        title.Font = new Font(Font.FontFamily, 24F, FontStyle.Bold);
        header.Controls.Add(title, 0, 0);
        var languageBox = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        languagePicker.Items.AddRange(["Français", "English"]);
        languagePicker.SelectedIndex = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fr" ? 0 : 1;
        languageBox.Controls.Add(languageLabel);
        languageBox.Controls.Add(languagePicker);
        header.Controls.Add(languageBox, 1, 0);
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(introduction, 0, 1);

        stage.Font = new Font(Font, FontStyle.Bold);
        root.Controls.Add(stage, 0, 2);
        var scanRow = MakeFlow();
        scanRow.Controls.Add(scan);
        scanRow.Controls.Add(progress);
        root.Controls.Add(scanRow, 0, 3);
        root.Controls.Add(status, 0, 4);

        var options = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = true, Margin = new Padding(0, 8, 0, 8) };
        options.Controls.Add(optionsLabel);
        var choices = MakeFlow();
        choices.Controls.AddRange([includeWindows, includeGraphics, includeIds]);
        options.Controls.Add(choices);
        root.Controls.Add(options, 0, 5);
        root.Controls.Add(preview, 0, 6);

        var actions = MakeFlow();
        actions.Margin = new Padding(0, 12, 0, 4);
        actions.Controls.AddRange([save, copy, join]);
        root.Controls.Add(actions, 0, 7);
        root.Controls.Add(shareHelp, 0, 8);
        root.Controls.Add(footer, 0, 9);
        Controls.Add(root);

        wrappingLabels.AddRange([introduction, stage, status, optionsLabel, shareHelp, footer]);
        void UpdateWrapping()
        {
            var width = Math.Max(180, root.ClientSize.Width - root.Padding.Horizontal - 24);
            foreach (var label in wrappingLabels) label.MaximumSize = new Size(width, 0);
        }
        root.Resize += (_, _) => UpdateWrapping();
        UpdateWrapping();
        status.LiveSetting = AutomationLiveSetting.Polite;
        languagePicker.SelectedIndexChanged += (_, _) => ApplyLanguage();
        foreach (var choice in new[] { includeWindows, includeGraphics, includeIds })
            choice.CheckedChanged += (_, _) =>
            {
                if (report is not null) SetStatus("updated");
                RefreshPreview();
            };
        scan.Click += async (_, _) => await ScanAsync();
        save.Click += (_, _) => SaveReport();
        copy.Click += (_, _) => CopyReport();
        join.Click += (_, _) => OpenJoinForm();
        ApplyLanguage();
        Shown += (_, _) =>
        {
            // Fit the initial window to the current work area, including display scaling.
            FitToScreen();
            scan.Focus();
        };
        DpiChanged += (_, _) => BeginInvoke((Action)FitToScreen);
    }

    private void FitToScreen()
    {
        var area = Screen.FromControl(this).WorkingArea;
        MinimumSize = new Size(Math.Min(640, area.Width), Math.Min(560, area.Height));
        Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
        Location = new Point(Math.Clamp(Left, area.Left, area.Right - Width),
            Math.Clamp(Top, area.Top, area.Bottom - Height));
    }

    private static Label MakeLabel(string name) => new()
    {
        Name = name, AutoSize = true, Margin = new Padding(0, 3, 0, 7), UseMnemonic = true,
    };

    private static CheckBox MakeCheck(string name, bool selected) => new()
    {
        Name = name, AutoSize = true, Checked = selected, Margin = new Padding(0, 3, 16, 3),
    };

    private static Button MakeButton(string name) => new()
    {
        Name = name, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(110, 36), Padding = new Padding(8, 3, 8, 3), Margin = new Padding(0, 3, 12, 3),
    };

    private static FlowLayoutPanel MakeFlow() => new()
    {
        Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0),
    };

    private string T(string key) => UiText.Get(key, Language);

    private void ApplyLanguage()
    {
        if (report is not null && statusKey is "copied" or "saved") statusKey = "updated";
        Text = T("windowTitle");
        languageLabel.Text = T("language");
        languagePicker.AccessibleName = T("languageName");
        introduction.Text = T("introduction");
        optionsLabel.Text = T("options");
        includeWindows.Text = T("windows");
        includeGraphics.Text = T("graphics");
        includeIds.Text = T("ids");
        includeIds.AccessibleDescription = T("idsHelp");
        save.Text = T("save");
        copy.Text = T("copy");
        join.Text = T("join");
        shareHelp.Text = T("shareHelp");
        footer.Text = T("footer");
        preview.AccessibleName = T("previewName");
        progress.AccessibleName = T("reading");
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        includeIds.Enabled = includeGraphics.Checked;
        scan.Enabled = !busy;
        scan.Text = T(busy ? "reading" : report is null ? "scan" : "scanAgain");
        stage.Text = T(report is null ? "stage1" : "stage2");
        SetStatus(statusKey);
        save.Enabled = copy.Enabled = report is not null && !busy && (Options.IncludeWindows || Options.IncludeGraphics);
        preview.Text = report is null ? T("emptyPreview") : ReportFormatter.ToPlainText(report, Language, Options);
        preview.SelectionStart = 0;
        preview.SelectionLength = 0;
    }

    private void SetStatus(string key)
    {
        statusKey = key;
        status.Text = T(key) + (key == "saved" ? " " + savedName : "");
        if (status.IsHandleCreated) status.AccessibilityObject.RaiseLiveRegionChanged();
    }

    private async Task ScanAsync()
    {
        if (busy) return;
        busy = true;
        timedOut = false;
        report = null;
        progress.Visible = true;
        SetStatus("readingHelp");
        RefreshPreview();
        var task = Task.Run(collect);
        try
        {
            var result = await task.WaitAsync(TimeSpan.FromSeconds(45));
            if (IsDisposed) return;
            report = result;
            SetStatus(result.Notices.Count > 0 ? "partial" : "complete");
        }
        catch (TimeoutException)
        {
            if (IsDisposed) return;
            timedOut = true;
            progress.Visible = false;
            SetStatus("timeout");
            // WMI calls cannot reliably be cancelled. Do not stack more scans or accept a late report.
            try { await task; } catch (Exception) { /* Never show raw system exceptions. */ }
            if (!IsDisposed) SetStatus("retry");
        }
        catch (Exception)
        {
            if (!IsDisposed) SetStatus("failed");
        }
        finally
        {
            if (!IsDisposed)
            {
                busy = false;
                progress.Visible = false;
                RefreshPreview();
                if (report is not null && !timedOut) preview.Focus();
                else scan.Focus();
            }
        }
    }

    private void SaveReport()
    {
        if (report is null || !save.Enabled) return;
        using var dialog = new SaveFileDialog
        {
            Title = T("saveTitle"), Filter = T("fileFilter"), DefaultExt = "md",
            FileName = "GRELECY-" + report.CollectedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            AddExtension = true, OverwritePrompt = true, RestoreDirectory = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var extension = Path.GetExtension(dialog.FileName);
        if (!extension.Equals(".md", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, T("extensionError"), T("saveTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var text = extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            ? ReportFormatter.ToJson(report, Options)
            : ReportFormatter.ToMarkdown(report, Language, Options);
        try
        {
            File.WriteAllText(dialog.FileName, text, new UTF8Encoding(false));
            savedName = Path.GetFileName(dialog.FileName);
            SetStatus("saved");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            MessageBox.Show(this, T("saveError"), T("saveTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private void CopyReport()
    {
        if (report is null || !copy.Enabled) return;
        try
        {
            Clipboard.SetText(ReportFormatter.ToMarkdown(report, Language, Options));
            SetStatus("copied");
        }
        catch (ExternalException)
        {
            MessageBox.Show(this, T("copyError"), T("windowTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void OpenJoinForm()
    {
        try
        {
            Process.Start(new ProcessStartInfo(JoinUrl) { UseShellExecute = true });
            SetStatus("opened");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or System.Security.SecurityException)
        {
            MessageBox.Show(this, T("browserError") + "\n\n" + JoinUrl, T("windowTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
