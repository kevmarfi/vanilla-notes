using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace VanillaNotes.App.Editing;

/// <summary>
/// The only images a note shows: the ones attached to the notes of the profile. An image at any
/// other address would be fetched from the network, and is never shown. Each picture is read and
/// drawn once, and kept while the profile is open, since a note is drawn again as it is typed.
/// </summary>
public sealed class AttachmentImages
{
    private readonly Func<long, byte[]?> _read;

    /// <summary>The pictures drawn so far, by the number of the attachment; nothing for one that cannot be shown.</summary>
    private readonly Dictionary<long, ImageSource?> _pictures = [];

    /// <param name="read">The bytes of an attached image, by its number; nothing when there is no such image.</param>
    public AttachmentImages(Func<long, byte[]?> read)
    {
        _read = read;
    }

    /// <summary>
    /// The picture of an attached image: nothing when there is no such image, or when its bytes
    /// are not a picture Windows can draw, so that the note shows its alternative text.
    /// </summary>
    public ImageSource? ImageOf(long attachmentId)
    {
        if (!_pictures.TryGetValue(attachmentId, out ImageSource? picture))
        {
            picture = Draw(_read(attachmentId));
            _pictures[attachmentId] = picture;
        }

        return picture;
    }

    /// <summary>Forgets the picture of an attachment that was removed, so that a note no longer shows it.</summary>
    public void Forget(long attachmentId) => _pictures.Remove(attachmentId);

    private static BitmapImage? Draw(byte[]? bytes)
    {
        if (bytes is null)
        {
            return null;
        }

        try
        {
            var picture = new BitmapImage();
            picture.BeginInit();
            picture.CacheOption = BitmapCacheOption.OnLoad;
            picture.StreamSource = new MemoryStream(bytes);
            picture.EndInit();
            picture.Freeze();
            return picture;
        }
        catch (Exception failure) when (failure is NotSupportedException or FileFormatException)
        {
            // Bytes no decoder of Windows reads, such as a file named as an image that is not one.
            return null;
        }
    }
}
