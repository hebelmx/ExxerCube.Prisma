using System.IO;
using ExxerCube.Prisma.Veriqan.Application.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Reporting.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Validation.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Infrastructure.Visual.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Orchestration.DependencyInjection;
using ExxerCube.Prisma.Veriqan.Orchestration.Observability;
using ExxerCube.Prisma.Veriqan.Orchestration.Pipeline;
using IndQuestResults;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// Parameterized end-to-end proof tests for the VEC checklist demo corpus.
/// </summary>
/// <remarks>
/// <para>
/// <b>Demo corpus</b> — 4 anonymized PDFs the owner is preparing, expected under:<br/>
/// <c>Prisma/Fixtures/PRP2/demo/</c> (relative to the repository root)
/// </para>
/// <list type="table">
///   <listheader><term>File</term><description>Expected verdict / rule</description></listheader>
///   <item><term>good.pdf</term><description>GREEN — statement is fully compliant</description></item>
///   <item><term>bad-math-cl21.pdf</term><description>RED — CL-21 arithmetic mismatch</description></item>
///   <item><term>bad-font-cl35.pdf</term><description>RED — CL-35 required font family absent</description></item>
///   <item><term>scanned.pdf</term><description>BLOCKED — image-only PDF, text-layer floor not met</description></item>
/// </list>
/// <para>
/// <b>Fixture guard</b> — each theory case skips cleanly via <see cref="Assert.Skip"/> when
/// its fixture PDF is absent.  The scaffold therefore builds and runs green (all skipped)
/// before the corpus lands, and becomes live as soon as the owner drops the files in.
/// </para>
/// <para>
/// <b>Bundle assumption</b> — all cases use <see cref="BuildFakeBundle"/>, which resolves the
/// product alias <c>"Tarjeta de Crédito BSSB"</c>.  When the real demo PDFs embed a different
/// product-name token the owner should update the alias list (or supply fixture-specific
/// bundles) so that binding succeeds and rules run against the correct tariff data.
/// </para>
/// <para>
/// <b>Harness</b> — DI setup and fake <see cref="IVecReferenceDataProvider"/> mirror
/// <see cref="VerificationPipelineEndToEndTests"/> exactly (same registration order, same
/// <c>Replace</c>-after-<c>AddVeriqanBinding</c> trick, same
/// <see cref="AddVeriqanInMemoryPersistence"/> for no-SQL-server operation).
/// See <c>VerificationPipelineEndToEndTests.cs</c> lines 180–208 for the canonical source.
/// </para>
/// </remarks>
[Collection(MetricsIsolationCollection.Name)]
public sealed class VecChecklistDemoE2ETests
{
    // -----------------------------------------------------------------------
    // Corpus location
    // -----------------------------------------------------------------------

    /// <summary>
    /// Sub-path from the repository root to the demo corpus directory.
    /// Fixtures are expected at <c>{RepoRoot}/{DemoCorpusRelativePath}/{filename}</c>.
    /// </summary>
    private const string DemoCorpusRelativePath = "Prisma/Fixtures/PRP2/demo";

    /// <summary>
    /// Absolute path to the demo corpus directory, resolved at runtime by walking up from the
    /// test assembly location until a directory containing <c>CLAUDE.md</c> is found (the
    /// repository root).  Returns <see langword="null"/> when the repo root cannot be located
    /// (e.g. running outside the normal build tree), in which case all theory cases skip.
    /// </summary>
    private static readonly string? DemoCorpusDir = ComputeDemoCorpusDir();

    private static string? ComputeDemoCorpusDir()
    {
        // Walk up from the assembly output directory looking for the repo root,
        // identified by the presence of CLAUDE.md.
        //
        // Two cases are handled at each level:
        //   1. Direct: CLAUDE.md in the current directory — assembly output is inside the
        //      repo tree (e.g. a Debug output dir placed within the repo).
        //   2. Sibling: a child directory named "ExxerCube.Prisma" contains CLAUDE.md —
        //      handles the standard layout where BuildArtifacts sits next to the repo root:
        //      IndFusion/
        //        BuildArtifacts/  ← assembly lives here
        //        ExxerCube.Prisma/CLAUDE.md  ← repo root sibling
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            // Case 1: assembly is inside the repo tree
            if (File.Exists(Path.Combine(dir.FullName, "CLAUDE.md")))
                return Path.Combine(dir.FullName, DemoCorpusRelativePath);

            // Case 2: repo root is a sibling of the build-artifacts tree at this level
            var siblingRepo = Path.Combine(dir.FullName, "ExxerCube.Prisma");
            if (Directory.Exists(siblingRepo) &&
                File.Exists(Path.Combine(siblingRepo, "CLAUDE.md")))
                return Path.Combine(siblingRepo, DemoCorpusRelativePath);

            dir = dir.Parent;
        }

        return null;
    }

    // -----------------------------------------------------------------------
    // Theory data — 4 demo fixtures
    // -----------------------------------------------------------------------

    /// <summary>
    /// MemberData source for <see cref="Pipeline_DemoFixture_ProducesExpectedVerdict"/>.
    /// </summary>
    /// <remarks>
    /// Columns: fixture file name, expected <see cref="VerdictSignal"/>,
    /// optional check ID that MUST appear in <see cref="VerdictSummary.FailCheckIds"/> when
    /// the signal is <see cref="VerdictSignal.Red"/> (<see langword="null"/> when no specific
    /// check ID is required by the demo spec, e.g. for GREEN and BLOCKED cases).
    /// </remarks>
    public static IEnumerable<object?[]> DemoFixtures =>
    [
        // good.pdf → fully compliant statement; engine must find zero Fail findings
        ["good.pdf",     VerdictSignal.Green,   null   ],

        // bad-math-cl21.pdf → arithmetic total mismatch; CL-21 must fire
        ["bad-math-cl21.pdf", VerdictSignal.Red,     "CL-21"],

        // bad-font-cl35.pdf → required font family absent; CL-35 must fire
        ["bad-font-cl35.pdf", VerdictSignal.Red,     "CL-35"],

        // scanned.pdf  → image-only PDF; text-layer floor guard blocks processing
        ["scanned.pdf",  VerdictSignal.Blocked, null   ],
    ];

    // -----------------------------------------------------------------------
    // Shared context key (matches the fake bundle's BundleMetadata.Institution)
    // -----------------------------------------------------------------------

    private static readonly StatementContextKey DemoContextKey =
        new("Demo Bank (Iqubica)", PeriodLabel: "Jul-Ago 2025");

    // -----------------------------------------------------------------------
    // Theory test
    // -----------------------------------------------------------------------

    /// <summary>
    /// Drives the full ingest → extract → bind → engine → verdict pipeline against each demo
    /// corpus fixture and asserts the expected VEC verdict signal.
    /// </summary>
    /// <param name="fixtureName">PDF file name inside the demo corpus directory.</param>
    /// <param name="expectedSignal">Expected <see cref="VerdictSignal"/> traffic-light.</param>
    /// <param name="expectedFailCheckId">
    /// When non-<see langword="null"/> the specified check ID must appear in
    /// <see cref="VerdictSummary.FailCheckIds"/> (proves the specific RED-triggering rule fired).
    /// </param>
    [Theory]
    [MemberData(nameof(DemoFixtures))]
    public async Task Pipeline_DemoFixture_ProducesExpectedVerdict(
        string fixtureName,
        VerdictSignal expectedSignal,
        string? expectedFailCheckId)
    {
        // ------------------------------------------------------------------
        // Fixture guard — skip if the demo corpus hasn't landed yet.
        // ------------------------------------------------------------------
        if (DemoCorpusDir is null)
            Assert.Skip("Repository root could not be determined from assembly location — demo corpus unreachable.");

        var fixturePath = Path.Combine(DemoCorpusDir, fixtureName);

        if (!File.Exists(fixturePath))
            Assert.Skip(
                $"Demo fixture '{fixtureName}' not present at '{fixturePath}'. " +
                "Drop the anonymized PDF in that directory to activate this test.");

        // ------------------------------------------------------------------
        // Arrange — fake IVecReferenceDataProvider (same pattern as
        // VerificationPipelineEndToEndTests, lines 174–208)
        // ------------------------------------------------------------------
        var fakeProvider = Substitute.For<IVecReferenceDataProvider>();
        fakeProvider
            .GetBundleAsync(Arg.Any<StatementContextKey>(), Arg.Any<CancellationToken>())
            .Returns(ci => Task.FromResult(
                Result<VecReferenceBundle>.WithSuccess(BuildFakeBundle())));

        var services = new ServiceCollection();
        services.AddLogging();

        // Application layer
        services.AddVeriqanIngestion();
        services.AddVeriqanBinding();
        services.AddVeriqanVerdict();

        // Infrastructure adapters (real implementations)
        services.AddVeriqanExtraction();
        services.AddVeriqanValidation();
        services.AddVeriqanVisual();
        services.AddVeriqanReporting();

        // Override the reference-data provider AFTER AddVeriqanBinding so TryAdd semantics
        // inside that call register first, and Replace wins unconditionally here.
        services.Replace(ServiceDescriptor.Scoped<IVecReferenceDataProvider>(_ => fakeProvider));

        // In-memory persistence stubs — no SQL Server required
        services.AddVeriqanInMemoryPersistence();

        // Metrics singleton required by VerificationPipeline
        services.AddSingleton<VeriqanMetrics>();

        // Pipeline
        services.AddScoped<IVerificationPipeline, VerificationPipeline>();
        services.AddSingleton(TimeProvider.System);

        var sp = services.BuildServiceProvider();

        var ct = TestContext.Current.CancellationToken;
        var pdfBytes = await File.ReadAllBytesAsync(fixturePath, ct);

        var submission = new StatementSubmission(
            Pdf: pdfBytes,
            FileName: fixtureName,
            ContextKey: DemoContextKey);

        // ------------------------------------------------------------------
        // Act
        // ------------------------------------------------------------------
        Result<VerificationOutcome> result;
        await using (var scope = sp.CreateAsyncScope())
        {
            var pipeline = scope.ServiceProvider.GetRequiredService<IVerificationPipeline>();
            result = await pipeline.ProcessAsync(submission, ct);
        }

        // ------------------------------------------------------------------
        // Assert — pipeline must return a successful Result<T> in all cases
        // (GREEN, RED, and BLOCKED are all returned as Result.WithSuccess).
        // ------------------------------------------------------------------
        result.IsSuccess.ShouldBeTrue(
            $"Pipeline returned a Result failure for '{fixtureName}': {result.Error ?? "<none>"}. " +
            "Check that the product alias in BuildFakeBundle() resolves the token in this PDF, " +
            "or that the PDF is not corrupt.");

        var outcome = result.Value!;

        // Job must always be recorded (proves ingestion + in-memory persistence ran).
        outcome.Job.ShouldNotBeNull(
            $"'{fixtureName}': VerificationJob must be set on the outcome (ingestion persisted).");
        outcome.Job.Id.ShouldNotBe(
            Guid.Empty,
            $"'{fixtureName}': VerificationJob.Id must be a real GUID.");

        // Summary must always be present.
        outcome.Summary.ShouldNotBeNull(
            $"'{fixtureName}': VerdictSummary must be populated.");

        // Processing duration must be positive (proves the pipeline actually ran, not short-circuited).
        outcome.ProcessingDuration.ShouldBeGreaterThan(
            TimeSpan.Zero,
            $"'{fixtureName}': ProcessingDuration must be positive — indicates the pipeline executed.");

        // Traffic-light signal must match the expected demo verdict.
        outcome.Summary.Signal.ShouldBe(
            expectedSignal,
            $"'{fixtureName}': expected {expectedSignal} but got {outcome.Summary.Signal}. " +
            $"FailCheckIds=[{string.Join(", ", outcome.Summary.FailCheckIds)}].");

        // ------------------------------------------------------------------
        // Signal-specific assertions (demo artifact evidence)
        // ------------------------------------------------------------------
        switch (expectedSignal)
        {
            case VerdictSignal.Green:
                // GREEN: no Fail findings — compliance is confirmed.
                outcome.Summary.FailCount.ShouldBe(
                    0,
                    $"'{fixtureName}': a GREEN verdict must have zero Fail findings. " +
                    $"Unexpected FailCheckIds=[{string.Join(", ", outcome.Summary.FailCheckIds)}].");
                break;

            case VerdictSignal.Red:
                // RED: at least one Fail finding must be present — engine ran real rules.
                outcome.Summary.FailCount.ShouldBeGreaterThan(
                    0,
                    $"'{fixtureName}': a RED verdict must have at least one Fail finding.");

                // When a specific check ID is required by the demo spec, assert it fired.
                if (expectedFailCheckId is not null)
                {
                    outcome.Summary.FailCheckIds.ShouldContain(
                        expectedFailCheckId,
                        $"'{fixtureName}': expected check '{expectedFailCheckId}' to be in FailCheckIds " +
                        $"but got [{string.Join(", ", outcome.Summary.FailCheckIds)}].");
                }

                // Findings list must also be non-empty (belt-and-suspenders with FailCount).
                outcome.Findings.Count.ShouldBeGreaterThan(
                    0,
                    $"'{fixtureName}': RED outcome must carry at least one RuleFinding.");
                break;

            case VerdictSignal.Blocked:
                // BLOCKED: the pipeline halted before rules ran (e.g. insufficient text layer).
                // BlockedOutcome on the summary proves which guard fired.
                outcome.Summary.BlockedOutcome.ShouldNotBeNull(
                    $"'{fixtureName}': BLOCKED verdict must carry a non-null BlockedOutcome " +
                    "identifying the reason (InsufficientTextLayer, ProductNotFound, etc.).");
                break;
        }
    }

    // -----------------------------------------------------------------------
    // Fake reference-data bundle
    // -----------------------------------------------------------------------

    /// <summary>
    /// Builds a <see cref="VecReferenceBundle"/> that resolves product alias
    /// <c>"Tarjeta de Crédito BSSB"</c> so the binding stage succeeds for PDFs that carry
    /// that product name token in the period summary band.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method mirrors <c>VerificationPipelineEndToEndTests.BuildFakeBundle()</c>
    /// (same data, same structure) so the two test classes share a consistent harness
    /// without creating a shared helper dependency in the test assembly.
    /// </para>
    /// <para>
    /// When the owner's demo PDFs embed a different product-name token, add that token to
    /// <see cref="VecProduct.Aliases"/> (or supply a fixture-specific bundle via a dedicated
    /// provider override) before expecting GREEN/RED verdicts.  BLOCKED cases (scanned.pdf)
    /// are guarded before binding and do not require alias resolution.
    /// </para>
    /// </remarks>
    private static VecReferenceBundle BuildFakeBundle() => new(
        BundleMetadata: new BundleMetadata(
            SchemaVersion: "1.0.0",
            Institution: "Demo Bank (Iqubica)",
            BundleId: "test-bundle-vec-demo",
            GeneratedAt: "2025-08-01T00:00:00Z",
            Period: new PeriodRange(Label: "Jul-Ago 2025", Start: "2025-07-05", End: "2025-08-04"),
            Source: new BundleSource(Mechanism: "manual", Reference: "vec-demo-e2e", Notes: null)),

        Products: new[]
        {
            new VecProduct(
                ProductId: "TC-BSSB",
                ProductName: "Tarjeta de Crédito BSSB",
                Aliases: new[] { "BSSB", "Tarjeta de Crédito BSSB" },
                HasRewardsProgram: false,
                CardImage: null,
                ImportantMessageImage: null,
                Tariffs: new ProductTariffs(
                    AnnualCommission: 1500m,
                    Currency: "MXN",
                    OtherCharges: null))
        },

        InterestRates: new[]
        {
            new InterestRateEntry(
                ProductId: "TC-BSSB",
                RatesByPeriod: new[]
                {
                    new RateByPeriod(
                        AnnualOrdinaryFixedRate: 0.2851m,
                        PeriodLabel: "Jul Ago",
                        PeriodStart: "2025-07-05",
                        PeriodEnd: "2025-08-04")
                })
        },

        ClientAccounts: new[]
        {
            new ClientAccount(
                ClientId: "CLIENT-001",
                ClientName: new ClientName(
                    FirstNames: "Juan",
                    LastNames: "Pérez García",
                    Full: "PÉREZ GARCÍA JUAN"),
                Rfc: "PEGJ800101ABC",
                ClientNumber: "12345678",
                Address: new Address(
                    Street: "Av. Insurgentes",
                    Number: "100",
                    Neighborhood: "Centro",
                    PostalCode: "06600",
                    State: "CDMX"),
                Accounts: new[]
                {
                    new AccountEntry(
                        AccountRef: "ACC-001",
                        ProductId: "TC-BSSB",
                        CardNumber: "4111XXXXXXXX1111",
                        Clabe: null,
                        BranchNumber: null,
                        CreditLine: 100000m,
                        AccountOpenDate: "2020-01-15")
                })
        },

        ToleranceConfig: new ToleranceConfig(
            CurrencyToleranceMxn: 0.50m,
            PointsTolerance: 1.00m,
            RewardsPesosToleranceMxn: 1.00m,
            PointsToPesosExchangeRate: 0.10m),

        ValidationConstants: new ValidationConstants(
            RequiredFontFamily: "Aptos",
            BankingYearDays: 360,
            CatAnnualCommissionMxn: 1500m),

        MandatoryLegends: null,
        SequentialImages: null,
        Promotions: null,
        PriorStatements: null,
        ExpectedTransactions: null);
}
