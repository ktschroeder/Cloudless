using System.Windows;
using Cloudless.Models;
using Xunit;

namespace Cloudless.Tests;

public class CropCoordinateConversionTests
{
    [Fact]
    public void TryConvert_MapsSelectionWithIdentityTransform()
    {
        var windowRect = new Rect(120, 60, 40, 20);
        var bounds = new Rect(100, 50, 200, 100);

        var success = Convert(windowRect, bounds, 1, 1, 0, 0, 200, 100, out var result);

        Assert.True(success);
        Assert.Equal(windowRect, result.WindowCoordinates);
        Assert.Equal(new Rect(20, 10, 40, 20), result.ImagePixelCoordinates);
        Assert.Equal(40, result.CropRenderWidth);
        Assert.Equal(20, result.CropRenderHeight);
        Assert.Equal(-20, result.CropPanX);
        Assert.Equal(-10, result.CropPanY);
    }

    [Fact]
    public void TryConvert_ReversesIndependentScaleAndPanTransforms()
    {
        var success = Convert(
            new Rect(60, 50, 40, 20),
            new Rect(10, 20, 200, 100),
            2,
            2,
            -10,
            10,
            200,
            100,
            out var result);

        Assert.True(success);
        Assert.Equal(new Rect(30, 10, 20, 10), result.ImagePixelCoordinates);
        Assert.Equal(-60, result.CropPanX);
        Assert.Equal(-20, result.CropPanY);
    }

    [Theory]
    [InlineData(-10, -20, 30, 40, 0, 0, 20, 20)]
    [InlineData(80, 80, 30, 30, 80, 80, 20, 20)]
    public void TryConvert_ClampsPartiallyOverlappingSelectionToDisplayBounds(
        double x,
        double y,
        double width,
        double height,
        double expectedX,
        double expectedY,
        double expectedWidth,
        double expectedHeight)
    {
        var success = Convert(
            new Rect(x, y, width, height),
            new Rect(0, 0, 100, 100),
            1,
            1,
            0,
            0,
            100,
            100,
            out var result);

        Assert.True(success);
        Assert.Equal(new Rect(expectedX, expectedY, expectedWidth, expectedHeight), result.ImagePixelCoordinates);
    }

    [Fact]
    public void TryConvert_ClampsCoordinatesToUnderlyingBitmapDimensions()
    {
        var success = Convert(
            new Rect(0, 0, 100, 80),
            new Rect(0, 0, 100, 80),
            1,
            1,
            0,
            0,
            40,
            30,
            out var result);

        Assert.True(success);
        Assert.Equal(new Rect(0, 0, 40, 30), result.ImagePixelCoordinates);
        Assert.Equal(40, result.CropRenderWidth);
        Assert.Equal(30, result.CropRenderHeight);
    }

    [Fact]
    public void TryConvert_AccountsForPanWhenMappingSelectionBackToImagePixels()
    {
        var success = Convert(
            new Rect(0, 0, 30, 20),
            new Rect(0, 0, 100, 100),
            1,
            1,
            -20,
            -5,
            100,
            100,
            out var result);

        Assert.True(success);
        Assert.Equal(new Rect(20, 5, 30, 20), result.ImagePixelCoordinates);
    }

    [Theory]
    [InlineData(-20, 10, 10, 10)]
    [InlineData(110, 10, 10, 10)]
    [InlineData(10, -20, 10, 10)]
    [InlineData(10, 110, 10, 10)]
    public void TryConvert_ReturnsFalseWhenSelectionDoesNotIntersectDisplay(
        double x,
        double y,
        double width,
        double height)
    {
        var success = Convert(
            new Rect(x, y, width, height),
            new Rect(0, 0, 100, 100),
            1,
            1,
            0,
            0,
            100,
            100,
            out var result);

        Assert.False(success);
        Assert.False(result.IsValid);
    }

    [Fact]
    public void TryConvert_ReturnsFalseWhenPanMovesSelectionOutsideBitmap()
    {
        var success = Convert(
            new Rect(0, 0, 10, 10),
            new Rect(0, 0, 100, 100),
            1,
            1,
            20,
            20,
            100,
            100,
            out var result);

        Assert.False(success);
        Assert.False(result.IsValid);
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    [InlineData(1, -1)]
    public void TryConvert_RejectsNonPositiveScale(double scaleX, double scaleY)
    {
        Assert.False(Convert(
            new Rect(0, 0, 10, 10),
            new Rect(0, 0, 100, 100),
            scaleX,
            scaleY,
            0,
            0,
            100,
            100,
            out _));
    }

    [Theory]
    [MemberData(nameof(NonFiniteTransforms))]
    public void TryConvert_RejectsNonFiniteTransformValues(double scaleX, double scaleY, double panX, double panY)
    {
        Assert.False(Convert(
            new Rect(0, 0, 10, 10),
            new Rect(0, 0, 100, 100),
            scaleX,
            scaleY,
            panX,
            panY,
            100,
            100,
            out _));
    }

    [Theory]
    [InlineData(0, 100)]
    [InlineData(100, 0)]
    [InlineData(-1, 100)]
    [InlineData(100, -1)]
    public void TryConvert_RejectsInvalidBitmapDimensions(int pixelWidth, int pixelHeight)
    {
        Assert.False(Convert(
            new Rect(0, 0, 10, 10),
            new Rect(0, 0, 100, 100),
            1,
            1,
            0,
            0,
            pixelWidth,
            pixelHeight,
            out _));
    }

    [Fact]
    public void TryConvert_RejectsEmptyAndZeroAreaRectangles()
    {
        Assert.False(Convert(Rect.Empty, new Rect(0, 0, 100, 100), 1, 1, 0, 0, 100, 100, out _));
        Assert.False(Convert(new Rect(10, 10, 0, 10), new Rect(0, 0, 100, 100), 1, 1, 0, 0, 100, 100, out _));
        Assert.False(Convert(new Rect(10, 10, 10, 0), new Rect(0, 0, 100, 100), 1, 1, 0, 0, 100, 100, out _));
        Assert.False(Convert(new Rect(0, 0, 10, 10), Rect.Empty, 1, 1, 0, 0, 100, 100, out _));
    }

    public static IEnumerable<object[]> NonFiniteTransforms()
    {
        yield return new object[] { double.NaN, 1d, 0d, 0d };
        yield return new object[] { double.PositiveInfinity, 1d, 0d, 0d };
        yield return new object[] { 1d, double.NegativeInfinity, 0d, 0d };
        yield return new object[] { 1d, 1d, double.NaN, 0d };
        yield return new object[] { 1d, 1d, 0d, double.PositiveInfinity };
    }

    private static bool Convert(
        Rect windowRect,
        Rect displayBounds,
        double scaleX,
        double scaleY,
        double panX,
        double panY,
        int pixelWidth,
        int pixelHeight,
        out SelectionRectangle result)
    {
        return CropGeometry.TryConvertWindowRectToImageCoordinates(
            windowRect,
            displayBounds,
            scaleX,
            scaleY,
            panX,
            panY,
            pixelWidth,
            pixelHeight,
            out result);
    }
}
