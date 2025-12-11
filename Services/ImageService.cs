using Avalonia.Media.Imaging;

namespace DailyMealPlannerExtended.Services;

/// <summary>
/// Service for image manipulation operations.
/// Handles resizing, compression, and format conversion of images.
/// </summary>
public class ImageService
{
    /// <summary>
    /// Resizes an image to a maximum height while maintaining aspect ratio.
    /// Returns original image if it's already smaller than maxHeight.
    /// </summary>
    /// <param name="imageBytes">Source image as byte array</param>
    /// <param name="maxHeight">Maximum height in pixels</param>
    /// <returns>Resized image as byte array (PNG format)</returns>
    public async Task<byte[]> ResizeImageAsync(byte[] imageBytes, int maxHeight)
    {
        await using var inputStream = new System.IO.MemoryStream(imageBytes);
        using var bitmap = new Bitmap(inputStream);

        // Calculate new dimensions maintaining aspect ratio
        var originalWidth = bitmap.PixelSize.Width;
        var originalHeight = bitmap.PixelSize.Height;

        if (originalHeight <= maxHeight)
        {
            // Image is already smaller than max height, return as is
            return imageBytes;
        }

        var scale = (double)maxHeight / originalHeight;
        var newWidth = (int)(originalWidth * scale);
        var newHeight = maxHeight;

        // Create resized bitmap
        var resizedBitmap = bitmap.CreateScaledBitmap(new Avalonia.PixelSize(newWidth, newHeight));

        // Save to memory stream as PNG for lossless compression
        await using var outputStream = new System.IO.MemoryStream();
        resizedBitmap.Save(outputStream);
        return outputStream.ToArray();
    }
}
