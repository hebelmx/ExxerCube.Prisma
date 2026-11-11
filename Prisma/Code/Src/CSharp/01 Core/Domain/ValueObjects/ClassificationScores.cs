namespace ExxerCube.Prisma.Domain.ValueObjects;

/// <summary>
/// Represents detailed classification scoring information for document classification.
/// </summary>
public class ClassificationScores
{
    /// <summary>
    /// Gets or sets the score for Aseguramiento category (0-100).
    /// </summary>
    public int AseguramientoScore { get; set; }

    /// <summary>
    /// Gets or sets the score for Desembargo category (0-100).
    /// </summary>
    public int DesembargoScore { get; set; }

    /// <summary>
    /// Gets or sets the score for Documentacion category (0-100).
    /// </summary>
    public int DocumentacionScore { get; set; }

    /// <summary>
    /// Gets or sets the score for Informacion category (0-100).
    /// </summary>
    public int InformacionScore { get; set; }

    /// <summary>
    /// Gets or sets the score for Transferencia category (0-100).
    /// </summary>
    public int TransferenciaScore { get; set; }

    /// <summary>
    /// Gets or sets the score for OperacionesIlicitas category (0-100).
    /// </summary>
    public int OperacionesIlicitasScore { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ClassificationScores"/> class.
    /// </summary>
    public ClassificationScores()
    {
    }
}

