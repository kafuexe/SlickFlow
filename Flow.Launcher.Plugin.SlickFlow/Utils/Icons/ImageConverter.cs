using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Flow.Launcher.Plugin.SlickFlow.Utils.Icons;

/// <summary>
/// Converts image data (bytes, Icon objects) to PNG files.
/// </summary>
internal static class ImageConverter
{
    /// <summary>
    /// Writes a <see cref="Icon"/> to a PNG file using WPF interop (preserves transparency).
    /// </summary>
    public static void SaveIconToPng(Icon icon, string destinationPath)
    {
        IntPtr hIcon = icon.Handle;
        BitmapSource src = System.Windows.Interop.Imaging.CreateBitmapSourceFromHIcon(
            hIcon,
            Int32Rect.Empty,
            BitmapSizeOptions.FromEmptyOptions());

        src.Freeze();

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(src));

        using var file = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        encoder.Save(file);
    }

    /// <summary>
    /// Tries to interpret raw bytes as an image (ICO, PNG, JPEG, GIF, BMP, etc.) and writes it as PNG.
    /// Returns true on success.
    /// </summary>
    public static bool TrySaveBytesAsPng(byte[] data, string pngPath)
    {
        if (data.Length == 0)
            return false;

        try
        {
            using var ms = new MemoryStream(data, writable: false);
            using Image img = Image.FromStream(ms, useEmbeddedColorManagement: false, validateImageData: false);

            using var bmp = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                g.DrawImageUnscaled(img, 0, 0);
            }

            bmp.Save(pngPath, ImageFormat.Png);
            return true;
        }
        catch (Exception ex) when (!ex.IsCritical())
        {
            System.Diagnostics.Debug.WriteLine($"TrySaveBytesAsPng failed: {ex.Message}");
            return false;
        }
    }
}
