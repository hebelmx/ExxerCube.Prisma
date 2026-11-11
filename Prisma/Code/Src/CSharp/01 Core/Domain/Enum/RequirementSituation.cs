// <copyright file="RequirementSituation.cs" company="Exxerpro Solutions SA de CV">
// Copyright (c) Exxerpro Solutions SA de CV. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Represents "The 5 Situations" - semantic classification of CNBV requirements.
/// Determines the processing workflow and bank system actions required.
/// </summary>
/// <remarks>
/// The 5 Situations framework provides semantic understanding beyond simple type classification:
///
/// Situation 1: Authority knows exact accounts → Direct query
/// Situation 2: Authority has RFC/CURP only → Account discovery required
/// Situation 3: Asset seizure order → Immediate freeze action
/// Situation 4: Unblocking order → Immediate unfreeze action
/// Situation 5: Transfer order → Execute transfer after unfreeze
///
/// This classification drives:
/// - Processing workflow selection
/// - SLA calculation
/// - Required bank system integrations
/// - Urgency and priority levels
/// </remarks>
public class RequirementSituation : EnumModel
{
    /// <summary>
    /// Situation 1: Authority knows specific account numbers.
    /// Processing: Direct account query, no discovery needed.
    /// SLA: Standard information request timeline.
    /// Keywords: AccountNumber populated, direct account reference.
    /// </summary>
    public static readonly RequirementSituation AccountsKnownByAuthority
        = new(1, "AccountsKnownByAuthority", "Cuentas Conocidas");

    /// <summary>
    /// Situation 2: Authority has only RFC/CURP, needs account discovery.
    /// Processing: Search across all products, compile comprehensive list.
    /// SLA: Extended timeline due to discovery requirement.
    /// Keywords: RFC/CURP populated, AccountNumber null.
    /// </summary>
    public static readonly RequirementSituation AccountsUnknownNeedDiscovery
        = new(2, "AccountsUnknownNeedDiscovery", "Cuentas Por Conocer");

    /// <summary>
    /// Situation 3: Asset seizure (aseguramiento) order.
    /// Processing: Immediate account freeze, notification generation.
    /// SLA: SAME DAY execution required.
    /// Keywords: TieneAseguramiento=true, "asegurar", "bloquear".
    /// Priority: CRITICAL - Immediate action required.
    /// </summary>
    public static readonly RequirementSituation AseguramientoOrdered
        = new(3, "AseguramientoOrdered", "Aseguramiento Ordenado");

    /// <summary>
    /// Situation 4: Unblocking (desbloqueo) order.
    /// Processing: Immediate account unfreeze, notify prior seizure lifted.
    /// SLA: 1-2 days typical.
    /// Keywords: "desbloquear", "liberar", reference to prior seizure.
    /// Priority: HIGH - Rapid action required.
    /// </summary>
    public static readonly RequirementSituation DesbloqueoOrdered
        = new(4, "DesbloqueoOrdered", "Desbloqueo Ordenado");

    /// <summary>
    /// Situation 5: Transfer order (electronic or physical).
    /// Processing: Execute transfer to authority account or issue cashier's check.
    /// SLA: 2-5 days (after unfreeze if needed).
    /// Keywords: "transferir", "CLABE", "cheque de caja".
    /// Priority: HIGH - Involves fund movement.
    /// </summary>
    public static readonly RequirementSituation TransferenciaOrdered
        = new(5, "TransferenciaOrdered", "Transferencia Ordenada");

    /// <summary>
    /// Unknown situation - classification failed.
    /// </summary>
    public static readonly RequirementSituation Unknown
        = new(999, "Unknown", "Desconocido");

    /// <summary>
    /// Initializes a new instance of the <see cref="RequirementSituation"/> class.
    /// Parameterless constructor required by EF Core.
    /// </summary>
    public RequirementSituation()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequirementSituation"/> class with specified values.
    /// </summary>
    private RequirementSituation(int value, string name, string displayName = "")
        : base(value, name, displayName)
    {
    }

    /// <summary>
    /// Creates a RequirementSituation instance from an integer value.
    /// </summary>
    public static RequirementSituation FromValue(int value) => FromValue<RequirementSituation>(value);

    /// <summary>
    /// Creates a RequirementSituation instance from a name.
    /// </summary>
    public static RequirementSituation FromName(string name) => FromName<RequirementSituation>(name);

    /// <summary>
    /// Creates a RequirementSituation instance from a display name.
    /// </summary>
    public static RequirementSituation FromDisplayName(string displayName) => FromDisplayName<RequirementSituation>(displayName);

    /// <summary>
    /// Implicit conversion to int.
    /// </summary>
    public static implicit operator int(RequirementSituation situation) => situation.Value;

    /// <summary>
    /// Implicit conversion from int.
    /// </summary>
    public static implicit operator RequirementSituation(int value) => FromValue(value);
}
