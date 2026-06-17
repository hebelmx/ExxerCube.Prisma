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
/// <para><strong>CSV layout (one directory, multiple files):</strong></para>
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

        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<VecReferenceBundle>();

        var bundle = new VecReferenceBundle(
            BundleMetadata: metadataResult.Value!,
            Products: products,
            InterestRates: interestRates,
            MandatoryLegends: null,
            SequentialImages: null,
            Promotions: null,
            ClientAccounts: null,
            PriorStatements: null,
            ExpectedTransactions: null,
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

    private static decimal? ParseDecimalField(CsvReader csv, string fieldName)
    {
        var raw = csv.GetField(fieldName);
        return decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }
}
