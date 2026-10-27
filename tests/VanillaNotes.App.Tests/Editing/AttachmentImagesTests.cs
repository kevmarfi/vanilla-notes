using System.Text;
using System.Windows.Media;
using Xunit;
using VanillaNotes.App.Editing;
using VanillaNotes.App.Tests.Helpers;

namespace VanillaNotes.App.Tests.Editing;

/// <summary>The pictures a note shows: only attached images, each read once.</summary>
public sealed class AttachmentImagesTests
{
    /// <summary>A picture of one pixel, as a PNG file.</summary>
    private static readonly byte[] _pixel = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    [Fact]
    public void AnAttachedImageIsDrawn()
    {
        Sta.Run(() =>
        {
            ImageSource? picture = new AttachmentImages(_ => _pixel).ImageOf(7);

            Assert.NotNull(picture);
            Assert.Equal(1, picture.Width);
        });
    }

    [Fact]
    public void WhatIsNotAnAttachedImageShowsNoPicture()
    {
        Sta.Run(() =>
        {
            var images = new AttachmentImages(id => id == 1 ? Encoding.UTF8.GetBytes("not a picture") : null);

            Assert.Null(images.ImageOf(1));
            Assert.Null(images.ImageOf(2));
        });
    }

    [Fact]
    public void AnImageIsReadOnceUntilItIsForgotten()
    {
        Sta.Run(() =>
        {
            int reads = 0;
            var images = new AttachmentImages(_ =>
            {
                reads++;
                return _pixel;
            });

            images.ImageOf(7);
            images.ImageOf(7);
            Assert.Equal(1, reads);

            images.Forget(7);
            images.ImageOf(7);
            Assert.Equal(2, reads);
        });
    }
}
