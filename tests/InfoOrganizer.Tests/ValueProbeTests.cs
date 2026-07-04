using InfoOrganizer.Domain;

namespace InfoOrganizer.Tests;

public class ValueProbeTests
{
    [Fact]
    public void InferType_detects_families()
    {
        Assert.Equal(RawCellType.Number, ValueProbe.InferType(new[] { "10", "20", "30" }));
        Assert.Equal(RawCellType.Currency, ValueProbe.InferType(new[] { "$10.00", "$20.50", "$5" }));
        Assert.Equal(RawCellType.Date, ValueProbe.InferType(new[] { "2024-01-01", "2024-02-03", "2024-03-09" }));
        Assert.Equal(RawCellType.Boolean, ValueProbe.InferType(new[] { "yes", "no", "sí" }));
        Assert.Equal(RawCellType.Text, ValueProbe.InferType(new[] { "apple", "banana", "pear" }));
    }

    [Theory]
    [InlineData("1.234,56", true, 1234.56)]
    [InlineData("1,234.56", false, 1234.56)]
    [InlineData("$1,200", false, 1200)]
    [InlineData("€ 99,90", true, 99.90)]
    public void TryParseDecimal_handles_locales_and_currency(string input, bool commaDecimal, double expected)
    {
        Assert.True(ValueProbe.TryParseDecimal(input, commaDecimal, out var value));
        Assert.Equal((decimal)expected, value);
    }

    [Fact]
    public void TryParseDate_uses_explicit_format_then_falls_back()
    {
        Assert.True(ValueProbe.TryParseDate("12/03/2024", "dd/MM/yyyy", out var d));
        Assert.Equal(new DateOnly(2024, 3, 12), d);

        Assert.True(ValueProbe.TryParseDate("2024-03-12", null, out var d2));
        Assert.Equal(new DateOnly(2024, 3, 12), d2);
    }

    [Theory]
    [InlineData("venta", MovementKind.Out)]
    [InlineData("compra", MovementKind.In)]
    [InlineData("Received", MovementKind.In)]
    [InlineData("Ajuste", MovementKind.Adjustment)]
    [InlineData("correccion", MovementKind.Adjustment)]
    [InlineData("merma", MovementKind.Adjustment)]
    [InlineData("+", MovementKind.In)]
    [InlineData("-", MovementKind.Out)]
    public void ClassifyDirection_reads_multilingual_keywords(string input, MovementKind expected)
    {
        Assert.Equal(expected, ValueProbe.ClassifyDirection(input));
    }

    [Fact]
    public void ClassifyDirection_returns_null_when_unclear()
    {
        Assert.Null(ValueProbe.ClassifyDirection("misc"));
    }

    [Fact]
    public void DetectCurrency_treats_plain_dollar_as_ambiguous()
    {
        Assert.Null(ValueProbe.DetectCurrency("$10.00"));
        Assert.Equal("USD", ValueProbe.DetectCurrency("USD 10.00"));
        Assert.Equal("MXN", ValueProbe.DetectCurrency("MXN 10.00"));
    }
}
