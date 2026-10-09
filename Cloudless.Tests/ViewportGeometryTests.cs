using System.Windows;
using Cloudless.Models;
using Xunit;

namespace Cloudless.Tests;

public class ViewportGeometryTests
{
    [Theory]
    [MemberData(nameof(FitWithinBoundsCases))]
    public void FitWithinBounds_PreservesAspectRatioWithoutUpscaling(
        double contentWidth,
        double contentHeight,
        double boundsWidth,
        double boundsHeight,
        double expectedWidth,
        double expectedHeight)
    {
        var fitted = ViewportGeometry.FitWithinBounds(contentWidth, contentHeight, boundsWidth, boundsHeight);

        Assert.Equal(expectedWidth, fitted.Width, 10);
        Assert.Equal(expectedHeight, fitted.Height, 10);
        Assert.True(fitted.Width <= boundsWidth);
        Assert.True(fitted.Height <= boundsHeight);
        Assert.True(fitted.Width <= contentWidth);
        Assert.True(fitted.Height <= contentHeight);
    }

    [Theory]
    [MemberData(nameof(InvalidFitCases))]
    public void FitWithinBounds_ReturnsEmptyForNonPositiveOrNonFiniteDimensions(
        double contentWidth,
        double contentHeight,
        double boundsWidth,
        double boundsHeight)
    {
        Assert.Equal(Size.Empty, ViewportGeometry.FitWithinBounds(contentWidth, contentHeight, boundsWidth, boundsHeight));
    }

    [Theory]
    [MemberData(nameof(ValidResizeScaleCases))]
    public void GetWindowResizeScale_UsesTheGeometricMeanOfDimensionRatios(
        Size previousSize,
        Size newSize,
        double expectedScale)
    {
        var scale = ViewportGeometry.GetWindowResizeScale(previousSize, newSize);

        Assert.Equal(expectedScale, scale, 10);
    }

    public static IEnumerable<object[]> FitWithinBoundsCases()
    {
        yield return new object[] { 100d, 50d, 200d, 200d, 100d, 50d };
        yield return new object[] { 400d, 200d, 300d, 300d, 300d, 150d };
        yield return new object[] { 200d, 400d, 300d, 300d, 150d, 300d };
        yield return new object[] { 400d, 200d, 300d, 100d, 200d, 100d };
        yield return new object[] { 200d, 400d, 100d, 300d, 100d, 200d };
        yield return new object[] { 400d, 400d, 300d, 200d, 200d, 200d };
        yield return new object[] { 600d, 1200d, 1000d, 800d, 400d, 800d };
        yield return new object[] { 800d, 1600d, 400d, 300d, 150d, 300d };
        yield return new object[] { 1600d, 900d, 1000d, 800d, 1000d, 562.5d };
        yield return new object[] { 1d, 1d, 1d, 1d, 1d, 1d };
    }

    public static IEnumerable<object[]> InvalidFitCases()
    {
        yield return new object[] { 0d, 100d, 100d, 100d };
        yield return new object[] { 100d, 0d, 100d, 100d };
        yield return new object[] { 100d, 100d, 0d, 100d };
        yield return new object[] { 100d, 100d, 100d, 0d };
        yield return new object[] { -1d, 100d, 100d, 100d };
        yield return new object[] { 100d, 100d, 100d, double.NaN };
        yield return new object[] { double.PositiveInfinity, 100d, 100d, 100d };
        yield return new object[] { 100d, 100d, double.PositiveInfinity, 100d };
    }

    [Theory]
    [MemberData(nameof(InvalidResizeScaleCases))]
    public void GetWindowResizeScale_ReturnsNaNWhenEitherSizeHasNonPositiveDimensions(Size previousSize, Size newSize)
    {
        Assert.True(double.IsNaN(ViewportGeometry.GetWindowResizeScale(previousSize, newSize)));
    }

    [Theory]
    [MemberData(nameof(TranslationCases))]
    public void ClampTranslation_CombinesCurrentOffsetAndDeltaThenConstrainsToVisibleBounds(
        double currentX,
        double currentY,
        Vector delta,
        double scaledWidth,
        double scaledHeight,
        double containerWidth,
        double containerHeight,
        bool constrain,
        double expectedX,
        double expectedY)
    {
        var actual = ViewportGeometry.ClampTranslation(
            currentX,
            currentY,
            delta,
            scaledWidth,
            scaledHeight,
            containerWidth,
            containerHeight,
            constrain);

        Assert.Equal(new Vector(expectedX, expectedY), actual);
    }

    [Fact]
    public void ClampTranslation_WithNoDeltaRetainsCurrentOffsetWhenWithinBounds()
    {
        var actual = ViewportGeometry.ClampTranslation(20, -30, null, 200, 200, 100, 100, true);

        Assert.Equal(new Vector(20, -30), actual);
    }

    [Fact]
    public void ClampTranslation_AllowsUnboundedOffsetsWhenSmartZoomIsDisabled()
    {
        var actual = ViewportGeometry.ClampTranslation(5, -10, new Vector(200, -300), 100, 100, 100, 100, false);

        Assert.Equal(new Vector(205, -310), actual);
    }

    [Theory]
    [MemberData(nameof(ZoomScaleCases))]
    public void ConstrainZoomScale_RespectsSmartZoomMaximumAndDisabledSetting(
        double requestedScale,
        double currentScaleX,
        double currentScaleY,
        bool disableSmartZoom,
        double expectedScale)
    {
        var scale = ViewportGeometry.ConstrainZoomScale(
            requestedScale,
            currentScaleX,
            currentScaleY,
            disableSmartZoom);

        Assert.Equal(expectedScale, scale, 10);
    }

    public static IEnumerable<object[]> ValidResizeScaleCases()
    {
        yield return new object[] { new Size(100, 100), new Size(100, 100), 1d };
        yield return new object[] { new Size(100, 100), new Size(200, 200), 2d };
        yield return new object[] { new Size(100, 100), new Size(400, 100), 2d };
        yield return new object[] { new Size(100, 100), new Size(400, 25), 1d };
        yield return new object[] { new Size(200, 100), new Size(100, 200), 1d };
        yield return new object[] { new Size(80, 60), new Size(160, 90), Math.Sqrt(3) };
        yield return new object[] { new Size(100, 100), new Size(25, 25), 0.25d };
    }

    public static IEnumerable<object[]> InvalidResizeScaleCases()
    {
        yield return new object[] { new Size(0, 100), new Size(100, 100) };
        yield return new object[] { new Size(100, 0), new Size(100, 100) };
        yield return new object[] { new Size(100, 100), new Size(0, 100) };
        yield return new object[] { new Size(100, 100), new Size(100, 0) };
    }

    public static IEnumerable<object[]> TranslationCases()
    {
        yield return new object[] { 0d, 0d, new Vector(120, -150), 200d, 300d, 100d, 100d, true, 50d, -100d };
        yield return new object[] { 0d, 0d, new Vector(-120, 150), 200d, 300d, 100d, 100d, true, -50d, 100d };
        yield return new object[] { 10d, 10d, new Vector(60, -5), 200d, 100d, 100d, 100d, true, 50d, 0d };
        yield return new object[] { 5d, -5d, new Vector(2, -2), 80d, 90d, 100d, 100d, true, 0d, 0d };
        yield return new object[] { 20d, -20d, new Vector(10, 15), 200d, 200d, 100d, 100d, true, 30d, -5d };
        yield return new object[] { 0d, 0d, new Vector(200, -300), 100d, 100d, 100d, 100d, false, 200d, -300d };
        yield return new object[] { 0d, 0d, new Vector(50, -50), 200d, 200d, 100d, 100d, true, 50d, -50d };
    }

    public static IEnumerable<object[]> ZoomScaleCases()
    {
        yield return new object[] { 1d, 1d, 1d, false, 1d };
        yield return new object[] { 0.5d, 2d, 2d, false, 0.5d };
        yield return new object[] { 2d, 1d, 1d, false, 2d };
        yield return new object[] { 20d, 1d, 1d, false, 10d };
        yield return new object[] { 10d, 2d, 5d, false, 2d };
        yield return new object[] { 5d, 10d, 4d, false, 1d };
        yield return new object[] { 8d, 20d, 20d, false, 1d };
        yield return new object[] { 2d, 20d, 20d, true, 2d };
        yield return new object[] { 2d, 0d, 0d, false, 2d };
        yield return new object[] { 4d, 2d, 8d, false, 1.25d };
    }
}
