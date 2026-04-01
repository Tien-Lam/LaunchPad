using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using LaunchDeck.Companion;
using Xunit;

namespace LaunchDeck.Tests;

public class IconExtractorCustomIconTests
{
    [Fact]
    public void LoadCustomIcon_PngFile_ReturnsBytesAndSuccess()
    {
        var tempFile = Path.GetTempFileName();
        var pngPath = Path.ChangeExtension(tempFile, ".png");
        File.Move(tempFile, pngPath);

        try
        {
            using (var bmp = new Bitmap(16, 16))
            {
                bmp.Save(pngPath, ImageFormat.Png);
            }

            var (success, data) = IconExtractor.LoadCustomIcon(pngPath);

            Assert.True(success);
            Assert.NotNull(data);
            Assert.True(data.Length > 0);
        }
        finally
        {
            File.Delete(pngPath);
        }
    }

    [Fact]
    public void LoadCustomIcon_IcoFile_ConvertsToPngBytes()
    {
        var tempFile = Path.GetTempFileName();
        var icoPath = Path.ChangeExtension(tempFile, ".ico");
        File.Move(tempFile, icoPath);

        try
        {
            using (var icon = Icon.ExtractAssociatedIcon(@"C:\Windows\notepad.exe"))
            {
                Assert.NotNull(icon);
                using var fs = File.Create(icoPath);
                icon.Save(fs);
            }

            var (success, data) = IconExtractor.LoadCustomIcon(icoPath);

            Assert.True(success);
            Assert.NotNull(data);
            Assert.True(data.Length > 0);

            // Verify it's valid PNG by checking PNG magic bytes
            Assert.Equal(0x89, data[0]);
            Assert.Equal(0x50, data[1]);
            Assert.Equal(0x4E, data[2]);
            Assert.Equal(0x47, data[3]);
        }
        finally
        {
            File.Delete(icoPath);
        }
    }

    [Fact]
    public void LoadCustomIcon_NonexistentFile_ReturnsFailure()
    {
        var (success, data) = IconExtractor.LoadCustomIcon(@"C:\nonexistent\icon.png");

        Assert.False(success);
        Assert.Null(data);
    }

    [Fact]
    public void LoadCustomIcon_UnsupportedExtension_ReturnsFailure()
    {
        var tempFile = Path.GetTempFileName();
        var txtPath = Path.ChangeExtension(tempFile, ".txt");
        File.Move(tempFile, txtPath);

        try
        {
            File.WriteAllText(txtPath, "not an image");

            var (success, data) = IconExtractor.LoadCustomIcon(txtPath);

            Assert.False(success);
            Assert.Null(data);
        }
        finally
        {
            File.Delete(txtPath);
        }
    }
}
