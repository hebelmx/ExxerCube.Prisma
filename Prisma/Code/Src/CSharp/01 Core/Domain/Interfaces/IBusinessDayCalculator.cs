namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines a calendar service that calculates business days, accounting for weekends and public holidays.
/// </summary>
/// <remarks>
/// <para>
/// Implementations are expected to be holiday-aware for the relevant jurisdiction (e.g. Mexican federal
/// holidays for the <c>MexicoBusinessDayCalculator</c> adapter). The interface is intentionally pure and
/// synchronous: business-day arithmetic is deterministic calendar math with no I/O dependency.
/// </para>
/// <para>
/// <see cref="CountBusinessDays"/> uses an <em>exclusive end</em> convention — the end date itself is
/// not counted. This matches the semantics of <c>SLAEnforcerService.CalculateBusinessDays(start, end)</c>
/// and the <c>PublicHoliday</c> package's <c>BusinessDaysBetween</c> method.
/// </para>
/// </remarks>
public interface IBusinessDayCalculator
{
    /// <summary>
    /// Returns the date that is <paramref name="businessDays"/> business days after <paramref name="startDate"/>,
    /// skipping weekends and public holidays.
    /// </summary>
    /// <param name="startDate">The starting date (not counted as a business day itself).</param>
    /// <param name="businessDays">The number of business days to add. Must be zero or positive.</param>
    /// <returns>
    /// The <see cref="DateTime"/> that falls exactly <paramref name="businessDays"/> business days after
    /// <paramref name="startDate"/>. When <paramref name="businessDays"/> is zero the method returns
    /// <paramref name="startDate"/> unchanged.
    /// </returns>
    DateTime AddBusinessDays(DateTime startDate, int businessDays);

    /// <summary>
    /// Counts the number of business days from <paramref name="startDate"/> (inclusive) to
    /// <paramref name="endDate"/> (exclusive), skipping weekends and public holidays.
    /// </summary>
    /// <param name="startDate">The start date (inclusive). The start date itself is counted if it is a business day.</param>
    /// <param name="endDate">The end date (exclusive). The end date itself is never counted.</param>
    /// <returns>
    /// The number of business days in <c>[startDate, endDate)</c>. Returns zero when
    /// <paramref name="endDate"/> is less than or equal to <paramref name="startDate"/>.
    /// </returns>
    int CountBusinessDays(DateTime startDate, DateTime endDate);
}
