namespace ExxerCube.Prisma.Infrastructure.Imaging;

/// <summary>
/// Filter selection strategy type.
/// </summary>
public enum FilterSelectionStrategyType
{
    /// <summary>
    /// Simple default strategy with fixed parameters.
    /// </summary>
    Default,

    /// <summary>
    /// Analytical strategy using threshold-based decision trees (NSGA-II optimized).
    /// </summary>
    Analytical,

    /// <summary>
    /// Advanced polynomial regression strategy for continuous parameter prediction.
    /// Requires trained models from filtering study results.
    /// </summary>
    Polynomial
}