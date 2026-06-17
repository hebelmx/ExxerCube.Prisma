using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Validation;
using IndQuestResults;
using IndQuestResults.Operations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Adapters;

/// <summary>
/// CSV adapter that implements <see cref="IVecReferenceDataProvider"/> by reading
/// a directory of typed CSV files, assembling a <see cref="VecReferenceBundle"/>,
/// and validating it against the JSON Schema contract before returning.
/// </summary>
/// <remarks>
/// <para><strong>CSV layout (one directory per institution, multiple files):</strong></para>
/// <list type="bullet">
///   <item><c>bundle-metadata.csv</c> — single data row:
///     <c>schemaVersion,institution,bundleId,generatedAt,periodLabel,periodStart,periodEnd,sourceMechanism,sourceReference,sourceNotes</c></item>
///   <item><c>products.csv</c> — one row per product:
///     <c>productId,productName,aliases,hasRewardsProgram,annualCommission,currency</c>
///     (aliases are pipe-separated, e.g. <c>"NL|TC-NL-ALIAS"</c>)</item>
///   <item><c>interest-rates.csv</c> — one row per product+period:
///     <c>productId,periodLabel,periodStart,periodEnd,annualOrdinaryFixedRate</c></item>
///   <item><c>tolerance-config.csv</c> — single data row:
///     <c>currencyToleranceMxn,pointsTolerance,rewardsPesosToleranceMxn,pointsToPesosExchangeRate</c></item>
///   <item><c>validation-constants.csv</c> — single data row:
///     <c>requiredFontFamily,bankingYearDays,catAnnualCommissionMxn</c></item>
///   <item><c>mandatory-legends.csv</c> — one row per legend:
///     <c>legendId,text,matchMode,appliesToProducts,section,required</c>
///     (<c>appliesToProducts</c> is pipe-separated, e.g. <c>"TC-NL|TC-TOR"</c>; empty = all products)</item>
///   <item><c>sequential-images.csv</c> — one row per ordered image entry:
///     <c>order,productId,imageId,imageUri,imageSha256,imagePerceptualHash,imageWidth,imageHeight,imageDescription</c></item>
///   <item><c>promotions.csv</c> — one row per promotion:
///     <c>promotionId,validFrom,validTo,appliesToProducts,imageId,imageUri,imageSha256,imagePerceptualHash,imageWidth,imageHeight,imageDescription</c>
///     (<c>appliesToProducts</c> is pipe-separated)</item>
///   <item><c>client-accounts.csv</c> — one row per client (header + address fields):
///     <c>clientId,firstNames,lastNames,fullName,rfc,clientNumber,street,number,neighborhood,postalCode,state</c></item>
///   <item><c>client-accounts-entries.csv</c> — one row per account, joined to client via <c>clientId</c>:
///     <c>clientId,accountRef,productId,cardNumber,clabe,branchNumber,creditLine,accountOpenDate</c></item>
///   <item><c>prior-statements.csv</c> — one row per prior statement (closing balances flattened in):
///     <c>accountRef,periodLabel,periodStart,periodEnd,pagoParaNoGenerarIntereses,saldoDeudorTotal,rewardsPointsBalance,rewardsPesosBalance,documentRefUri,documentRefSha256</c></item>
///   <item><c>prior-statements-installments.csv</c> — one row per MSI installment, joined via <c>accountRef</c>:
///     <c>accountRef,purchaseId,description,saldoPendiente,pagoRequerido,numeroDePago,totalPagos</c></item>
///   <item><c>expected-transactions.csv</c> — one row per transaction, grouped by <c>accountRef</c>:
///     <c>accountRef,description,amount,operationDate,chargeDate,sign</c></item>
/// </list>
/// <para>
/// All files are optional except <c>bundle-metadata.csv</c>.
/// Missing sections produce a bundle with that section as <c>null</c>,
/// which causes <c>INSUFFICIENT_REFERENCE_DATA</c> findings for the affected checks
/// (graceful degradation as required by the contract).
/// </para>
/// <para>
/// The adapter resolves the data directory by looking for a sub-directory under
/// <see cref="CsvReferenceDataOptions.RootDirectory"/> whose name matches the
/// <see cref="StatementContextKey.Institution"/> (case-insensitive, whitespace-normalized).
/// </para>
/// </remarks>
public sealed class CsvReferenceDataAdapter : IVecReferenceDataProvider
{
    private readonly CsvReferenceDataOptions _options;
    private readonly ReferenceBundleSchemaValidator _validator;
    private readonly ILogger<CsvReferenceDataAdapter> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="CsvReferenceDataAdapter"/>.
    /// </summary>
    /// <param name="options">Configuration options (root directory).</param>
    /// <param name="validator">JSON Schema validator shared across all adapters.</param>
    /// <param name="logger">Logger.</param>
    public CsvReferenceDataAdapter(
        IOptions<CsvReferenceDataOptions> options,
        ReferenceBundleSchemaValidator validator,
        ILogger<CsvReferenceDataAdapter> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(validator);
        ArgumentNullException.ThrowIfNull(logger);
        _options = options.Value;
        _validator = validator;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<VecReferenceBundle>> GetBundleAsync(
        StatementContextKey key,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<VecReferenceBundle>();

        _logger.LogInformation(
            "CsvReferenceDataAdapter loading bundle for institution={Institution} period={Period}",
            key.Institution, key.PeriodLabel);

        // Locate data directory
        string dataDir = ResolveDataDirectory(key);
        if (!Directory.Exists(dataDir))
            return Result<VecReferenceBundle>.WithFailure(
                $"CSV data directory not found: '{dataDir}'. Check CsvReferenceDataOptions.RootDirectory and institution name.");

        // Load required section: bundleMetadata
        var metadataResult = await LoadBundleMetadataAsync(dataDir, ct).ConfigureAwait(false);
        if (!metadataResult.IsSuccess)
            return Result<VecReferenceBundle>.WithFailure(
                $"Failed to load bundle-metadata.csv: {metadataResult.Error}");

        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<VecReferenceBundle>();

        // Load optional sections
        var products = await LoadProductsAsync(dataDir, ct).ConfigureAwait(false);
        var interestRates = await LoadInterestRatesAsync(dataDir, ct).ConfigureAwait(false);
        var toleranceConfig = await LoadToleranceConfigAsync(dataDir, ct).ConfigureAwait(false);
        var validationConstants = await LoadValidationConstantsAsync(dataDir, ct).ConfigureAwait(false);
        var mandatoryLegends = await LoadMandatoryLegendsAsync(dataDir, ct).ConfigureAwait(false);
        var sequentialImages = await LoadSequentialImagesAsync(dataDir, ct).ConfigureAwait(false);
        var promotions = await LoadPromotionsAsync(dataDir, ct).ConfigureAwait(false);
        var clientAccounts = await LoadClientAccountsAsync(dataDir, ct).ConfigureAwait(false);
        var priorStatements = await LoadPriorStatementsAsync(dataDir, ct).ConfigureAwait(false);
        var expectedTransactions = await LoadExpectedTransactionsAsync(dataDir, ct).ConfigureAwait(false);

        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<VecReferenceBundle>();

        var bundle = new VecReferenceBundle(
            BundleMetadata: metadataResult.Value!,
            Products: products,
            InterestRates: interestRates,
            MandatoryLegends: mandatoryLegends,
            SequentialImages: sequentialImages,
            Promotions: promotions,
            ClientAccounts: clientAccounts,
            PriorStatements: priorStatements,
            ExpectedTransactions: expectedTransactions,
            ToleranceConfig: toleranceConfig,
            ValidationConstants: validationConstants
        );

        // Validate against JSON Schema (ADR-V4)
        var validationResult = _validator.Validate(bundle);
        if (!validationResult.IsSuccess)
        {
            _logger.LogWarning(
                "CsvReferenceDataAdapter bundle failed schema validation: {Errors}",
                validationResult.Error);
            return Result<VecReferenceBundle>.WithFailure(validationResult.Error ?? "Schema validation failed");
        }

        _logger.LogInformation(
            "CsvReferenceDataAdapter successfully loaded and validated bundle for {Institution}",
            key.Institution);

        return Result<VecReferenceBundle>.WithSuccess(validationResult.Value!);
    }

    // ── private helpers ──────────────────────────────────────────────────────

    private string ResolveDataDirectory(StatementContextKey key)
    {
        // Normalize institution name to a safe directory name segment
        string normalized = NormalizeToDirectoryName(key.Institution);
        return Path.Combine(_options.RootDirectory, normalized);
    }

    private static string NormalizeToDirectoryName(string name) =>
        string.Join("_", name.Split(Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries))
              .Replace(' ', '_');

    private static CsvConfiguration CsvConfig() => new(CultureInfo.InvariantCulture)
    {
        HasHeaderRecord = true,
        MissingFieldFound = null,
        HeaderValidated = null,
        TrimOptions = TrimOptions.Trim
    };

    private static async Task<Result<BundleMetadata>> LoadBundleMetadataAsync(
        string dataDir, CancellationToken ct)
    {
        string path = Path.Combine(dataDir, "bundle-metadata.csv");
        if (!File.Exists(path))
            return Result<BundleMetadata>.WithFailure($"Required file not found: {path}");

        try
        {
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CsvConfig());
            await csv.ReadAsync().ConfigureAwait(false);
            csv.ReadHeader();
            await csv.ReadAsync().ConfigureAwait(false);

            var schemaVersion = csv.GetField("schemaVersion") ?? "1.0.0";
            var institution = csv.GetField("institution") ?? string.Empty;
            var bundleId = csv.GetField("bundleId");
            var generatedAt = csv.GetField("generatedAt");
            var periodLabel = csv.GetField("periodLabel");
            var periodStart = csv.GetField("periodStart");
            var periodEnd = csv.GetField("periodEnd");
            var sourceMechanism = csv.GetField("sourceMechanism");
            var sourceReference = csv.GetField("sourceReference");
            var sourceNotes = csv.GetField("sourceNotes");

            PeriodRange? period = (periodLabel != null || periodStart != null || periodEnd != null)
                ? new PeriodRange(periodLabel, periodStart, periodEnd)
                : null;

            BundleSource? source = (sourceMechanism != null || sourceReference != null || sourceNotes != null)
                ? new BundleSource(sourceMechanism, sourceReference, sourceNotes)
                : null;

            var metadata = new BundleMetadata(
                SchemaVersion: schemaVersion,
                Institution: institution,
                BundleId: string.IsNullOrWhiteSpace(bundleId) ? null : bundleId,
                GeneratedAt: string.IsNullOrWhiteSpace(generatedAt) ? null : generatedAt,
                Period: period,
                Source: source
            );

            return Result<BundleMetadata>.WithSuccess(metadata);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Result<BundleMetadata>.WithFailure($"Error reading {path}: {ex.Message}");
        }
    }

    private static async Task<IReadOnlyList<VecProduct>?> LoadProductsAsync(
        string dataDir, CancellationToken ct)
    {
        string path = Path.Combine(dataDir, "products.csv");
        if (!File.Exists(path)) return null;

        try
        {
            var products = new List<VecProduct>();
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CsvConfig());
            await csv.ReadAsync().ConfigureAwait(false);
            csv.ReadHeader();

            while (await csv.ReadAsync().ConfigureAwait(false))
            {
                if (ct.IsCancellationRequested) return null;

                var productId = csv.GetField("productId") ?? string.Empty;
                var productName = csv.GetField("productName") ?? string.Empty;
                var aliasesRaw = csv.GetField("aliases");
                var hasRewardRaw = csv.GetField("hasRewardsProgram");
                var annualCommissionRaw = csv.GetField("annualCommission");
                var currency = csv.GetField("currency");

                IReadOnlyList<string>? aliases = string.IsNullOrWhiteSpace(aliasesRaw)
                    ? null
                    : aliasesRaw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                bool? hasRewards = string.IsNullOrWhiteSpace(hasRewardRaw)
                    ? null
                    : bool.TryParse(hasRewardRaw, out var b) ? b : null;

                ProductTariffs? tariffs = null;
                if (!string.IsNullOrWhiteSpace(annualCommissionRaw) &&
                    decimal.TryParse(annualCommissionRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var commission))
                {
                    tariffs = new ProductTariffs(commission, string.IsNullOrWhiteSpace(currency) ? "MXN" : currency, null);
                }

                products.Add(new VecProduct(productId, productName, aliases, hasRewards, null, null, tariffs));
            }

            return products.Count > 0 ? products : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null; // graceful degradation — missing section, not a hard failure
        }
    }

    private static async Task<IReadOnlyList<InterestRateEntry>?> LoadInterestRatesAsync(
        string dataDir, CancellationToken ct)
    {
        string path = Path.Combine(dataDir, "interest-rates.csv");
        if (!File.Exists(path)) return null;

        try
        {
            var ratesByProduct = new Dictionary<string, List<RateByPeriod>>(StringComparer.OrdinalIgnoreCase);
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CsvConfig());
            await csv.ReadAsync().ConfigureAwait(false);
            csv.ReadHeader();

            while (await csv.ReadAsync().ConfigureAwait(false))
            {
                if (ct.IsCancellationRequested) return null;

                var productId = csv.GetField("productId") ?? string.Empty;
                var periodLabel = csv.GetField("periodLabel");
                var periodStart = csv.GetField("periodStart");
                var periodEnd = csv.GetField("periodEnd");
                var rateRaw = csv.GetField("annualOrdinaryFixedRate");

                if (!decimal.TryParse(rateRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var rate))
                    continue;

                if (!ratesByProduct.TryGetValue(productId, out var list))
                {
                    list = [];
                    ratesByProduct[productId] = list;
                }

                list.Add(new RateByPeriod(rate,
                    string.IsNullOrWhiteSpace(periodLabel) ? null : periodLabel,
                    string.IsNullOrWhiteSpace(periodStart) ? null : periodStart,
                    string.IsNullOrWhiteSpace(periodEnd) ? null : periodEnd));
            }

            if (ratesByProduct.Count == 0) return null;

            return ratesByProduct
                .Select(kvp => new InterestRateEntry(kvp.Key, kvp.Value))
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static async Task<ToleranceConfig?> LoadToleranceConfigAsync(
        string dataDir, CancellationToken ct)
    {
        string path = Path.Combine(dataDir, "tolerance-config.csv");
        if (!File.Exists(path)) return null;

        try
        {
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CsvConfig());
            await csv.ReadAsync().ConfigureAwait(false);
            csv.ReadHeader();
            if (!await csv.ReadAsync().ConfigureAwait(false)) return null;

            return new ToleranceConfig(
                ParseDecimalField(csv, "currencyToleranceMxn"),
                ParseDecimalField(csv, "pointsTolerance"),
                ParseDecimalField(csv, "rewardsPesosToleranceMxn"),
                ParseDecimalField(csv, "pointsToPesosExchangeRate")
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static async Task<ValidationConstants?> LoadValidationConstantsAsync(
        string dataDir, CancellationToken ct)
    {
        string path = Path.Combine(dataDir, "validation-constants.csv");
        if (!File.Exists(path)) return null;

        try
        {
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CsvConfig());
            await csv.ReadAsync().ConfigureAwait(false);
            csv.ReadHeader();
            if (!await csv.ReadAsync().ConfigureAwait(false)) return null;

            var fontFamily = csv.GetField("requiredFontFamily");
            var bankingDaysRaw = csv.GetField("bankingYearDays");
            var catRaw = csv.GetField("catAnnualCommissionMxn");

            int? bankingDays = int.TryParse(bankingDaysRaw, out var bd) ? bd : null;
            decimal? catAmount = decimal.TryParse(catRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var cat) ? cat : null;

            return new ValidationConstants(
                string.IsNullOrWhiteSpace(fontFamily) ? null : fontFamily,
                bankingDays,
                catAmount
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<MandatoryLegend>?> LoadMandatoryLegendsAsync(
        string dataDir, CancellationToken ct)
    {
        string path = Path.Combine(dataDir, "mandatory-legends.csv");
        if (!File.Exists(path)) return null;

        try
        {
            var legends = new List<MandatoryLegend>();
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CsvConfig());
            await csv.ReadAsync().ConfigureAwait(false);
            csv.ReadHeader();

            while (await csv.ReadAsync().ConfigureAwait(false))
            {
                if (ct.IsCancellationRequested) return null;

                var legendId = csv.GetField("legendId") ?? string.Empty;
                var text = csv.GetField("text") ?? string.Empty;
                var matchMode = csv.GetField("matchMode");
                var appliesToProductsRaw = csv.GetField("appliesToProducts");
                var section = csv.GetField("section");
                var requiredRaw = csv.GetField("required");

                IReadOnlyList<string>? appliesToProducts =
                    string.IsNullOrWhiteSpace(appliesToProductsRaw)
                        ? null
                        : appliesToProductsRaw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                bool? required = string.IsNullOrWhiteSpace(requiredRaw)
                    ? null
                    : bool.TryParse(requiredRaw, out var b) ? b : null;

                legends.Add(new MandatoryLegend(
                    legendId,
                    text,
                    string.IsNullOrWhiteSpace(matchMode) ? null : matchMode,
                    appliesToProducts,
                    string.IsNullOrWhiteSpace(section) ? null : section,
                    required));
            }

            return legends.Count > 0 ? legends : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<SequentialImage>?> LoadSequentialImagesAsync(
        string dataDir, CancellationToken ct)
    {
        string path = Path.Combine(dataDir, "sequential-images.csv");
        if (!File.Exists(path)) return null;

        try
        {
            var images = new List<SequentialImage>();
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CsvConfig());
            await csv.ReadAsync().ConfigureAwait(false);
            csv.ReadHeader();

            while (await csv.ReadAsync().ConfigureAwait(false))
            {
                if (ct.IsCancellationRequested) return null;

                var orderRaw = csv.GetField("order");
                var productId = csv.GetField("productId");

                if (!int.TryParse(orderRaw, out var order)) continue;

                var imageRef = ReadImageRef(csv, prefix: "image");
                if (imageRef is null) continue;

                images.Add(new SequentialImage(order, imageRef, string.IsNullOrWhiteSpace(productId) ? null : productId));
            }

            return images.Count > 0 ? images : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<Promotion>?> LoadPromotionsAsync(
        string dataDir, CancellationToken ct)
    {
        string path = Path.Combine(dataDir, "promotions.csv");
        if (!File.Exists(path)) return null;

        try
        {
            var promotions = new List<Promotion>();
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CsvConfig());
            await csv.ReadAsync().ConfigureAwait(false);
            csv.ReadHeader();

            while (await csv.ReadAsync().ConfigureAwait(false))
            {
                if (ct.IsCancellationRequested) return null;

                var promotionId = csv.GetField("promotionId") ?? string.Empty;
                var validFrom = csv.GetField("validFrom") ?? string.Empty;
                var validTo = csv.GetField("validTo") ?? string.Empty;
                var appliesToProductsRaw = csv.GetField("appliesToProducts");

                var imageRef = ReadImageRef(csv, prefix: "image");
                if (imageRef is null) continue;

                IReadOnlyList<string>? appliesToProducts =
                    string.IsNullOrWhiteSpace(appliesToProductsRaw)
                        ? null
                        : appliesToProductsRaw.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                promotions.Add(new Promotion(promotionId, imageRef, validFrom, validTo, appliesToProducts));
            }

            return promotions.Count > 0 ? promotions : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<ClientAccount>?> LoadClientAccountsAsync(
        string dataDir, CancellationToken ct)
    {
        string clientsPath = Path.Combine(dataDir, "client-accounts.csv");
        if (!File.Exists(clientsPath)) return null;

        string entriesPath = Path.Combine(dataDir, "client-accounts-entries.csv");

        try
        {
            // Load account entries first, grouped by clientId
            var entriesByClient = new Dictionary<string, List<AccountEntry>>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(entriesPath))
            {
                using var entryReader = new StreamReader(entriesPath);
                using var entryCsv = new CsvReader(entryReader, CsvConfig());
                await entryCsv.ReadAsync().ConfigureAwait(false);
                entryCsv.ReadHeader();

                while (await entryCsv.ReadAsync().ConfigureAwait(false))
                {
                    if (ct.IsCancellationRequested) return null;

                    var clientId = entryCsv.GetField("clientId") ?? string.Empty;
                    var accountRef = entryCsv.GetField("accountRef") ?? string.Empty;
                    var productId = entryCsv.GetField("productId") ?? string.Empty;
                    var cardNumber = entryCsv.GetField("cardNumber");
                    var clabe = entryCsv.GetField("clabe");
                    var branchNumber = entryCsv.GetField("branchNumber");
                    var creditLineRaw = entryCsv.GetField("creditLine");
                    var accountOpenDate = entryCsv.GetField("accountOpenDate");

                    decimal? creditLine = decimal.TryParse(creditLineRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var cl)
                        ? cl : null;

                    if (!entriesByClient.TryGetValue(clientId, out var list))
                    {
                        list = [];
                        entriesByClient[clientId] = list;
                    }

                    list.Add(new AccountEntry(
                        accountRef,
                        productId,
                        string.IsNullOrWhiteSpace(cardNumber) ? null : cardNumber,
                        string.IsNullOrWhiteSpace(clabe) ? null : clabe,
                        string.IsNullOrWhiteSpace(branchNumber) ? null : branchNumber,
                        creditLine,
                        string.IsNullOrWhiteSpace(accountOpenDate) ? null : accountOpenDate));
                }
            }

            // Load client rows
            var clients = new List<ClientAccount>();
            using var reader = new StreamReader(clientsPath);
            using var csv = new CsvReader(reader, CsvConfig());
            await csv.ReadAsync().ConfigureAwait(false);
            csv.ReadHeader();

            while (await csv.ReadAsync().ConfigureAwait(false))
            {
                if (ct.IsCancellationRequested) return null;

                var clientId = csv.GetField("clientId") ?? string.Empty;
                var firstNames = csv.GetField("firstNames");
                var lastNames = csv.GetField("lastNames");
                var fullName = csv.GetField("fullName");
                var rfc = csv.GetField("rfc");
                var clientNumber = csv.GetField("clientNumber");
                var street = csv.GetField("street");
                var number = csv.GetField("number");
                var neighborhood = csv.GetField("neighborhood");
                var postalCode = csv.GetField("postalCode");
                var state = csv.GetField("state");

                ClientName? clientName = (firstNames != null || lastNames != null || fullName != null)
                    ? new ClientName(
                        string.IsNullOrWhiteSpace(firstNames) ? null : firstNames,
                        string.IsNullOrWhiteSpace(lastNames) ? null : lastNames,
                        string.IsNullOrWhiteSpace(fullName) ? null : fullName)
                    : null;

                Address? address = (street != null || number != null || neighborhood != null || postalCode != null || state != null)
                    ? new Address(
                        string.IsNullOrWhiteSpace(street) ? null : street,
                        string.IsNullOrWhiteSpace(number) ? null : number,
                        string.IsNullOrWhiteSpace(neighborhood) ? null : neighborhood,
                        string.IsNullOrWhiteSpace(postalCode) ? null : postalCode,
                        string.IsNullOrWhiteSpace(state) ? null : state)
                    : null;

                entriesByClient.TryGetValue(clientId, out var accounts);

                clients.Add(new ClientAccount(
                    clientId,
                    clientName,
                    string.IsNullOrWhiteSpace(rfc) ? null : rfc,
                    string.IsNullOrWhiteSpace(clientNumber) ? null : clientNumber,
                    address,
                    accounts is { Count: > 0 } ? accounts : null));
            }

            return clients.Count > 0 ? clients : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<PriorStatement>?> LoadPriorStatementsAsync(
        string dataDir, CancellationToken ct)
    {
        string statementsPath = Path.Combine(dataDir, "prior-statements.csv");
        if (!File.Exists(statementsPath)) return null;

        string installmentsPath = Path.Combine(dataDir, "prior-statements-installments.csv");

        try
        {
            // Load installments first, grouped by accountRef
            var installmentsByAccount = new Dictionary<string, List<InstallmentEntry>>(StringComparer.OrdinalIgnoreCase);
            if (File.Exists(installmentsPath))
            {
                using var instReader = new StreamReader(installmentsPath);
                using var instCsv = new CsvReader(instReader, CsvConfig());
                await instCsv.ReadAsync().ConfigureAwait(false);
                instCsv.ReadHeader();

                while (await instCsv.ReadAsync().ConfigureAwait(false))
                {
                    if (ct.IsCancellationRequested) return null;

                    var accountRef = instCsv.GetField("accountRef") ?? string.Empty;
                    var purchaseId = instCsv.GetField("purchaseId") ?? string.Empty;
                    var description = instCsv.GetField("description");
                    var saldoPendienteRaw = instCsv.GetField("saldoPendiente");
                    var pagoRequeridoRaw = instCsv.GetField("pagoRequerido");
                    var numeroDePagoRaw = instCsv.GetField("numeroDePago");
                    var totalPagosRaw = instCsv.GetField("totalPagos");

                    decimal? saldoPendiente = decimal.TryParse(saldoPendienteRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var sp) ? sp : null;
                    decimal? pagoRequerido = decimal.TryParse(pagoRequeridoRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var pr) ? pr : null;
                    int? numeroDePago = int.TryParse(numeroDePagoRaw, out var ndp) ? ndp : null;
                    int? totalPagos = int.TryParse(totalPagosRaw, out var tp) ? tp : null;

                    if (!installmentsByAccount.TryGetValue(accountRef, out var list))
                    {
                        list = [];
                        installmentsByAccount[accountRef] = list;
                    }

                    list.Add(new InstallmentEntry(
                        purchaseId,
                        string.IsNullOrWhiteSpace(description) ? null : description,
                        saldoPendiente,
                        pagoRequerido,
                        numeroDePago,
                        totalPagos));
                }
            }

            // Load prior statement rows
            var statements = new List<PriorStatement>();
            using var reader = new StreamReader(statementsPath);
            using var csv = new CsvReader(reader, CsvConfig());
            await csv.ReadAsync().ConfigureAwait(false);
            csv.ReadHeader();

            while (await csv.ReadAsync().ConfigureAwait(false))
            {
                if (ct.IsCancellationRequested) return null;

                var accountRef = csv.GetField("accountRef") ?? string.Empty;
                var periodLabel = csv.GetField("periodLabel");
                var periodStart = csv.GetField("periodStart");
                var periodEnd = csv.GetField("periodEnd");
                var pagoRaw = csv.GetField("pagoParaNoGenerarIntereses");
                var saldoRaw = csv.GetField("saldoDeudorTotal");
                var pointsRaw = csv.GetField("rewardsPointsBalance");
                var pesosRaw = csv.GetField("rewardsPesosBalance");
                var docUri = csv.GetField("documentRefUri");
                var docSha = csv.GetField("documentRefSha256");

                var period = new PeriodRange(
                    string.IsNullOrWhiteSpace(periodLabel) ? null : periodLabel,
                    string.IsNullOrWhiteSpace(periodStart) ? null : periodStart,
                    string.IsNullOrWhiteSpace(periodEnd) ? null : periodEnd);

                decimal? pago = decimal.TryParse(pagoRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var pg) ? pg : null;
                decimal? saldo = decimal.TryParse(saldoRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var sd) ? sd : null;
                decimal? points = decimal.TryParse(pointsRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var pt) ? pt : null;
                decimal? pesos = decimal.TryParse(pesosRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var ps) ? ps : null;

                ClosingBalances? closingBalances = (pago != null || saldo != null || points != null || pesos != null)
                    ? new ClosingBalances(pago, saldo, points, pesos)
                    : null;

                DocumentRef? documentRef = (!string.IsNullOrWhiteSpace(docUri) || !string.IsNullOrWhiteSpace(docSha))
                    ? new DocumentRef(
                        string.IsNullOrWhiteSpace(docUri) ? null : docUri,
                        string.IsNullOrWhiteSpace(docSha) ? null : docSha)
                    : null;

                installmentsByAccount.TryGetValue(accountRef, out var installments);

                statements.Add(new PriorStatement(
                    accountRef,
                    period,
                    closingBalances,
                    installments is { Count: > 0 } ? installments : null,
                    documentRef));
            }

            return statements.Count > 0 ? statements : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static async Task<IReadOnlyList<ExpectedTransactionGroup>?> LoadExpectedTransactionsAsync(
        string dataDir, CancellationToken ct)
    {
        string path = Path.Combine(dataDir, "expected-transactions.csv");
        if (!File.Exists(path)) return null;

        try
        {
            var byAccount = new Dictionary<string, List<ExpectedTransaction>>(StringComparer.OrdinalIgnoreCase);
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, CsvConfig());
            await csv.ReadAsync().ConfigureAwait(false);
            csv.ReadHeader();

            while (await csv.ReadAsync().ConfigureAwait(false))
            {
                if (ct.IsCancellationRequested) return null;

                var accountRef = csv.GetField("accountRef") ?? string.Empty;
                var description = csv.GetField("description") ?? string.Empty;
                var amountRaw = csv.GetField("amount");
                var operationDate = csv.GetField("operationDate");
                var chargeDate = csv.GetField("chargeDate");
                var sign = csv.GetField("sign");

                if (!decimal.TryParse(amountRaw, NumberStyles.Any, CultureInfo.InvariantCulture, out var amount))
                    continue;

                if (!byAccount.TryGetValue(accountRef, out var list))
                {
                    list = [];
                    byAccount[accountRef] = list;
                }

                list.Add(new ExpectedTransaction(
                    description,
                    amount,
                    string.IsNullOrWhiteSpace(operationDate) ? null : operationDate,
                    string.IsNullOrWhiteSpace(chargeDate) ? null : chargeDate,
                    string.IsNullOrWhiteSpace(sign) ? null : sign));
            }

            if (byAccount.Count == 0) return null;

            return byAccount
                .Select(kvp => new ExpectedTransactionGroup(kvp.Key, kvp.Value))
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a flattened <see cref="ImageRef"/> from the current CSV row.
    /// Column names follow the convention: <c>{prefix}Id</c>, <c>{prefix}Uri</c>,
    /// <c>{prefix}Sha256</c>, <c>{prefix}PerceptualHash</c>, <c>{prefix}Width</c>,
    /// <c>{prefix}Height</c>, <c>{prefix}Description</c>.
    /// Returns <c>null</c> when neither id nor uri is present.
    /// </summary>
    private static ImageRef? ReadImageRef(CsvReader csv, string prefix)
    {
        var imageId = csv.GetField($"{prefix}Id");
        var uri = csv.GetField($"{prefix}Uri");
        var sha256 = csv.GetField($"{prefix}Sha256");
        var perceptualHash = csv.GetField($"{prefix}PerceptualHash");
        var widthRaw = csv.GetField($"{prefix}Width");
        var heightRaw = csv.GetField($"{prefix}Height");
        var description = csv.GetField($"{prefix}Description");

        if (string.IsNullOrWhiteSpace(imageId) && string.IsNullOrWhiteSpace(uri))
            return null;

        int? width = int.TryParse(widthRaw, out var w) ? w : null;
        int? height = int.TryParse(heightRaw, out var h) ? h : null;

        return new ImageRef(
            string.IsNullOrWhiteSpace(imageId) ? null : imageId,
            string.IsNullOrWhiteSpace(uri) ? null : uri,
            string.IsNullOrWhiteSpace(sha256) ? null : sha256,
            string.IsNullOrWhiteSpace(perceptualHash) ? null : perceptualHash,
            width,
            height,
            string.IsNullOrWhiteSpace(description) ? null : description);
    }

    private static decimal? ParseDecimalField(CsvReader csv, string fieldName)
    {
        var raw = csv.GetField(fieldName);
        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
