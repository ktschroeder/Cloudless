using System.Windows.Input;
using Xunit;

namespace Cloudless.Tests;

public sealed class ArrangementModeTests
{
    [Theory]
    [InlineData(ModifierKeys.None, 1.05)]
    [InlineData(ModifierKeys.Alt, 1.01)]
    [InlineData(ModifierKeys.Shift, 1.10)]
    [InlineData(ModifierKeys.Alt | ModifierKeys.Shift, 1.05)]
    [InlineData(ModifierKeys.Control | ModifierKeys.Alt, 1.01)]
    public void ScalePerNotchUsesFineAndCoarseModifiers(ModifierKeys modifiers, double expected)
    {
        Assert.Equal(expected, MainWindow.GetArrangementScalePerNotch(modifiers));
    }
}
