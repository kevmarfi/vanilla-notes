using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace VanillaNotes.App.Controls;

/// <summary>
/// An image on the clipboard, such as a screenshot, as the editors of a note take it when it is
/// pasted: a PNG file. Only what the clipboard holds as an image alone is taken; anything that also
/// holds text, as a copy from a web page does, is pasted as its text.
/// </summary>
public static class PastedImage
{
    /// <summary>The image a paste brings, as the bytes of a PNG file, or nothing when it brings text or no image.</summary>
    public static byte[]? PngOf(IDataObject data)
    {
        if (data.GetDataPresent(DataFormats.UnicodeText) || !data.GetDataPresent(DataFormats.Bitmap) || data.GetData(DataFormats.Bitmap) is not BitmapSource image)
        {
            return null;
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        return stream.ToArray();
    }
}
