using Xunit;

namespace Cloudless.Tests;

public sealed class MagnifierModeTests
{
    [Theory]
    [InlineData(2.0, 120, false, false, 2.2)]
    [InlineData(2.0, -120, false, false, 2.0 / 1.1)]
    [InlineData(2.0, 120, true, false, 2.04)]
    [InlineData(8.0, 120, false, false, 8.0)]
    [InlineData(1.0, -120, false, false, 1.0)]
    public void WheelAdjustsMagnificationWithBounds(double zoom, int delta, bool fine, bool coarse, double expected)
    {
        double adjustedZoom = MagnifierOverlayWindow.CalculateZoomAfterWheel(zoom, delta, fine, coarse);

        Assert.Equal(expected, adjustedZoom, 8);
    }

    [Theory]
    [InlineData(280, 120, false, false, 308)]
    [InlineData(280, 120, true, false, 286)]
    [InlineData(120, -120, false, false, 120)]
    [InlineData(600, 120, false, false, 660)]
    [InlineData(1800, 120, false, false, 1800)]
    public void WheelAdjustsLensDiameterWithBounds(int diameter, int delta, bool fine, bool coarse, int expected)
    {
        int adjustedDiameter = MagnifierOverlayWindow.CalculateDiameterAfterWheel(diameter, delta, fine, coarse);

        Assert.Equal(expected, adjustedDiameter);
    }

    [Theory]
    [InlineData(0, 200, -100)]
    [InlineData(1920, 200, 1820)]
    public void SourceRectangleRemainsCenteredPastDisplayEdges(int cursorCoordinate, int sourceLength, int expectedStart)
    {
        int sourceStart = MagnifierOverlayWindow.GetSourceStart(cursorCoordinate, sourceLength);

        Assert.Equal(expectedStart, sourceStart);
    }
}
