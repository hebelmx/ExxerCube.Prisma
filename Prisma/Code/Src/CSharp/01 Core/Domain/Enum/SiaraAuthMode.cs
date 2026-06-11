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
}
