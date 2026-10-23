// SPDX-License-Identifier: MPL-2.0
namespace Grelecy.Diagnostics.Windows;

/// <summary>Original GRELECY artwork; its separate brand terms apply to the embedded images.</summary>
public static class Branding
{
    /// <summary>Return an independent icon. The receiving window owns its lifetime.</summary>
    public static Icon CreateIcon()
    {
        using var stream = typeof(Branding).Assembly.GetManifestResourceStream("Grelecy.Brand.Icon")
            ?? throw new InvalidOperationException("The GRELECY icon resource is missing.");
        using var source = new Icon(stream);
        return (Icon)source.Clone();
    }

    /// <summary>Create a DPI-scalable header image that disposes its own bitmap.</summary>
    public static PictureBox CreateLogoPictureBox(string name, int logicalSize = 52)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(logicalSize);
        using var stream = typeof(Branding).Assembly.GetManifestResourceStream("Grelecy.Brand.Logo")
            ?? throw new InvalidOperationException("The GRELECY logo resource is missing.");
        using var original = Image.FromStream(stream, useEmbeddedColorManagement: true, validateImageData: true);
        var image = new Bitmap(original);
        var picture = new PictureBox
        {
            Name = name,
            Image = image,
            Size = new Size(logicalSize, logicalSize),
            MinimumSize = new Size(logicalSize, logicalSize),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 12, 0),
            TabStop = false,
            AccessibleName = "GRELECY",
            AccessibleRole = AccessibleRole.Graphic,
        };
        picture.Disposed += (_, _) => image.Dispose();
        return picture;
    }
}
