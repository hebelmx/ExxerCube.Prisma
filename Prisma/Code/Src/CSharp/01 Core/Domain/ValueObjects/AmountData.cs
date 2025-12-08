namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents a monetary amount with currency.
/// </summary>
public class AmountData
{
    /// <summary>
    /// Gets or sets the currency code (e.g., "MXN", "USD").
    /// </summary>
    public string Currency { get; set; } = "MXN";

    /// <summary>
    /// Gets or sets the monetary value.
    /// </summary>
    public decimal Value { get; set; }

    /// <summary>
    /// Gets or sets the original text from which the amount was extracted.
    /// </summary>
    public string OriginalText { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="AmountData"/> class.
    /// </summary>
    public AmountData()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AmountData"/> class with specified values.
    /// </summary>
    /// <param name="currency">The currency code.</param>
    /// <param name="value">The monetary value.</param>
    /// <param name="originalText">The original text.</param>
    public AmountData(string currency, decimal value, string originalText)
    {
        Currency = currency;
        Value = value;
        OriginalText = originalText;
    }
}
