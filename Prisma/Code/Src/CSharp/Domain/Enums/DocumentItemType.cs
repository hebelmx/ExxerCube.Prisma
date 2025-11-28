namespace ExxerCube.Prisma.Domain.Enums;

/// <summary>
/// Represents types of documents commonly requested by authorities.
/// </summary>
public enum DocumentItemType
{
    /// <summary>Document type not determined.</summary>
    Unknown = 0,
    /// <summary>Account statement.</summary>
    EstadoCuenta,
    /// <summary>Contract document.</summary>
    Contrato,
    /// <summary>Identification document.</summary>
    Identificacion,
    /// <summary>Proof of address.</summary>
    ComprobanteDomicilio,
    /// <summary>Signature sample.</summary>
    MuestraFirma,
    /// <summary>Cheque image.</summary>
    ImagenCheque,
    /// <summary>Account opening file.</summary>
    ExpedienteApertura,
    /// <summary>Document type outside the known list.</summary>
    Other
}
