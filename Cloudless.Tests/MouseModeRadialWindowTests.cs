using System.Windows;
using System.Windows.Input;
using Xunit;

namespace Cloudless.Tests;

public sealed class MouseModeRadialWindowTests
{
    [Theory]
    [InlineData(0, -80, "Normal")]
    [InlineData(80, -80, "Control")]
    [InlineData(80, 40, "Selection")]
    [InlineData(0, 80, "Arrangement")]
    [InlineData(-80, 80, "PageControl")]
    [InlineData(-80, -80, "Annotation")]
    [InlineData(80, 0, "Spotlight")]
    public void DragDirectionSelectsTheCorrespondingMouseMode(double offsetX, double offsetY, string expected)
    {
        Point anchor = new(200, 200);
        Point releasePosition = new(anchor.X + offsetX, anchor.Y + offsetY);

        CloudlessMouseMode? selected = MouseModeRadialWindow.GetModeAtScreenPosition(anchor, releasePosition);

        Assert.Equal(Enum.Parse<CloudlessMouseMode>(expected), selected);
    }

    [Theory]
    [InlineData(-80, 0)]
    public void UnusedRadialSectorsDoNotSelectAMode(double offsetX, double offsetY)
    {
        Point anchor = new(200, 200);

        CloudlessMouseMode? selected = MouseModeRadialWindow.GetModeAtScreenPosition(
            anchor,
            new Point(anchor.X + offsetX, anchor.Y + offsetY));

        Assert.Null(selected);
    }

    [Theory]
    [InlineData(ModifierKeys.None)]
    [InlineData(ModifierKeys.Control)]
    public void ArrangementModeDoesNotPanImageOnLeftClick(ModifierKeys modifiers)
    {
        Assert.False(MainWindow.ShouldPanImageOnLeftMouseDown(CloudlessMouseMode.Arrangement, modifiers));
    }

    [Theory]
    [InlineData(ModifierKeys.None, false)]
    [InlineData(ModifierKeys.Control, true)]
    public void SpotlightModePansOnlyWhenControlIsHeld(ModifierKeys modifiers, bool expected)
    {
        Assert.Equal(expected, MainWindow.ShouldPanImageOnLeftMouseDown(CloudlessMouseMode.Spotlight, modifiers));
    }

    [Fact]
    public void ReleasingInsideTheRadialDeadZoneCancelsSelection()
    {
        Point anchor = new(200, 200);

        CloudlessMouseMode? selected = MouseModeRadialWindow.GetModeAtScreenPosition(anchor, new Point(210, 210));

        Assert.Null(selected);
    }
}
