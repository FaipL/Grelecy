// SPDX-License-Identifier: MPL-2.0
using Grelecy.Diagnostics.Windows;

internal static class ThemeSmoke
{
    public static void Run(Action<bool, string> check)
    {
        var baseline = Theme.AttachedFormCount;
        using var form = new Form();
        using var windowIcon = Branding.CreateIcon();
        form.Icon = windowIcon;
        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill };
        var input = new TextBox { Text = "A retained answer — réponse محفوظة", Width = 240 };
        var preview = new TextBox { ReadOnly = true, Text = "A retained report", Width = 240 };
        var selection = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList };
        selection.Items.AddRange(["First", "Second"]);
        selection.SelectedIndex = 1;
        var choice = new CheckBox { Text = "Selected category", Checked = true, AutoSize = true };
        var disabled = new Button { Text = "Unavailable", Enabled = false };
        var button = new Button { Text = "Continue" };
        var logo = Branding.CreateLogoPictureBox("brandLogo", 52);
        panel.Controls.AddRange([input, preview, selection, choice, disabled, button, logo]);
        form.Controls.Add(panel);
        Theme.Attach(form);
        Theme.Attach(form);
        _ = form.Handle;
        check(Theme.AttachedFormCount == baseline + 1, "Theme: attachment is idempotent and registers exactly one window");
        check(windowIcon.Width >= 16 && logo.Image is { Width: 1254, Height: 1254 },
            "Branding: the window icon and original logo are embedded and load successfully");

        Theme.Apply(form, dark: true);
        check(Luminance(form.BackColor) < 0.05 && Contrast(form.ForeColor, form.BackColor) >= 7,
            "Theme: the dark window has a readable foreground/background pair");
        check(Contrast(input.ForeColor, input.BackColor) >= 7 &&
            Contrast(preview.ForeColor, preview.BackColor) >= 7 &&
            Contrast(selection.ForeColor, selection.BackColor) >= 7,
            "Theme: editable, read-only and dropdown text colors retain strong contrast in dark mode");
        check(Contrast(disabled.ForeColor, disabled.BackColor) >= 3 && !disabled.Enabled,
            "Theme: disabled control colors remain distinguishable without enabling the action");
        check(choice.Checked && logo.BackColor == Color.Transparent,
            "Theme: checkbox state and logo transparency are preserved");
        var later = new TextBox { Text = "Added after attachment" };
        panel.Controls.Add(later);
        check(later.BackColor == input.BackColor && later.ForeColor == input.ForeColor,
            "Theme: dynamically added controls receive the current palette");
        button.Enabled = false;
        check(button.ForeColor == disabled.ForeColor, "Theme: disabling a control updates its text color");
        button.Enabled = true;

        var answer = input.Text;
        Theme.Apply(form, dark: false);
        check(form.BackColor == SystemColors.Control && input.BackColor == SystemColors.Window &&
            preview.ForeColor == SystemColors.WindowText,
            "Theme: light mode restores native Windows surface and input colors");
        check(input.Text == answer && preview.Text == "A retained report" && selection.SelectedIndex == 1 && choice.Checked,
            "Theme: changing palettes preserves answers, report text, selection and checkboxes");

        Theme.Apply(form, dark: true, highContrast: true);
        check(form.BackColor == SystemColors.Control && form.ForeColor == SystemColors.ControlText &&
            preview.BackColor == SystemColors.Window && preview.ForeColor == SystemColors.WindowText &&
            button.UseVisualStyleBackColor && choice.UseVisualStyleBackColor,
            "Theme: accessibility contrast colors take precedence over the dark preference");
        form.Dispose();
        check(Theme.AttachedFormCount == baseline, "Theme: disposing the form removes its system-event subscription");
    }

    private static double Luminance(Color color)
    {
        static double Channel(byte value)
        {
            var channel = value / 255.0;
            return channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }
        return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
    }

    private static double Contrast(Color first, Color second)
    {
        var left = Luminance(first);
        var right = Luminance(second);
        return (Math.Max(left, right) + 0.05) / (Math.Min(left, right) + 0.05);
    }
}
