using ExxerCube.Prisma.Domain.Interfaces;
using IndQuestResults;
using IndQuestResults.Operations;
using NSubstitute;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Builds the contract-conforming NSubstitute mock that backs the blueprint instance of
/// <see cref="BrowserSessionContextContract"/> (ADR-005 §6, ADR-010).
/// </summary>
/// <remarks>
/// This is the <strong>executable design specification</strong> for <see cref="IBrowserSessionContext"/>:
/// it encodes the input-validation, cancellation, and fail-closed semantics any implementation must
/// exhibit. It models a provider with no established session (so <c>ExportStorageStateAsync</c> fails
/// closed) — the real capture/restore happy paths require a live browser and are proven in the
/// adapter's E2E tests.
/// </remarks>
public static class BrowserSessionContextMockFactory
{
    /// <summary>
    /// Creates an <see cref="IBrowserSessionContext"/> mock that satisfies every test in
    /// <see cref="BrowserSessionContextContract"/>.
    /// </summary>
    /// <returns>The configured mock.</returns>
    public static IBrowserSessionContext CreateContractConformingMock()
    {
        var mock = Substitute.For<IBrowserSessionContext>();

        mock.ExportStorageStateAsync(Arg.Any<CancellationToken>())
            .Returns(call => Export(call.ArgAt<CancellationToken>(0)));

        mock.LoadStorageStateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Load(call.ArgAt<string?>(0), call.ArgAt<CancellationToken>(1)));

        mock.ConnectToExistingContextAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Connect(call.ArgAt<string?>(0), call.ArgAt<CancellationToken>(1)));

        mock.IsAuthenticatedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => IsAuthenticated(call.ArgAt<string?>(0), call.ArgAt<CancellationToken>(1)));

        return mock;
    }

    private static Result<string> Export(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<string>();
        }

        // No session established on a bare provider — fail closed (never a silent empty success).
        return Result<string>.WithFailure("No browser session established");
    }

    private static Result Load(string? storageStateRef, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(storageStateRef))
        {
            return Result.WithFailure("Storage-state reference cannot be null or empty");
        }

        return Result.Success();
    }

    private static Result Connect(string? endpoint, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled();
        }

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return Result.WithFailure("Connect endpoint cannot be null or empty");
        }

        return Result.Success();
    }

    private static Result<bool> IsAuthenticated(string? postLoginSelector, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return ResultExtensions.Cancelled<bool>();
        }

        if (string.IsNullOrWhiteSpace(postLoginSelector))
        {
            return Result<bool>.WithFailure("Post-login selector cannot be null or empty");
        }

        return Result<bool>.Success(true);
    }
}
