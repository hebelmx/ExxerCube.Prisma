using System.Collections.Generic;

namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// Carries the contextual data needed to compose and route a VEC alert email.
/// </summary>
/// <param name="StatementId">
/// Human-readable identifier for the statement being evaluated (e.g. a file name, job ID,
/// or SIARA statement reference).  Used in the email subject and body.
/// </param>
/// <param name="Recipients">
/// Ordered list of recipient email addresses for the alert.
/// Typically sourced from <c>AlertOptions.Recipients</c> in configuration.
/// Must contain at least one entry for an alert to be dispatched.
/// </param>
public sealed record AlertContext(
    string StatementId,
    IReadOnlyList<string> Recipients);
