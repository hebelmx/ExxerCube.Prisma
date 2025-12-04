namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Strategy confidence information.
/// </summary>
/// <param name="StrategyName">Name of the strategy.</param>
/// <param name="Confidence">Confidence score (0-100).</param>
public record StrategyConfidence(string StrategyName, int Confidence);