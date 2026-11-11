using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Tests.Domain.ValueObjects;

/// <summary>
/// Tests for LawMandatedFields value object.
/// Minimal coverage - proves properties exist and can be set/get.
/// </summary>
public class LawMandatedFieldsTests
{
    [Fact]
    public void Can_Create_Law_Mandated_Fields_With_Core_Identification()
    {
        // Arrange & Act
        var fields = new LawMandatedFields
        {
            InternalCaseId = Guid.NewGuid(),
            SourceAuthorityCode = "SAT-AGAF",
            ProcessingStatus = "Received"
        };

        // Assert
        fields.InternalCaseId.ShouldNotBeNull();
        fields.SourceAuthorityCode.ShouldBe("SAT-AGAF");
        fields.ProcessingStatus.ShouldBe("Received");
    }

    [Fact]
    public void Can_Create_Law_Mandated_Fields_With_Classification()
    {
        // Arrange & Act
        var fields = new LawMandatedFields
        {
            RequirementType = "Aseguramiento",
            RequirementTypeCode = 101
        };

        // Assert
        fields.RequirementType.ShouldBe("Aseguramiento");
        fields.RequirementTypeCode.ShouldBe(101);
    }

    [Fact]
    public void Can_Create_Law_Mandated_Fields_With_Financial_Data()
    {
        // Arrange & Act
        var fields = new LawMandatedFields
        {
            BranchCode = "0123",
            StateINEGI = 27,
            AccountNumber = "9876543210",
            ProductType = 101,
            Currency = "MXN",
            InitialBlockedAmount = 50000.00m,
            OperationAmount = 25000.00m,
            FinalBalance = 1500.50m
        };

        // Assert
        fields.BranchCode.ShouldBe("0123");
        fields.StateINEGI.ShouldBe(27);
        fields.AccountNumber.ShouldBe("9876543210");
        fields.ProductType.ShouldBe(101);
        fields.Currency.ShouldBe("MXN");
        fields.InitialBlockedAmount.ShouldBe(50000.00m);
        fields.OperationAmount.ShouldBe(25000.00m);
        fields.FinalBalance.ShouldBe(1500.50m);
    }

    [Fact]
    public void All_Fields_Are_Nullable_By_Default()
    {
        // Arrange & Act
        var fields = new LawMandatedFields();

        // Assert - all should be null (not set yet)
        fields.InternalCaseId.ShouldBeNull();
        fields.SourceAuthorityCode.ShouldBeNull();
        fields.ProcessingStatus.ShouldBeNull();
        fields.RequirementType.ShouldBeNull();
        fields.RequirementTypeCode.ShouldBeNull();
        fields.IsPrimaryTitular.ShouldBeNull();
        fields.BranchCode.ShouldBeNull();
        fields.StateINEGI.ShouldBeNull();
        fields.AccountNumber.ShouldBeNull();
        fields.ProductType.ShouldBeNull();
        fields.Currency.ShouldBeNull();
        fields.InitialBlockedAmount.ShouldBeNull();
        fields.OperationAmount.ShouldBeNull();
        fields.FinalBalance.ShouldBeNull();
    }

    [Fact]
    public void Has_Validation_State()
    {
        // Arrange & Act
        var fields = new LawMandatedFields();

        // Assert
        fields.Validation.ShouldNotBeNull();
    }
}
