using System.Collections.Generic;
using ExxerCube.Prisma.Domain.Enum;

namespace ExxerCube.Prisma.Domain.Services.Manifest;

/// <summary>
/// The parsed expected-case Listado (manifest) loaded from the demo-controllable JSON file.
/// Lists every oficio the operator expects SIARA to present in a given window, with the companion
/// file formats expected for each. Loaded by <c>IExpectedManifestProvider</c> at the start of
/// each reconciliation cycle.
/// </summary>
/// <remarks>
/// Format on disk (JSON):
/// <code>
/// {
///   "oficios": [
///     { "caseId": "222AAA-44444444442025", "expectedFormats": ["Pdf", "Xml", "Docx"] }
///   ]
/// }
/// </code>
/// </remarks>
public sealed record ExpectedManifest
{
    /// <summary>Gets the list of expected oficios (one entry per case the operator expects).</summary>
    public IReadOnlyList<ExpectedOficio> Oficios { get; init; } = [];

    /// <summary>An empty manifest (no oficios expected — yields all-extra reconciliation).</summary>
    public static readonly ExpectedManifest Empty = new();
}

/// <summary>
/// One entry in the expected manifest: a case id plus the set of companion file formats the
/// operator expects to be present for that case.
/// </summary>
/// <param name="CaseId">
/// The SIARA case identifier (URL path segment / base name), e.g. <c>222AAA-44444444442025</c>.
/// </param>
/// <param name="ExpectedFormats">
/// The companion file formats expected for this case. Matching is by format name (case-insensitive),
/// so "Pdf"/"pdf"/"PDF" all match <see cref="FileFormat.Pdf"/>.
/// </param>
public sealed record ExpectedOficio(
    string CaseId,
    IReadOnlyList<FileFormat> ExpectedFormats);
