using System.Text.RegularExpressions;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Domain.Common.Validation;

/// <summary>
/// Best-effort validation helpers for account/product data tied to compliance actions.
/// Adds warnings for missing/invalid values without blocking processing.
/// </summary>
public static class AccountValidator
{
    // Account number: digits only, typical ranges 6-20.
    private static readonly Regex AccountRegex = new(@"^\d{6,20}$", RegexOptions.Compiled);
    // Currency: ISO alpha-3
    private static readonly Regex CurrencyRegex = new(@"^[A-Z]{3}$", RegexOptions.Compiled);
    // SWIFT/BIC: 8 or 11 alphanumeric (if present in AdditionalData["Swift"] or Cuenta.Producto as a proxy)
    private static readonly Regex SwiftRegex = new(@"^[A-Za-z0-9]{8}([A-Za-z0-9]{3})?$", RegexOptions.Compiled);

    /// <summary>
    /// Flags account issues on a compliance action (best effort: warnings only).
    /// </summary>
    public static void FlagAccountIssues(ComplianceAction action)
    {
        if (action == null) return;

        // Only enforce account presence for actions that typically need it.
        var requiresAccount = action.ActionType == ComplianceActionKind.Block
                              || action.ActionType == ComplianceActionKind.Unblock
                              || action.ActionType == ComplianceActionKind.Transfer;

        if (requiresAccount)
        {
            var numero = action.Cuenta?.Numero ?? action.AccountNumber ?? string.Empty;
            action.Validation.WarnIf(!string.IsNullOrWhiteSpace(numero), "AccountMissing");

            if (!string.IsNullOrWhiteSpace(numero) && !AccountRegex.IsMatch(numero))
            {
                action.Validation.Warn("AccountInvalidFormat");
            }
        }

        if (action.Cuenta != null)
        {
            if (!string.IsNullOrWhiteSpace(action.Cuenta.Moneda) && !CurrencyRegex.IsMatch(action.Cuenta.Moneda))
            {
                action.Validation.Warn("CurrencyInvalid");
            }

            // If a SWIFT/BIC is provided in AdditionalData or Producto, validate its form.
            if (action.AdditionalData.TryGetValue("Swift", out var swiftObj) && swiftObj is string swift && !string.IsNullOrWhiteSpace(swift))
            {
                if (!SwiftRegex.IsMatch(swift))
                {
                    action.Validation.Warn("SwiftInvalid");
                }
            }
            else if (!string.IsNullOrWhiteSpace(action.Cuenta.Producto) && SwiftRegex.IsMatch(action.Cuenta.Producto))
            {
                // If Producto carries a SWIFT-like code, accept it; otherwise, no warning.
            }
        }
    }
}
