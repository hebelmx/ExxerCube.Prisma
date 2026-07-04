namespace ExxerCube.Prisma.Veriqan.Web.UI.Services;

/// <summary>Thread-safe singleton implementation of <see cref="IPipelineReadiness"/>.</summary>
public sealed class PipelineReadiness : IPipelineReadiness
{
    private volatile bool _isReady;

    /// <inheritdoc />
    public bool IsReady => _isReady;

    /// <inheritdoc />
    public void MarkReady() => _isReady = true;
}
