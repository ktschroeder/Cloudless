using Xunit;

namespace Cloudless.Tests;

public sealed class SpotlightModeTests
{
    [Theory]
    [InlineData(300, 120, false, false, 330)]
    [InlineData(300, 120, true, false, 306)]
    [InlineData(300, 120, false, true, 375)]
    [InlineData(300, -120, false, false, 273)]
    [InlineData(10, -1200, false, false, 10)]
    [InlineData(4000, 1200, false, true, 4000)]
    [InlineData(300, 0, false, false, 300)]
    public void WheelAdjustsDiameterWithModifiersAndBounds(
        int currentDiameter,
        int delta,
        bool fine,
        bool coarse,
        int expectedDiameter)
    {
        int diameter = SpotlightOverlayWindow.CalculateDiameterAfterWheel(currentDiameter, delta, fine, coarse);

        Assert.Equal(expectedDiameter, diameter);
    }
}
