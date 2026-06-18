using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Reporting;

/// <summary>
/// <see cref="System.Net.Mail"/>-based implementation of <see cref="IEmailSender"/>.
/// All send failures are returned as typed <see cref="Result"/> failures — this class never throws.
/// </summary>
/// <remarks>
/// <para>
/// SMTP credentials and host are sourced from <see cref="SmtpOptions"/> (bound from
/// <c>"Veriqan:Smtp"</c> in configuration).  When <see cref="SmtpOptions.UserName"/> is
/// null or empty the client is used in unauthenticated relay mode.
/// </para>
/// <para>
/// This class is intentionally kept minimal: retry logic lives in the calling
/// <see cref="VecAlertService"/>, not here.
/// </para>
/// </remarks>
public sealed class SmtpEmailSender : IEmailSender
{
    private readonly SmtpOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="SmtpEmailSender"/>.
    /// </summary>
    /// <param name="options">Snapshot of the current SMTP options.</param>
    /// <param name="logger">Logger for diagnostic output.</param>
    public SmtpEmailSender(IOptions<SmtpOptions> options, ILogger<SmtpEmailSender> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<Result> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("SmtpEmailSender.SendAsync cancelled before starting for subject {Subject}.", message.Subject);
            return ResultExtensions.Cancelled();
        }

        try
        {
            using var client = BuildSmtpClient();
            using var mailMessage = BuildMailMessage(message);

            _logger.LogDebug(
                "Sending email via {Host}:{Port} — Subject: {Subject}, Recipients: {RecipientCount}.",
                _options.Host, _options.Port, message.Subject, message.To.Count);

            await client.SendMailAsync(mailMessage, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Email dispatched — Subject: {Subject}, Recipients: {RecipientCount}.",
                message.Subject, message.To.Count);

            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug("SmtpEmailSender.SendAsync cancelled during send for subject {Subject}.", message.Subject);
            return ResultExtensions.Cancelled();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "SMTP send failed for subject {Subject} via {Host}:{Port}: {Error}",
                message.Subject, _options.Host, _options.Port, ex.Message);

            return Result.WithFailure($"SMTP send failed: {ex.Message}", ex);
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private SmtpClient BuildSmtpClient()
    {
        var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
        };

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        }

        return client;
    }

    private MailMessage BuildMailMessage(EmailMessage message)
    {
        var mail = new MailMessage
        {
            From = new MailAddress(_options.From),
            Subject = message.Subject,
            Body = message.Body,
            IsBodyHtml = false,
        };

        foreach (var to in message.To)
        {
            mail.To.Add(to);
        }

        if (message.Attachments is not null)
        {
            foreach (var (fileName, content) in message.Attachments)
            {
                // MemoryStream is disposed when MailMessage is disposed (BCL handles this).
                var stream = new System.IO.MemoryStream(content);
                mail.Attachments.Add(new Attachment(stream, fileName));
            }
        }

        return mail;
    }
}
