namespace ExxerCube.Prisma.Infrastructure.Calendar;

/// <summary>
/// Holiday-aware business-day calculator for Mexico, backed by the <c>PublicHoliday</c> package's
/// <see cref="MexicoPublicHoliday"/> which tracks Mexican federal holidays as defined by the
/// Ley Federal del Trabajo.
/// </summary>
/// <remarks>
/// <para>
/// Both <see cref="AddBusinessDays"/> and <see cref="CountBusinessDays"/> delegate to the
/// <c>PublicHoliday</c> package methods, which skip weekends (Saturday/Sunday) AND recognised
/// Mexican federal public holidays (1 Jan, 5 Feb, 21 Mar, 1 May, 16 Sep, 18 Nov, 25 Dec, etc.).
/// </para>
/// <para>
/// <see cref="CountBusinessDays"/> uses an <em>exclusive end</em> convention consistent with the
/// existing <c>SLAEnforcerService.CalculateBusinessDays(start, end)</c> semantics (exclusive-end).
/// Note: <c>MexicoPublicHoliday.BusinessDaysBetween</c> counts both endpoints <em>inclusively</em>,
/// so the wrapper subtracts 1 when the end date itself is a working day.
/// </para>
/// <para>
/// The <see cref="MexicoPublicHoliday"/> instance is thread-safe and stateless beyond the year it
/// is queried for; a single shared instance per calculator is sufficient.
/// </para>
/// </remarks>
public sealed class MexicoBusinessDayCalculator : IBusinessDayCalculator
{
    private readonly MexicoPublicHoliday _calendar;

    /// <summary>
    /// Initializes a new instance of <see cref="MexicoBusinessDayCalculator"/> using the default
    /// <see cref="MexicoPublicHoliday"/> configuration (federal holidays, no state-specific variants).
    /// </summary>
    public MexicoBusinessDayCalculator()
    {
        _calendar = new MexicoPublicHoliday();
    }

    /// <inheritdoc />
    /// <remarks>
    /// Delegates to <c>MexicoPublicHoliday.BusinessDaysAdd(startDate, businessDays)</c>, which skips
    /// weekends and Mexican federal holidays. Passing zero returns <paramref name="startDate"/> unchanged.
    /// </remarks>
    public DateTime AddBusinessDays(DateTime startDate, int businessDays)
    {
        if (businessDays == 0)
        {
            return startDate;
        }

        return _calendar.BusinessDaysAdd(startDate, businessDays);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// The <see cref="IBusinessDayCalculator.CountBusinessDays"/> contract uses an <em>exclusive end</em>
    /// convention: <paramref name="endDate"/> itself is never counted. This matches the behaviour of
    /// <c>SLAEnforcerService.CalculateBusinessDays(start, end)</c> whose loop condition is
    /// <c>while (currentDate &lt; endDate)</c>.
    /// </para>
    /// <para>
    /// The underlying <c>MexicoPublicHoliday.BusinessDaysBetween</c> method counts both endpoints
    /// inclusively. This wrapper corrects for that by subtracting 1 when <paramref name="endDate"/>
    /// itself is a working day, so the two implementations remain semantically equivalent.
    /// </para>
    /// </remarks>
    public int CountBusinessDays(DateTime startDate, DateTime endDate)
    {
        if (endDate <= startDate)
        {
            return 0;
        }

        // BusinessDaysBetween counts both endpoints inclusively.
        // Our contract is exclusive-end, so subtract 1 if the end date is itself a working day.
        var inclusive = _calendar.BusinessDaysBetween(startDate, endDate);
        return _calendar.IsWorkingDay(endDate) ? inclusive - 1 : inclusive;
    }
}
