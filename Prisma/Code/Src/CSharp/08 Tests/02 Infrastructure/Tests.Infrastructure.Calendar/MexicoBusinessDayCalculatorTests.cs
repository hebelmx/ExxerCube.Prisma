using ExxerCube.Prisma.Infrastructure.Calendar;

namespace ExxerCube.Prisma.Tests.Infrastructure.Calendar;

/// <summary>
/// Tests for <see cref="MexicoBusinessDayCalculator"/> — verifies holiday-awareness, weekend skipping,
/// exclusive-end count semantics, and that known Mexican federal holidays are recognised.
/// </summary>
/// <remarks>
/// <para>
/// <c>MexicoPublicHoliday.BusinessDaysBetween</c> counts business days <em>inclusive</em> of both
/// endpoints. The <see cref="MexicoBusinessDayCalculator"/> wrapper adjusts this to the contract's
/// <em>exclusive-end</em> semantics by subtracting 1 when the end date is a working day.
/// </para>
/// <para>
/// Mexican federal holidays exercised here (fixed-date subset used because they are invariant):
/// <list type="bullet">
///   <item>16 September — Día de la Independencia (Independence Day)</item>
///   <item>1 May — Día del Trabajo (Labour Day)</item>
///   <item>25 December — Navidad (Christmas Day)</item>
///   <item>1 January — Año Nuevo (New Year)</item>
/// </list>
/// </para>
/// </remarks>
public sealed class MexicoBusinessDayCalculatorTests
{
    private readonly MexicoBusinessDayCalculator _sut = new();

    // ──────────────────────────────────────────────────────────────────────────────────
    // AddBusinessDays — basic behaviour
    // ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void AddBusinessDays_ZeroDays_ReturnsSameDate()
    {
        var date = new DateTime(2025, 6, 9); // Monday
        _sut.AddBusinessDays(date, 0).ShouldBe(date);
    }

    [Fact]
    public void AddBusinessDays_PlainWeekdaySpan_SkipsWeekend()
    {
        // Friday 2025-06-06: adding 1 business day skips Sat/Sun → Monday 2025-06-09
        var friday = new DateTime(2025, 6, 6);
        _sut.AddBusinessDays(friday, 1).ShouldBe(new DateTime(2025, 6, 9));
    }

    [Fact]
    public void AddBusinessDays_FiveDaysStartMonday_LandsOnNextMonday()
    {
        // Mon 2025-06-09 + 5 business days (no holiday week) → Mon 2025-06-16
        var monday = new DateTime(2025, 6, 9);
        _sut.AddBusinessDays(monday, 5).ShouldBe(new DateTime(2025, 6, 16));
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    // AddBusinessDays — holiday-awareness
    // ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// 16 September (Día de la Independencia) falls on a Tuesday in 2025.
    /// Starting Monday 15 Sep, adding 1 business day without holiday awareness gives Tuesday 16 Sep.
    /// With holiday awareness the calculator SKIPS Tuesday and lands on Wednesday 17 Sep instead.
    /// </summary>
    [Fact]
    public void AddBusinessDays_AcrossIndependenceDay2025_ShiftsOneExtraDay()
    {
        // Mon 15 Sep 2025 — day before Día de la Independencia
        var startDate = new DateTime(2025, 9, 15);

        var result = _sut.AddBusinessDays(startDate, 1);

        // If holiday-UNAWARE: Tuesday 16 Sep
        // If holiday-AWARE:   Wednesday 17 Sep (Tuesday is skipped as federal holiday)
        result.ShouldBe(new DateTime(2025, 9, 17),
            "Día de la Independencia (16 Sep 2025, Tuesday) must be skipped as a Mexican federal holiday");
    }

    /// <summary>
    /// Adds 2 business days starting Friday 14 Sep 2025.
    /// Weekend-only: Fri→Mon 22 Sep (skips Sat 20 + Sun 21... wait: Fri + 1 = Mon 22? No:
    /// Fri 14 → 1st = Mon 17 (skips Sat 20... actually Fri 14 + 1 skip Sat+Sun = Mon 17 if no holiday).
    /// With holiday on Tue 16: Fri 14 + 1 = Wed 17 + 1 = Thu 18.
    /// Actually weekend-only from Fri 14: +1 = Mon 17, +2 = Tue 16 — wait Tue 16 is holiday so skip → Wed 17.
    /// Let's be precise: start Fri 12 Sep (two weeks earlier to avoid crossing holiday on first day).
    /// Actually let's use a clear span of 5 days crossing the holiday.
    /// </summary>
    [Fact]
    public void AddBusinessDays_FiveDaysCrossing16Sep2025_ShiftsOneExtraDay()
    {
        // Start: Mon 2025-09-08 (no holiday this week).
        // Weekend-only (+5): Mon→Mon 15 Sep would be 5 days (Mon 8→Tue 9→Wed 10→Thu 11→Fri 12→ = 5 days → Fri 12 Sep).
        // Actually Mon+5 business days weekend-only = Mon 15 Sep (5 full days Mon-Fri).
        // Wait: Mon 8 + 5bd = Mon 8 → Tue 9(1) → Wed 10(2) → Thu 11(3) → Fri 12(4) → Mon 15(5) = Mon 15 Sep
        // Holiday-aware: same span, Mon 15 Sep is NOT a holiday. So +5 still = Mon 15.
        //
        // Better: start Mon 2025-09-15 (Mon before Día de la Independencia) and add 3 bd.
        // Weekend-only: Mon 15 +3 = Tue 16(1) Wed 17(2) Thu 18(3) = Thu 18
        // Holiday-aware: Tue 16 is skipped → Wed 17(1) Thu 18(2) Fri 19(3) = Fri 19
        var startDate = new DateTime(2025, 9, 15); // Monday
        var result = _sut.AddBusinessDays(startDate, 3);

        // Weekend-only result would be: Thu 18 Sep 2025
        // Holiday-aware result must be: Fri 19 Sep 2025
        result.ShouldBe(new DateTime(2025, 9, 19),
            "3 business days from Mon 15 Sep 2025 must skip Día de la Independencia (Tue 16 Sep) → Fri 19 Sep");
    }

    /// <summary>
    /// 1 May (Día del Trabajo / Labour Day) falls on a Thursday in 2025.
    /// Adding 1 business day from Wednesday 30 April 2025 must skip Thursday and return Friday 2 May.
    /// </summary>
    [Fact]
    public void AddBusinessDays_AcrossLabourDay2025_ShiftsOneExtraDay()
    {
        var startDate = new DateTime(2025, 4, 30); // Wednesday
        var result = _sut.AddBusinessDays(startDate, 1);

        // Weekend-only: Thu 1 May
        // Holiday-aware: Fri 2 May (Thu 1 May is Día del Trabajo)
        result.ShouldBe(new DateTime(2025, 5, 2),
            "Día del Trabajo (1 May 2025, Thursday) must be skipped as a Mexican federal holiday");
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    // CountBusinessDays — basic behaviour
    // ──────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void CountBusinessDays_EndBeforeStart_ReturnsZero()
    {
        var start = new DateTime(2025, 6, 10);
        var end = new DateTime(2025, 6, 9);
        _sut.CountBusinessDays(start, end).ShouldBe(0);
    }

    [Fact]
    public void CountBusinessDays_SameDateStartAndEnd_ReturnsZero()
    {
        var date = new DateTime(2025, 6, 9); // Monday
        _sut.CountBusinessDays(date, date).ShouldBe(0);
    }

    /// <summary>
    /// Exclusive-end pin: Mon 9 Jun to Fri 13 Jun (exclusive) = 4 business days (Mon-Thu inclusive).
    /// This documents the exclusive-end contract.
    /// </summary>
    [Fact]
    public void CountBusinessDays_ExclusiveEnd_FridayNotCounted()
    {
        var start = new DateTime(2025, 6, 9);  // Monday
        var end = new DateTime(2025, 6, 13);   // Friday — exclusive, so Thu is last counted day
        // Mon(1) Tue(2) Wed(3) Thu(4) — Fri is excluded
        _sut.CountBusinessDays(start, end).ShouldBe(4,
            "end date is exclusive — Friday 13 Jun must not be counted");
    }

    [Fact]
    public void CountBusinessDays_FullWeek_Monday_to_NextMonday_ExclusiveEnd_Returns5()
    {
        // Mon 2025-06-09 to Mon 2025-06-16 (exclusive) = Mon Tue Wed Thu Fri = 5 days
        var start = new DateTime(2025, 6, 9);
        var end = new DateTime(2025, 6, 16);
        _sut.CountBusinessDays(start, end).ShouldBe(5);
    }

    [Fact]
    public void CountBusinessDays_SpansWeekend_WeekendNotCounted()
    {
        // Fri 2025-06-06 to Tue 2025-06-10 (exclusive) = Fri(1) Mon(2) = 2 business days (Sat/Sun skipped)
        var start = new DateTime(2025, 6, 6);  // Friday
        var end = new DateTime(2025, 6, 10);   // Tuesday (exclusive)
        _sut.CountBusinessDays(start, end).ShouldBe(2);
    }

    // ──────────────────────────────────────────────────────────────────────────────────
    // CountBusinessDays — holiday-awareness
    // ──────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Mon 15 Sep 2025 to Thu 18 Sep 2025 (exclusive).
    /// Weekend-only: Mon(1) Tue(2) Wed(3) = 3 business days.
    /// Holiday-aware: Tue 16 Sep is skipped → Mon(1) Wed(2) = 2 business days.
    /// </summary>
    [Fact]
    public void CountBusinessDays_SpansIndependenceDay2025_HolidayExcluded()
    {
        var start = new DateTime(2025, 9, 15); // Monday
        var end = new DateTime(2025, 9, 18);   // Thursday (exclusive)

        _sut.CountBusinessDays(start, end).ShouldBe(2,
            "Día de la Independencia (Tue 16 Sep 2025) must not be counted as a business day");
    }

    /// <summary>
    /// Wed 30 Apr 2025 to Fri 2 May 2025 (exclusive).
    /// Weekend-only: Wed(1) Thu(2) = 2 business days.
    /// Holiday-aware: Thu 1 May is Día del Trabajo → Wed(1) = 1 business day.
    /// </summary>
    [Fact]
    public void CountBusinessDays_SpansLabourDay2025_HolidayExcluded()
    {
        var start = new DateTime(2025, 4, 30); // Wednesday
        var end = new DateTime(2025, 5, 2);    // Friday (exclusive)

        _sut.CountBusinessDays(start, end).ShouldBe(1,
            "Día del Trabajo (1 May 2025, Thursday) must not be counted as a business day");
    }

    /// <summary>
    /// Roundtrip consistency: counting business days in [start, AddBusinessDays(start, n)] (exclusive)
    /// must recover n for a standard weekday-only span (no holiday in range).
    /// </summary>
    [Fact]
    public void CountBusinessDays_RoundtripWithAdd_ReturnsOriginalDays()
    {
        var start = new DateTime(2025, 3, 3); // Monday, no Mexican holiday nearby
        const int days = 10;
        var end = _sut.AddBusinessDays(start, days);
        _sut.CountBusinessDays(start, end).ShouldBe(days,
            "CountBusinessDays(start, AddBusinessDays(start, n)) must return n for a holiday-free span");
    }
}
