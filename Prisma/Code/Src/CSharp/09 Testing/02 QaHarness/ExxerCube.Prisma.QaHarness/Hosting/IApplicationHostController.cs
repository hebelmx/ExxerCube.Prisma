// <copyright file="IApplicationHostController.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Hosting;

/// <summary>
/// Manages the lifecycle of Prisma application processes during a QA harness run.
/// Implementations wrap <c>PrismaWebApplicationFactory</c> (Web UI) and/or the
/// Gate host trio (Orion + Athena + Reconciliator).
/// </summary>
/// <remarks>
/// Implement <see cref="IAsyncDisposable"/> — callers must <c>await using</c> or call
/// <see cref="StopAsync"/> explicitly.  Disposing without stopping is safe; the
/// implementation calls <see cref="StopAsync"/> from <c>DisposeAsync</c>.
/// </remarks>
public interface IApplicationHostController : IAsyncDisposable
{
    /// <summary>
    /// Starts the application processes described by <paramref name="options"/> and waits
    /// until health probes succeed or a timeout elapses.
    /// </summary>
    /// <param name="options">Configures which processes to start and their runtime settings.</param>
    /// <param name="cancellationToken">Token used to cancel the startup wait.</param>
    /// <returns>
    /// A successful <see cref="Result{T}"/> containing <see cref="ApplicationStartupResult"/>
    /// when at least one process bound a port; a failure result when startup cannot begin at all.
    /// </returns>
    Task<Result<ApplicationStartupResult>> StartAsync(
        HostingOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gracefully stops all running processes and releases their resources.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the shutdown wait.</param>
    /// <returns>A successful <see cref="Result"/> when all processes stopped; a failure result when one or more could not be stopped cleanly.</returns>
    Task<Result> StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the DI service provider of the primary hosted process after a successful
    /// <see cref="StartAsync"/>, or <see langword="null"/> before startup.
    /// </summary>
    IServiceProvider? Services { get; }

    /// <summary>
    /// Gets the base URI of the primary hosted process after a successful <see cref="StartAsync"/>,
    /// or <see langword="null"/> before startup or after failure.
    /// </summary>
    Uri? BaseAddress { get; }
}
