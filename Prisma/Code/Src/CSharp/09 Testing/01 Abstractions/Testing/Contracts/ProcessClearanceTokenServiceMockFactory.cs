using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using IndQuestResults;
using IndQuestResults.Operations;
using NSubstitute;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Builds the contract-conforming NSubstitute mock that backs the blueprint instance of
/// <see cref="ProcessClearanceTokenServiceContract"/> (ADR-005 §6, MVP-PATH 1.5 A5).
/// </summary>
/// <remarks>
/// The configuration is the executable design specification for <see cref="IProcessClearanceTokenService"/>:
/// a pre-cancelled token yields a cancelled result; MintAsync returns a predictable opaque string; ValidateAsync
/// round-trips that string to fixed claims. The fuller stateful reference lives in
/// <see cref="FakeProcessClearanceTokenService"/>.
/// </remarks>
public static class ProcessClearanceTokenServiceMockFactory
{
    private const string MockToken = "mock-clearance-token";

    /// <summary>
    /// Creates an <see cref="IProcessClearanceTokenService"/> mock that satisfies every test in
    /// <see cref="ProcessClearanceTokenServiceContract"/>.
    /// </summary>
    /// <returns>The configured mock.</returns>
    public static IProcessClearanceTokenService CreateContractConformingMock()
    {
        var mock = Substitute.For<IProcessClearanceTokenService>();

        mock.MintAsync(
                Arg.Any<Domain.ValueObjects.SiaraActor>(),
                Arg.Any<ProcessClearance>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var ct = call.ArgAt<CancellationToken>(3);
                if (ct.IsCancellationRequested)
                    return ResultExtensions.Cancelled<string>();

                var actor = call.ArgAt<Domain.ValueObjects.SiaraActor>(0);
                var clearance = call.ArgAt<ProcessClearance>(1);
                var fileId = call.ArgAt<Guid>(2);
                return Result<string>.Success($"{MockToken}:{actor.ActorId}:{clearance}:{fileId:D}");
            });

        mock.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var ct = call.ArgAt<CancellationToken>(1);
                if (ct.IsCancellationRequested)
                    return ResultExtensions.Cancelled<ClearanceTokenClaims>();

                var token = call.ArgAt<string>(0);
                if (string.IsNullOrWhiteSpace(token) || !token.StartsWith(MockToken, StringComparison.Ordinal))
                    return Result<ClearanceTokenClaims>.WithFailure("Token not recognised by mock.");

                // Parse the token that MintAsync produced: "{MockToken}:{actorId}:{clearance}:{fileId}"
                var parts = token.Split(':');
                if (parts.Length < 4)
                    return Result<ClearanceTokenClaims>.WithFailure("Token format unexpected.");

                var actorId = parts[1];
                if (!System.Enum.TryParse<ProcessClearance>(parts[2], out var clearance))
                    return Result<ClearanceTokenClaims>.WithFailure("Clearance claim unrecognised.");
                if (!Guid.TryParse(parts[3], out var fileId))
                    return Result<ClearanceTokenClaims>.WithFailure("FileId claim unrecognised.");

                return Result<ClearanceTokenClaims>.Success(new ClearanceTokenClaims
                {
                    ActorId = actorId,
                    ActorType = Domain.Enum.SiaraActorType.ServiceAccount,
                    Clearance = clearance,
                    FileId = fileId,
                });
            });

        return mock;
    }
}
