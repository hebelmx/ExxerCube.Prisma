// <copyright file="DocumentItemKind.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// SmartEnum for requested document items.
/// </summary>
public sealed class DocumentItemKind : EnumModel
{
    /// <summary>Document type not determined.</summary>
    public static readonly DocumentItemKind Unknown = new(0, "Unknown", "Desconocido");

    /// <summary>Account statement.</summary>
    public static readonly DocumentItemKind EstadoCuenta = new(1, "EstadoCuenta", "Estado de Cuenta");

    /// <summary>Contract document.</summary>
    public static readonly DocumentItemKind Contrato = new(2, "Contrato", "Contrato");

    /// <summary>Identification document.</summary>
    public static readonly DocumentItemKind Identificacion = new(3, "Identificacion", "Identificación");

    /// <summary>Proof of address.</summary>
    public static readonly DocumentItemKind ComprobanteDomicilio = new(4, "ComprobanteDomicilio", "Comprobante de Domicilio");

    /// <summary>Signature sample.</summary>
    public static readonly DocumentItemKind MuestraFirma = new(5, "MuestraFirma", "Muestra de Firma");

    /// <summary>Cheque image.</summary>
    public static readonly DocumentItemKind ImagenCheque = new(6, "ImagenCheque", "Imagen de Cheque");

    /// <summary>Account opening file.</summary>
    public static readonly DocumentItemKind ExpedienteApertura = new(7, "ExpedienteApertura", "Expediente de Apertura");

    /// <summary>Document type outside the known list.</summary>
    public static readonly DocumentItemKind Other = new(999, "Other", "Otro");

    /// <summary>
    /// Initializes a new instance of the <see cref="DocumentItemKind"/> class.
    /// </summary>
    public DocumentItemKind()
    {
    }

    private DocumentItemKind(int value, string name, string displayName)
        : base(value, name, displayName)
    {
    }

    /// <summary>
    /// Creates a DocumentItemKind from an integer value.
    /// </summary>
    /// <param name="value">Stored integer value.</param>
    public static DocumentItemKind FromValue(int value) => FromValue<DocumentItemKind>(value);

    /// <summary>
    /// Creates a DocumentItemKind from a name.
    /// </summary>
    /// <param name="name">Internal name.</param>
    public static DocumentItemKind FromName(string name) => FromName<DocumentItemKind>(name);

    /// <summary>
    /// Implicit conversion to int for storage/serialization.
    /// </summary>
    /// <param name="value">The DocumentItemKind to convert.</param>
    public static implicit operator int(DocumentItemKind value) => value.Value;

    /// <summary>
    /// Implicit conversion from int for convenience.
    /// </summary>
    /// <param name="value">Stored integer value.</param>
    public static implicit operator DocumentItemKind(int value) => FromValue(value);
}
