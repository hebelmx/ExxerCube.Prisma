namespace ExxerCube.Prisma.Veriqan.Domain.Extraction;

/// <summary>
/// Facts extracted from the CFDI fiscal block section of a VEC statement (Story 6.2 — CL-50..53).
/// </summary>
/// <remarks>
/// <para>
/// The fiscal block is identified by the presence of the CFDI legend
/// "REPRESENTACIÓN IMPRESA SIN VALIDEZ FISCAL" on any page of the statement.
/// When the block is absent (<see cref="BlockPresent"/> == <see langword="false"/>), all other
/// properties are <see langword="null"/> or <see langword="false"/>.
/// </para>
/// <para>
/// <b>Required vs. not-required gating (CL-50..53):</b>
/// The fiscal block is REQUIRED only when the statement carries reembolsos, commissions, or IVA
/// (<c>PeriodSummary.MontoComisiones &gt; 0</c> or <c>IvaInteresesYComisiones &gt; 0</c>),
/// OR when the block is already present.
/// When the block is not required AND not present, its absence must NOT produce a FAIL on
/// any of CL-50 through CL-53 — the rules return Pass (n/a) or InsufficientData instead.
/// </para>
/// </remarks>
/// <param name="BlockPresent">
/// <see langword="true"/> when the CFDI legend "REPRESENTACIÓN IMPRESA SIN VALIDEZ FISCAL"
/// was found on any page of the statement.
/// </param>
/// <param name="QrDecoded">
/// <see langword="true"/> when a QR code was found and decoded on the fiscal-block page.
/// <see langword="false"/> when the page was scanned but no decodable QR was found,
/// or when <see cref="BlockPresent"/> is <see langword="false"/>.
/// </param>
/// <param name="QrPayload">
/// The decoded QR code payload string, or <see langword="null"/> when <see cref="QrDecoded"/>
/// is <see langword="false"/>.
/// </param>
/// <param name="FiscalCode">
/// The fiscal code string extracted from the page text near the fiscal block
/// (e.g. the SAT verification UUID or folio fiscal), or <see langword="null"/> when not found.
/// </param>
/// <param name="IssuerRfc">
/// RFC of the issuer (emisor) extracted from the page text, or <see langword="null"/> when not found.
/// </param>
/// <param name="ReceiverRfc">
/// RFC of the receiver (receptor) extracted from the page text, or <see langword="null"/> when not found.
/// </param>
/// <param name="Locator">
/// Page-level locator for the fiscal block (page on which the CFDI legend was found).
/// <see cref="FieldLocator.NoPage()"/> when no block was found (sentinel — page 0).
/// </param>
public sealed record FiscalBlock(
    bool BlockPresent,
    bool QrDecoded,
    string? QrPayload,
    string? FiscalCode,
    string? IssuerRfc,
    string? ReceiverRfc,
    FieldLocator Locator)
{
    /// <summary>
    /// Returns a <see cref="FiscalBlock"/> representing the absence of any fiscal block in the document.
    /// Used when no CFDI legend page is found.
    /// </summary>
    /// <remarks>
    /// The <see cref="Locator"/> is <see cref="FieldLocator.NoPage()"/> (page 0) rather than a
    /// page-1 hint so that Pass (n/a) findings for CL-50..53 do not misleadingly cite "page 1"
    /// when no fiscal block exists.
    /// </remarks>
    public static FiscalBlock NotPresent() =>
        new(
            BlockPresent: false,
            QrDecoded: false,
            QrPayload: null,
            FiscalCode: null,
            IssuerRfc: null,
            ReceiverRfc: null,
            Locator: FieldLocator.NoPage());
}
