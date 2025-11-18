using System.Collections.Generic;
using System.Threading.Tasks;
using IndQuestResults;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Sources;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines the generic field extractor service for extracting structured data from typed document sources.
/// This interface extends the field extraction capability to support multiple source types (XML, DOCX, PDF)
/// while maintaining backward compatibility with the existing non-generic <see cref="IFieldExtractor"/> interface.
/// </summary>
/// <typeparam name="T">The document source type (e.g., <see cref="DocxSource"/>, <see cref="PdfSource"/>, <see cref="XmlSource"/>).</typeparam>
public interface IFieldExtractor<T>
{
    /// <summary>
    /// Extracts structured fields from the specified document source.
    /// </summary>
    /// <param name="source">The document source to process.</param>
    /// <param name="fieldDefinitions">The field definitions specifying which fields to extract.</param>
    /// <returns>A result containing the extracted fields or an error.</returns>
    Task<Result<ExtractedFields>> ExtractFieldsAsync(T source, FieldDefinition[] fieldDefinitions);

    /// <summary>
    /// Extracts a specific field by name from the specified document source.
    /// </summary>
    /// <param name="source">The document source to process.</param>
    /// <param name="fieldName">The name of the field to extract.</param>
    /// <returns>A result containing the field value or an error.</returns>
    Task<Result<FieldValue>> ExtractFieldAsync(T source, string fieldName);
}

