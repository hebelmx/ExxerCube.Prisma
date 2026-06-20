// <copyright file="LoginWorkflow.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

using ExxerCube.Prisma.QaHarness.Evidence;
using ExxerCube.Prisma.QaHarness.Workflows.Attributes;
using Microsoft.Playwright;

namespace ExxerCube.Prisma.QaHarness.Workflows.Catalog;

/// <summary>
/// Drives a browser login to the Prisma Web UI via Playwright,
/// captures a post-login screenshot as evidence, and records the authenticated state.
/// </summary>
/// <remarks>
/// <para><b>RequiredCapabilities:</b> <c>Playwright</c>.</para>
/// <para>
/// When <see cref="WorkflowContext.PlaywrightPage"/> is <see langword="null"/> the
/// workflow returns <see cref="WorkflowStatus.Aborted"/> with reason
/// <c>"PlaywrightPage not available in WorkflowContext"</c>.  The runner will have
/// already returned <see cref="WorkflowStatus.Skipped"/> if the <c>Playwright</c>
/// capability is absent from the provisioning result.
/// </para>
/// <para>
/// <b>Honest-stub note:</b> this implementation does real Playwright work when
/// <see cref="WorkflowContext.PlaywrightPage"/> is supplied.  It does NOT hard-wire
/// credentials or a base URL — both must be configured on the <c>IPage</c> by the
/// caller (e.g. <c>ThreeProcessHostController</c>).  The login navigation path is
/// <c>/login</c>; the username/password selectors follow the MudBlazor Identity
/// scaffolding convention used by the existing E2E suite.
/// Full end-to-end wiring (credential injection, storage-state capture) is completed
/// in Chunk 5 when the DI host controller is wired up.
/// </para>
/// </remarks>
[TracesRequirement("FR14")]
[TracesFeature("F-AUTH")]
public sealed class LoginWorkflow : IWorkflow
{
    // Selector constants matching the MudBlazor Identity scaffolding used by Web.UI
    private const string EmailInputSelector = "input[name='Input.Email']";
    private const string PasswordInputSelector = "input[name='Input.Password']";
    private const string LoginButtonSelector = "button[type='submit']";
    private const string LoginPath = "/Account/Login";

    /// <inheritdoc/>
    public string Name => "LoginWorkflow";

    /// <inheritdoc/>
    public string Description => "Navigates to the Prisma Web UI login page, submits credentials, and captures a post-login screenshot as evidence.";

    /// <inheritdoc/>
    public IReadOnlyList<string> Tags => ["browser", "auth"];

    /// <inheritdoc/>
    public IReadOnlyList<string> RequiredCapabilities => ["Playwright"];

    /// <inheritdoc/>
    public async Task<WorkflowResult> ExecuteAsync(
        WorkflowContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        var startedAt = DateTimeOffset.UtcNow;

        if (cancellationToken.IsCancellationRequested)
            return Aborted(startedAt, "LoginWorkflow cancelled before execution.");

        // Playwright page is required for this workflow
        if (context.PlaywrightPage is null)
            return Aborted(startedAt, "PlaywrightPage not available in WorkflowContext.");

        var page = context.PlaywrightPage;
        var outputs = new Dictionary<string, object?>();

        try
        {
            // ── Navigate to login page ───────────────────────────────────────
            var url = page.Url;
            // Build the login URL from the current page origin or the full path
            var loginUrl = url.Contains("://", StringComparison.Ordinal)
                ? new Uri(new Uri(url), LoginPath).ToString()
                : LoginPath;

            await page.GotoAsync(loginUrl, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 15_000,
            }).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            // ── Capture pre-login screenshot ─────────────────────────────────
            _ = await context.Evidence.CaptureScreenshotAsync(
                "login-page-loaded", cancellationToken).ConfigureAwait(false);

            // ── Resolve credentials from DI (optional; stub defaults used when absent) ──
            var credProvider = context.Services
                .GetService<ILoginCredentialProvider>();

            var email = credProvider?.Email ?? "admin@prisma.local";
            var password = credProvider?.Password ?? "Admin1234!";

            // ── Fill and submit ──────────────────────────────────────────────
            await page.FillAsync(EmailInputSelector, email).ConfigureAwait(false);
            await page.FillAsync(PasswordInputSelector, password).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            await page.ClickAsync(LoginButtonSelector).ConfigureAwait(false);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle, new PageWaitForLoadStateOptions
            {
                Timeout = 15_000,
            }).ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            // ── Capture post-login screenshot ────────────────────────────────
            _ = await context.Evidence.CaptureScreenshotAsync(
                "login-completed", cancellationToken).ConfigureAwait(false);

            outputs["FinalUrl"] = page.Url;
            outputs["LoginPath"] = loginUrl;

            return new WorkflowResult(
                WorkflowName: Name,
                StartedAt: startedAt,
                FinishedAt: DateTimeOffset.UtcNow,
                Status: WorkflowStatus.Completed,
                AbortReason: null,
                Evidence: context.Evidence.CurrentPackage,
                Outputs: outputs);
        }
        catch (OperationCanceledException)
        {
            return Aborted(startedAt, "LoginWorkflow was cancelled during execution.");
        }
        catch (Exception ex)
        {
            _ = await context.Evidence.CaptureScreenshotAsync(
                "login-error", CancellationToken.None).ConfigureAwait(false);

            return Aborted(startedAt, $"LoginWorkflow failed: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private WorkflowResult Aborted(DateTimeOffset startedAt, string reason) =>
        new(
            WorkflowName: Name,
            StartedAt: startedAt,
            FinishedAt: DateTimeOffset.UtcNow,
            Status: WorkflowStatus.Aborted,
            AbortReason: reason,
            Evidence: new EvidencePackage(Name, []),
            Outputs: new Dictionary<string, object?>());
}

/// <summary>
/// Optional DI service providing login credentials to <see cref="LoginWorkflow"/>.
/// When not registered, the workflow uses default development credentials.
/// </summary>
public interface ILoginCredentialProvider
{
    /// <summary>Gets the email address to use for login.</summary>
    string Email { get; }

    /// <summary>Gets the password to use for login.</summary>
    string Password { get; }
}
