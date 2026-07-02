namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Domain port for LLM-backed expediente extraction.
/// Returns the full <see cref="Expediente"/> entity (including
/// <see cref="Expediente.SolicitudPartes"/>) so the party table (name/RFC/CURP/DOB/carácter)
/// survives the extraction pipeline without being flattened into <see cref="ExtractedFields"/>.
/// </summary>
/// <typeparam name="T">The document source type (e.g. <see cref="TxtSource"/>,
/// <c>ImageSource</c>).</typeparam>
/// <remarks>
/// Ships DARK — neither <c>LlmProviders:TextExtractorEnabled</c> nor
/// <c>LlmProviders:VisionExtractorEnabled</c> activate LLM tracks by default.
/// </remarks>
public interface ILlmExpedienteExtractor<T>
{
    /// <summary>
    /// Extracts a fully-populated <see cref="Expediente"/> (including
    /// <see cref="Expediente.SolicitudPartes"/>) from the given source using an LLM provider.
    /// </summary>
    /// <param name="source">The document source to process.</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    /// <returns>
    /// A result containing the populated <see cref="Expediente"/> or a failure description;
    /// never throws.
    /// </returns>
    Task<Result<Expediente>> ExtractExpedienteAsync(
        T source,
        CancellationToken cancellationToken = default);
}
