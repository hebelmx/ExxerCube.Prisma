using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Tests.Domain.Domain.Entities;

/// <summary>
/// Best-effort validation for account data tied to compliance actions.
/// </summary>
public class ComplianceActionValidationTests
{
    [Fact]
    public void FlagAccountIssues_ShouldWarnWhenAccountMissingForBlock()
    {
        var action = new ComplianceAction
        {
            ActionType = ComplianceActionKind.Block,
            Cuenta = null,
            AccountNumber = null
        };

        AccountValidator.FlagAccountIssues(action);

        action.Validation.Warnings.ShouldContain("AccountMissing");
        action.Validation.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void FlagAccountIssues_ShouldWarnOnInvalidAccountFormat()
    {
        var action = new ComplianceAction
        {
            ActionType = ComplianceActionKind.Transfer,
            Cuenta = new Cuenta { Numero = "ABC123INVALID" }
        };

        AccountValidator.FlagAccountIssues(action);

        action.Validation.Warnings.ShouldContain("AccountInvalidFormat");
    }

    [Fact]
    public void FlagAccountIssues_ShouldWarnOnInvalidCurrency()
    {
        var action = new ComplianceAction
        {
            ActionType = ComplianceActionKind.Transfer,
            Cuenta = new Cuenta { Numero = "1234567890", Moneda = "USDX" }
        };

        AccountValidator.FlagAccountIssues(action);

        action.Validation.Warnings.ShouldContain("CurrencyInvalid");
    }

    [Fact]
    public void FlagAccountIssues_ShouldWarnOnInvalidSwift()
    {
        var action = new ComplianceAction
        {
            ActionType = ComplianceActionKind.Transfer,
            Cuenta = new Cuenta { Numero = "1234567890" },
            AdditionalData = new Dictionary<string, object> { { "Swift", "BAD" } }
        };

        AccountValidator.FlagAccountIssues(action);

        action.Validation.Warnings.ShouldContain("SwiftInvalid");
    }

    [Fact]
    public void FlagAccountIssues_ShouldStayCleanWhenDataIsValid()
    {
        var action = new ComplianceAction
        {
            ActionType = ComplianceActionKind.Transfer,
            Cuenta = new Cuenta { Numero = "123456789012", Moneda = "USD" },
            AdditionalData = new Dictionary<string, object> { { "Swift", "AAAABBCCDDD" } }
        };

        AccountValidator.FlagAccountIssues(action);

        action.Validation.Warnings.ShouldBeEmpty();
        action.Validation.IsValid.ShouldBeTrue();
    }
}
