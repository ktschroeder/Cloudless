using System.Windows;

namespace Cloudless.Models;

internal static class CropGeometry
{
    internal static bool TryConvertWindowRectToImageCoordinates(
        Rect windowRect,
        Rect imageDisplayBounds,
        double scaleX,
        double scaleY,
        double panX,
        double panY,
        int pixelWidth,
        int pixelHeight,
        out SelectionRectangle result)
    {
        result = default;

        if (windowRect.IsEmpty || imageDisplayBounds.IsEmpty ||
            !IsFinite(windowRect.X, windowRect.Y, windowRect.Width, windowRect.Height) ||
            !IsFinite(imageDisplayBounds.X, imageDisplayBounds.Y, imageDisplayBounds.Width, imageDisplayBounds.Height) ||
            windowRect.Width <= 0 || windowRect.Height <= 0 ||
            imageDisplayBounds.Width <= 0 || imageDisplayBounds.Height <= 0 ||
            !IsFinite(scaleX, scaleY, panX, panY) || scaleX <= 0 || scaleY <= 0 ||
            pixelWidth <= 0 || pixelHeight <= 0)
        {
            return false;
        }

        double selectionLeft = windowRect.Left - imageDisplayBounds.Left;
        double selectionTop = windowRect.Top - imageDisplayBounds.Top;
        double selectionRight = selectionLeft + windowRect.Width;
        double selectionBottom = selectionTop + windowRect.Height;

        double clampedLeft = Math.Max(0, selectionLeft);
        double clampedTop = Math.Max(0, selectionTop);
        double clampedRight = Math.Min(imageDisplayBounds.Width, selectionRight);
        double clampedBottom = Math.Min(imageDisplayBounds.Height, selectionBottom);
        if (clampedLeft >= clampedRight || clampedTop >= clampedBottom)
            return false;

        double imagePixelLeft = Math.Max(0, (clampedLeft - panX) / scaleX);
        double imagePixelTop = Math.Max(0, (clampedTop - panY) / scaleY);
        double imagePixelRight = Math.Min(pixelWidth, (clampedRight - panX) / scaleX);
        double imagePixelBottom = Math.Min(pixelHeight, (clampedBottom - panY) / scaleY);
        if (!IsFinite(imagePixelLeft, imagePixelTop, imagePixelRight, imagePixelBottom) ||
            imagePixelLeft >= imagePixelRight || imagePixelTop >= imagePixelBottom)
        {
            return false;
        }

        double cropPixelWidth = imagePixelRight - imagePixelLeft;
        double cropPixelHeight = imagePixelBottom - imagePixelTop;
        result = new SelectionRectangle
        {
            WindowCoordinates = windowRect,
            ImagePixelCoordinates = new Rect(imagePixelLeft, imagePixelTop, cropPixelWidth, cropPixelHeight),
            CropRenderWidth = cropPixelWidth,
            CropRenderHeight = cropPixelHeight,
            CropPanX = -imagePixelLeft * scaleX,
            CropPanY = -imagePixelTop * scaleY
        };

        return result.IsValid;
    }

    private static bool IsFinite(params double[] values)
    {
        return values.All(double.IsFinite);
    }
}
