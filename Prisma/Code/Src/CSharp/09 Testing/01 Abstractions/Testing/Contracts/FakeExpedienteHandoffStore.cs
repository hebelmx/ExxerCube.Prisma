using System.Collections.Concurrent;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Services;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Hand-written reference fake of <see cref="IExpedienteHandoffStore"/> (ADR-005 §6): an in-memory handoff
/// store with a configurable storage base and no Infrastructure dependency, so Domain- and Processing-layer
/// tests can exercise the Extractor → Reconciliator handoff without a real volume (MVP-PATH 1.4, ADR-011).
/// </summary>
/// <remarks>
/// It deliberately delegates path validation to the same <see cref="StoragePathResolution"/> primitive as the
/// production <c>SharedStoragePathResolver</c> / <c>FileSystemExpedienteHandoffStore</c>: the security-sensitive
/// confinement guard is single-sourced, so the fake can never drift from real behavior on the traversal check.
/// Storage is an in-memory map keyed by the resolved absolute path; the contract verifies round-trip and the
/// fail-closed cases.
/// </remarks>
public sealed class FakeExpedienteHandoffStore : IExpedienteHandoffStore
{
    /// <summary>A deterministic, platform-rooted default base so resolution succeeds without configuration.</summary>
    public static readonly string DefaultBasePath = Path.Combine(Path.GetTempPath(), "prisma-fake-handoff");

    private readonly string _basePath;
    private readonly ConcurrentDictionary<string, Expediente> _store = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Initializes the fake.</summary>
    /// <param name="basePath">The storage base to resolve against; defaults to <see cref="DefaultBasePath"/>.</param>
    public FakeExpedienteHandoffStore(string? basePath = null)
    {
        _basePath = string.IsNullOrWhiteSpace(basePath) ? DefaultBasePath : basePath!;
    }

    /// <inheritdoc />
    public Task<Result<string>> SaveAsync(Expediente expediente, string relativeStoragePath, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<string>());
        }

        if (expediente is null)
        {
            return Task.FromResult(Result<string>.WithFailure("Expediente cannot be null"));
        }

        var resolved = StoragePathResolution.Resolve(_basePath, relativeStoragePath);
        if (resolved.IsFailure)
        {
            return Task.FromResult(Result<string>.WithFailure(resolved.Errors));
        }

        _store[resolved.Value!] = expediente;
        return Task.FromResult(Result<string>.Success(relativeStoragePath));
    }

    /// <inheritdoc />
    public Task<Result<Expediente>> LoadAsync(string relativeStoragePath, CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return Task.FromResult(ResultExtensions.Cancelled<Expediente>());
        }

        var resolved = StoragePathResolution.Resolve(_basePath, relativeStoragePath);
        if (resolved.IsFailure)
        {
            return Task.FromResult(Result<Expediente>.WithFailure(resolved.Errors));
        }

        return Task.FromResult(_store.TryGetValue(resolved.Value!, out var expediente)
            ? Result<Expediente>.Success(expediente)
            : Result<Expediente>.WithFailure($"No handoff artifact at '{relativeStoragePath}'"));
    }
}
