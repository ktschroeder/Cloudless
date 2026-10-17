using System.Windows;
using Xunit;

namespace Cloudless.Tests;

public sealed class MouseModeRadialWindowTests
{
    [Theory]
    [InlineData(0, -80, "Normal")]
    [InlineData(80, 0, "Control")]
    [InlineData(0, 80, "Selection")]
    [InlineData(-80, 0, "PageControl")]
    public void DragDirectionSelectsTheCorrespondingMouseMode(double offsetX, double offsetY, string expected)
    {
        Point anchor = new(200, 200);
        Point releasePosition = new(anchor.X + offsetX, anchor.Y + offsetY);

        CloudlessMouseMode? selected = MouseModeRadialWindow.GetModeAtScreenPosition(anchor, releasePosition);

        Assert.Equal(Enum.Parse<CloudlessMouseMode>(expected), selected);
    }

    [Fact]
    public void ReleasingInsideTheRadialDeadZoneCancelsSelection()
    {
        Point anchor = new(200, 200);

        CloudlessMouseMode? selected = MouseModeRadialWindow.GetModeAtScreenPosition(anchor, new Point(210, 210));

        Assert.Null(selected);
    }
}
