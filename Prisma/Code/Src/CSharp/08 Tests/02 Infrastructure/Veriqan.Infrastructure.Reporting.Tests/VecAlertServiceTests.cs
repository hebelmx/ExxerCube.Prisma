using System.Collections.Generic;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Binding;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Application.Verdict;
using ExxerCube.Prisma.Veriqan.Domain.Entities;
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.Verification;
using ExxerCube.Prisma.Veriqan.Infrastructure.Reporting;
using ExxerCube.Prisma.Veriqan.Infrastructure.Reporting.DependencyInjection;
using IndQuestResults;
using IndQuestResults.Operations;
using Meziantou.Extensions.Logging.Xunit.v3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Reporting.Tests;

/// <summary>
/// Unit tests for <see cref="VecAlertService"/> (Story 7.3, FR-17, CL-54).
/// </summary>
/// <remarks>
/// A hand-written fake <see cref="IEmailSender"/> drives the transport side, keeping tests
/// fast and hermetic.  <see cref="VecAlertService"/> is the system under test.
/// </remarks>
public sealed class VecAlertServiceTests
{
    // -----------------------------------------------------------------------
    // Helpers / shared fixtures
    // -----------------------------------------------------------------------

    /// <summary>
    /// A controllable fake <see cref="IEmailSender"/> that records send calls and returns
    /// a pre-configured sequence of results.
    /// </summary>
    private sealed class FakeEmailSender : IEmailSender
    {
        private readonly Queue<Result> _resultQueue;

        /// <summary>All <see cref="EmailMessage"/> instances received by this sender.</summary>
        public List<EmailMessage> SentMessages { get; } = [];

        /// <summary>Total number of <see cref="SendAsync"/> calls (including retried attempts).</summary>
        public int CallCount { get; private set; }

        /// <summary>
        /// Creates a fake sender that will return the supplied results in order.
        /// When the queue is exhausted the last result is repeated indefinitely.
        /// </summary>
        public FakeEmailSender(params Result[] results)
        {
            _resultQueue = new Queue<Result>(results.Length == 0
                ? [Result.Success()]
                : results);
        }

        public Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
                return Task.FromResult(ResultExtensions.Cancelled());

            CallCount++;
            var result = _resultQueue.Count > 1 ? _resultQueue.Dequeue() : _resultQueue.Peek();

            if (result.IsSuccess)
                SentMessages.Add(message);

            return Task.FromResult(result);
        }
    }

    /// <summary>Builds a minimal RED <see cref="VerdictSummary"/> for testing.</summary>
    private static VerdictSummary BuildRedVerdict(
        string[] failCheckIds,
        int passCount = 0,
        int insufficientDataCount = 0) =>
        VerdictTestHelpers.RedVerdict(failCheckIds, passCount, insufficientDataCount);

    /// <summary>Builds a GREEN <see cref="VerdictSummary"/>.</summary>
    private static VerdictSummary BuildGreenVerdict() =>
        VerdictTestHelpers.GreenVerdict();

    /// <summary>Builds a BLOCKED <see cref="VerdictSummary"/>.</summary>
    private static VerdictSummary BuildBlockedVerdict() =>
        VerdictTestHelpers.BlockedVerdict();

    /// <summary>Builds a YELLOW <see cref="VerdictSummary"/> (bank improvement opportunities).</summary>
    private static VerdictSummary BuildYellowVerdict() =>
        VerdictTestHelpers.YellowVerdict();

    /// <summary>Default alert recipients for tests.</summary>
    private static readonly IReadOnlyList<string> DefaultRecipients = ["compliance@example.com", "ops@example.com"];

    /// <summary>Builds a default <see cref="AlertContext"/> with the given statement ID.</summary>
    private static AlertContext MakeContext(
        string statementId = "STMT-2026-001",
        Guid? jobVerdictId = null) =>
        new(statementId, DefaultRecipients, jobVerdictId);

    /// <summary>Builds <see cref="AlertOptions"/> with fast retries suitable for unit tests.</summary>
    private static IOptions<AlertOptions> FastAlertOptions(int maxAttempts = 3, int baseDelayMs = 0) =>
        Options.Create(new AlertOptions
        {
            MaxRetryAttempts = maxAttempts,
            BaseRetryDelayMs = baseDelayMs,
            Recipients = ["compliance@example.com"],
        });

    /// <summary>
    /// Builds the system under test wired with the supplied fake sender and optional repository.
    /// </summary>
    private VecAlertService BuildSut(
        FakeEmailSender fake,
        IOptions<AlertOptions>? alertOptions = null,
        IJobVerdictAlertRepository? verdictRepo = null)
    {
        var logger = XUnitLogger.CreateLogger<VecAlertService>(TestContext.Current.TestOutputHelper);
        return new VecAlertService(fake, alertOptions ?? FastAlertOptions(), logger, verdictRepo);
    }

    // -----------------------------------------------------------------------
    // Test 1: RED verdict → exactly one email sent; subject/body contain key data
    // -----------------------------------------------------------------------

    /// <summary>
    /// A RED verdict must trigger exactly one email.  The subject must contain the
    /// statement ID.  The body must contain the signal name, the fail count, and each
    /// failing CheckId.
    /// </summary>
    [Fact]
    public async Task SendRedAlertAsync_RedVerdict_SendsExactlyOneEmail()
    {
        var failIds = new[] { "CL-28", "CL-31" };
        var verdict = BuildRedVerdict(failIds, passCount: 5, insufficientDataCount: 1);
        var context = MakeContext("STMT-2026-001");

        var fake = new FakeEmailSender(Result.Success());
        var sut = BuildSut(fake);

        var result = await sut.SendRedAlertAsync(verdict, context, TestContext.Current.CancellationToken);

        // Overall result is success
        result.IsSuccess.ShouldBeTrue("RED alert must return success when the email was delivered.");

        // Exactly one email was dispatched
        fake.CallCount.ShouldBe(1, "Exactly one send attempt must occur for a first-try success.");
        fake.SentMessages.Count.ShouldBe(1, "Exactly one email message must be recorded.");

        var email = fake.SentMessages[0];

        // Subject contains the statement ID
        email.Subject.Contains("STMT-2026-001").ShouldBeTrue("Subject must identify the statement.");

        // Body contains the signal name
        email.Body.Contains(VerdictSignal.Red.ToString()).ShouldBeTrue("Body must name the RED signal.");

        // Body contains every failing CheckId
        foreach (var id in failIds)
        {
            email.Body.Contains(id).ShouldBeTrue($"Body must include failing CheckId {id}.");
        }

        // Recipients are wired
        email.To.ShouldBe(DefaultRecipients, "Recipients must match the alert context.");
    }

    // -----------------------------------------------------------------------
    // Test 2: GREEN verdict → no email sent
    // -----------------------------------------------------------------------

    /// <summary>
    /// A GREEN verdict must not trigger any email dispatch.
    /// </summary>
    [Fact]
    public async Task SendRedAlertAsync_GreenVerdict_SendsNoEmail()
    {
        var verdict = BuildGreenVerdict();
        var context = MakeContext("STMT-GREEN-001");

        var fake = new FakeEmailSender(Result.Success());
        var sut = BuildSut(fake);

        var result = await sut.SendRedAlertAsync(verdict, context, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("GREEN verdict must return success (no-op).");
        fake.CallCount.ShouldBe(0, "GREEN verdict must not invoke IEmailSender at all.");
    }

    // -----------------------------------------------------------------------
    // Test 3: BLOCKED verdict → no email sent
    // -----------------------------------------------------------------------

    /// <summary>
    /// A BLOCKED verdict must not trigger any email dispatch.
    /// </summary>
    [Fact]
    public async Task SendRedAlertAsync_BlockedVerdict_SendsNoEmail()
    {
        var verdict = BuildBlockedVerdict();
        var context = MakeContext("STMT-BLOCKED-001");

        var fake = new FakeEmailSender(Result.Success());
        var sut = BuildSut(fake);

        var result = await sut.SendRedAlertAsync(verdict, context, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("BLOCKED verdict must return success (no-op).");
        fake.CallCount.ShouldBe(0, "BLOCKED verdict must not invoke IEmailSender at all.");
    }

    // -----------------------------------------------------------------------
    // Test 3b: YELLOW verdict → alert IS sent with bank-improvement wording (adversarial fix)
    // -----------------------------------------------------------------------

    /// <summary>
    /// A YELLOW verdict must trigger exactly one alert email.
    /// The subject must contain "YELLOW" (not "RED"), and the body must NOT claim
    /// regulatory failure — it must describe bank improvement opportunities.
    /// Owner policy: Yellow = bank improvement opportunities; must not mislead recipients
    /// into treating a bank-tier gap as a CONDUSEF non-compliance.
    /// </summary>
    [Fact]
    public async Task SendRedAlertAsync_YellowVerdict_SendsAlertWithBankImprovementWording()
    {
        var verdict = BuildYellowVerdict();
        var context = MakeContext("STMT-YELLOW-001");

        verdict.Signal.ShouldBe(VerdictSignal.Yellow,
            "Pre-condition: BuildYellowVerdict must produce a Yellow verdict.");

        var fake = new FakeEmailSender(Result.Success());
        var sut = BuildSut(fake);

        var result = await sut.SendRedAlertAsync(verdict, context, TestContext.Current.CancellationToken);

        // Overall result is success
        result.IsSuccess.ShouldBeTrue("Yellow alert must return success when the email was delivered.");

        // Exactly one email dispatched
        fake.CallCount.ShouldBe(1, "Exactly one send attempt must occur for a first-try success.");
        fake.SentMessages.Count.ShouldBe(1, "Exactly one email message must be recorded.");

        var email = fake.SentMessages[0];

        // Subject must name the signal accurately (no false "RED" claim)
        email.Subject.Contains("YELLOW").ShouldBeTrue(
            "Subject must contain YELLOW to accurately identify the signal.");
        email.Subject.Contains("RED").ShouldBeFalse(
            "Subject must NOT claim RED for a Yellow outcome.");
        email.Subject.Contains("STMT-YELLOW-001").ShouldBeTrue(
            "Subject must identify the statement.");

        // Body must name the Yellow signal
        email.Body.Contains(VerdictSignal.Yellow.ToString()).ShouldBeTrue(
            "Body must name the Yellow signal.");

        // Body must NOT claim regulatory failure
        email.Body.Contains("regulatory non-compliance").ShouldBeFalse(
            "Yellow body must NOT claim CONDUSEF regulatory non-compliance.");

        // Body must describe bank improvement opportunities
        email.Body.Contains("improvement").ShouldBeTrue(
            "Yellow body must describe bank improvement opportunities.");

        // Recipients wired correctly
        email.To.ShouldBe(DefaultRecipients, "Recipients must match the alert context.");
    }

    /// <summary>
    /// A GREEN verdict must not trigger any email.
    /// Regression guard: after the Yellow-allowance guard change the Green-is-silent
    /// invariant must still hold.
    /// </summary>
    [Fact]
    public async Task SendRedAlertAsync_GreenVerdictAfterYellowFix_SendsNoEmail()
    {
        var verdict = BuildGreenVerdict();
        var context = MakeContext("STMT-GREEN-REGRESSION");

        var fake = new FakeEmailSender(Result.Success());
        var sut = BuildSut(fake);

        var result = await sut.SendRedAlertAsync(verdict, context, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("GREEN verdict must return success (no-op) after Yellow fix.");
        fake.CallCount.ShouldBe(0, "GREEN verdict must still not invoke IEmailSender after Yellow fix.");
    }

    // -----------------------------------------------------------------------
    // Test 4: Transient failure then success → retries and succeeds; one logical alert
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the sender fails twice then succeeds on the third attempt, the overall result
    /// must be success and exactly ONE logical alert email must have been delivered (i.e.
    /// the retried calls all carry the same composed message — only one is recorded as
    /// "sent" by the fake because that is the one that returned Success).
    /// </summary>
    [Fact]
    public async Task SendRedAlertAsync_TransientFailureThenSuccess_RetriesAndSucceeds()
    {
        var verdict = BuildRedVerdict(["CL-50"], passCount: 3);
        var context = MakeContext("STMT-RETRY-001");

        // 2 failures then 1 success → 3 total attempts = MaxRetryAttempts.
        var fake = new FakeEmailSender(
            Result.WithFailure("SMTP timeout"),
            Result.WithFailure("SMTP timeout"),
            Result.Success());

        var sut = BuildSut(fake, FastAlertOptions(maxAttempts: 3, baseDelayMs: 0));

        var result = await sut.SendRedAlertAsync(verdict, context, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue("After retries the result must be success.");

        // Three attempts were made (2 failures + 1 success).
        fake.CallCount.ShouldBe(3, "IEmailSender must be called exactly 3 times (2 failures + 1 success).");

        // Only one logical alert was delivered (the third attempt succeeded).
        fake.SentMessages.Count.ShouldBe(1, "Exactly one email must be recorded as delivered.");
        fake.SentMessages[0].Subject.Contains("STMT-RETRY-001").ShouldBeTrue(
            "The delivered email subject must contain the statement ID.");
    }

    // -----------------------------------------------------------------------
    // Test 5: Permanent failure → logs error and returns failure (never silent)
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the sender always fails, the service must return a typed failure result after
    /// exhausting all retries, AND must have emitted at least one Error-level log entry
    /// naming the statement ID.
    /// </summary>
    [Fact]
    public async Task SendRedAlertAsync_PermanentFailure_LogsAndReturnsFailureNeverSilent()
    {
        var verdict = BuildRedVerdict(["CL-54"], passCount: 1);
        var context = MakeContext("STMT-PERM-FAIL");

        // Always fails — 3 attempts total.
        var fake = new FakeEmailSender(
            Result.WithFailure("SMTP connection refused"),
            Result.WithFailure("SMTP connection refused"),
            Result.WithFailure("SMTP connection refused"));

        // Use a capturing logger to assert log emission.
        var logCapture = new CapturingLogger<VecAlertService>();
        var sut = new VecAlertService(fake, FastAlertOptions(maxAttempts: 3, baseDelayMs: 0), logCapture);

        var result = await sut.SendRedAlertAsync(verdict, context, TestContext.Current.CancellationToken);

        // 1. Result must be a failure (not silent).
        result.IsFailure.ShouldBeTrue("Permanent failure must return a typed failure result.");
        result.Error.ShouldNotBeNullOrEmpty("Failure result must carry an error message.");
        result.Error!.Contains("STMT-PERM-FAIL").ShouldBeTrue(
            "Error message must include the statement ID.");

        // 2. All 3 attempts were made.
        fake.CallCount.ShouldBe(3);

        // 3. An Error-level log was emitted (proves failure is never silent).
        logCapture.HasErrorLog.ShouldBeTrue(
            "An Error-level log entry must be emitted when the alert fails permanently.");
        logCapture.LastErrorMessage.ShouldNotBeNull();
        logCapture.LastErrorMessage!.Contains("STMT-PERM-FAIL").ShouldBeTrue(
            "The error log must include the statement ID for structured traceability.");
    }

    // -----------------------------------------------------------------------
    // Test 6: Cancellation → Cancelled result, no email
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the cancellation token is pre-cancelled, the service must return a Cancelled
    /// result without invoking the email sender.
    /// </summary>
    [Fact]
    public async Task SendRedAlertAsync_PreCancelledToken_ReturnsCancelledNoEmail()
    {
        var verdict = BuildRedVerdict(["CL-99"]);
        var context = MakeContext("STMT-CANCEL");

        var fake = new FakeEmailSender(Result.Success());
        var sut = BuildSut(fake);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var result = await sut.SendRedAlertAsync(verdict, context, cts.Token);

        result.IsCancelled().ShouldBeTrue("Pre-cancelled token must produce a Cancelled result.");
        fake.CallCount.ShouldBe(0, "Email sender must not be invoked when already cancelled.");
    }

    // -----------------------------------------------------------------------
    // Test 7: Dedup guard — AlertSentAt already set → email NOT sent (Story E2-S15)
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the <see cref="JobVerdict.AlertSentAt"/> flag is already set, the service must
    /// skip the email send entirely and return success (duplicate suppressed).
    /// </summary>
    [Fact]
    public async Task SendRedAlertAsync_AlertSentAtAlreadySet_SkipsEmailSend()
    {
        var verdictId = Guid.NewGuid();
        var verdict = BuildRedVerdict(["CL-01"], passCount: 2);
        var context = MakeContext("STMT-DEDUP-SKIP", jobVerdictId: verdictId);

        // Build a JobVerdict that already has AlertSentAt stamped.
        var existingVerdict = new JobVerdict(verdictId, Guid.NewGuid(), VerdictSignal.Red);
        existingVerdict.RecordAlertSent(DateTimeOffset.UtcNow.AddMinutes(-5));

        var repo = Substitute.For<IJobVerdictAlertRepository>();
        repo.FindByIdAsync(verdictId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<JobVerdict?>.WithSuccess(existingVerdict)));

        var fake = new FakeEmailSender(Result.Success());
        var sut = BuildSut(fake, verdictRepo: repo);

        var result = await sut.SendRedAlertAsync(verdict, context, TestContext.Current.CancellationToken);

        // Result is success (dedup = expected outcome, not an error).
        result.IsSuccess.ShouldBeTrue("Dedup suppression must return success, not failure.");

        // Email sink must NOT have been invoked.
        fake.CallCount.ShouldBe(0, "No email must be sent when AlertSentAt is already set.");
        fake.SentMessages.Count.ShouldBe(0, "No email messages must be recorded for a duplicate.");

        // Repository SaveAlertSentAsync must NOT be called (nothing changed).
        await repo.DidNotReceive().SaveAlertSentAsync(Arg.Any<JobVerdict>(), Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Test 8: Dedup guard — AlertSentAt null → email IS sent, flag persisted (Story E2-S15)
    // -----------------------------------------------------------------------

    /// <summary>
    /// When the <see cref="JobVerdict.AlertSentAt"/> flag is null the service must send the
    /// email, call <see cref="JobVerdict.RecordAlertSent"/>, and persist via the repository.
    /// </summary>
    [Fact]
    public async Task SendRedAlertAsync_AlertSentAtNull_SendsEmailAndPersistsFlag()
    {
        var verdictId = Guid.NewGuid();
        var verdict = BuildRedVerdict(["CL-02"], passCount: 1);
        var context = MakeContext("STMT-DEDUP-FIRST", jobVerdictId: verdictId);

        // Verdict has no AlertSentAt yet.
        var freshVerdict = new JobVerdict(verdictId, Guid.NewGuid(), VerdictSignal.Red);
        freshVerdict.AlertSentAt.ShouldBeNull("Pre-condition: AlertSentAt must be null before test.");

        var repo = Substitute.For<IJobVerdictAlertRepository>();
        repo.FindByIdAsync(verdictId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<JobVerdict?>.WithSuccess(freshVerdict)));
        repo.SaveAlertSentAsync(Arg.Any<JobVerdict>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result.Success()));

        var fake = new FakeEmailSender(Result.Success());
        var sut = BuildSut(fake, verdictRepo: repo);

        var result = await sut.SendRedAlertAsync(verdict, context, TestContext.Current.CancellationToken);

        // Result is success.
        result.IsSuccess.ShouldBeTrue("First send must succeed.");

        // Email was dispatched exactly once.
        fake.CallCount.ShouldBe(1, "Email sender must be called exactly once for first send.");
        fake.SentMessages.Count.ShouldBe(1, "Exactly one email must be recorded.");
        fake.SentMessages[0].Subject.Contains("STMT-DEDUP-FIRST").ShouldBeTrue(
            "Email subject must reference the statement ID.");

        // AlertSentAt was stamped on the verdict entity.
        freshVerdict.AlertSentAt.ShouldNotBeNull(
            "RecordAlertSent must have been called after successful send.");

        // Repository must have been asked to persist the flag.
        await repo.Received(1).SaveAlertSentAsync(
            Arg.Is<JobVerdict>(v => v.Id == verdictId),
            Arg.Any<CancellationToken>());
    }

    // -----------------------------------------------------------------------
    // Test 9: DI wiring — AddVeriqanReporting registers IVecAlertService and IEmailSender
    // -----------------------------------------------------------------------

    [Fact]
    public void AddVeriqanReporting_RegistersAlertServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddVeriqanReporting();

        using var sp = services.BuildServiceProvider();

        var alertService = sp.GetService<IVecAlertService>();
        alertService.ShouldNotBeNull("AddVeriqanReporting must register IVecAlertService.");
        alertService.ShouldBeOfType<VecAlertService>();

        var emailSender = sp.GetService<IEmailSender>();
        emailSender.ShouldNotBeNull("AddVeriqanReporting must register IEmailSender.");
        emailSender.ShouldBeOfType<SmtpEmailSender>();
    }
}

// ---------------------------------------------------------------------------
// Test-local helpers
// ---------------------------------------------------------------------------

/// <summary>
/// Thin factory shim that builds <see cref="VerdictSummary"/> instances for unit tests
/// by driving the public <see cref="VerdictAggregator"/> API.
/// </summary>
internal static class VerdictTestHelpers
{
    private static readonly VerdictAggregator Aggregator = new();

    public static VerdictSummary RedVerdict(
        string[] failCheckIds,
        int passCount = 0,
        int insufficientDataCount = 0)
    {
        var findings = new List<RuleFinding>();
        foreach (var id in failCheckIds)
        {
            findings.Add(RuleFinding.Fail(
                checkId: id,
                technique: TechniqueClass.Deterministic,
                severity: FindingSeverity.Critical,
                engineVersion: "1.0",
                expected: "e",
                observed: "o"));
        }

        for (var i = 0; i < passCount; i++)
        {
            findings.Add(RuleFinding.Pass(
                checkId: $"CL-PASS-{i}",
                technique: TechniqueClass.Deterministic,
                engineVersion: "1.0"));
        }

        for (var i = 0; i < insufficientDataCount; i++)
        {
            findings.Add(RuleFinding.InsufficientData(
                checkId: $"CL-INSUF-{i}",
                technique: TechniqueClass.Deterministic,
                engineVersion: "1.0"));
        }

        var result = Aggregator.Aggregate(findings);
        return result.Value!;
    }

    public static VerdictSummary GreenVerdict()
    {
        var findings = new List<RuleFinding>
        {
            RuleFinding.Pass(
                checkId: "CL-PASS-1",
                technique: TechniqueClass.Deterministic,
                engineVersion: "1.0"),
        };
        var result = Aggregator.Aggregate(findings);
        return result.Value!;
    }

    public static VerdictSummary BlockedVerdict()
    {
        var blockedOutcome = new BlockedOutcome(
            BlockReason.UnknownProduct,
            "Test blocking condition");
        // Pass the BlockedOutcome to drive the BLOCKED branch.
        var result = Aggregator.Aggregate(findings: [], blocked: blockedOutcome);
        return result.Value!;
    }

    /// <summary>
    /// Builds a YELLOW <see cref="VerdictSummary"/> by supplying a Bank-only tier map so the
    /// aggregator's two-tier combination rule yields Yellow (bankTier=Yellow, condusefTier=Green).
    /// </summary>
    public static VerdictSummary YellowVerdict()
    {
        // The failing check is mapped to Bank only → condusefFails stays empty → overall = Yellow.
        var tiers = new Dictionary<string, ChecklistTier>
        {
            ["CL-BANK-ONLY"] = ChecklistTier.Bank,
        };
        var findings = new List<RuleFinding>
        {
            RuleFinding.Fail(
                checkId: "CL-BANK-ONLY",
                technique: TechniqueClass.Deterministic,
                severity: FindingSeverity.Critical,
                engineVersion: "1.0",
                expected: "e",
                observed: "o"),
            RuleFinding.Pass(
                checkId: "CL-PASS-1",
                technique: TechniqueClass.Deterministic,
                engineVersion: "1.0"),
        };
        var result = Aggregator.Aggregate(findings, checklistTiers: tiers);
        return result.Value!;
    }
}

/// <summary>
/// A minimal <see cref="ILogger{T}"/> implementation that captures log entries
/// so tests can assert that errors are never silently dropped.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly List<(LogLevel Level, string Message)> _entries = [];

    public bool HasErrorLog => _entries.Any(e => e.Level >= LogLevel.Error);

    public string? LastErrorMessage => _entries
        .LastOrDefault(e => e.Level >= LogLevel.Error)
        .Message;

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _entries.Add((logLevel, formatter(state, exception)));
    }
}
