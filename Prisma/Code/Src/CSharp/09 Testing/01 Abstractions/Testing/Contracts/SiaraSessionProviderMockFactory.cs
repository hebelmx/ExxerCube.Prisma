using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;
using NSubstitute;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Builds the contract-conforming NSubstitute mock that backs the blueprint instance of
/// <see cref="SiaraSessionProviderContract"/> (ADR-005 §6, ADR-010).
/// </summary>
/// <remarks>
/// The configuration below is the <strong>executable design specification</strong> for
/// <see cref="ISiaraSessionProvider"/>: it encodes, in one place, the behavior any implementation must
/// exhibit. It is argument-sensitive and lightly stateful (it tracks an acquisition counter so each
/// session gets a distinct id) — the contract asserts different outcomes for different inputs, which a
/// blanket stub could never satisfy. Growing such factories toward a reference fake is expected
/// (ADR-005 §6); the fuller stateful reference lives in <see cref="FakeSiaraSessionProvider"/>.
/// </remarks>
public static class SiaraSessionProviderMockFactory
{
    /// <summary>
    /// Creates an <see cref="ISiaraSessionProvider"/> mock that satisfies every test in
    /// <see cref="SiaraSessionProviderContract"/>.
    /// </summary>
    /// <param name="mode">The auth mode the mock should report. Defaults to
    /// <see cref="SiaraAuthMode.SessionPassthrough"/>.</param>
    /// <returns>The configured mock.</returns>
    public static ISiaraSessionProvider CreateContractConformingMock(
        SiaraAuthMode mode = SiaraAuthMode.SessionPassthrough)
    {
        var mock = Substitute.For<ISiaraSessionProvider>();
        var counter = new int[1];

        mock.Mode.Returns(mode);

        mock.AcquireAsync(Arg.Any<SiaraSessionRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => Acquire(
                call.ArgAt<SiaraSessionRequest?>(0),
                call.ArgAt<CancellationToken>(1),
                mode,
                counter));

        mock.EnsureValidAsync(Arg.Any<SiaraSession>(), Arg.Any<CancellationToken>())
            .Returns(call => EnsureValid(
                call.ArgAt<SiaraSession?>(0),
                call.ArgAt<CancellationToken>(1)));

        mock.ReleaseAsync(Arg.Any<SiaraSession>(), Arg.Any<CancellationToken>())
            .Returns(call => Release(
                call.ArgAt<SiaraSession?>(0),
                call.ArgAt<CancellationToken>(1)));

        return mock;
    }

    private static Result<SiaraSession> Acquire(
        SiaraSessionRequest? request,
        CancellationToken cancellationToken,
        SiaraAuthMode mode,
        int[] counter)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<SiaraSession>();
        }

        if (request is null)
        {
            return Result<SiaraSession>.WithFailure("Session request cannot be null");
        }

        var n = ++counter[0];
        var session = new SiaraSession
        {
            SessionId = $"mock-session-{n}",
            Mode = mode,
            StorageStateRef = $"mock-storage-state-{n}",
            ExpiresAt = null, // unknown / valid-until-proven-invalid
            AcquiredBy = new SiaraActor
            {
                ActorId = request.RequestedBy ?? "mock-actor",
                ActorType = SiaraActorType.ServiceAccount,
            },
        };

        return Result<SiaraSession>.Success(session);
    }

    private static Result<SiaraSession> EnsureValid(SiaraSession? session, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<SiaraSession>();
        }

        if (session is null)
        {
            return Result<SiaraSession>.WithFailure("Session cannot be null");
        }

        // Fail closed when the session has demonstrably expired (cannot re-auth from a mock).
        if (session.ExpiresAt is { } expiry && expiry <= DateTimeOffset.UtcNow)
        {
            return Result<SiaraSession>.WithFailure("Session has expired");
        }

        return Result<SiaraSession>.Success(session);
    }

    private static Result Release(SiaraSession? session, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        if (session is null)
        {
            return Result.WithFailure("Session cannot be null");
        }

        // Idempotent: releasing any (even already-released) session is a success.
        return Result.Success();
    }
}
