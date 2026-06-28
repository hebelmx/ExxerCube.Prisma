using ExxerCube.Prisma.Veriqan.Domain.Enums;

namespace ExxerCube.Prisma.Veriqan.Web.UI.Services;

/// <summary>
/// Provides hard-coded, pipeline-shaped demo data for the Veriqan visual demo UI.
/// Returns four pre-built <see cref="DemoStatementCase"/> instances covering the
/// four verdict classes: GREEN (all pass), YELLOW (bank-tier improvement
/// opportunities only, CONDUSEF clean), RED (arithmetic + font failures),
/// and BLOCKED (image-only statement, insufficient text layer).
/// </summary>
/// <remarks>
/// This service is intentionally non-production. It does not connect to the pipeline
/// or database. Its sole purpose is to let the demo pages render meaningful content
/// without a live Veriqan worker. Wiring to the real pipeline can come later.
/// </remarks>
public sealed class DemoDataService
{
    private readonly IReadOnlyList<DemoStatementCase> _cases;

    /// <summary>Initialises the service and builds the three demo cases in memory.</summary>
    public DemoDataService()
    {
        var greenJobId   = new Guid("11111111-0000-0000-0000-000000000001");
        var redJobId     = new Guid("22222222-0000-0000-0000-000000000002");
        var blockedJobId = new Guid("33333333-0000-0000-0000-000000000003");
        var yellowJobId  = new Guid("44444444-0000-0000-0000-000000000004");

        _cases =
        [
            BuildGreenCase(greenJobId),
            BuildRedCase(redJobId),
            BuildBlockedCase(blockedJobId),
            BuildYellowCase(yellowJobId),
        ];
    }

    /// <summary>Returns all three demo cases.</summary>
    public IReadOnlyList<DemoStatementCase> GetAllCases() => _cases;

    /// <summary>Returns the demo case with the specified <paramref name="signal"/>.</summary>
    public DemoStatementCase? GetBySignal(VerdictSignal signal) =>
        _cases.FirstOrDefault(c => c.Signal == signal);

    /// <summary>Returns a demo case by job identifier.</summary>
    public DemoStatementCase? GetByJobId(Guid jobId) =>
        _cases.FirstOrDefault(c => c.JobId == jobId);

    // -------------------------------------------------------------------------
    // Case builders
    // -------------------------------------------------------------------------

    private static DemoStatementCase BuildGreenCase(Guid jobId)
    {
        var receivedAt = DateTimeOffset.UtcNow.AddMinutes(-8);
        var findings = BuildAllFindings(failCheckIds: [], insufficientCheckIds: []);
        var fields = new DemoExtractedFields
        {
            BankName = "BANCO DEMO S.A.",
            MaskedAccount = "****0001",
            ProductType = "Tarjeta de Crédito Visa",
            Period = "15/03/2026 – 14/04/2026",
            CutDate = new DateOnly(2026, 4, 14),
            OpeningBalance = 1_250.00m,
            ClosingBalance = 3_480.75m,
            MovementCount = 24,
            ContentHash = "a3f9b2c1d4e5f678901234567890abcdef1234567890abcdef1234567890abcd",
            PageCount = 3,
        };

        return new DemoStatementCase
        {
            JobId = jobId,
            CaseName = "Caso GREEN — Estado conforme",
            FileName = "estado-cuenta-visa-demo.pdf",
            Signal = VerdictSignal.Green,
            CondusefTierVerdict = ComputeCondusefTierVerdict(findings),
            BankTierVerdict = ComputeBankTierVerdict(findings),
            TotalChecks = 55,
            PassCount = 55,
            FailCount = 0,
            InsufficientDataCount = 0,
            FailCheckIds = [],
            Findings = findings,
            ExtractedFields = fields,
            ProcessingDuration = TimeSpan.FromSeconds(1.43),
            ReceivedAtUtc = receivedAt,
            MarkedPdfPath = null,
            AuditRows = BuildAuditRows(jobId, receivedAt, VerdictSignal.Green),
            Dispositions = [],
        };
    }

    private static DemoStatementCase BuildRedCase(Guid jobId)
    {
        var receivedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        // CL-21: arithmetic error, CL-35: incorrect font
        var failIds = new[] { "CL-21", "CL-35" };
        var insufficientIds = new[] { "CL-52" };
        var findings = BuildAllFindings(failCheckIds: failIds, insufficientCheckIds: insufficientIds);
        var fields = new DemoExtractedFields
        {
            BankName = "BANCO DEMO S.A.",
            MaskedAccount = "****0002",
            ProductType = "Tarjeta de Crédito Mastercard",
            Period = "15/03/2026 – 14/04/2026",
            CutDate = new DateOnly(2026, 4, 14),
            OpeningBalance = 8_900.00m,
            ClosingBalance = 9_432.10m,
            MovementCount = 41,
            ContentHash = "b7c8d9e0f1a2b3c4d5e6f7a8b9c0d1e2f3a4b5c6d7e8f9a0b1c2d3e4f5a6b7",
            PageCount = 5,
        };

        var preSeeded = new List<DemoDisposition>
        {
            new()
            {
                Id = new Guid("aaaaaaaa-0000-0000-0000-000000000001"),
                JobId = jobId,
                CheckId = "CL-21",
                Action = DispositionAction.Reject,
                Comment = "Diferencia aritmética detectada en saldo final. Se rechaza para revisión senior.",
                AnalystName = "Ana García",
                RecordedAtUtc = receivedAt.AddMinutes(3),
            },
        };

        return new DemoStatementCase
        {
            JobId = jobId,
            CaseName = "Caso RED — Hallazgos de incumplimiento",
            FileName = "estado-cuenta-mc-demo.pdf",
            Signal = VerdictSignal.Red,
            CondusefTierVerdict = ComputeCondusefTierVerdict(findings),
            BankTierVerdict = ComputeBankTierVerdict(findings),
            TotalChecks = 55,
            PassCount = 52,
            FailCount = 2,
            InsufficientDataCount = 1,
            FailCheckIds = failIds,
            Findings = findings,
            ExtractedFields = fields,
            ProcessingDuration = TimeSpan.FromSeconds(1.87),
            ReceivedAtUtc = receivedAt,
            MarkedPdfPath = null, // placeholder — real pipeline writes to exports/
            AuditRows = BuildAuditRows(jobId, receivedAt, VerdictSignal.Red),
            Dispositions = preSeeded,
        };
    }

    private static DemoStatementCase BuildBlockedCase(Guid jobId)
    {
        var receivedAt = DateTimeOffset.UtcNow.AddMinutes(-2);
        // BLOCKED: image-only PDF, no extractable text layer
        var findings = BuildAllFindings(
            failCheckIds: [],
            insufficientCheckIds: ChecklistIds.AllIds);
        var fields = new DemoExtractedFields
        {
            BankName = "(no extraído — documento escaneado)",
            MaskedAccount = "****????",
            ProductType = "(desconocido)",
            Period = "(no extraído)",
            CutDate = default,
            OpeningBalance = 0m,
            ClosingBalance = 0m,
            MovementCount = 0,
            ContentHash = "c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1",
            PageCount = 2,
        };

        return new DemoStatementCase
        {
            JobId = jobId,
            CaseName = "Caso BLOCKED — Solo imagen, requiere revisión humana",
            FileName = "estado-cuenta-escaneado.pdf",
            Signal = VerdictSignal.Blocked,
            CondusefTierVerdict = ComputeCondusefTierVerdict(findings),
            BankTierVerdict = ComputeBankTierVerdict(findings),
            TotalChecks = 55,
            PassCount = 0,
            FailCount = 0,
            InsufficientDataCount = 55,
            FailCheckIds = [],
            BlockReason = ExxerCube.Prisma.Veriqan.Domain.Enums.BlockReason.InsufficientTextLayer,
            BlockDetail = "PDF word count 4 is below the minimum floor of 50. " +
                          "The document appears to be a scanned image. " +
                          "Automatic verification cannot proceed; route to manual review.",
            Findings = findings,
            ExtractedFields = fields,
            ProcessingDuration = TimeSpan.FromMilliseconds(210),
            ReceivedAtUtc = receivedAt,
            MarkedPdfPath = null,
            AuditRows = BuildAuditRows(jobId, receivedAt, VerdictSignal.Blocked),
            Dispositions = [],
        };
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static IReadOnlyList<DemoFinding> BuildAllFindings(
        IEnumerable<string> failCheckIds,
        IEnumerable<string> insufficientCheckIds)
    {
        var failSet = new HashSet<string>(failCheckIds, StringComparer.Ordinal);
        var insuffSet = new HashSet<string>(insufficientCheckIds, StringComparer.Ordinal);

        return ChecklistIds.AllIds
            .Select(id =>
            {
                if (failSet.Contains(id))
                    return BuildFailFinding(id);
                if (insuffSet.Contains(id))
                    return BuildInsufficientFinding(id);
                return BuildPassFinding(id);
            })
            .ToList()
            .AsReadOnly();
    }

    private static DemoFinding BuildPassFinding(string checkId) => new()
    {
        CheckId = checkId,
        Verdict = FindingVerdict.Pass,
        Technique = TechniqueClass.Deterministic,
        Severity = FindingSeverity.Info,
        Label = ChecklistIds.Label(checkId),
        DofNumeral = ChecklistIds.DofNumeral(checkId),
        Tier = ChecklistIds.Tier(checkId),
    };

    private static DemoFinding BuildFailFinding(string checkId)
    {
        (string expected, string observed) = checkId switch
        {
            "CL-21" => ("$9,432.10", "$9,512.90"),
            "CL-35" => ("Aptos 10 pt", "Arial 8 pt"),
            "CL-37" => ("≥ 4.5:1", "3.1:1"),
            _ => ("(esperado)", "(observado)"),
        };
        return new DemoFinding
        {
            CheckId = checkId,
            Verdict = FindingVerdict.Fail,
            Technique = TechniqueClass.Deterministic,
            Severity = FindingSeverity.Critical,
            Label = ChecklistIds.Label(checkId),
            Expected = expected,
            Observed = observed,
            DofNumeral = ChecklistIds.DofNumeral(checkId),
            Tier = ChecklistIds.Tier(checkId),
        };
    }

    private static DemoFinding BuildInsufficientFinding(string checkId) => new()
    {
        CheckId = checkId,
        Verdict = FindingVerdict.InsufficientData,
        Technique = TechniqueClass.Deterministic,
        Severity = FindingSeverity.Info,
        Label = ChecklistIds.Label(checkId),
        DofNumeral = ChecklistIds.DofNumeral(checkId),
        Tier = ChecklistIds.Tier(checkId),
    };

    private static DemoStatementCase BuildYellowCase(Guid jobId)
    {
        var receivedAt = DateTimeOffset.UtcNow.AddMinutes(-3);
        // CL-35 (Tipo de fuente — Bank tier) and CL-37 (Contraste texto/fondo — Bank tier).
        // Both are bank-internal visual standards; neither is a CONDUSEF regulatory mandate.
        // Zero Both/Condusef failures → CondusefTierVerdict=Green, BankTierVerdict=Yellow,
        // overall Signal=Yellow (good.pdf-style: compliant at law, improvement opportunities at bank bar).
        var failIds = new[] { "CL-35", "CL-37" };
        var findings = BuildAllFindings(failCheckIds: failIds, insufficientCheckIds: []);
        var fields = new DemoExtractedFields
        {
            BankName = "BANCO DEMO S.A.",
            MaskedAccount = "****0003",
            ProductType = "Tarjeta de Crédito Gold",
            Period = "15/03/2026 – 14/04/2026",
            CutDate = new DateOnly(2026, 4, 14),
            OpeningBalance = 2_100.00m,
            ClosingBalance = 2_350.50m,
            MovementCount = 18,
            ContentHash = "d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4",
            PageCount = 3,
        };

        return new DemoStatementCase
        {
            JobId = jobId,
            CaseName = "Caso YELLOW — Oportunidades de mejora del banco",
            FileName = "estado-cuenta-gold-demo.pdf",
            Signal = VerdictSignal.Yellow,
            CondusefTierVerdict = ComputeCondusefTierVerdict(findings),
            BankTierVerdict = ComputeBankTierVerdict(findings),
            TotalChecks = 55,
            PassCount = 53,
            FailCount = 2,
            InsufficientDataCount = 0,
            FailCheckIds = failIds,
            Findings = findings,
            ExtractedFields = fields,
            ProcessingDuration = TimeSpan.FromSeconds(1.55),
            ReceivedAtUtc = receivedAt,
            MarkedPdfPath = null,
            AuditRows = BuildAuditRows(jobId, receivedAt, VerdictSignal.Yellow),
            Dispositions = [],
        };
    }

    // ── Tier verdict helpers ───────────────────────────────────────────────────

    /// <summary>
    /// Returns <see cref="VerdictSignal.Red"/> when at least one Fail finding belongs to
    /// <see cref="ChecklistTier.Condusef"/> or <see cref="ChecklistTier.Both"/>;
    /// otherwise <see cref="VerdictSignal.Green"/>.
    /// </summary>
    private static VerdictSignal ComputeCondusefTierVerdict(IReadOnlyList<DemoFinding> findings)
    {
        bool hasCondusefFail = findings.Any(f =>
            f.Verdict == FindingVerdict.Fail &&
            (f.Tier == ChecklistTier.Condusef || f.Tier == ChecklistTier.Both));
        return hasCondusefFail ? VerdictSignal.Red : VerdictSignal.Green;
    }

    /// <summary>
    /// Returns <see cref="VerdictSignal.Yellow"/> when at least one Fail finding belongs to
    /// the bank tier (<see cref="ChecklistTier.Bank"/> or <see cref="ChecklistTier.Both"/>);
    /// otherwise <see cref="VerdictSignal.Green"/>. Mirrors the canonical
    /// <c>VerdictAggregator</c> rule (a <see cref="ChecklistTier.Both"/> check belongs to
    /// both tiers).
    /// </summary>
    private static VerdictSignal ComputeBankTierVerdict(IReadOnlyList<DemoFinding> findings)
    {
        bool hasBankFail = findings.Any(f =>
            f.Verdict == FindingVerdict.Fail
            && (f.Tier == ChecklistTier.Bank || f.Tier == ChecklistTier.Both));
        return hasBankFail ? VerdictSignal.Yellow : VerdictSignal.Green;
    }

    private static IReadOnlyList<DemoAuditRow> BuildAuditRows(
        Guid jobId, DateTimeOffset receivedAt, VerdictSignal signal)
    {
        var rows = new List<DemoAuditRow>
        {
            new() { JobId = jobId, OccurredAtUtc = receivedAt,
                EventType = "Ingestion", Actor = "Pipeline",
                Description = "Documento recibido; hash SHA-256 calculado y registrado." },
            new() { JobId = jobId, OccurredAtUtc = receivedAt.AddMilliseconds(80),
                EventType = "Extraction", Actor = "Pipeline",
                Description = "Campos extraídos: encabezado, período, saldos, movimientos." },
            new() { JobId = jobId, OccurredAtUtc = receivedAt.AddMilliseconds(250),
                EventType = "Binding", Actor = "Pipeline",
                Description = "Paquete de referencia vinculado; producto identificado." },
            new() { JobId = jobId, OccurredAtUtc = receivedAt.AddMilliseconds(1200),
                EventType = "Verdict", Actor = "Pipeline",
                Description = $"Veredicto emitido: {signal}. Resultado persistido en base de datos." },
        };

        if (signal == VerdictSignal.Red)
        {
            rows.Add(new DemoAuditRow
            {
                JobId = jobId,
                OccurredAtUtc = receivedAt.AddMinutes(3),
                EventType = "Disposition",
                Actor = "Ana García",
                Description = "CL-21 escalado a revisor senior con comentario de analista.",
            });
        }

        if (signal == VerdictSignal.Yellow)
        {
            rows.Add(new DemoAuditRow
            {
                JobId = jobId,
                OccurredAtUtc = receivedAt.AddMinutes(1),
                EventType = "Recommendation",
                Actor = "Pipeline",
                Description = "CL-35 y CL-37 marcados como oportunidades de mejora (nivel banco). " +
                              "Sin incumplimiento CONDUSEF detectado.",
            });
        }

        return rows.AsReadOnly();
    }
}
