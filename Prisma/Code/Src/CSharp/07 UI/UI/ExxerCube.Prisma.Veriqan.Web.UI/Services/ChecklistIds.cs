namespace ExxerCube.Prisma.Veriqan.Web.UI.Services;

/// <summary>
/// Static registry of the 55 VEC checklist identifiers (CL-1…CL-55) with
/// human-readable labels and DOF numeral references used by the demo UI grid.
/// </summary>
internal static class ChecklistIds
{
    // Ordered list of all 55 check IDs used to build the grid.
    private static readonly string[] _ids = Enumerable
        .Range(1, 55)
        .Select(n => $"CL-{n}")
        .ToArray();

    /// <summary>All 55 check identifiers in checklist order.</summary>
    internal static IReadOnlyList<string> AllIds => _ids;

    /// <summary>Returns a short human-readable label for the given check ID.</summary>
    internal static string Label(string checkId) => checkId switch
    {
        // Group 1: Identificación del emisor (CL-1…CL-5)
        "CL-1" => "Nombre de la institución bancaria",
        "CL-2" => "Logotipo del banco en portada",
        "CL-3" => "Número de cuenta / contrato",
        "CL-4" => "Nombre del titular en el encabezado",
        "CL-5" => "CLABE interbancaria declarada",
        // Group 2: Datos del período (CL-6…CL-10)
        "CL-6" => "Fecha de corte presente",
        "CL-7" => "Período de facturación correcto",
        "CL-8" => "Fecha límite de pago",
        "CL-9" => "Días del período declarados",
        "CL-10" => "Año del estado de cuenta",
        // Group 3: Saldos y aritmética (CL-11…CL-22)
        "CL-11" => "Saldo inicial declarado",
        "CL-12" => "Saldo final declarado",
        "CL-13" => "Límite de crédito declarado",
        "CL-14" => "Crédito disponible = Límite − Saldo",
        "CL-15" => "Pago mínimo declarado",
        "CL-16" => "Número de columnas de movimientos",
        "CL-17" => "Suma de cargos cuadra",
        "CL-18" => "Suma de abonos cuadra",
        "CL-19" => "Continuidad saldo anterior",
        "CL-20" => "Firma de secciones de resumen",
        "CL-21" => "Aritmética saldo final (cargos vs abonos)",
        "CL-22" => "Redondeo permitido (≤ 2 centavos)",
        // Group 4: Movimientos (CL-23…CL-32)
        "CL-23" => "Fecha de cada movimiento presente",
        "CL-24" => "Descripción de cada movimiento",
        "CL-25" => "Importe de cada movimiento",
        "CL-26" => "Referencia de cada transacción",
        "CL-27" => "Signo de cargo/abono coherente",
        "CL-28" => "Pagos interbancarios identificados",
        "CL-29" => "MSI — cuotas y tasa declaradas",
        "CL-30" => "Reversos vinculados a cargos",
        "CL-31" => "Paginación en pie de página",
        "CL-32" => "Correlación de páginas continua",
        // Group 5: Identidad visual (CL-33…CL-40)
        "CL-33" => "Color primario de marca",
        "CL-34" => "Imagen de la tarjeta en página 1",
        "CL-35" => "Tipo de fuente (Aptos requerido)",
        "CL-36" => "Tamaño mínimo de fuente (10 pt)",
        "CL-37" => "Contraste texto/fondo ≥ 4.5:1",
        "CL-38" => "Formato A4 o carta declarado",
        "CL-39" => "Márgenes dentro de tolerancia",
        "CL-40" => "Marca de agua de seguridad",
        // Group 6: Leyendas regulatorias (CL-41…CL-50)
        "CL-41" => "Leyenda CONDUSEF presente",
        "CL-42" => "Teléfono CONDUSEF correcto",
        "CL-43" => "URL CONDUSEF correcta",
        "CL-44" => "Leyenda CNBV presente",
        "CL-45" => "Leyenda de protección de datos (LFPDPPP)",
        "CL-46" => "Leyenda de cuotas de intercambio",
        "CL-47" => "Leyenda de CETES/GAT presente",
        "CL-48" => "Idioma español en todo el documento",
        "CL-49" => "Moneda declarada (MXN)",
        "CL-50" => "Código de barras o QR legible",
        // Group 7: Campos opcionales / avanzados (CL-51…CL-55)
        "CL-51" => "Desglose de intereses ordinarios",
        "CL-52" => "Desglose de intereses moratorios",
        "CL-53" => "Número de acreditados adicionales",
        "CL-54" => "Firma digital del emisor",
        "CL-55" => "Hash de integridad en pie de página",
        _ => checkId,
    };

    /// <summary>Returns the DOF regulation numeral for the given check, or an empty string.</summary>
    internal static string DofNumeral(string checkId) => checkId switch
    {
        "CL-1" or "CL-2" => "§4",
        "CL-3" or "CL-4" or "CL-5" => "§5",
        "CL-6" or "CL-7" or "CL-8" or "CL-9" or "CL-10" => "§6",
        "CL-11" or "CL-12" or "CL-13" or "CL-14" or "CL-15" => "§8",
        "CL-16" => "§16",
        "CL-17" or "CL-18" or "CL-19" or "CL-20" or "CL-21" or "CL-22" => "§9",
        "CL-23" or "CL-24" or "CL-25" or "CL-26" or "CL-27" => "§10",
        "CL-28" or "CL-29" or "CL-30" => "§11",
        "CL-31" or "CL-32" => "§12",
        "CL-33" or "CL-34" or "CL-35" or "CL-36" or "CL-37" => "§20",
        "CL-38" or "CL-39" or "CL-40" => "§21",
        "CL-41" or "CL-42" or "CL-43" => "§22",
        "CL-44" => "§23",
        "CL-45" => "§24",
        "CL-46" or "CL-47" or "CL-48" or "CL-49" or "CL-50" => "§25",
        _ => string.Empty,
    };
}
