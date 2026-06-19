using System;
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
/// <param name="JobVerdictId">
/// The identifier of the persisted <see cref="Domain.Entities.JobVerdict"/> associated with
/// this alert.  When supplied, <c>VecAlertService</c> uses it to load the verdict and
/// check the <see cref="Domain.Entities.JobVerdict.AlertSentAt"/> flag before sending, ensuring
/// exactly-once delivery across retries.  <see langword="null"/> disables the dedup check
/// (backwards-compatible for callers that do not yet have a persisted verdict).
/// </param>
public sealed record AlertContext(
    string StatementId,
    IReadOnlyList<string> Recipients,
    Guid? JobVerdictId = null);
