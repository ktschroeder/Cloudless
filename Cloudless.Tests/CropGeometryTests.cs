using System.Windows;
using Cloudless.Models;
using Xunit;

namespace Cloudless.Tests;

public class CropGeometryTests
{
    [Fact]
    public void IsValid_IsTrueWhenBothCropDimensionsArePositive()
    {
        var selection = new SelectionRectangle
        {
            CropRenderWidth = 120,
            CropRenderHeight = 80
        };

        Assert.True(selection.IsValid);
    }

    [Theory]
    [InlineData(0, 80)]
    [InlineData(120, 0)]
    [InlineData(-1, 80)]
    [InlineData(120, -1)]
    public void IsValid_IsFalseWhenEitherCropDimensionIsNotPositive(double width, double height)
    {
        var selection = new SelectionRectangle
        {
            CropRenderWidth = width,
            CropRenderHeight = height
        };

        Assert.False(selection.IsValid);
    }

    [Fact]
    public void Coordinates_PreserveAssignedRectangleValues()
    {
        var windowRect = new Rect(10, 20, 100, 60);
        var imageRect = new Rect(30, 40, 200, 120);
        var selection = new SelectionRectangle
        {
            WindowCoordinates = windowRect,
            ImagePixelCoordinates = imageRect
        };

        Assert.Equal(windowRect, selection.WindowCoordinates);
        Assert.Equal(imageRect, selection.ImagePixelCoordinates);
    }
}
