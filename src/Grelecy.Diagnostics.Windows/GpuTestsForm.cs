// SPDX-License-Identifier: MPL-2.0
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms.Automation;

namespace Grelecy.Diagnostics.Windows;

/// <summary>Opt-in, local GPU checks with a report scoped to the exact tests performed.</summary>
public sealed class GpuTestsForm : Form
{
    private readonly Func<CancellationToken, Task<GpuAdapterScan>> enumerate;
    private readonly Func<GpuTestAdapter, CancellationToken, Task<GpuTestReport>> run;
    private readonly ComboBox languagePicker = new()
    {
        Name = "gpuLanguage", DropDownStyle = ComboBoxStyle.DropDownList, Width = 180,
        RightToLeft = RightToLeft.No,
    };
    private readonly ComboBox adapterPicker = new()
    {
        Name = "gpuAdapter", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Fill,
        FormattingEnabled = true, MinimumSize = new Size(200, 0),
    };
    private readonly Label languageLabel = MakeLabel("gpuLanguageLabel");
    private readonly Label introduction = MakeLabel("gpuIntroduction");
    private readonly Label adapterLabel = MakeLabel("gpuAdapterLabel");
    private readonly Label status = MakeLabel("gpuStatus");
    private readonly Label optionsLabel = MakeLabel("gpuOptionsLabel");
    private readonly Label sharing = MakeLabel("gpuSharing");
    private readonly Button load = MakeButton("gpuLoad");
    private readonly Button start = MakeButton("gpuRun");
    private readonly Button cancel = MakeButton("gpuCancel");
    private readonly Button save = MakeButton("gpuSave");
    private readonly Button copy = MakeButton("gpuCopy");
    private readonly Button close = MakeButton("gpuClose");
    private readonly CheckBox includeGraphics = new()
    {
        Name = "gpuIncludeGraphics", AutoSize = true, Checked = true, Margin = new Padding(0, 3, 16, 3),
    };
    private readonly CheckBox includeIds = new()
    {
        Name = "gpuIncludeIds", AutoSize = true, Checked = false, Margin = new Padding(0, 3, 16, 3),
    };
    private readonly ProgressBar progress = new()
    {
        Name = "gpuProgress", Width = 130, Height = 24, Style = ProgressBarStyle.Marquee, Visible = false,
    };
    private readonly TextBox preview = new()
    {
        Name = "gpuPreview", Multiline = true, ReadOnly = true, Dock = DockStyle.Fill,
        ScrollBars = ScrollBars.Vertical, WordWrap = true, MinimumSize = new Size(100, 120),
        BackColor = SystemColors.Window, ForeColor = SystemColors.WindowText,
    };
    private GpuTestReport? report;
    private CancellationTokenSource? operation;
    private bool closing;
    private string statusKey = "gpu.ready";
    private bool Busy => operation is not null;
    public UiLanguage SelectedLanguage => LanguageCatalog.All[Math.Max(0, languagePicker.SelectedIndex)].Language;
    private GpuTestReportOptions Options => new(includeGraphics.Checked,
        includeGraphics.Checked && includeIds.Checked);

    // Delegates keep the UI checks independent from the test computer's actual GPU.
    public GpuTestsForm(UiLanguage language = UiLanguage.English,
        Func<CancellationToken, Task<GpuAdapterScan>>? enumerator = null,
        Func<GpuTestAdapter, CancellationToken, Task<GpuTestReport>>? runner = null)
    {
        var worker = new GpuProbeRunner();
        enumerate = enumerator ?? worker.EnumerateAsync;
        run = runner ?? worker.RunAsync;
        AutoScaleMode = AutoScaleMode.Dpi;
        Font = new Font("Segoe UI", 11F);
        MinimumSize = new Size(660, 600);
        Size = new Size(920, 780);
        StartPosition = FormStartPosition.CenterParent;
        Icon = Branding.CreateIcon();
        Disposed += (_, _) =>
        {
            closing = true;
            operation?.Cancel();
            Icon?.Dispose();
        };

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 9,
            Padding = new Padding(22), AutoScroll = true,
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var i = 0; i < root.RowCount; i++)
            root.RowStyles.Add(new RowStyle(i == 6 ? SizeType.Percent : SizeType.AutoSize, i == 6 ? 100 : 0));
        var header = MakeFlow();
        header.Controls.Add(Branding.CreateLogoPictureBox("gpuBrandLogo"));
        header.Controls.AddRange([languageLabel, languagePicker]);
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(introduction, 0, 1);
        var actions = MakeFlow();
        actions.Controls.AddRange([load, start, cancel, progress]);
        root.Controls.Add(actions, 0, 2);
        var selection = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = true };
        selection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        selection.Controls.Add(adapterLabel, 0, 0);
        selection.Controls.Add(adapterPicker, 0, 1);
        root.Controls.Add(selection, 0, 3);
        root.Controls.Add(status, 0, 4);
        var options = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, AutoSize = true };
        options.Controls.Add(optionsLabel, 0, 0);
        var choices = MakeFlow();
        choices.Controls.AddRange([includeGraphics, includeIds]);
        options.Controls.Add(choices, 0, 1);
        root.Controls.Add(options, 0, 5);
        root.Controls.Add(preview, 0, 6);
        var exports = MakeFlow();
        exports.Margin = new Padding(0, 12, 0, 4);
        exports.Controls.AddRange([save, copy, close]);
        root.Controls.Add(exports, 0, 7);
        root.Controls.Add(sharing, 0, 8);
        Controls.Add(root);

        languagePicker.Items.AddRange(LanguageCatalog.All.Select(item => (object)item.NativeName).ToArray());
        languagePicker.SelectedIndex = Math.Max(0, LanguageCatalog.All.ToList().FindIndex(item => item.Language == language));
        languagePicker.SelectedIndexChanged += (_, _) => ApplyLanguage();
        adapterPicker.Format += (_, args) =>
        {
            if (args.ListItem is GpuTestAdapter adapter)
            {
                var memory = (adapter.DedicatedVideoMemoryBytes / 1048576UL).ToString(CultureInfo.InvariantCulture);
                args.Value = SafeText.Normalize(adapter.Name) + " — " + memory + " MiB";
            }
        };
        adapterPicker.SelectedIndexChanged += (_, _) =>
        {
            if (Busy) return;
            report = null;
            SetStatus("gpu.selectAdapter");
            RefreshState();
        };
        foreach (var choice in new[] { includeGraphics, includeIds })
            choice.CheckedChanged += (_, _) =>
            {
                if (report is not null) SetStatus("gpu.updated");
                RefreshState();
            };
        load.Click += async (_, _) => await LoadAdaptersAsync();
        start.Click += async (_, _) => await RunTestsAsync();
        cancel.Click += (_, _) => CancelOperation();
        save.Click += (_, _) => SaveReport();
        copy.Click += (_, _) => CopyReport();
        close.Click += (_, _) => Close();
        CancelButton = close;
        status.LiveSetting = AutomationLiveSetting.Polite;
        void UpdateWrapping()
        {
            var width = Math.Max(180, root.ClientSize.Width - root.Padding.Horizontal - 24);
            foreach (var label in new[] { introduction, adapterLabel, status, optionsLabel, sharing })
                label.MaximumSize = new Size(width, 0);
        }
        root.Resize += (_, _) => UpdateWrapping();
        UpdateWrapping();
        FormClosing += (_, _) =>
        {
            closing = true;
            operation?.Cancel();
        };
        Shown += (_, _) => { FitToScreen(); load.Focus(); };
        DpiChanged += (_, _) => BeginInvoke((Action)FitToScreen);
        ApplyLanguage();
        Theme.Attach(this);
    }

    private static Label MakeLabel(string name) => new()
    {
        Name = name, AutoSize = true, Margin = new Padding(0, 3, 0, 7), UseMnemonic = true,
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

    private void FitToScreen()
    {
        var area = Screen.FromControl(this).WorkingArea;
        MinimumSize = new Size(Math.Min(660, area.Width), Math.Min(600, area.Height));
        Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
        Location = new Point(Math.Clamp(Left, area.Left, area.Right - Width),
            Math.Clamp(Top, area.Top, area.Bottom - Height));
    }

    private string T(string key) => LocalizedText.Get(key, SelectedLanguage);

    private void ApplyLanguage()
    {
        var rtl = LanguageCatalog.Get(SelectedLanguage).IsRightToLeft;
        RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = rtl;
        if (report is not null && statusKey is "ui.copied" or "guided.saved") statusKey = "gpu.updated";
        Text = T("gpu.title");
        languageLabel.Text = T("ui.language");
        languagePicker.AccessibleName = T("ui.languageName");
        introduction.Text = T("gpu.intro");
        load.Text = T("gpu.load");
        start.Text = T("gpu.run");
        cancel.Text = T("gpu.cancel");
        close.Text = T("gpu.close");
        adapterLabel.Text = T("gpu.adapter");
        adapterPicker.AccessibleName = T("gpu.adapter").Replace("&", "", StringComparison.Ordinal);
        optionsLabel.Text = T("gpu.options");
        includeGraphics.Text = T("gpu.includeGraphics");
        includeIds.Text = T("gpu.includeIds");
        includeIds.AccessibleDescription = T("ui.idsHelp");
        save.Text = T("ui.save");
        copy.Text = T("ui.copy");
        preview.AccessibleName = T("gpu.preview");
        progress.AccessibleName = T("gpu.running");
        sharing.Text = T("gpu.sharing");
        RefreshState();
    }

    private void RefreshState()
    {
        if (closing || IsDisposed) return;
        load.Enabled = !Busy;
        start.Enabled = !Busy && adapterPicker.SelectedItem is GpuTestAdapter;
        cancel.Enabled = Busy && operation?.IsCancellationRequested == false;
        adapterPicker.Enabled = !Busy && adapterPicker.Items.Count > 0;
        includeIds.Enabled = includeGraphics.Checked;
        save.Enabled = copy.Enabled = report is not null && !Busy;
        progress.Visible = Busy;
        SetStatus(statusKey);
        preview.Text = report is null ? T("gpu.empty") : GpuTestReportFormatter.ToPlainText(report, SelectedLanguage, Options);
        preview.SelectionStart = 0;
        preview.SelectionLength = 0;
    }

    private void SetStatus(string key)
    {
        statusKey = key;
        if (closing || IsDisposed) return;
        status.Text = T(key);
        if (status.IsHandleCreated) status.AccessibilityObject.RaiseLiveRegionChanged();
    }

    private void CancelOperation()
    {
        if (operation is null || operation.IsCancellationRequested) return;
        operation.Cancel();
        SetStatus("gpu.cancelling");
        RefreshState();
    }

    private async Task LoadAdaptersAsync()
    {
        if (Busy) return;
        using var current = new CancellationTokenSource();
        operation = current;
        report = null;
        adapterPicker.Items.Clear();
        SetStatus("gpu.loading");
        RefreshState();
        try
        {
            var result = await enumerate(current.Token);
            if (closing || IsDisposed) return;
            if (current.IsCancellationRequested) { SetStatus("gpu.cancelled"); return; }
            if (result.Status == GpuTestStatus.Passed)
            {
                // Software rendering is reserved for developer checks, never offered as a hardware benchmark.
                adapterPicker.Items.AddRange(result.Adapters.Where(adapter => !adapter.IsSoftware).Cast<object>().ToArray());
                adapterPicker.SelectedIndex = -1;
                SetStatus(adapterPicker.Items.Count == 0 ? "gpu.noAdapters" : "gpu.selectAdapter");
            }
            else SetStatus(result.Status switch
            {
                GpuTestStatus.Cancelled => "gpu.cancelled",
                GpuTestStatus.TimedOut => "gpu.timedOut",
                GpuTestStatus.NotAvailable => "gpu.noAdapters",
                _ => "gpu.failed",
            });
        }
        catch (OperationCanceledException) { SetStatus("gpu.cancelled"); }
        catch (Exception) { SetStatus("gpu.failed"); }
        finally
        {
            operation = null;
            if (!closing && !IsDisposed) { RefreshState(); if (adapterPicker.Enabled) adapterPicker.Focus(); }
        }
    }

    private async Task RunTestsAsync()
    {
        if (Busy || adapterPicker.SelectedItem is not GpuTestAdapter selected) return;
        using var current = new CancellationTokenSource();
        operation = current;
        report = null;
        SetStatus("gpu.running");
        RefreshState();
        try
        {
            var result = await run(selected, current.Token);
            if (closing || IsDisposed) return;
            // An uncooperative or late worker must never overwrite cancellation with successful results.
            report = current.IsCancellationRequested
                ? GpuTestProtocol.WithoutResults(GpuTestStatus.Cancelled, "cancelled", selected) : result;
            var outcomes = new[] { report.Direct3D11.Status, report.ComputeShader.Status, report.Cuda.Status, report.Timing.Status };
            SetStatus(outcomes.Contains(GpuTestStatus.Cancelled) ? "gpu.cancelled"
                : outcomes.Contains(GpuTestStatus.TimedOut) ? "gpu.timedOut"
                : outcomes.All(outcome => outcome == GpuTestStatus.Passed) ? "gpu.complete" : "gpu.partial");
        }
        catch (OperationCanceledException)
        {
            if (!closing && !IsDisposed) report = GpuTestProtocol.WithoutResults(GpuTestStatus.Cancelled, "cancelled", selected);
            SetStatus("gpu.cancelled");
        }
        catch (Exception)
        {
            if (!closing && !IsDisposed) report = GpuTestProtocol.WithoutResults(GpuTestStatus.Inconclusive, "worker_failed", selected);
            SetStatus("gpu.failed");
        }
        finally
        {
            operation = null;
            if (!closing && !IsDisposed) { RefreshState(); if (report is not null) preview.Focus(); }
        }
    }

    private void CopyReport()
    {
        if (report is null || !copy.Enabled) return;
        try
        {
            Clipboard.SetText(GpuTestReportFormatter.ToMarkdown(report, SelectedLanguage, Options));
            SetStatus("ui.copied");
        }
        catch (ExternalException)
        {
            MessageBox.Show(this, T("ui.copyError"), T("gpu.title"), MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void SaveReport()
    {
        if (report is null || !save.Enabled) return;
        using var dialog = new SaveFileDialog
        {
            Title = T("ui.saveTitle"), Filter = T("ui.fileFilter"), DefaultExt = "md",
            FileName = "GRELECY-GPU-" + report.TestedAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            AddExtension = true, OverwritePrompt = true, RestoreDirectory = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var extension = Path.GetExtension(dialog.FileName);
        if (!extension.Equals(".md", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".json", StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(this, T("ui.extensionError"), T("ui.saveTitle"), MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var text = extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            ? GpuTestReportFormatter.ToJson(report, Options)
            : GpuTestReportFormatter.ToMarkdown(report, SelectedLanguage, Options);
        try
        {
            File.WriteAllText(dialog.FileName, text, new UTF8Encoding(false));
            SetStatus("guided.saved");
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            MessageBox.Show(this, T("ui.saveError"), T("ui.saveTitle"), MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
