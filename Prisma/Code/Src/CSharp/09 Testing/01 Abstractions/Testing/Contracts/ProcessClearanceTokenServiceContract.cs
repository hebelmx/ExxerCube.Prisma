using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="IProcessClearanceTokenService"/> — every implementation (the
/// reference <see cref="FakeProcessClearanceTokenService"/> and the production
/// <c>JwtProcessClearanceTokenService</c>) must pass these tests unchanged (ADR-005, MVP-PATH 1.5 A5).
/// </summary>
/// <remarks>
/// <para>
/// Uses the ADR-005 §3 default injected-<see cref="Sut"/> mechanism. The class is <c>abstract</c>, so
/// xUnit does not discover it; each inherited Fact/Theory runs once per deriving class.
/// </para>
/// <para>
/// Scope rule (ADR-005 §5): only behavior <em>any</em> correct clearance token service must exhibit —
/// a mint → round-trip validate succeeds and returns the original claims; an invalid token returns
/// failure; a file_id mismatch on a re-validated token can be detected by comparing the extracted
/// <see cref="ClearanceTokenClaims.FileId"/> against the expected value; a pre-cancelled token yields
/// a cancelled result. JWT internals (signing algorithm, issuer, audience) are the production SUT's
/// own concern and stay in its own tests.
/// </para>
/// </remarks>
public abstract class ProcessClearanceTokenServiceContract
{
    /// <summary>Initializes the contract with the service under test.</summary>
    /// <param name="sut">The <see cref="IProcessClearanceTokenService"/> implementation to verify.</param>
    protected ProcessClearanceTokenServiceContract(IProcessClearanceTokenService sut)
    {
        ArgumentNullException.ThrowIfNull(sut);
        Sut = sut;
    }

    /// <summary>Gets the service under test.</summary>
    protected IProcessClearanceTokenService Sut { get; }

    private static SiaraActor SampleActor(string actorId = "orion-downloader-test") => new()
    {
        ActorId = actorId,
        ActorType = SiaraActorType.ServiceAccount,
    };

    /// <summary>
    /// Contract: minting a token and then validating it succeeds and the round-tripped claims
    /// match the original actor, clearance, and file_id.
    /// </summary>
    [Fact]
    public async Task MintThenValidate_ValidInputs_RoundTripsAllClaims()
    {
        var actor = SampleActor();
        var fileId = Guid.NewGuid();
        const ProcessClearance clearance = ProcessClearance.Download;

        var mint = await Sut.MintAsync(actor, clearance, fileId, TestContext.Current.CancellationToken);

        mint.IsSuccess.ShouldBeTrue();
        mint.Value.ShouldNotBeNullOrEmpty();

        var validate = await Sut.ValidateAsync(mint.Value!, TestContext.Current.CancellationToken);

        validate.IsSuccess.ShouldBeTrue();
        validate.Value.ShouldNotBeNull();
        ClearanceTokenClaims claims = validate.Value!;
        claims.ActorId.ShouldBe(actor.ActorId);
        claims.ActorType.ShouldBe(actor.ActorType);
        claims.Clearance.ShouldBe(clearance);
        claims.FileId.ShouldBe(fileId);
    }

    /// <summary>
    /// Contract: validating a clearly invalid token string returns failure (never an exception).
    /// </summary>
    [Fact]
    public async Task ValidateAsync_InvalidToken_ReturnsFailure()
    {
        var result = await Sut.ValidateAsync("not-a-valid-token", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>
    /// Contract: a minted token carries the file_id in its claims, enabling the forwarder to detect
    /// a mismatch between the token's file_id and the event's FileId.
    /// </summary>
    [Fact]
    public async Task MintThenValidate_DifferentFileId_ClaimsFileIdDoesNotMatchExpected()
    {
        var actor = SampleActor();
        var mintedForFileId = Guid.NewGuid();
        var differentFileId = Guid.NewGuid();

        var mint = await Sut.MintAsync(actor, ProcessClearance.Extract, mintedForFileId,
            TestContext.Current.CancellationToken);
        mint.IsSuccess.ShouldBeTrue();

        var validate = await Sut.ValidateAsync(mint.Value!, TestContext.Current.CancellationToken);

        validate.IsSuccess.ShouldBeTrue();
        validate.Value!.FileId.ShouldBe(mintedForFileId);
        validate.Value!.FileId.ShouldNotBe(differentFileId);
    }

    /// <summary>
    /// Contract: a pre-cancelled token on MintAsync yields a cancelled Result — never an exception.
    /// </summary>
    [Fact]
    public async Task MintAsync_PreCancelledToken_ReturnsCancelled()
    {
        var cancelled = new CancellationToken(canceled: true);

        var result = await Sut.MintAsync(SampleActor(), ProcessClearance.Download, Guid.NewGuid(), cancelled);

        result.ShouldNotBeNull();
        result.IsCancelled().ShouldBeTrue();
    }

    /// <summary>
    /// Contract: a pre-cancelled token on ValidateAsync yields a cancelled Result — never an exception.
    /// </summary>
    [Fact]
    public async Task ValidateAsync_PreCancelledToken_ReturnsCancelled()
    {
        var cancelled = new CancellationToken(canceled: true);

        var result = await Sut.ValidateAsync("any-token", cancelled);

        result.ShouldNotBeNull();
        result.IsCancelled().ShouldBeTrue();
    }
}
