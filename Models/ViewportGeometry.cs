using System.Windows;

namespace Cloudless.Models;

internal static class ViewportGeometry
{
    internal static Size FitWithinBounds(double contentWidth, double contentHeight, double boundsWidth, double boundsHeight)
    {
        if (!double.IsFinite(contentWidth) || !double.IsFinite(contentHeight) ||
            !double.IsFinite(boundsWidth) || !double.IsFinite(boundsHeight) ||
            contentWidth <= 0 || contentHeight <= 0 || boundsWidth <= 0 || boundsHeight <= 0)
        {
            return Size.Empty;
        }

        double scale = Math.Min(1, Math.Min(boundsWidth / contentWidth, boundsHeight / contentHeight));
        return new Size(contentWidth * scale, contentHeight * scale);
    }

    internal static double GetWindowResizeScale(Size previousSize, Size newSize)
    {
        if (previousSize.Width <= 0 || previousSize.Height <= 0 || newSize.Width <= 0 || newSize.Height <= 0)
            return double.NaN;

        return Math.Sqrt(
            (newSize.Width / previousSize.Width) *
            (newSize.Height / previousSize.Height));
    }

    internal static Vector ClampTranslation(
        double currentX,
        double currentY,
        Vector? delta,
        double scaledWidth,
        double scaledHeight,
        double containerWidth,
        double containerHeight,
        bool constrainToBounds)
    {
        double translateX = currentX + (delta?.X ?? 0);
        double translateY = currentY + (delta?.Y ?? 0);

        if (constrainToBounds)
        {
            double maxTranslateX = Math.Max(0, (scaledWidth - containerWidth) / 2);
            double maxTranslateY = Math.Max(0, (scaledHeight - containerHeight) / 2);
            translateX = Math.Min(Math.Max(translateX, -maxTranslateX), maxTranslateX);
            translateY = Math.Min(Math.Max(translateY, -maxTranslateY), maxTranslateY);
        }

        return new Vector(translateX, translateY);
    }

    internal static double ConstrainZoomScale(
        double requestedScale,
        double currentScaleX,
        double currentScaleY,
        bool disableSmartZoom)
    {
        if (requestedScale > 1 && !disableSmartZoom)
        {
            double currentMaxScale = Math.Max(currentScaleX, currentScaleY);
            return Math.Min(requestedScale, Math.Max(1.0, 10.0 / currentMaxScale));
        }

        return requestedScale;
    }
}
