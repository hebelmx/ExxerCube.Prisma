using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Adapters;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Validation;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Tests;

/// <summary>
/// Tests for the six previously-null sections added to <see cref="CsvReferenceDataAdapter"/>:
/// MandatoryLegends, SequentialImages, Promotions, ClientAccounts, PriorStatements, ExpectedTransactions.
/// </summary>
public sealed class CsvReferenceDataAdapterExtendedSectionsTests
{
    private static string CsvRootPath =>
        Path.Combine(AppContext.BaseDirectory, "TestAssets", "csv");

    private static CsvReferenceDataAdapter CreateAdapter(string? rootDir = null)
    {
        var options = Options.Create(new CsvReferenceDataOptions
        {
            RootDirectory = rootDir ?? CsvRootPath
        });
        var validator = new ReferenceBundleSchemaValidator();
        var logger = NullLogger<CsvReferenceDataAdapter>.Instance;
        return new CsvReferenceDataAdapter(options, validator, logger);
    }

    // ── MandatoryLegends ────────────────────────────────────────────────────

    [Fact]
    public async Task GetBundleAsync_MandatoryLegendsCsv_PopulatesSection()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetBundleAsync(key, ct);

        result.IsSuccess.ShouldBeTrue($"Expected success: {result.Error}");
        var legends = result.Value!.MandatoryLegends;
        legends.ShouldNotBeNull();
        legends!.Count.ShouldBe(1);
        legends[0].LegendId.ShouldBe("repr-impresa");
        legends[0].Text.ShouldBe("ESTE DOCUMENTO ES UNA REPRESENTACIÓN IMPRESA SIN VALIDEZ FISCAL");
        legends[0].MatchMode.ShouldBe("normalized");
        legends[0].Section.ShouldBe("fiscal");
        legends[0].Required.ShouldBe(true);
    }

    [Fact]
    public async Task GetBundleAsync_MandatoryLegendsCsv_BundlePassesSchemaValidation()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetBundleAsync(key, ct);

        result.IsSuccess.ShouldBeTrue($"Bundle with mandatory-legends should pass schema: {result.Error}");
    }

    [Fact]
    public async Task GetBundleAsync_MissingMandatoryLegendsCsv_SectionIsNull_BundleValid()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempRoot = CreateTempInstitutionDir("No_Legends_Bank", out var instDir);
        try
        {
            WriteMinimalMetadata(instDir, "No Legends Bank");
            var adapter = CreateAdapter(tempRoot);
            var key = new StatementContextKey("No Legends Bank");

            var result = await adapter.GetBundleAsync(key, ct);

            result.IsSuccess.ShouldBeTrue($"Missing mandatory-legends.csv must not fail: {result.Error}");
            result.Value!.MandatoryLegends.ShouldBeNull();
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    // ── SequentialImages ────────────────────────────────────────────────────

    [Fact]
    public async Task GetBundleAsync_SequentialImagesCsv_PopulatesSection()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetBundleAsync(key, ct);

        result.IsSuccess.ShouldBeTrue($"Expected success: {result.Error}");
        var images = result.Value!.SequentialImages;
        images.ShouldNotBeNull();
        images!.Count.ShouldBe(2);

        var first = images.Single(i => i.Order == 1);
        first.ProductId.ShouldBe("TC-NL");
        first.Image.ImageId.ShouldBe("seq-nl-1");
        first.Image.Uri.ShouldBe("catalog/sequential/nl-1.png");

        var second = images.Single(i => i.Order == 2);
        second.Image.ImageId.ShouldBe("seq-nl-2");
    }

    [Fact]
    public async Task GetBundleAsync_SequentialImagesCsv_BundlePassesSchemaValidation()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetBundleAsync(key, ct);

        result.IsSuccess.ShouldBeTrue($"Bundle with sequential-images should pass schema: {result.Error}");
    }

    [Fact]
    public async Task GetBundleAsync_MissingSequentialImagesCsv_SectionIsNull_BundleValid()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempRoot = CreateTempInstitutionDir("No_Images_Bank", out var instDir);
        try
        {
            WriteMinimalMetadata(instDir, "No Images Bank");
            var adapter = CreateAdapter(tempRoot);
            var key = new StatementContextKey("No Images Bank");

            var result = await adapter.GetBundleAsync(key, ct);

            result.IsSuccess.ShouldBeTrue($"Missing sequential-images.csv must not fail: {result.Error}");
            result.Value!.SequentialImages.ShouldBeNull();
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    // ── Promotions ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBundleAsync_PromotionsCsv_PopulatesSection()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetBundleAsync(key, ct);

        result.IsSuccess.ShouldBeTrue($"Expected success: {result.Error}");
        var promotions = result.Value!.Promotions;
        promotions.ShouldNotBeNull();
        promotions!.Count.ShouldBe(1);
        var promo = promotions[0];
        promo.PromotionId.ShouldBe("promo-sep-2025-a");
        promo.ValidFrom.ShouldBe("2025-09-01");
        promo.ValidTo.ShouldBe("2025-09-30");
        promo.Image.ImageId.ShouldBe("promo-a");
        promo.Image.Uri.ShouldBe("catalog/promotions/sep-a.png");
        promo.AppliesToProducts.ShouldNotBeNull();
        promo.AppliesToProducts!.ShouldContain("TC-NL");
    }

    [Fact]
    public async Task GetBundleAsync_PromotionsCsv_BundlePassesSchemaValidation()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetBundleAsync(key, ct);

        result.IsSuccess.ShouldBeTrue($"Bundle with promotions should pass schema: {result.Error}");
    }

    [Fact]
    public async Task GetBundleAsync_MissingPromotionsCsv_SectionIsNull_BundleValid()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempRoot = CreateTempInstitutionDir("No_Promos_Bank", out var instDir);
        try
        {
            WriteMinimalMetadata(instDir, "No Promos Bank");
            var adapter = CreateAdapter(tempRoot);
            var key = new StatementContextKey("No Promos Bank");

            var result = await adapter.GetBundleAsync(key, ct);

            result.IsSuccess.ShouldBeTrue($"Missing promotions.csv must not fail: {result.Error}");
            result.Value!.Promotions.ShouldBeNull();
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    // ── ClientAccounts ──────────────────────────────────────────────────────

    [Fact]
    public async Task GetBundleAsync_ClientAccountsCsv_PopulatesSection()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetBundleAsync(key, ct);

        result.IsSuccess.ShouldBeTrue($"Expected success: {result.Error}");
        var clients = result.Value!.ClientAccounts;
        clients.ShouldNotBeNull();
        clients!.Count.ShouldBe(1);

        var client = clients[0];
        client.ClientId.ShouldBe("CLI-0001");
        client.ClientName.ShouldNotBeNull();
        client.ClientName!.FirstNames.ShouldBe("Eugenio");
        client.ClientName.LastNames.ShouldBe("Garcia Zavala");
        client.ClientName.Full.ShouldBe("Eugenio Garcia Zavala");
        client.Rfc.ShouldBe("GAZE800101AAA");
        client.ClientNumber.ShouldBe("1234567");
        client.Address.ShouldNotBeNull();
        client.Address!.Street.ShouldBe("Av. Cumbres");
        client.Address.PostalCode.ShouldBe("64610");
        client.Address.State.ShouldBe("Nuevo Leon");

        // Accounts (from client-accounts-entries.csv)
        client.Accounts.ShouldNotBeNull();
        client.Accounts!.Count.ShouldBe(1);
        var account = client.Accounts[0];
        account.AccountRef.ShouldBe("ACC-0001");
        account.ProductId.ShouldBe("TC-NL");
        account.CardNumber.ShouldBe("5512 34** **** 7890");
        account.Clabe.ShouldBe("012345678901234567");
        account.BranchNumber.ShouldBe("0420");
        account.CreditLine.ShouldBe(100000m);
        account.AccountOpenDate.ShouldBe("2020-01-15");
    }

    [Fact]
    public async Task GetBundleAsync_ClientAccountsCsv_BundlePassesSchemaValidation()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetBundleAsync(key, ct);

        result.IsSuccess.ShouldBeTrue($"Bundle with clientAccounts should pass schema: {result.Error}");
    }

    [Fact]
    public async Task GetBundleAsync_MissingClientAccountsCsv_SectionIsNull_BundleValid()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempRoot = CreateTempInstitutionDir("No_Clients_Bank", out var instDir);
        try
        {
            WriteMinimalMetadata(instDir, "No Clients Bank");
            var adapter = CreateAdapter(tempRoot);
            var key = new StatementContextKey("No Clients Bank");

            var result = await adapter.GetBundleAsync(key, ct);

            result.IsSuccess.ShouldBeTrue($"Missing client-accounts.csv must not fail: {result.Error}");
            result.Value!.ClientAccounts.ShouldBeNull();
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GetBundleAsync_ClientAccountsCsv_NoEntriesFile_AccountsAreNull()
    {
        // client-accounts.csv present, client-accounts-entries.csv absent
        var ct = TestContext.Current.CancellationToken;
        var tempRoot = CreateTempInstitutionDir("No_Entries_Bank", out var instDir);
        try
        {
            WriteMinimalMetadata(instDir, "No Entries Bank");
            File.WriteAllText(
                Path.Combine(instDir, "client-accounts.csv"),
                "clientId,firstNames,lastNames,fullName,rfc,clientNumber,street,number,neighborhood,postalCode,state\n" +
                "CLI-0001,Test,User,Test User,RFC001,9999,,,,,\n");

            var adapter = CreateAdapter(tempRoot);
            var key = new StatementContextKey("No Entries Bank");

            var result = await adapter.GetBundleAsync(key, ct);

            result.IsSuccess.ShouldBeTrue($"Client without entries-file must not fail: {result.Error}");
            var clients = result.Value!.ClientAccounts;
            clients.ShouldNotBeNull();
            clients![0].Accounts.ShouldBeNull("Accounts should be null when entries file is absent");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    // ── PriorStatements ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetBundleAsync_PriorStatementsCsv_PopulatesSection()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetBundleAsync(key, ct);

        result.IsSuccess.ShouldBeTrue($"Expected success: {result.Error}");
        var statements = result.Value!.PriorStatements;
        statements.ShouldNotBeNull();
        statements!.Count.ShouldBe(1);

        var stmt = statements[0];
        stmt.AccountRef.ShouldBe("ACC-0001");
        stmt.Period.Label.ShouldBe("Ago-Sep 2025");
        stmt.Period.Start.ShouldBe("2025-08-01");
        stmt.Period.End.ShouldBe("2025-08-31");

        stmt.ClosingBalances.ShouldNotBeNull();
        stmt.ClosingBalances!.PagoParaNoGenerarIntereses.ShouldBe(18540.32m);
        stmt.ClosingBalances.SaldoDeudorTotal.ShouldBe(18540.32m);
        stmt.ClosingBalances.RewardsPointsBalance.ShouldBe(5400m);
        stmt.ClosingBalances.RewardsPesosBalance.ShouldBe(540.0m);

        stmt.DocumentRef.ShouldNotBeNull();
        stmt.DocumentRef!.Uri.ShouldBe("PRP2/01+Dummie+VEC+jul_ago+20252.pdf");

        // Installments (from prior-statements-installments.csv)
        stmt.Installments.ShouldNotBeNull();
        stmt.Installments!.Count.ShouldBe(1);
        var inst = stmt.Installments[0];
        inst.PurchaseId.ShouldBe("MSI-DONCOLCHON");
        inst.Description.ShouldBe("DON COLCHON CUMBRES");
        inst.SaldoPendiente.ShouldBe(2914.38m);
        inst.PagoRequerido.ShouldBe(485.73m);
        inst.NumeroDePago.ShouldBe(5);
        inst.TotalPagos.ShouldBe(12);
    }

    [Fact]
    public async Task GetBundleAsync_PriorStatementsCsv_BundlePassesSchemaValidation()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetBundleAsync(key, ct);

        result.IsSuccess.ShouldBeTrue($"Bundle with priorStatements should pass schema: {result.Error}");
    }

    [Fact]
    public async Task GetBundleAsync_MissingPriorStatementsCsv_SectionIsNull_BundleValid()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempRoot = CreateTempInstitutionDir("No_Prior_Bank", out var instDir);
        try
        {
            WriteMinimalMetadata(instDir, "No Prior Bank");
            var adapter = CreateAdapter(tempRoot);
            var key = new StatementContextKey("No Prior Bank");

            var result = await adapter.GetBundleAsync(key, ct);

            result.IsSuccess.ShouldBeTrue($"Missing prior-statements.csv must not fail: {result.Error}");
            result.Value!.PriorStatements.ShouldBeNull();
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    [Fact]
    public async Task GetBundleAsync_PriorStatementsCsv_NoInstallmentsFile_InstallmentsAreNull()
    {
        // prior-statements.csv present, prior-statements-installments.csv absent
        var ct = TestContext.Current.CancellationToken;
        var tempRoot = CreateTempInstitutionDir("No_Install_Bank", out var instDir);
        try
        {
            WriteMinimalMetadata(instDir, "No Install Bank");
            File.WriteAllText(
                Path.Combine(instDir, "prior-statements.csv"),
                "accountRef,periodLabel,periodStart,periodEnd,pagoParaNoGenerarIntereses,saldoDeudorTotal,rewardsPointsBalance,rewardsPesosBalance,documentRefUri,documentRefSha256\n" +
                "ACC-0001,Aug 2025,2025-08-01,2025-08-31,100.00,100.00,,,, \n");

            var adapter = CreateAdapter(tempRoot);
            var key = new StatementContextKey("No Install Bank");

            var result = await adapter.GetBundleAsync(key, ct);

            result.IsSuccess.ShouldBeTrue($"Prior statement without installments file must not fail: {result.Error}");
            var statements = result.Value!.PriorStatements;
            statements.ShouldNotBeNull();
            statements![0].Installments.ShouldBeNull("Installments should be null when file is absent");
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    // ── ExpectedTransactions ────────────────────────────────────────────────

    [Fact]
    public async Task GetBundleAsync_ExpectedTransactionsCsv_PopulatesSection()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetBundleAsync(key, ct);

        result.IsSuccess.ShouldBeTrue($"Expected success: {result.Error}");
        var groups = result.Value!.ExpectedTransactions;
        groups.ShouldNotBeNull();
        groups!.Count.ShouldBe(1);

        var group = groups[0];
        group.AccountRef.ShouldBe("ACC-0001");
        group.Transactions.Count.ShouldBe(2);

        var netflix = group.Transactions.Single(t => t.Description.Contains("NETFLIX"));
        netflix.Amount.ShouldBe(329.00m);
        netflix.OperationDate.ShouldBe("2025-07-05");
        netflix.ChargeDate.ShouldBe("2025-07-07");
        netflix.Sign.ShouldBe("+");

        var programa = group.Transactions.Single(t => t.Description.Contains("PROGRAMA"));
        programa.Amount.ShouldBe(6523.00m);
        programa.Sign.ShouldBe("-");
    }

    [Fact]
    public async Task GetBundleAsync_ExpectedTransactionsCsv_BundlePassesSchemaValidation()
    {
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetBundleAsync(key, ct);

        result.IsSuccess.ShouldBeTrue($"Bundle with expectedTransactions should pass schema: {result.Error}");
    }

    [Fact]
    public async Task GetBundleAsync_MissingExpectedTransactionsCsv_SectionIsNull_BundleValid()
    {
        var ct = TestContext.Current.CancellationToken;
        var tempRoot = CreateTempInstitutionDir("No_Txns_Bank", out var instDir);
        try
        {
            WriteMinimalMetadata(instDir, "No Txns Bank");
            var adapter = CreateAdapter(tempRoot);
            var key = new StatementContextKey("No Txns Bank");

            var result = await adapter.GetBundleAsync(key, ct);

            result.IsSuccess.ShouldBeTrue($"Missing expected-transactions.csv must not fail: {result.Error}");
            result.Value!.ExpectedTransactions.ShouldBeNull();
        }
        finally
        {
            Directory.Delete(tempRoot, recursive: true);
        }
    }

    // ── Round-trip parity ───────────────────────────────────────────────────

    [Fact]
    public async Task GetBundleAsync_FullCsvSet_AllSixNewSectionsPopulated()
    {
        // Verify that a full CSV set yields a bundle with all six previously-null sections populated.
        var ct = TestContext.Current.CancellationToken;
        var adapter = CreateAdapter();
        var key = new StatementContextKey("Demo Bank (Iqubica)");

        var result = await adapter.GetBundleAsync(key, ct);

        result.IsSuccess.ShouldBeTrue($"Full CSV set must succeed: {result.Error}");
        var bundle = result.Value!;

        bundle.MandatoryLegends.ShouldNotBeNull("MandatoryLegends should be populated");
        bundle.SequentialImages.ShouldNotBeNull("SequentialImages should be populated");
        bundle.Promotions.ShouldNotBeNull("Promotions should be populated");
        bundle.ClientAccounts.ShouldNotBeNull("ClientAccounts should be populated");
        bundle.PriorStatements.ShouldNotBeNull("PriorStatements should be populated");
        bundle.ExpectedTransactions.ShouldNotBeNull("ExpectedTransactions should be populated");

        // Existing sections still populated
        bundle.Products.ShouldNotBeNull("Products must remain populated");
        bundle.InterestRates.ShouldNotBeNull("InterestRates must remain populated");
        bundle.ToleranceConfig.ShouldNotBeNull("ToleranceConfig must remain populated");
        bundle.ValidationConstants.ShouldNotBeNull("ValidationConstants must remain populated");
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static string CreateTempInstitutionDir(string institutionDirName, out string instDir)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), $"vec-ext-test-{Guid.NewGuid():N}");
        instDir = Path.Combine(tempRoot, institutionDirName);
        Directory.CreateDirectory(instDir);
        return tempRoot;
    }

    private static void WriteMinimalMetadata(string instDir, string institution)
    {
        File.WriteAllText(
            Path.Combine(instDir, "bundle-metadata.csv"),
            "schemaVersion,institution,bundleId,generatedAt,periodLabel,periodStart,periodEnd,sourceMechanism,sourceReference,sourceNotes\n" +
            $"1.0.0,{institution},,,,,,csv,,\n");
    }
}
