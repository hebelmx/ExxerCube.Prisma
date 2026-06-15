# Item G (#13) — Holiday-aware Mexican business-day calendar — Design

**Owner decision:** calendar **service** via the `PublicHoliday` NuGet package (`<PackageReference Include="PublicHoliday" Version="3.13.0" />`),
which ships `MexicoPublicHoliday`. (Authorized new dependency — owner specified it explicitly.)

## Package facts (researched)
`PublicHoliday` v3.13.0 (May 2026, supports .NET 10). `MexicoPublicHoliday : IPublicHoliday` exposes:
`IsPublicHoliday(DateTime)`, `IsWorkingDay(DateTime)`, `NextWorkingDay(DateTime)`, `BusinessDaysAdd(DateTime,int)`,
`BusinessDaysBetween(DateTime,DateTime)`, `PublicHolidays(int year)`. `BusinessDaysAdd` skips weekends AND Mexican
federal holidays — exactly what we need.

## The two weekend-only sites being replaced
- `Infrastructure.Classification/FusionExpedienteService.cs:2778` `CalculateBusinessDays(start, n)` — ADDS n
  business days (used for `FechaEstimadaConclusion = FechaRecepcion + DiasPlazo`, called at :120). Weekend-only.
- `Infrastructure.Database/SLAEnforcerService.cs:453` `AddBusinessDays(start, n)` — ADDS n (deadline, :67); and
  `:477` `CalculateBusinessDays(start, end)` — COUNTS business days between (exclusive end). Both weekend-only.

## Components
1. **`IBusinessDayCalculator`** (Domain.Interfaces):
   ```csharp
   DateTime AddBusinessDays(DateTime startDate, int businessDays);
   int CountBusinessDays(DateTime startDate, DateTime endDate); // exclusive end, to match current semantics
   ```
   (Pure/sync; no async/ct needed — these are calendar math.)
2. **`MexicoBusinessDayCalculator : IBusinessDayCalculator`** (Infrastructure) — wraps a `MexicoPublicHoliday`
   instance: `AddBusinessDays` → `BusinessDaysAdd`; `CountBusinessDays` → `BusinessDaysBetween` (VERIFY the
   package's inclusive/exclusive semantics against the current `:477` behavior; adjust by ±1 day boundary if the
   package differs, and pin it with a test). Lives in a project BOTH `Infrastructure.Classification` and
   `Infrastructure.Database` can reference (verify the project graph — likely the base `Infrastructure` project, or
   add a project reference). Add `PublicHoliday` to `Directory.Packages.props` (central pinning) + a
   `PackageReference` (no version) in the hosting project.
3. **Wire both services:** inject `IBusinessDayCalculator? calculator = null` into `FusionExpedienteService` and
   `SLAEnforcerService` (OPTIONAL ctor param to avoid churning every test construction site). When provided →
   delegate; when null → the EXISTING weekend-only private method is the documented fallback (keeps current tests
   green). Replace the inline call sites to use the calculator when present.
   - **Production DI MUST register `MexicoBusinessDayCalculator`** and inject it into both services (this is the
     whole point — a silent weekend-only fallback in production would be the Phase-1 "Major 3" bug class). Register
     in the DI extension(s) where these services are registered; confirm by tracing the composition.

## Tests (ITDD per ADR-005)
- **Adapter** `MexicoBusinessDayCalculatorTests`: `AddBusinessDays` over a span containing a known Mexican federal
  holiday (e.g. 16 Sep Independence Day, 1 May Labour Day, 25 Dec, 1 Jan) shifts the result one extra day vs a
  weekend-only count; a plain weekday span; a weekend-spanning span. `CountBusinessDays` excludes the holiday.
  Pin the exclusive-end semantics.
- **Service** extend `FusionExpedienteService*Tests`: with the Mexico calculator injected, a `FechaRecepcion +
  DiasPlazo` deadline that crosses a Mexican holiday lands one day later than weekend-only. Without the calculator
  (null), behavior is the documented weekend-only fallback (existing tests unchanged).
- **Service** extend `SLAEnforcerService*Tests`: deadline/count uses the calculator when injected.
- Keep the existing `FusionExpedienteServiceMutationTests`, `SLAEnforcerService*Tests` green.

## Definition of done
Build 0/0 (incl. the new package restoring); adapter + service tests green; existing fusion/SLA tests green;
architecture 22/22; `dotnet list package --vulnerable` clean for the new package. `git diff` confined to the new
interface + adapter, the package files, the two services' calculator delegation, the DI registration, and tests.
