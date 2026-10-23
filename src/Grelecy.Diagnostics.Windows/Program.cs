// SPDX-License-Identifier: MPL-2.0
namespace Grelecy.Diagnostics.Windows;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
