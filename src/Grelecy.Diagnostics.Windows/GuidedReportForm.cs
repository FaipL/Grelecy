// SPDX-License-Identifier: MPL-2.0
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms.Automation;

namespace Grelecy.Diagnostics.Windows;

/// <summary>Collect a user's account of an individual test, separately from automatic configuration data.</summary>
public sealed class GuidedReportForm : Form
{
    private readonly ObservationKind kind;
    private readonly Func<DiagnosticReport> collector;
    private readonly DateTimeOffset createdAt = DateTimeOffset.UtcNow;
    private readonly TimeSpan collectionTimeout;
    private DiagnosticReport? configuration;
    private readonly ComboBox languagePicker = new() { Name = "language", DropDownStyle = ComboBoxStyle.DropDownList, Width = 180, RightToLeft = RightToLeft.No };
    private readonly TextBox software = Input("software", 256);
    private readonly TextBox version = Input("version", 256);
    private readonly ComboBox testedGpu = new() { Name = "testedGpu", DropDownStyle = ComboBoxStyle.DropDown, Dock = DockStyle.Top, MaxLength = 256 };
    private readonly ComboBox category = new() { Name = "category", DropDownStyle = ComboBoxStyle.DropDownList, Dock = DockStyle.Top };
    private readonly TextBox steps = Input("steps", 4000, 120);
    private readonly TextBox expected = Input("expected", 4000, 75);
    private readonly TextBox observed = Input("observed", 4000, 110);
    private readonly TextBox duration = Input("duration", 256);
    private readonly TextBox settings = Input("settings", 4000, 75);
    private readonly TextBox limitations = Input("limitations", 4000, 75);
    private readonly CheckBox includeWindows = Choice("includeWindows", true);
    private readonly CheckBox includeGraphics = Choice("includeGraphics", true);
    private readonly CheckBox includeIds = Choice("includeIds", false);
    private readonly TextBox preview = new()
    {
        Name = "preview", ReadOnly = true, Multiline = true, Dock = DockStyle.Fill,
        ScrollBars = ScrollBars.Vertical, WordWrap = true, MinimumSize = new Size(100, 140),
        BackColor = SystemColors.Window, ForeColor = SystemColors.WindowText,
    };
    private readonly Button next = Button("next");
    private readonly Button back = Button("back");
    private readonly Button save = Button("save");
    private readonly Button copy = Button("copy");
    private readonly Button openIssue = Button("openIssue");
    private readonly Button readConfiguration = Button("readConfiguration");
    private readonly Label title = new() { AutoSize = true, Font = new Font("Segoe UI", 18, FontStyle.Bold), Margin = new Padding(0, 0, 12, 6) };
    private readonly Label stepLabel = new() { Name = "step", AutoSize = true, Margin = new Padding(0, 6, 0, 10) };
    private readonly Label status = new() { Name = "status", AutoSize = true, LiveSetting = AutomationLiveSetting.Polite, Margin = new Padding(0, 8, 0, 6) };
    private readonly Dictionary<Control, string> labels = [];
    private readonly List<Label> wrapping = [];
    private readonly Panel[] pages = new Panel[3];
    private int step;
    private bool reading;
    private bool scanInFlight;
    private bool translating;
    private string statusKey = "optionalConfiguration";
    private UiLanguage Language => LanguageCatalog.All[Math.Max(0, languagePicker.SelectedIndex)].Language;
    public UiLanguage SelectedLanguage => Language;
    private ReportOptions Options => new(includeWindows.Checked, includeGraphics.Checked, includeGraphics.Checked && includeIds.Checked);

    public GuidedReportForm(ObservationKind kind, DiagnosticReport? configuration = null,
        Func<DiagnosticReport>? collector = null, UiLanguage? language = null, TimeSpan? collectionTimeout = null,
        ReportOptions? initialOptions = null)
    {
        this.kind = kind;
        this.configuration = configuration;
        this.collector = collector ?? WindowsCollector.Collect;
        this.collectionTimeout = collectionTimeout ?? TimeSpan.FromSeconds(45);
        includeWindows.Checked = initialOptions?.IncludeWindows ?? true;
        includeGraphics.Checked = initialOptions?.IncludeGraphics ?? true;
        includeIds.Checked = initialOptions?.IncludeHardwareIds ?? false;
        Font = new Font("Segoe UI", 11);
        AutoScaleMode = AutoScaleMode.Dpi;
        MinimumSize = new Size(640, 590);
        Size = new Size(960, 800);
        StartPosition = FormStartPosition.CenterParent;
        Icon = Branding.CreateIcon();
        Disposed += (_, _) => Icon?.Dispose();
        languagePicker.Items.AddRange(LanguageCatalog.All.Select(item => (object)item.NativeName).ToArray());
        languagePicker.SelectedIndex = LanguageIndex(language ?? LanguageCatalog.FromCulture(CultureInfo.CurrentUICulture));

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(22), ColumnCount = 1, RowCount = 6, AutoScroll = true };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 6; row++) root.RowStyles.Add(new RowStyle(row == 2 ? SizeType.Percent : SizeType.AutoSize, row == 2 ? 100 : 0));
        var header = new TableLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, ColumnCount = 2 };
        header.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        header.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        var brand = Flow();
        brand.WrapContents = false;
        brand.Controls.Add(Branding.CreateLogoPictureBox("brandLogo"));
        brand.Controls.Add(title);
        header.Controls.Add(brand, 0, 0);
        var languages = Flow();
        languages.FlowDirection = FlowDirection.TopDown;
        languages.WrapContents = false;
        languages.Controls.Add(LabelFor("language", "languageLabel"));
        languages.Controls.Add(languagePicker);
        header.Controls.Add(languages, 1, 0);
        root.Controls.Add(header, 0, 0);
        root.Controls.Add(stepLabel, 0, 1);
        var host = new Panel { Dock = DockStyle.Fill, MinimumSize = new Size(100, 200) };
        for (var index = 0; index < pages.Length; index++)
        {
            pages[index] = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Visible = index == 0 };
            host.Controls.Add(pages[index]);
        }
        root.Controls.Add(host, 0, 2);
        root.Controls.Add(status, 0, 3);
        var navigation = Flow();
        navigation.Controls.AddRange([back, next, save, copy, openIssue]);
        root.Controls.Add(navigation, 0, 4);
        root.Controls.Add(LabelFor("sharing", "sharing"), 0, 5);
        Controls.Add(root);

        var first = Fields();
        AddField(first, "software", software);
        AddField(first, "version", version);
        AddField(first, "testedGpu", testedGpu);
        first.Controls.Add(LabelFor("gpuHelp", "gpuHelp"));
        pages[0].Controls.Add(first);
        var detail = Fields();
        if (kind == ObservationKind.Problem)
        {
            AddField(detail, "category", category);
            AddField(detail, "observed", observed);
            AddField(detail, "problemSteps", steps);
            AddField(detail, "expected", expected);
        }
        else
        {
            AddField(detail, "workingSteps", steps);
            AddField(detail, "duration", duration);
        }
        AddField(detail, "settings", settings);
        AddField(detail, kind == ObservationKind.Problem ? "workarounds" : "limitations", limitations);
        detail.Controls.Add(LabelFor("textHelp", "textHelp"));
        pages[1].Controls.Add(detail);
        var review = new TableLayoutPanel
        {
            Dock = DockStyle.Top, ColumnCount = 1, RowCount = 4,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        };
        review.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (var row = 0; row < 4; row++) review.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        review.Controls.Add(LabelFor("autoOptions", "autoOptions"), 0, 0);
        var choices = Flow();
        choices.Controls.AddRange([includeWindows, includeGraphics, includeIds]);
        review.Controls.Add(choices, 0, 1);
        var readRow = Flow();
        readRow.Controls.Add(readConfiguration);
        readRow.Controls.Add(LabelFor("manualIncluded", "manualIncluded"));
        review.Controls.Add(readRow, 0, 2);
        preview.Dock = DockStyle.Top;
        preview.Height = 220;
        review.Controls.Add(preview, 0, 3);
        pages[2].Controls.Add(review);
        // A top-docked, auto-sized child exposes its full extent to the page's scrolling.
        // A fill-docked child would keep the page-sized bounds and clip the lower preview.

        Bind(next, "next"); Bind(back, "back"); Bind(save, "save"); Bind(copy, "copy");
        Bind(openIssue, "openIssue"); Bind(includeWindows, "includeWindows");
        Bind(includeGraphics, "includeGraphics"); Bind(includeIds, "includeIds");
        next.Click += (_, _) => Advance();
        back.Click += (_, _) => { step--; SetStatus("optionalConfiguration"); ShowStep(); };
        readConfiguration.Click += async (_, _) => await ReadConfigurationAsync();
        copy.Click += (_, _) => CopyReport();
        save.Click += (_, _) => SaveReport();
        openIssue.Click += (_, _) => OpenIssue();
        foreach (var field in new[] { software, version, steps, expected, observed, duration, settings, limitations })
            field.TextChanged += (_, _) => Changed();
        testedGpu.TextChanged += (_, _) => Changed();
        category.SelectedIndexChanged += (_, _) => Changed();
        foreach (var choice in new[] { includeWindows, includeGraphics, includeIds })
            choice.CheckedChanged += (_, _) => Changed();
        languagePicker.SelectedIndexChanged += (_, _) => Translate();
        wrapping.AddRange([stepLabel, status]);
        void Wrap()
        {
            foreach (var label in wrapping) label.MaximumSize = new Size(Math.Max(140, ClientSize.Width - 100), 0);
            title.MaximumSize = new Size(Math.Max(100, brand.ClientSize.Width - 64 - title.Margin.Horizontal), 0);
        }
        root.Resize += (_, _) => Wrap();
        brand.Resize += (_, _) => Wrap();
        Wrap();
        PopulateGpuChoices();
        Translate();
        Theme.Attach(this);
        Shown += (_, _) => { Fit(); software.Focus(); };
        DpiChanged += (_, _) => BeginInvoke((Action)Fit);
    }

    private static TextBox Input(string name, int limit, int height = 0) => new()
    {
        Name = name, Dock = DockStyle.Top, MaxLength = limit, Multiline = height > 0,
        Height = height > 0 ? height : 30, AcceptsReturn = height > 0, AcceptsTab = false,
        ScrollBars = height > 0 ? ScrollBars.Vertical : ScrollBars.None,
        Margin = new Padding(0, 0, 0, 12),
    };

    private static CheckBox Choice(string name, bool selected) => new() { Name = name, Checked = selected, AutoSize = true, Margin = new Padding(0, 2, 16, 6) };
    private static Button Button(string name) => new()
    {
        Name = name, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
        MinimumSize = new Size(110, 38), Padding = new Padding(8, 3, 8, 3), Margin = new Padding(0, 3, 10, 3),
    };
    private static FlowLayoutPanel Flow() => new() { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Margin = new Padding(0) };
    private static TableLayoutPanel Fields()
    {
        var fields = new TableLayoutPanel { Dock = DockStyle.Top, ColumnCount = 1, AutoSize = true, Padding = new Padding(0, 0, 16, 10) };
        fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        return fields;
    }

    private void Bind(Control control, string key) => labels.Add(control, key);
    private Label LabelFor(string key, string name)
    {
        var label = new Label { Name = name, AutoSize = true, Margin = new Padding(0, 4, 0, 6) };
        Bind(label, key);
        wrapping.Add(label);
        return label;
    }
    private void AddField(TableLayoutPanel panel, string key, Control input)
    {
        var label = LabelFor(key, key + "Label");
        label.Font = new Font(Font, FontStyle.Bold);
        panel.Controls.Add(label);
        panel.Controls.Add(input);
        input.AccessibleName = T(key);
    }
    private static int LanguageIndex(UiLanguage language)
    {
        var index = LanguageCatalog.All.ToList().FindIndex(item => item.Language == language);
        return index >= 0 ? index : 1;
    }

    private string T(string key) => GuidedText.Get(key, Language);
    private void Fit()
    {
        var area = Screen.FromControl(this).WorkingArea;
        MinimumSize = new Size(Math.Min(640, area.Width), Math.Min(590, area.Height));
        Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
        Location = new Point(Math.Clamp(Left, area.Left, area.Right - Width), Math.Clamp(Top, area.Top, area.Bottom - Height));
    }

    private void Translate()
    {
        var rtl = LanguageCatalog.Get(Language).IsRightToLeft;
        RightToLeft = rtl ? RightToLeft.Yes : RightToLeft.No;
        RightToLeftLayout = rtl;
        translating = true;
        title.Text = T(kind == ObservationKind.Problem ? "problemTitle" : "workingTitle");
        Text = "GRELECY — " + title.Text;
        foreach (var (control, key) in labels) control.Text = T(key);
        var selectedCategory = Math.Max(0, category.SelectedIndex);
        category.Items.Clear();
        category.Items.AddRange(Enum.GetValues<ProblemCategory>().Select(value => (object)T("category_" + value)).ToArray());
        category.SelectedIndex = selectedCategory;
        if (testedGpu.Items.Count > 0)
        {
            var previous = GpuDescription();
            testedGpu.Items[0] = T("unspecified");
            testedGpu.SelectedIndex = -1;
            if (previous.Length == 0) testedGpu.SelectedIndex = 0;
            else testedGpu.Text = previous;
        }
        foreach (var field in new[] { software, version, steps, expected, observed, duration, settings, limitations })
            field.AccessibleName = T(field == steps ? kind == ObservationKind.Problem ? "problemSteps" : "workingSteps"
                : field == limitations && kind == ObservationKind.Problem ? "workarounds" : field.Name).Replace("&", "");
        testedGpu.AccessibleName = T("testedGpu").Replace("&", "");
        category.AccessibleName = T("category");
        languagePicker.AccessibleName = T("languageName");
        preview.AccessibleName = T("preview");
        software.PlaceholderText = T("softwareExample");
        version.PlaceholderText = T("unknownAllowed");
        duration.PlaceholderText = T("durationExample");
        if (statusKey is "copied" or "saved") statusKey = "updated";
        translating = false;
        ShowStep();
    }

    private void PopulateGpuChoices()
    {
        var previous = GpuDescription();
        testedGpu.Items.Clear();
        testedGpu.Items.Add(T("unspecified"));
        if (configuration is not null)
            foreach (var name in configuration.Graphics.Select(g => g.Name).Where(name => name.Length > 0).Distinct()) testedGpu.Items.Add(name);
        testedGpu.SelectedIndex = -1;
        if (previous.Length == 0) testedGpu.SelectedIndex = 0;
        else testedGpu.Text = previous;
    }

    // Editable ComboBox.Text can differ from Items[SelectedIndex]. Preserve the typed value.
    private string GpuDescription() => testedGpu.Items.Count > 0 &&
        testedGpu.Text.Equals(testedGpu.GetItemText(testedGpu.Items[0]), StringComparison.Ordinal)
        ? "" : testedGpu.Text;

    private UserObservation Observation() => new(kind, software.Text, version.Text,
        GpuDescription(),
        (ProblemCategory)Math.Max(0, category.SelectedIndex), steps.Text, expected.Text,
        observed.Text, duration.Text, settings.Text, limitations.Text);
    private ObservationReport Report() => new(createdAt, Observation(), configuration);

    private void Changed()
    {
        if (translating) return;
        if (statusKey is "copied" or "saved" or "opened") SetStatus("updated");
        if (step == 2) UpdatePreview();
    }
    private void SetStatus(string key)
    {
        statusKey = key;
        status.Text = T(key);
        if (status.IsHandleCreated) status.AccessibilityObject.RaiseLiveRegionChanged();
    }
    private void Advance()
    {
        var errors = ObservationFormatter.Validate(Observation());
        var key = step == 0 && errors.Contains("software_required") ? "software_required"
            : step == 1 && errors.Contains(kind == ObservationKind.Problem ? "observed_required" : "steps_required")
                ? kind == ObservationKind.Problem ? "observed_required" : "steps_required" : null;
        if (key is not null)
        {
            SetStatus(key);
            (step == 0 ? software : kind == ObservationKind.Problem ? observed : steps).Focus();
            return;
        }
        if (step < 2) step++;
        SetStatus(step == 2 ? "reviewReady" : "optionalDetails");
        ShowStep();
    }
    private void ShowStep()
    {
        for (var index = 0; index < pages.Length; index++) pages[index].Visible = index == step;
        pages[step].BringToFront();
        stepLabel.Text = T("step" + step);
        back.Visible = step > 0;
        next.Visible = step < 2;
        save.Visible = copy.Visible = openIssue.Visible = step == 2;
        SetStatus(statusKey);
        UpdatePreview();
        if (step == 0) software.Focus();
        else if (step == 1) (kind == ObservationKind.Problem ? observed : steps).Focus();
        else preview.Focus();
    }
    private void UpdatePreview()
    {
        readConfiguration.Text = T(scanInFlight ? "reading" : configuration is null ? "readConfiguration" : "readAgain");
        readConfiguration.Enabled = !scanInFlight;
        includeWindows.Enabled = includeGraphics.Enabled = configuration is not null;
        includeIds.Enabled = configuration is not null && includeGraphics.Checked;
        save.Enabled = copy.Enabled = openIssue.Enabled = step == 2 && !reading && ObservationFormatter.Validate(Observation()).Count == 0;
        if (step != 2) return;
        preview.Text = ObservationFormatter.ToPlainText(Report(), Language, Options);
        preview.SelectionStart = 0;
        preview.SelectionLength = 0;
    }

    private async Task ReadConfigurationAsync()
    {
        if (scanInFlight) return;
        scanInFlight = true;
        reading = true;
        configuration = null;
        SetStatus("readingHelp");
        UpdatePreview();
        var task = Task.Run(collector);
        try
        {
            var result = await task.WaitAsync(collectionTimeout);
            if (IsDisposed) return;
            configuration = result;
            PopulateGpuChoices();
            SetStatus(result.Notices.Count > 0 ? "partialConfiguration" : "configurationReady");
        }
        catch (TimeoutException)
        {
            if (IsDisposed) return;
            reading = false;
            SetStatus("timeout");
            UpdatePreview();
            // Keep a single native WMI scan in flight. Closing the dialog is always allowed.
            try { await task; } catch (Exception) { }
            if (!IsDisposed) SetStatus("configurationFailed");
        }
        catch (Exception)
        {
            if (!IsDisposed) SetStatus("configurationFailed");
        }
        finally
        {
            if (!IsDisposed) { reading = false; scanInFlight = false; UpdatePreview(); }
        }
    }
    private void CopyReport()
    {
        if (!copy.Enabled) return;
        try { Clipboard.SetText(ObservationFormatter.ToMarkdown(Report(), Language, Options)); SetStatus("copied"); }
        catch (ExternalException) { SetStatus("copyError"); }
    }
    private void SaveReport()
    {
        if (!save.Enabled) return;
        using var dialog = new SaveFileDialog
        {
            Title = T("save"), Filter = UiText.Get("fileFilter", Language), DefaultExt = "md",
            FileName = "GRELECY-" + (kind == ObservationKind.Problem ? "problem-" : "working-") + createdAt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            AddExtension = true, OverwritePrompt = true, RestoreDirectory = true,
        };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        var extension = Path.GetExtension(dialog.FileName);
        if (!extension.Equals(".md", StringComparison.OrdinalIgnoreCase) && !extension.Equals(".json", StringComparison.OrdinalIgnoreCase)) { SetStatus("extensionError"); return; }
        var content = extension.Equals(".json", StringComparison.OrdinalIgnoreCase)
            ? ObservationFormatter.ToJson(Report(), Options) : ObservationFormatter.ToMarkdown(Report(), Language, Options);
        try { File.WriteAllText(dialog.FileName, content, new UTF8Encoding(false)); SetStatus("saved"); }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException) { SetStatus("saveError"); }
    }
    private void OpenIssue()
    {
        if (!openIssue.Enabled) return;
        var url = "https://github.com/FaipL/Grelecy/issues/new?template=" + (kind == ObservationKind.Problem ? "04-guided-problem.yml" : "05-working.yml");
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); SetStatus("opened"); }
        catch (Exception error) when (error is System.ComponentModel.Win32Exception or InvalidOperationException or System.Security.SecurityException)
        {
            MessageBox.Show(this, T("browserError") + "\n\n" + url, Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
