namespace ExxerCube.Prisma.Domain.Enums;

/// <summary>
/// Indicates where a field value was obtained.
/// </summary>
public enum FieldOrigin
{
    /// <summary>
    /// Source is unknown or not specified.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Value extracted directly from XML.
    /// </summary>
    Xml = 1,

    /// <summary>
    /// Value extracted from PDF via OCR.
    /// </summary>
    PdfOcr = 2,

    /// <summary>
    /// Value extracted from DOCX.
    /// </summary>
    Docx = 3,

    /// <summary>
    /// Value derived/inferred from other fields.
    /// </summary>
    Derived = 4,

    /// <summary>
    /// Value supplied manually by a reviewer.
    /// </summary>
    Manual = 5
}
