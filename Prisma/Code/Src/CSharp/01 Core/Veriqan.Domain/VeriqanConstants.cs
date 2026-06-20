using System.Runtime.InteropServices;

namespace ExxerCube.Prisma.Veriqan.Domain;

/// <summary>
/// Shared constants for the Veriqan domain kernel.
/// </summary>
public static class VeriqanConstants
{
    // ASSUMPTION: VEC statements are issued by Mexican financial institutions
    // (CNBV-regulated entities) and their printed calendar dates follow the
    // America/Mexico_City local time.  Converting a reference instant (e.g. the
    // processing clock) to a local date for year-repair must therefore use this
    // timezone so that a cut-date that falls on the local evening of Dec 31 is
    // not misclassified as Jan 1 UTC, which would produce an off-by-one year in
    // period/movement-date repair.  Windows uses "Central Standard Time" as the
    // IANA equivalent; all other platforms use the IANA id directly.
    /// <summary>
    /// The canonical <see cref="TimeZoneInfo"/> for all period-date arithmetic in
    /// Veriqan extractors: <c>America/Mexico_City</c> (UTC-6 / UTC-5 during DST).
    /// </summary>
    public static readonly TimeZoneInfo MexicoCityTimezone = TimeZoneInfo.FindSystemTimeZoneById(
        RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "Central Standard Time"
            : "America/Mexico_City");
}
