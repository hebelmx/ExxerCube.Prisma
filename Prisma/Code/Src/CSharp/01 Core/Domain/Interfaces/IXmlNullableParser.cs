namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines a generic XML parser that handles nullable values and provides structured parsing for XML documents.
/// </summary>
/// <typeparam name="T">The type of entity to parse from XML.</typeparam>
public interface IXmlNullableParser<T>
{
    /// <summary>
    /// Parses an XML document and extracts a strongly-typed entity, handling nullable values appropriately.
    /// </summary>
    /// <param name="xmlContent">The XML content as a byte array.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the parsed entity or an error.</returns>
    Task<Result<T>> ParseAsync(
        byte[] xmlContent,
        CancellationToken cancellationToken = default);
}

