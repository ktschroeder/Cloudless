using System.Globalization;
using Xunit;

namespace Cloudless.Tests;

public class VideoDelayParsingTests
{
    [Theory]
    [InlineData("1.25", 1_250_000)]
    [InlineData("120ms", 120_000)]
    [InlineData(" -0.25 S ", -250_000)]
    [InlineData("0.0000005s", 1)]
    [InlineData(".5ms", 500)]
    [InlineData("0", 0)]
    [InlineData("0ms", 0)]
    [InlineData("-0.0000005s", -1)]
    [InlineData("0.0000004s", 0)]
    [InlineData("1.9ms", 1_900)]
    [InlineData("1mS", 1_000)]
    [InlineData(" 2 s ", 2_000_000)]
    [InlineData("1e-6s", 1)]
    [InlineData("1e3ms", 1_000_000)]
    [InlineData("+2.5S", 2_500_000)]
    [InlineData("-.0005s", -500)]
    [InlineData("1.", 1_000_000)]
    [InlineData("9223372036854.775807s", long.MaxValue)]
    [InlineData("-9223372036854.775808s", long.MinValue)]
    [InlineData("9223372036854775.807ms", long.MaxValue)]
    public void TryParseVideoDelay_ParsesSecondsAndMilliseconds(string input, long expectedMicroseconds)
    {
        Assert.True(MainWindow.TryParseVideoDelay(input, out var delayMicroseconds));
        Assert.Equal(expectedMicroseconds, delayMicroseconds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("1,5s")]
    [InlineData("5min")]
    [InlineData("10000000000000s")]
    [InlineData("9223372036854.775808s")]
    [InlineData("-9223372036854.775809s")]
    [InlineData("9223372036854775.808ms")]
    [InlineData("79228162514264337593543950335")]
    public void TryParseVideoDelay_RejectsInvalidOrOutOfRangeValues(string input)
    {
        Assert.False(MainWindow.TryParseVideoDelay(input, out var delayMicroseconds));
        Assert.Equal(0, delayMicroseconds);
    }

    [Fact]
    public void FormatVideoDelay_UsesInvariantDecimalFormatting()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            Assert.Equal("+1.25 s (+1250 ms)", MainWindow.FormatVideoDelay(1_250_000));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void TryParseVideoDelay_UsesInvariantDecimalSeparator()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");

            Assert.True(MainWindow.TryParseVideoDelay("1.5s", out var delayMicroseconds));
            Assert.Equal(1_500_000, delayMicroseconds);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData(-250_000, "-0.25 s (-250 ms)")]
    [InlineData(0, "0 s (0 ms)")]
    [InlineData(1_000_000, "+1 s (+1000 ms)")]
    [InlineData(1_234_567, "+1.235 s (+1234.57 ms)")]
    [InlineData(-10_000, "-0.01 s (-10 ms)")]
    public void FormatVideoDelay_FormatsSignedValuesInSecondsAndMilliseconds(long delayMicroseconds, string expected)
    {
        Assert.Equal(expected, MainWindow.FormatVideoDelay(delayMicroseconds));
    }
}
