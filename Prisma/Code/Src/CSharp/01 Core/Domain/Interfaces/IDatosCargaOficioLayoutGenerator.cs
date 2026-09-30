namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Generates the "Datos Carga de Oficio" Excel workbook (24 columns) from a
/// <see cref="UnifiedMetadataRecord"/> using a template-driven layout.
/// </summary>
/// <remarks>
/// Column layout is defined by the built-in DatosCargaOficioTemplate.Default and can be
/// overridden via <see cref="ITemplateRepository"/>
/// (type key <c>"DatosCargaOficio"</c>). Fixed values are carried as
/// <see cref="FieldMapping.DefaultValue"/> entries in the
/// template so they are config-editable without code changes.
/// </remarks>
public interface IDatosCargaOficioLayoutGenerator
{
    /// <summary>
    /// Generates the xlsx workbook and writes it to <paramref name="outputStream"/>.
    /// </summary>
    /// <param name="metadata">The unified metadata record (must include a non-null Expediente).</param>
    /// <param name="outputStream">A writable stream that receives the xlsx bytes.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    /// <c>Result.Success()</c> on success; <c>Result.WithFailure(message)</c> with a
    /// descriptive message when validation fails; <c>ResultExtensions.Cancelled()</c> when
    /// cancelled.
    /// </returns>
    Task<Result> GenerateAsync(
        UnifiedMetadataRecord metadata,
        Stream outputStream,
        CancellationToken cancellationToken = default);
}
