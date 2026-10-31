// SPDX-License-Identifier: MPL-2.0
namespace Grelecy.Diagnostics.Windows;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--gpu-worker") return GpuProbeWorker.Run();
        if (args.Length != 0) return 2;
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
        return 0;
    }
}
