namespace ExxerCube.Prisma.Veriqan.Web.UI.Options;

/// <summary>
/// Configuration for the demo live/canned decision (<see cref="Services.IDemoRunner"/>).
/// </summary>
/// <remarks>
/// Bound from the <c>Veriqan:Demo</c> configuration section via
/// <c>services.Configure&lt;DemoOptions&gt;(config.GetSection(DemoOptions.Section))</c>.
/// Overridable via <c>appsettings.json</c> or environment variables
/// (e.g. <c>Veriqan__Demo__LiveModeEnabled</c>).
/// </remarks>
public sealed class DemoOptions
{
    /// <summary>Configuration section path.</summary>
    public const string Section = "Veriqan:Demo";

    /// <summary>
    /// When <see langword="true"/> (default), <see cref="Services.IDemoRunner"/> attempts to run
    /// the real <c>IVerificationPipeline</c> before falling back to a canned case. When
    /// <see langword="false"/>, the runner always returns a canned <c>DemoStatementCase</c>
    /// without ever touching the live pipeline.
    /// </summary>
    public bool LiveModeEnabled { get; set; } = true;

    /// <summary>
    /// When <see langword="true"/> (default), a failed/timed-out/thrown live pipeline run falls
    /// back to a canned demo case instead of propagating the failure to the caller.
    /// </summary>
    public bool FallbackOnFailure { get; set; } = true;

    /// <summary>
    /// Maximum time allowed for a single live pipeline run before it is treated as timed out.
    /// Defaults to 20 seconds.
    /// </summary>
    public TimeSpan LiveTimeout { get; set; } = TimeSpan.FromSeconds(20);
}
