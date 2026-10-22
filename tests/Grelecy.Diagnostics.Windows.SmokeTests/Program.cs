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
            Console.Error.WriteLine("FAIL: Windows smoke checks exceeded 30 seconds.");
            Environment.Exit(1);
        }, null, TimeSpan.FromSeconds(30), Timeout.InfiniteTimeSpan);
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

    private static T Find<T>(Control root, string name) where T : Control =>
        root.Controls.Find(name, true).OfType<T>().Single();

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
