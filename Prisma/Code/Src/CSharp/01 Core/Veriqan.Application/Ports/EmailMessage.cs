using System.Collections.Generic;

namespace ExxerCube.Prisma.Veriqan.Application.Ports;

/// <summary>
/// An immutable value object representing an outbound email to be dispatched by
/// <see cref="IEmailSender"/>.
/// </summary>
/// <param name="To">
/// One or more recipient addresses.  Must contain at least one entry.
/// </param>
/// <param name="Subject">The email subject line (non-empty).</param>
/// <param name="Body">The plain-text email body.</param>
/// <param name="Attachments">
/// Optional named binary attachments (e.g. a colour-marked PDF).
/// Each entry is a <c>(FileName, Content)</c> pair.
/// Pass an empty list (or omit) when no attachments are needed.
/// </param>
public sealed record EmailMessage(
    IReadOnlyList<string> To,
    string Subject,
    string Body,
    IReadOnlyList<(string FileName, byte[] Content)>? Attachments = null);
