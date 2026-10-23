// SPDX-License-Identifier: MPL-2.0
using Microsoft.Win32;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

[assembly: InternalsVisibleTo("Grelecy.Diagnostics.Windows.SmokeTests")]

namespace Grelecy.Diagnostics.Windows;

/// <summary>Follow the Windows app theme without changing Windows preferences.</summary>
public static class Theme
{
    private static readonly ConditionalWeakTable<Form, Subscription> Subscriptions = new();
    private static int subscriptionCount;

    internal static int AttachedFormCount => Volatile.Read(ref subscriptionCount);

    // WinForms 10's Application.SetColorMode(System) supports Windows 11 only and
    // documents that an OS theme change needs a restart. Explicit control colors
    // provide the same live behavior on Windows 10 and Windows 11 instead.
    public static void Attach(Form form)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (form.IsDisposed || Subscriptions.TryGetValue(form, out _)) return;
        Subscriptions.Add(form, new Subscription(form));
    }

    /// <summary>Apply a palette locally, also usable by UI checks without changing the registry.</summary>
    public static void Apply(Form form, bool dark, bool highContrast = false)
    {
        ArgumentNullException.ThrowIfNull(form);
        if (form.IsDisposed) return;
        var palette = new Palette(dark && !highContrast, highContrast);
        if (Subscriptions.TryGetValue(form, out var subscription)) subscription.Apply(palette);
        else ApplyTree(form, palette);
    }

    /// <summary>Re-read the current Windows preference and accessibility colors.</summary>
    public static void Refresh(Form form) => Apply(form, PrefersDark(), SystemInformation.HighContrast);

    private static bool PrefersDark()
    {
        if (SystemInformation.HighContrast) return false;
        try
        {
            using var preferences = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize", writable: false);
            return preferences?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            // The system palette is the readable fallback if this optional preference is unavailable.
            return false;
        }
    }

    private readonly record struct Palette(bool Dark, bool HighContrast)
    {
        public Color Surface => Dark ? Color.FromArgb(24, 27, 31) : SystemColors.Control;
        public Color Input => Dark ? Color.FromArgb(31, 35, 40) : SystemColors.Window;
        public Color Text => Dark ? Color.FromArgb(236, 239, 242) : SystemColors.ControlText;
        public Color InputText => Dark ? Text : SystemColors.WindowText;
        public Color DisabledText => Dark ? Color.FromArgb(162, 170, 178) : SystemColors.GrayText;
        public Color Border => Dark ? Color.FromArgb(104, 113, 122) : SystemColors.ControlDark;
        public Color Hover => Dark ? Color.FromArgb(46, 54, 61) : SystemColors.ControlLight;
        public Color Pressed => Dark ? Color.FromArgb(58, 68, 76) : SystemColors.ControlDark;
    }

    private static void ApplyTree(Control control, Palette palette)
    {
        ApplyControl(control, palette);
        foreach (Control child in control.Controls) ApplyTree(child, palette);
        if (control is Form form) ApplyTitleBar(form, palette.Dark);
        control.Invalidate();
    }

    private static void ApplyControl(Control control, Palette palette)
    {
        var text = control.Enabled ? palette.Text : palette.DisabledText;
        switch (control)
        {
            case TextBoxBase input:
                input.BackColor = control.Enabled ? palette.Input : palette.Surface;
                input.ForeColor = control.Enabled ? palette.InputText : palette.DisabledText;
                break;
            case ComboBox input:
                input.BackColor = control.Enabled ? palette.Input : palette.Surface;
                input.ForeColor = control.Enabled ? palette.InputText : palette.DisabledText;
                input.FlatStyle = palette.Dark ? FlatStyle.Flat : FlatStyle.Standard;
                break;
            case Button button:
                button.FlatStyle = palette.Dark ? FlatStyle.Flat : FlatStyle.Standard;
                button.BackColor = palette.Dark ? palette.Input : SystemColors.Control;
                button.ForeColor = text;
                button.UseVisualStyleBackColor = !palette.Dark;
                button.FlatAppearance.BorderColor = palette.Border;
                button.FlatAppearance.MouseOverBackColor = palette.Hover;
                button.FlatAppearance.MouseDownBackColor = palette.Pressed;
                break;
            case ButtonBase choice:
                choice.FlatStyle = FlatStyle.Standard;
                choice.BackColor = palette.Surface;
                choice.ForeColor = text;
                choice.UseVisualStyleBackColor = !palette.Dark;
                break;
            case PictureBox picture:
                // Preserve the official logo's alpha rather than adding a gray rectangle.
                picture.BackColor = Color.Transparent;
                picture.ForeColor = text;
                break;
            case LinkLabel link:
                link.BackColor = palette.Surface;
                link.ForeColor = text;
                link.LinkColor = palette.Dark ? Color.FromArgb(174, 205, 184) : SystemColors.HotTrack;
                link.ActiveLinkColor = palette.Dark ? Color.FromArgb(205, 225, 210) : SystemColors.HotTrack;
                link.VisitedLinkColor = link.LinkColor;
                link.DisabledLinkColor = palette.DisabledText;
                break;
            default:
                control.BackColor = palette.Surface;
                control.ForeColor = text;
                break;
        }
    }

    private static void ApplyTitleBar(Form form, bool dark)
    {
        if (!form.IsHandleCreated || !OperatingSystem.IsWindowsVersionAtLeast(10, 0, 17763)) return;
        var enabled = dark ? 1 : 0;
        // Unsupported Windows builds return an HRESULT; client-area theming still works.
        _ = DwmSetWindowAttribute(form.Handle, 20 /* DWMWA_USE_IMMERSIVE_DARK_MODE */, ref enabled, sizeof(int));
    }

    [DllImport("dwmapi.dll", ExactSpelling = true)]
    private static extern int DwmSetWindowAttribute(nint window, int attribute, ref int value, int size);

    private sealed class Subscription : IDisposable
    {
        private readonly Form form;
        private readonly HashSet<Control> observed = [];
        private Palette palette;
        private int refreshPending;
        private bool disposed;

        public Subscription(Form form)
        {
            this.form = form;
            Watch(form);
            form.Disposed += FormDisposed;
            form.HandleCreated += HandleCreated;
            SystemEvents.UserPreferenceChanged += PreferencesChanged;
            Interlocked.Increment(ref subscriptionCount);
            Apply(new Palette(PrefersDark(), SystemInformation.HighContrast));
        }

        public void Apply(Palette next)
        {
            if (disposed || form.IsDisposed) return;
            palette = next;
            ApplyTree(form, palette);
        }

        private void Watch(Control control)
        {
            if (!observed.Add(control)) return;
            control.ControlAdded += ControlAdded;
            control.ControlRemoved += ControlRemoved;
            control.EnabledChanged += StateChanged;
            control.Disposed += ControlDisposed;
            foreach (Control child in control.Controls) Watch(child);
        }

        private void Unwatch(Control control)
        {
            if (!observed.Remove(control)) return;
            control.ControlAdded -= ControlAdded;
            control.ControlRemoved -= ControlRemoved;
            control.EnabledChanged -= StateChanged;
            control.Disposed -= ControlDisposed;
            foreach (Control child in control.Controls) Unwatch(child);
        }

        private void ControlAdded(object? sender, ControlEventArgs args)
        {
            if (args.Control is not { } control) return;
            Watch(control);
            ApplyTree(control, palette);
        }
        private void ControlRemoved(object? sender, ControlEventArgs args)
        {
            if (args.Control is { } control) Unwatch(control);
        }
        private void ControlDisposed(object? sender, EventArgs args)
        {
            if (sender is Control control) Unwatch(control);
        }
        private void StateChanged(object? sender, EventArgs args)
        {
            if (!disposed && sender is Control control && !control.IsDisposed) ApplyControl(control, palette);
        }
        private void HandleCreated(object? sender, EventArgs args)
        {
            Interlocked.Exchange(ref refreshPending, 0);
            if (!disposed) Apply(new Palette(PrefersDark(), SystemInformation.HighContrast));
        }

        private void PreferencesChanged(object sender, UserPreferenceChangedEventArgs args)
        {
            if (disposed || form.IsDisposed || !form.IsHandleCreated || Interlocked.Exchange(ref refreshPending, 1) != 0) return;
            try
            {
                // SystemEvents can run on a different thread. Repaint only on this window's UI thread.
                form.BeginInvoke((Action)(() =>
                {
                    Interlocked.Exchange(ref refreshPending, 0);
                    if (!disposed && !form.IsDisposed)
                        Apply(new Palette(PrefersDark(), SystemInformation.HighContrast));
                }));
            }
            catch (InvalidOperationException) { Interlocked.Exchange(ref refreshPending, 0); }
        }

        private void FormDisposed(object? sender, EventArgs args) => Dispose();
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SystemEvents.UserPreferenceChanged -= PreferencesChanged;
            form.Disposed -= FormDisposed;
            form.HandleCreated -= HandleCreated;
            foreach (var control in observed.ToArray()) Unwatch(control);
            Subscriptions.Remove(form);
            Interlocked.Decrement(ref subscriptionCount);
        }
    }
}
