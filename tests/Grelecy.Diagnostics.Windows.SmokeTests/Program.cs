// SPDX-License-Identifier: MPL-2.0
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using Grelecy.Diagnostics;
using Grelecy.Diagnostics.Windows;

internal static class Program
{
    private static int assertions;
    private static Exception? failure;
    private static readonly DiagnosticReport Example = new(
        DateTimeOffset.Parse("2026-10-22T12:00:00Z", CultureInfo.InvariantCulture),
        new WindowsInfo("Windows 11 Pro", "10.0.26100", "26100", "64-bit"),
        [
            new GraphicsInfo("NVIDIA GeForce GTX 1060", "32.0.15.9999", new PciIdentity("10DE", "1C03", "000010DE", "A1")),
            new GraphicsInfo("Intel UHD Graphics", "31.0.101.9999", new PciIdentity("8086", "3E92", null, null)),
        ], []);

    [STAThread]
    private static int Main(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("FAIL: these smoke checks require Windows.");
            return 1;
        }
        var screenshots = ArgumentValue(args, "--screenshots");
        if (screenshots is not null) Directory.CreateDirectory(screenshots);
        // This also terminates a native modal dialog or a hung WMI call. CI must never wait indefinitely.
        using var deadline = new System.Threading.Timer(_ =>
        {
            Console.Error.WriteLine("FAIL: Windows smoke checks exceeded 60 seconds.");
            Environment.Exit(1);
        }, null, TimeSpan.FromSeconds(60), Timeout.InfiniteTimeSpan);
        Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);

        using var releaseCollector = new ManualResetEventSlim(false);
        var calls = 0;
        var shouldFail = false;
        var partial = false;
        var uiChecksCompleted = false;
        DiagnosticReport Collect()
        {
            Interlocked.Increment(ref calls);
            if (!releaseCollector.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Synthetic collector deadline");
            if (shouldFail) throw new InvalidOperationException("PRIVATE_MACHINE_NAME must not reach the interface");
            return partial ? new DiagnosticReport(Example.CollectedAt, null, Example.Graphics,
                [CollectionNotice.WindowsUnavailable]) : Example;
        }

        using var form = new MainForm(Collect);
        form.Shown += async (_, _) =>
        {
            try
            {
                var scan = Find<Button>(form, "scan");
                var save = Find<Button>(form, "save");
                var copy = Find<Button>(form, "copy");
                var join = Find<Button>(form, "join");
                var preview = Find<TextBox>(form, "preview");
                var language = Find<ComboBox>(form, "language");
                var windows = Find<CheckBox>(form, "includeWindows");
                var graphics = Find<CheckBox>(form, "includeGraphics");
                var ids = Find<CheckBox>(form, "includeIds");
                var status = Find<Label>(form, "status");
                language.SelectedIndex = 0;

                Check(!save.Enabled && !copy.Enabled, "No export before collection");
                Check(join.Enabled && scan.Enabled, "Participation is available without a report");
                Check(preview.ReadOnly && !string.IsNullOrWhiteSpace(preview.AccessibleName), "Preview is read only and named for assistive technology");
                Check(status.LiveSetting == System.Windows.Forms.Automation.AutomationLiveSetting.Polite, "Status is a polite live region");
                Check(!ids.Checked, "Hardware identifiers are excluded by default");
                scan.PerformClick();
                Check(!scan.Enabled && !save.Enabled && !copy.Enabled, "Busy collection disables duplicate scans and exports");
                scan.PerformClick();
                await WaitUntil(() => Volatile.Read(ref calls) == 1);
                Check(calls == 1 && join.Enabled, "A second click cannot start another collection");
                releaseCollector.Set();
                await WaitUntil(() => scan.Enabled);

                Check(save.Enabled && copy.Enabled, "A successful report can be exported");
                Check(preview.Text.Contains("Windows 11 Pro", StringComparison.Ordinal) &&
                    preview.Text.Contains("NVIDIA GeForce GTX 1060", StringComparison.Ordinal) &&
                    preview.Text.Contains("Intel UHD Graphics", StringComparison.Ordinal), "All GPUs and Windows data appear");
                Check(!preview.Text.Contains("10DE", StringComparison.Ordinal), "PCI data remains absent until selected");
                Capture(form, screenshots, "diagnostics-fr.png");

                language.SelectedIndex = 1;
                Check(preview.Text.Contains("Configuration report", StringComparison.Ordinal) &&
                    preview.Text.Contains("NVIDIA GeForce GTX 1060", StringComparison.Ordinal) && calls == 1,
                    "Language changes preserve the collected report without rereading hardware");
                Capture(form, screenshots, "diagnostics-en.png");

                ids.Checked = true;
                Check(preview.Text.Contains("10DE", StringComparison.Ordinal), "Optional model identifiers can be included");
                // Clipboard interaction is local to this disposable Windows test session.
                copy.PerformClick();
                Check(Clipboard.GetText().Contains("10DE", StringComparison.Ordinal), "Copy uses the currently selected report");
                ids.Checked = false;
                Check(!preview.Text.Contains("10DE", StringComparison.Ordinal) &&
                    status.Text.Contains("previous copies", StringComparison.Ordinal), "Changing options invalidates the previous sharing status");
                Check(Clipboard.GetText().Contains("10DE", StringComparison.Ordinal), "The warning accurately describes the older clipboard copy");
                copy.PerformClick();
                language.SelectedIndex = 0;
                Check(status.Text.Contains("copies précédentes", StringComparison.Ordinal), "Changing language also invalidates an older copied report");

                windows.Checked = false;
                Check(!preview.Text.Contains("Windows 11 Pro", StringComparison.Ordinal) &&
                    preview.Text.Contains("NVIDIA GeForce GTX 1060", StringComparison.Ordinal), "Excluding Windows preserves only selected sections");
                graphics.Checked = false;
                Check(!save.Enabled && !copy.Enabled && !ids.Enabled && join.Enabled,
                    "With no selected section, exports are disabled and participation remains available");
                windows.Checked = true;
                graphics.Checked = true;
                form.Size = new Size(640, 560);
                await Task.Delay(100);
                Check(preview.Width > 100 && preview.Height >= 120, "Report preview retains a usable size in a small window");
                Capture(form, screenshots, "diagnostics-small-fr.png");

                shouldFail = true;
                scan.PerformClick();
                await WaitUntil(() => scan.Enabled);
                Check(!save.Enabled && !copy.Enabled && join.Enabled, "A failed refresh cannot export an earlier report");
                Check(!preview.Text.Contains("NVIDIA GeForce GTX 1060", StringComparison.Ordinal) &&
                    !status.Text.Contains("PRIVATE_MACHINE_NAME", StringComparison.Ordinal), "Failure clears stale results and hides raw exception text");

                shouldFail = false;
                partial = true;
                scan.PerformClick();
                await WaitUntil(() => scan.Enabled);
                Check(save.Enabled && preview.Text.Contains("NVIDIA GeForce GTX 1060", StringComparison.Ordinal) &&
                    status.Text.Contains("indisponibles", StringComparison.Ordinal), "Retry accepts an explicitly incomplete report");
                Check(calls == 3, "Exactly the three requested scans ran");
                Check(Find<Button>(form, "problem").Enabled && Find<Button>(form, "working").Enabled,
                    "Both guided contribution paths are available from the main window");
                TestInheritedOptions(form);
                await TestGuidedAsync(form, ObservationKind.Problem, screenshots);
                await TestGuidedAsync(form, ObservationKind.Working, screenshots);
                await TestGuidedWithoutConfigurationAsync(form, screenshots);
                await TestGuidedTimeoutAsync(form);
                uiChecksCompleted = true;
            }
            catch (Exception error) { failure = error; }
            finally { releaseCollector.Set(); form.Close(); }
        };
        try
        {
            Application.Run(form);
            if (failure is not null) throw failure;
            if (!uiChecksCompleted) throw new CheckFailure("The window closed before the UI checks finished");
            if (args.Contains("--live-collector", StringComparer.Ordinal))
            {
                var live = Task.Run(WindowsCollector.Collect).WaitAsync(TimeSpan.FromSeconds(20)).GetAwaiter().GetResult();
                Check(live.Windows is not null && live.Windows.Edition.Length > 0 &&
                    uint.TryParse(live.Windows.Build, out var build) && build > 0,
                    "Live WMI returns an identified Windows edition and numeric build on the test machine");
                Check(live.Graphics.Count > 0 || live.Notices.Any(n => n is CollectionNotice.GraphicsUnavailable or CollectionNotice.NoGraphicsFound),
                    "Live WMI reports graphics or an explicit unavailable notice");
                Console.WriteLine("Live collector completed; hardware values were not logged.");
            }
            Console.WriteLine($"PASS: {assertions} Windows UI/collector assertions.");
            return 0;
        }
        catch (Exception error)
        {
            Console.Error.WriteLine("FAIL: " + error.GetType().Name + (error is CheckFailure ? " — " + error.Message : " (system details omitted)"));
            return 1;
        }
    }

    private static void TestInheritedOptions(MainForm main)
    {
        var windows = Find<CheckBox>(main, "includeWindows");
        var previous = windows.Checked;
        var inspected = false;
        Exception? inspectionFailure = null;
        using var inspect = new System.Windows.Forms.Timer { Interval = 20 };
        inspect.Tick += (_, _) =>
        {
            var dialog = Application.OpenForms.OfType<GuidedReportForm>().FirstOrDefault();
            if (dialog is null) return;
            inspect.Stop();
            try
            {
                Check(!Find<CheckBox>(dialog, "includeWindows").Checked &&
                    Find<CheckBox>(dialog, "includeGraphics").Checked == Find<CheckBox>(main, "includeGraphics").Checked,
                    "Opening a guided report from the main button preserves excluded configuration sections");
                inspected = true;
            }
            catch (Exception error) { inspectionFailure = error; }
            finally { dialog.Close(); }
        };
        try
        {
            windows.Checked = false;
            inspect.Start();
            // ShowDialog runs its native message loop, allowing the timer to inspect and close it.
            Find<Button>(main, "problem").PerformClick();
            if (inspectionFailure is not null) throw inspectionFailure;
            Check(inspected, "The main problem button opens the guided reporting window");
        }
        finally
        {
            inspect.Stop();
            windows.Checked = previous;
        }
    }

    private static async Task TestGuidedAsync(Form owner, ObservationKind kind, string? screenshots)
    {
        var collectionCalls = 0;
        using var guided = new GuidedReportForm(kind, Example, () =>
        {
            collectionCalls++;
            return Example;
        }, UiLanguage.French);
        guided.Show(owner);
        await Task.Delay(50);
        var prefix = kind == ObservationKind.Problem ? "problem" : "working";
        var next = Find<Button>(guided, "next");
        var back = Find<Button>(guided, "back");
        var software = Find<TextBox>(guided, "software");
        var version = Find<TextBox>(guided, "version");
        var gpu = Find<ComboBox>(guided, "testedGpu");
        var category = kind == ObservationKind.Problem ? Find<ComboBox>(guided, "category") : null;
        var steps = Find<TextBox>(guided, "steps");
        var expected = kind == ObservationKind.Problem ? Find<TextBox>(guided, "expected") : null;
        var observed = kind == ObservationKind.Problem ? Find<TextBox>(guided, "observed") : null;
        var duration = kind == ObservationKind.Working ? Find<TextBox>(guided, "duration") : null;
        var settings = Find<TextBox>(guided, "settings");
        var limitations = Find<TextBox>(guided, "limitations");
        var language = Find<ComboBox>(guided, "language");
        var preview = Find<TextBox>(guided, "preview");
        var save = Find<Button>(guided, "save");
        var copy = Find<Button>(guided, "copy");
        var openIssue = Find<Button>(guided, "openIssue");
        var windows = Find<CheckBox>(guided, "includeWindows");
        var graphics = Find<CheckBox>(guided, "includeGraphics");
        var ids = Find<CheckBox>(guided, "includeIds");

        Check(collectionCalls == 0, prefix + ": opening the guided path does not read hardware");
        Check(!gpu.Text.Contains("NVIDIA", StringComparison.Ordinal), prefix + ": a detected card is not assumed to be the tested GPU");
        Capture(guided, screenshots, prefix + "-step1-fr.png");
        next.PerformClick();
        Check(software.Visible && !preview.Visible, prefix + ": software is required before advancing");
        software.Text = "Example Game";
        version.Text = "1.2.3";
        gpu.Text = "Custom GPU name not in the detected list";
        if (category is { Items.Count: > 1 }) category.SelectedIndex = 1;
        next.PerformClick();
        var requiredDetail = kind == ObservationKind.Problem ? observed! : steps;
        Check(requiredDetail.Visible && !preview.Visible, prefix + ": software advances to the details step");
        next.PerformClick();
        Check(requiredDetail.Visible && !preview.Visible, prefix + ": the essential observation is required before review");

        steps.Text = "Start the game and load the tutorial.";
        if (expected is not null) expected.Text = "The main menu remains open.";
        if (observed is not null) observed.Text = "The game closes after the intro.";
        if (duration is not null) duration.Text = "15 minutes";
        settings.Text = "1920 x 1080, medium quality";
        limitations.Text = "Multiplayer and longer sessions were not tested.";
        Capture(guided, screenshots, prefix + "-step2-fr.png");
        back.PerformClick();
        Check(software.Text == "Example Game" && version.Text == "1.2.3" && software.Visible,
            prefix + ": going back preserves software and version");
        language.SelectedIndex = 1;
        Check(software.Text == "Example Game" && gpu.Text == "Custom GPU name not in the detected list",
            prefix + ": changing language preserves an explicitly typed GPU absent from the detected list");
        if (category is not null) Check(category.SelectedIndex == 1, "problem: translating categories preserves the user's selection");
        next.PerformClick();
        Check(steps.Text == "Start the game and load the tutorial." &&
            limitations.Text.Contains("Multiplayer", StringComparison.Ordinal),
            prefix + ": detail inputs survive backward navigation and language changes");
        next.PerformClick();
        Check(preview.Visible && preview.ReadOnly && save.Enabled && copy.Enabled && openIssue.Enabled,
            prefix + ": a valid observation reaches the review and sharing step");
        Check(preview.Text.Contains("Example Game", StringComparison.Ordinal) &&
            preview.Text.Contains("1.2.3", StringComparison.Ordinal) &&
            preview.Text.Contains(kind == ObservationKind.Problem ? "closes after the intro" : "Start the game", StringComparison.Ordinal),
            prefix + ": the preview includes the user's observation");
        Check(!ids.Checked && !preview.Text.Contains("10DE", StringComparison.Ordinal),
            prefix + ": optional hardware identifiers are excluded by default");
        ids.Checked = true;
        Check(preview.Text.Contains("10DE", StringComparison.Ordinal), prefix + ": opting in includes model identifiers");
        windows.Checked = false;
        graphics.Checked = false;
        Check(!preview.Text.Contains("Windows 11 Pro", StringComparison.Ordinal) &&
            !preview.Text.Contains("Intel UHD Graphics", StringComparison.Ordinal) &&
            !preview.Text.Contains("10DE", StringComparison.Ordinal) && save.Enabled,
            prefix + ": excluding configuration preserves a shareable manual observation");
        copy.PerformClick();
        Check(Clipboard.GetText().Contains("Example Game", StringComparison.Ordinal) &&
            !Clipboard.GetText().Contains("Windows 11 Pro", StringComparison.Ordinal),
            prefix + ": copying respects configuration choices");
        windows.Checked = true;
        graphics.Checked = true;
        ids.Checked = false;
        Check(Find<Label>(guided, "status").Text.Contains("Previous copies", StringComparison.Ordinal),
            prefix + ": a copied observation is marked outdated after configuration choices change");
        Capture(guided, screenshots, prefix + "-review-en.png");
        language.SelectedIndex = 0;
        Check(preview.Text.Contains("Example Game", StringComparison.Ordinal) &&
            preview.Text.Contains("Multiplayer", StringComparison.Ordinal) &&
            gpu.Text == "Custom GPU name not in the detected list" && collectionCalls == 0,
            prefix + ": review translation preserves observations without automatic collection");
        Capture(guided, screenshots, prefix + "-review-fr.png");
        guided.Size = new Size(640, 590);
        await Task.Delay(100);
        var scrollPage = preview.Parent?.Parent as ScrollableControl
            ?? throw new CheckFailure("Guided review needs a scrollable page");
        scrollPage.ScrollControlIntoView(preview);
        await Task.Delay(50);
        var visiblePreview = Rectangle.Intersect(preview.RectangleToScreen(preview.ClientRectangle),
            scrollPage.RectangleToScreen(scrollPage.ClientRectangle));
        Check(preview.Height >= 140 && visiblePreview.Width > 100 && visiblePreview.Height >= 100,
            prefix + ": the report remains readable and reachable in a 640 by 590 window");
        var visibleWindow = guided.RectangleToScreen(guided.ClientRectangle);
        Check(visibleWindow.Contains(copy.RectangleToScreen(copy.ClientRectangle)) &&
            visibleWindow.Contains(openIssue.RectangleToScreen(openIssue.ClientRectangle)),
            prefix + ": sharing buttons are not clipped in the small window");
        if (kind == ObservationKind.Problem) Capture(guided, screenshots, "problem-review-small-fr.png");
        guided.Close();
    }

    private static async Task TestGuidedWithoutConfigurationAsync(Form owner, string? screenshots)
    {
        var calls = 0;
        var collectionFails = true;
        using var guided = new GuidedReportForm(ObservationKind.Problem, null, () =>
        {
            Interlocked.Increment(ref calls);
            if (collectionFails) throw new InvalidOperationException("PRIVATE_CONFIGURATION_ERROR");
            return Example;
        }, UiLanguage.English);
        guided.Show(owner);
        await Task.Delay(50);
        Check(calls == 0, "Manual observation: opening the form does not trigger WMI");
        Find<TextBox>(guided, "software").Text = "Example Game without a hardware report";
        var next = Find<Button>(guided, "next");
        next.PerformClick();
        Find<TextBox>(guided, "observed").Text = "The loading screen stays black.";
        next.PerformClick();
        var preview = Find<TextBox>(guided, "preview");
        Check(preview.Visible && Find<Button>(guided, "copy").Enabled && calls == 0,
            "Manual observation: a contribution can be prepared without any hardware collection");
        var read = Find<Button>(guided, "readConfiguration");
        read.PerformClick();
        await WaitUntil(() => calls == 1 && read.Enabled);
        Check(!Descendants(guided).Any(control => control.Text.Contains("PRIVATE_CONFIGURATION_ERROR", StringComparison.Ordinal)),
            "Manual observation: raw collection exceptions stay out of the interface");
        Check(preview.Visible && preview.Text.Contains("loading screen stays black", StringComparison.Ordinal) &&
            Find<Button>(guided, "save").Enabled && Find<Button>(guided, "copy").Enabled && Find<Button>(guided, "openIssue").Enabled,
            "Manual observation: failed hardware collection does not prevent a valid contribution");
        Capture(guided, screenshots, "problem-review-without-configuration.png");
        collectionFails = false;
        read.PerformClick();
        await WaitUntil(() => calls == 2 && read.Enabled);
        Check(preview.Text.Contains("Windows 11 Pro", StringComparison.Ordinal) &&
            preview.Text.Contains("loading screen stays black", StringComparison.Ordinal),
            "Manual observation: retrying collection adds configuration while preserving the account of the problem");
        guided.Close();
    }

    private static async Task TestGuidedTimeoutAsync(Form owner)
    {
        using var releaseCollector = new ManualResetEventSlim(false);
        var calls = 0;
        using var guided = new GuidedReportForm(ObservationKind.Problem, null, () =>
        {
            Interlocked.Increment(ref calls);
            if (!releaseCollector.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("Synthetic collector deadline");
            return Example;
        }, UiLanguage.English, TimeSpan.FromMilliseconds(100));
        guided.Show(owner);
        await Task.Delay(50);
        try
        {
            Find<TextBox>(guided, "software").Text = "Example Game while WMI is slow";
            var next = Find<Button>(guided, "next");
            next.PerformClick();
            Find<TextBox>(guided, "observed").Text = "The game does not reach the main menu.";
            next.PerformClick();
            var read = Find<Button>(guided, "readConfiguration");
            var copy = Find<Button>(guided, "copy");
            var save = Find<Button>(guided, "save");
            var preview = Find<TextBox>(guided, "preview");
            read.PerformClick();
            await WaitUntil(() => Volatile.Read(ref calls) == 1 && copy.Enabled);
            Check(!read.Enabled && save.Enabled && Find<Button>(guided, "openIssue").Enabled,
                "Timed-out collection: the manual observation remains shareable while native work finishes");
            Check(preview.Text.Contains("does not reach the main menu", StringComparison.Ordinal) &&
                !preview.Text.Contains("Windows 11 Pro", StringComparison.Ordinal),
                "Timed-out collection: the preview contains the observation without unconfirmed configuration");
            read.PerformClick();
            Check(calls == 1, "Timed-out collection: a new native read cannot overlap the unfinished one");
            releaseCollector.Set();
            await WaitUntil(() => read.Enabled);
            Check(!preview.Text.Contains("Windows 11 Pro", StringComparison.Ordinal) && copy.Enabled && calls == 1,
                "Timed-out collection: a late native result is discarded instead of silently changing the report");
        }
        finally
        {
            releaseCollector.Set();
            guided.Close();
        }
    }

    private static IEnumerable<Control> Descendants(Control root)
    {
        foreach (Control control in root.Controls)
        {
            yield return control;
            foreach (var child in Descendants(control)) yield return child;
        }
    }

    private static T Find<T>(Control root, string name) where T : Control
    {
        var matches = root.Controls.Find(name, true).OfType<T>().ToArray();
        return matches.Length == 1 ? matches[0]
            : throw new CheckFailure($"Expected exactly one {typeof(T).Name} control named {name}");
    }

    private static void Check(bool condition, string description)
    {
        if (!condition) throw new CheckFailure(description);
        assertions++;
        Console.WriteLine("PASS: " + description);
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var clock = Stopwatch.StartNew();
        while (!condition())
        {
            if (clock.Elapsed > TimeSpan.FromSeconds(5)) throw new CheckFailure("Expected UI state was not reached within five seconds");
            await Task.Delay(20);
        }
    }

    private static string? ArgumentValue(string[] args, string key)
    {
        var index = Array.IndexOf(args, key);
        if (index < 0) return null;
        if (index + 1 >= args.Length) throw new ArgumentException("Missing value for " + key);
        return Path.GetFullPath(args[index + 1]);
    }

    private static void Capture(Form form, string? directory, string name)
    {
        if (directory is null) return;
        using var bitmap = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, form.Size));
        bitmap.Save(Path.Combine(directory, name), ImageFormat.Png);
    }

    private sealed class CheckFailure(string message) : Exception(message);
}
