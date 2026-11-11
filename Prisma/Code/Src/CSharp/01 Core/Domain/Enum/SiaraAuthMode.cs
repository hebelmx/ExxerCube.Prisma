namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// The sanctioned, credential-free ways to obtain an authenticated SIARA browser session.
/// </summary>
/// <remarks>
/// SIARA exposes no API and no auth API — login is a basic web form, and (owner constraint)
/// the system must <strong>never</strong> hold raw credentials. The chosen mode is a
/// deployment-time, client-controlled <em>technical-legal control</em> selected via configuration;
/// it keys the registered <c>ISiaraSessionProvider</c> implementation. See ADR-010.
/// </remarks>
public enum SiaraAuthMode
{
    /// <summary>
    /// The downloader rides an already-authenticated browser session handed to it from outside
    /// Prisma (attach to a live context, or import its storage-state). Prisma never authenticates.
    /// </summary>
    SessionPassthrough = 0,

    /// <summary>
    /// A human authenticates once in a headed browser on the real SIARA page; Prisma captures the
    /// resulting authenticated context and keeps it warm for subsequent headless downloads.
    /// Credentials are typed into SIARA's own page, never into Prisma.
    /// </summary>
    InteractiveLogin = 1,

    /// <summary>
    /// Prisma drives SIARA's login form unattended with credentials sourced at runtime from a
    /// client-owned secret store (Key Vault / environment / secrets manager) — used transiently and
    /// <strong>never persisted</strong> by Prisma. Enables 24/7 operation without a human; the
    /// resulting session is captured as a credential-free handle (the no-raw-credential-storage rule
    /// is preserved). See ADR-010.
    /// </summary>
    AutomatedLogin = 2,
}
