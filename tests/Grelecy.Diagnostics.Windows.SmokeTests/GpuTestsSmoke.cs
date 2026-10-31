// SPDX-License-Identifier: MPL-2.0
using System.Diagnostics;
using Grelecy.Diagnostics;
using Grelecy.Diagnostics.Windows;

internal static class GpuTestsSmoke
{
    private static readonly GpuTestAdapter First = new(0, "NVIDIA GeForce GTX 1060", 0x10DE, 0x1C03,
        6UL * 1024 * 1024 * 1024, false, "32.0.15.9999");
    private static readonly GpuTestAdapter Second = new(1, "Intel UHD Graphics", 0x8086, 0x3E92, 0, false);

    public static async Task RunAsync(Form owner, Action<bool, string> check, Action<Form, string> capture)
    {
        CheckMainButton(owner, check);
        var enumerationCalls = 0;
        var runCalls = 0;
        var mode = "success";
        var enumerationGate = new TaskCompletionSource<GpuAdapterScan>(TaskCreationOptions.RunContinuationsAsynchronously);
        var runGate = new TaskCompletionSource<GpuTestReport>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken lastToken = default;
        GpuTestAdapter? lastAdapter = null;
        using var form = new GpuTestsForm(UiLanguage.French,
            _ => { enumerationCalls++; return enumerationGate.Task; },
            (adapter, token) =>
            {
                runCalls++;
                lastAdapter = adapter;
                lastToken = token;
                return mode switch
                {
                    "pending" => runGate.Task,
                    "failure" => Task.FromException<GpuTestReport>(new InvalidOperationException("PRIVATE_MACHINE_PATH")),
                    _ => Task.FromResult(Example(adapter)),
                };
            });
        form.Show(owner);
        await Task.Delay(30);
        var load = Find<Button>(form, "gpuLoad");
        var start = Find<Button>(form, "gpuRun");
        var cancel = Find<Button>(form, "gpuCancel");
        var save = Find<Button>(form, "gpuSave");
        var copy = Find<Button>(form, "gpuCopy");
        var adapters = Find<ComboBox>(form, "gpuAdapter");
        var language = Find<ComboBox>(form, "gpuLanguage");
        var graphics = Find<CheckBox>(form, "gpuIncludeGraphics");
        var ids = Find<CheckBox>(form, "gpuIncludeIds");
        var preview = Find<TextBox>(form, "gpuPreview");
        var status = Find<Label>(form, "gpuStatus");

        check(enumerationCalls == 0 && runCalls == 0, "GPU: opening the window performs no hardware work");
        check(load.Enabled && !start.Enabled && !save.Enabled && !copy.Enabled && !cancel.Enabled,
            "GPU: testing and export require explicit discovery and selection");
        check(preview.ReadOnly && !string.IsNullOrWhiteSpace(preview.AccessibleName) &&
            status.LiveSetting == System.Windows.Forms.Automation.AutomationLiveSetting.Polite,
            "GPU: read-only preview and live status are accessible");
        check(!ids.Checked, "GPU: hardware identifiers are excluded initially");
        load.PerformClick();
        load.PerformClick();
        check(enumerationCalls == 1 && !load.Enabled && !start.Enabled && cancel.Enabled,
            "GPU: discovery cannot overlap itself or a benchmark");
        enumerationGate.SetResult(new GpuAdapterScan(
            [First, Second, new GpuTestAdapter(2, "Software renderer", 0, 0, 0, true)], GpuTestStatus.Passed, "ok"));
        await Until(() => load.Enabled);
        check(adapters.Items.Count == 2 && adapters.SelectedIndex == -1 && !start.Enabled,
            "GPU: software is excluded and no hardware adapter is assumed to be selected");
        adapters.SelectedIndex = 0;
        check(start.Enabled && !save.Enabled, "GPU: a selected card enables the opt-in test only");
        start.PerformClick();
        await Until(() => save.Enabled);
        check(runCalls == 1 && lastAdapter == First && preview.Text.Contains(First.Name, StringComparison.Ordinal),
            "GPU: the explicit adapter is passed to the runner and shown in its report");
        check(!preview.Text.Contains("10DE", StringComparison.Ordinal), "GPU: model IDs do not leak into the default report");
        capture(form, "gpu-tests-fr.png");

        foreach (var item in LanguageCatalog.All)
        {
            language.SelectedIndex = LanguageCatalog.All.ToList().FindIndex(entry => entry.Language == item.Language);
            check(form.Text == LocalizedText.Get("gpu.title", item.Language) &&
                form.RightToLeft == (item.IsRightToLeft ? RightToLeft.Yes : RightToLeft.No) &&
                preview.Text.Contains(First.Name, StringComparison.Ordinal) && runCalls == 1,
                "GPU: " + item.Code + " translates results without rerunning the workload");
        }
        language.SelectedIndex = LanguageCatalog.All.ToList().FindIndex(item => item.Language == UiLanguage.English);
        ids.Checked = true;
        check(preview.Text.Contains("10DE", StringComparison.Ordinal), "GPU: optional PCI model data is included only on request");
        copy.PerformClick();
        check(Clipboard.GetText().Contains("10DE", StringComparison.Ordinal), "GPU: copy uses current report options");
        graphics.Checked = false;
        check(!preview.Text.Contains(First.Name, StringComparison.Ordinal) &&
            !preview.Text.Contains("10DE", StringComparison.Ordinal) && !ids.Enabled && save.Enabled,
            "GPU: excluding graphics hides identity while preserving the measured result");
        check(status.Text == LocalizedText.Get("gpu.updated", UiLanguage.English),
            "GPU: changing sharing options marks previously copied content outdated");
        copy.PerformClick();
        check(!Clipboard.GetText().Contains(First.Name, StringComparison.Ordinal) &&
            !Clipboard.GetText().Contains("10DE", StringComparison.Ordinal), "GPU: the next copy respects identity exclusion");
        graphics.Checked = true;
        ids.Checked = false;

        mode = "pending";
        start.PerformClick();
        check(!start.Enabled && !load.Enabled && !adapters.Enabled && !save.Enabled && !copy.Enabled &&
            !preview.Text.Contains(First.Name, StringComparison.Ordinal), "GPU: a fresh run clears earlier measurements and disables export");
        cancel.PerformClick();
        check(lastToken.IsCancellationRequested && !cancel.Enabled && !start.Enabled,
            "GPU: cancellation reaches the worker and does not allow overlapping retries");
        runGate.SetResult(Example(First));
        await Until(() => start.Enabled);
        check(status.Text == LocalizedText.Get("gpu.cancelled", UiLanguage.English),
            "GPU: a late successful result cannot replace cancellation");
        copy.PerformClick();
        check(Clipboard.GetText().Contains("cancelled", StringComparison.Ordinal) &&
            !Clipboard.GetText().Contains("gpu_timing_measured", StringComparison.Ordinal),
            "GPU: the exported report preserves cancellation instead of late success");

        mode = "failure";
        start.PerformClick();
        await Until(() => start.Enabled);
        check(status.Text == LocalizedText.Get("gpu.failed", UiLanguage.English) &&
            !preview.Text.Contains("PRIVATE_MACHINE_PATH", StringComparison.Ordinal) &&
            !status.Text.Contains("PRIVATE_MACHINE_PATH", StringComparison.Ordinal),
            "GPU: worker exceptions expose only a scoped inconclusive result");
        adapters.SelectedIndex = 1;
        check(!save.Enabled && !copy.Enabled && !preview.Text.Contains(First.Name, StringComparison.Ordinal),
            "GPU: changing the selected card removes the previous card's report");
        mode = "success";
        start.PerformClick();
        await Until(() => save.Enabled);
        check(lastAdapter == Second && preview.Text.Contains(Second.Name, StringComparison.Ordinal) &&
            !preview.Text.Contains(First.Name, StringComparison.Ordinal), "GPU: a second adapter is tested and reported independently");
        Theme.Apply(form, dark: true);
        check(preview.BackColor.R < 60 && preview.ForeColor.R > 200, "GPU: the report follows the dark palette");
        capture(form, "gpu-tests-dark-en.png");

        mode = "pending";
        runGate = new TaskCompletionSource<GpuTestReport>(TaskCreationOptions.RunContinuationsAsynchronously);
        start.PerformClick();
        form.Close();
        check(lastToken.IsCancellationRequested, "GPU: closing the window cancels the active worker");
        runGate.SetResult(Example(Second));
        await Task.Delay(30);
        check(form.IsDisposed, "GPU: a completed worker does not reopen or update a closed window");
    }

    private static void CheckMainButton(Form owner, Action<bool, string> check)
    {
        var mainLanguage = Find<ComboBox>(owner, "language");
        var before = mainLanguage.SelectedIndex;
        var inspected = false;
        Exception? failure = null;
        using var timer = new System.Windows.Forms.Timer { Interval = 20 };
        timer.Tick += (_, _) =>
        {
            var dialog = Application.OpenForms.OfType<GpuTestsForm>().FirstOrDefault();
            if (dialog is null) return;
            timer.Stop();
            try
            {
                check(Find<ComboBox>(dialog, "gpuAdapter").Items.Count == 0 && !Find<Button>(dialog, "gpuRun").Enabled,
                    "GPU: the main action opens the opt-in testing window without starting a test");
                Find<ComboBox>(dialog, "gpuLanguage").SelectedIndex = 1;
                inspected = true;
            }
            catch (Exception error) { failure = error; }
            finally { dialog.Close(); }
        };
        try
        {
            timer.Start();
            Find<Button>(owner, "gpuTests").PerformClick();
            if (failure is not null) throw failure;
            check(inspected && mainLanguage.SelectedIndex == 1,
                "GPU: the main action preserves the selected language on return");
        }
        finally { timer.Stop(); mainLanguage.SelectedIndex = before; }
    }

    private static GpuTestReport Example(GpuTestAdapter adapter) => new(DateTimeOffset.UtcNow, adapter,
        new(GpuTestStatus.Passed, "d3d11_created", "11_0"), new(GpuTestStatus.Passed, "compute_verified"),
        adapter.VendorId == 0x10DE ? new(GpuTestStatus.Passed, "cuda_driver_ready", 12080, 6, 1)
            : new(GpuTestStatus.NotAvailable, "cuda_not_nvidia"),
        new(GpuTestStatus.Passed, "gpu_timing_measured", GpuTestProtocol.Scenario, GpuTestProtocol.ElementCount,
            GpuTestProtocol.Iterations, GpuTestProtocol.WarmupCount, [2.2, 2.0, 2.1, 2.3, 2.4]));

    private static T Find<T>(Control root, string name) where T : Control =>
        root.Controls.Find(name, true).OfType<T>().Single();

    private static async Task Until(Func<bool> condition)
    {
        var time = Stopwatch.StartNew();
        while (!condition())
        {
            if (time.Elapsed > TimeSpan.FromSeconds(5)) throw new InvalidOperationException("GPU UI condition timed out");
            await Task.Delay(10);
        }
    }
}
