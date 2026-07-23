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
using ExxerCube.Prisma.Veriqan.Domain.Enums;
using ExxerCube.Prisma.Veriqan.Domain.ReferenceData;
using ExxerCube.Prisma.Veriqan.Infrastructure.ReferenceData.Integrity;
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
    /// Guards the "integrity verification disabled" warning so it is logged at most once per
    /// adapter instance, even though every call re-checks the (unchanging) configured key.
    /// </summary>
    private int _noKeyWarningLogged;

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

        var (integrityFailure, verifiedFiles) =
            await VerifyBundleIntegrityAsync<VecReferenceBundle>(dataDir, ct).ConfigureAwait(false);
        if (integrityFailure is not null)
            return integrityFailure;

        // Load required section: bundleMetadata
        var metadataResult = await LoadBundleMetadataAsync(dataDir, verifiedFiles, ct).ConfigureAwait(false);
        if (!metadataResult.IsSuccess)
            return Result<VecReferenceBundle>.WithFailure(
                $"Failed to load bundle-metadata.csv: {metadataResult.Error}");

        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<VecReferenceBundle>();

        // Load optional sections
        var products = await LoadProductsAsync(dataDir, verifiedFiles, ct).ConfigureAwait(false);
        var interestRates = await LoadInterestRatesAsync(dataDir, verifiedFiles, ct).ConfigureAwait(false);
        var toleranceConfig = await LoadToleranceConfigAsync(dataDir, verifiedFiles, ct).ConfigureAwait(false);
        var validationConstants = await LoadValidationConstantsAsync(dataDir, verifiedFiles, ct).ConfigureAwait(false);
        var mandatoryLegends = await LoadMandatoryLegendsAsync(dataDir, verifiedFiles, ct).ConfigureAwait(false);
        var sequentialImages = await LoadSequentialImagesAsync(dataDir, verifiedFiles, ct).ConfigureAwait(false);
        var promotions = await LoadPromotionsAsync(dataDir, verifiedFiles, ct).ConfigureAwait(false);
        var clientAccounts = await LoadClientAccountsAsync(dataDir, verifiedFiles, ct).ConfigureAwait(false);
        var priorStatements = await LoadPriorStatementsAsync(dataDir, verifiedFiles, ct).ConfigureAwait(false);
        var expectedTransactions = await LoadExpectedTransactionsAsync(dataDir, verifiedFiles, ct).ConfigureAwait(false);

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

    /// <inheritdoc />
    public async Task<Result<IReadOnlyDictionary<string, ChecklistTier>>> GetChecklistTiersAsync(
        StatementContextKey key,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(key);

        if (ct.IsCancellationRequested)
            return ResultExtensions.Cancelled<IReadOnlyDictionary<string, ChecklistTier>>();

        string dataDir = ResolveDataDirectory(key);

        var (integrityFailure, verifiedFiles) =
            await VerifyBundleIntegrityAsync<IReadOnlyDictionary<string, ChecklistTier>>(dataDir, ct)
                .ConfigureAwait(false);
        if (integrityFailure is not null)
            return integrityFailure;

        const string fileName = "checklist-tiers.csv";
        string path = Path.Combine(dataDir, fileName);

        using var stream = TryOpenContent(dataDir, fileName, verifiedFiles);
        if (stream is null)
        {
            _logger.LogDebug(
                "CsvReferenceDataAdapter: checklist-tiers.csv not found at '{Path}'; returning empty tier map. " +
                "Callers must treat missing keys as Condusef (conservative default).",
                path);
            return Result<IReadOnlyDictionary<string, ChecklistTier>>.WithSuccess(
                new Dictionary<string, ChecklistTier>(StringComparer.OrdinalIgnoreCase));
        }

        try
        {
            var map = new Dictionary<string, ChecklistTier>(StringComparer.OrdinalIgnoreCase);
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, CsvConfig());
            await csv.ReadAsync().ConfigureAwait(false);
            csv.ReadHeader();

            while (await csv.ReadAsync().ConfigureAwait(false))
            {
                if (ct.IsCancellationRequested)
                    return ResultExtensions.Cancelled<IReadOnlyDictionary<string, ChecklistTier>>();

                var checkId = csv.GetField("checkId");
                var tierRaw = csv.GetField("tier");

                if (string.IsNullOrWhiteSpace(checkId) || string.IsNullOrWhiteSpace(tierRaw))
                    continue;

                if (!Enum.TryParse<ChecklistTier>(tierRaw.Trim(), ignoreCase: true, out var tier))
                {
                    _logger.LogWarning(
                        "CsvReferenceDataAdapter: unrecognised tier value '{Tier}' for checkId '{CheckId}' in {Path}; row skipped.",
                        tierRaw, checkId, path);
                    continue;
                }

                map[checkId.Trim()] = tier;
            }

            _logger.LogDebug(
                "CsvReferenceDataAdapter: loaded {Count} checklist-tier entries from '{Path}'.",
                map.Count, path);

            return Result<IReadOnlyDictionary<string, ChecklistTier>>.WithSuccess(map);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex,
                "CsvReferenceDataAdapter: error reading checklist-tiers.csv at '{Path}'; returning empty map.",
                path);
            return Result<IReadOnlyDictionary<string, ChecklistTier>>.WithSuccess(
                new Dictionary<string, ChecklistTier>(StringComparer.OrdinalIgnoreCase));
        }
    }

    // ── private helpers ──────────────────────────────────────────────────────

    /// <summary>
    /// Runs <see cref="BundleIntegrityVerifier"/> against <paramref name="dataDir"/> when
    /// <see cref="CsvReferenceDataOptions.BundleHmacKey"/> is configured. When no key is
    /// configured (including a whitespace-only value), integrity verification is skipped for
    /// backwards compatibility with existing unsigned bundles, and a warning is logged at most
    /// once per adapter instance (in practice, once per load, since this adapter is registered
    /// Transient).
    /// </summary>
    /// <typeparam name="T">The success-value type of the caller's <see cref="Result{T}"/>.</typeparam>
    /// <param name="dataDir">The resolved institution bundle directory.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>
    /// <c>Failure</c> is non-null (a failed or cancelled <see cref="Result{T}"/>) when the
    /// caller must return it as-is; when it is <see langword="null"/>, verification either
    /// passed or was disabled, and <c>VerifiedFiles</c> carries the verified file bytes to
    /// parse from (or <see langword="null"/> when verification is disabled and callers must
    /// fall back to reading disk directly, unchanged from pre-integrity behavior).
    /// </returns>
    private async Task<(Result<T>? Failure, IReadOnlyDictionary<string, byte[]>? VerifiedFiles)>
        VerifyBundleIntegrityAsync<T>(string dataDir, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.BundleHmacKey))
        {
            if (Interlocked.CompareExchange(ref _noKeyWarningLogged, 1, 0) == 0)
            {
                _logger.LogWarning(
                    "CsvReferenceDataAdapter: bundle integrity verification disabled — no BundleHmacKey configured.");
            }

            return (null, null);
        }

        if (ct.IsCancellationRequested)
            return (ResultExtensions.Cancelled<T>(), null);

        var verifyResult = await BundleIntegrityVerifier.VerifyAsync(dataDir, _options.BundleHmacKey, ct)
            .ConfigureAwait(false);

        if (verifyResult.IsCancelled())
            return (ResultExtensions.Cancelled<T>(), null);

        if (!verifyResult.IsSuccess)
        {
            _logger.LogWarning(
                "CsvReferenceDataAdapter: bundle integrity verification failed for directory '{DataDir}': {Error}",
                dataDir, verifyResult.Error);
            return (Result<T>.WithFailure($"Bundle integrity verification failed: {verifyResult.Error}"), null);
        }

        return (null, verifyResult.Value);
    }

    /// <summary>
    /// Opens <paramref name="fileName"/> for reading, sourcing content from
    /// <paramref name="verifiedFiles"/> when integrity verification produced verified bytes
    /// (closing the verify-then-reread / TOCTOU gap), or from disk under
    /// <paramref name="dataDir"/> when verification is disabled (<paramref name="verifiedFiles"/>
    /// is <see langword="null"/>) — identical to pre-integrity behavior in that case.
    /// </summary>
    /// <param name="dataDir">The resolved institution bundle directory (disk fallback only).</param>
    /// <param name="fileName">The bundle-relative file name to open, e.g. <c>products.csv</c>.</param>
    /// <param name="verifiedFiles">
    /// Verified filename → bytes map from <see cref="BundleIntegrityVerifier.VerifyAsync"/>, or
    /// <see langword="null"/> when verification is disabled for this call.
    /// </param>
    /// <returns>
    /// A readable, seekable <see cref="Stream"/> positioned at the start, or <see langword="null"/>
    /// when the file is absent — from the verified set when verification is enabled (a file
    /// listed nowhere in that set is treated as not found and never falls back to disk), or from
    /// disk when verification is disabled.
    /// </returns>
    private static Stream? TryOpenContent(
        string dataDir,
        string fileName,
        IReadOnlyDictionary<string, byte[]>? verifiedFiles)
    {
        if (verifiedFiles is not null)
        {
            return verifiedFiles.TryGetValue(fileName, out var bytes)
                ? new MemoryStream(bytes, index: 0, count: bytes.Length, writable: false, publiclyVisible: true)
                : null;
        }

        var path = Path.Combine(dataDir, fileName);
        return File.Exists(path) ? new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read) : null;
    }

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
        string dataDir, IReadOnlyDictionary<string, byte[]>? verifiedFiles, CancellationToken ct)
    {
        const string fileName = "bundle-metadata.csv";
        using var stream = TryOpenContent(dataDir, fileName, verifiedFiles);
        if (stream is null)
            return Result<BundleMetadata>.WithFailure($"Required file not found: {Path.Combine(dataDir, fileName)}");

        try
        {
            using var reader = new StreamReader(stream);
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
            return Result<BundleMetadata>.WithFailure($"Error reading {Path.Combine(dataDir, fileName)}: {ex.Message}");
        }
    }

    private static async Task<IReadOnlyList<VecProduct>?> LoadProductsAsync(
        string dataDir, IReadOnlyDictionary<string, byte[]>? verifiedFiles, CancellationToken ct)
    {
        using var stream = TryOpenContent(dataDir, "products.csv", verifiedFiles);
        if (stream is null) return null;

        try
        {
            var products = new List<VecProduct>();
            using var reader = new StreamReader(stream);
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
        string dataDir, IReadOnlyDictionary<string, byte[]>? verifiedFiles, CancellationToken ct)
    {
        using var stream = TryOpenContent(dataDir, "interest-rates.csv", verifiedFiles);
        if (stream is null) return null;

        try
        {
            var ratesByProduct = new Dictionary<string, List<RateByPeriod>>(StringComparer.OrdinalIgnoreCase);
            using var reader = new StreamReader(stream);
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
        string dataDir, IReadOnlyDictionary<string, byte[]>? verifiedFiles, CancellationToken ct)
    {
        using var stream = TryOpenContent(dataDir, "tolerance-config.csv", verifiedFiles);
        if (stream is null) return null;

        try
        {
            using var reader = new StreamReader(stream);
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
        string dataDir, IReadOnlyDictionary<string, byte[]>? verifiedFiles, CancellationToken ct)
    {
        using var stream = TryOpenContent(dataDir, "validation-constants.csv", verifiedFiles);
        if (stream is null) return null;

        try
        {
            using var reader = new StreamReader(stream);
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
        string dataDir, IReadOnlyDictionary<string, byte[]>? verifiedFiles, CancellationToken ct)
    {
        using var stream = TryOpenContent(dataDir, "mandatory-legends.csv", verifiedFiles);
        if (stream is null) return null;

        try
        {
            var legends = new List<MandatoryLegend>();
            using var reader = new StreamReader(stream);
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
        string dataDir, IReadOnlyDictionary<string, byte[]>? verifiedFiles, CancellationToken ct)
    {
        using var stream = TryOpenContent(dataDir, "sequential-images.csv", verifiedFiles);
        if (stream is null) return null;

        try
        {
            var images = new List<SequentialImage>();
            using var reader = new StreamReader(stream);
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
        string dataDir, IReadOnlyDictionary<string, byte[]>? verifiedFiles, CancellationToken ct)
    {
        using var stream = TryOpenContent(dataDir, "promotions.csv", verifiedFiles);
        if (stream is null) return null;

        try
        {
            var promotions = new List<Promotion>();
            using var reader = new StreamReader(stream);
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
        string dataDir, IReadOnlyDictionary<string, byte[]>? verifiedFiles, CancellationToken ct)
    {
        using var clientsStream = TryOpenContent(dataDir, "client-accounts.csv", verifiedFiles);
        if (clientsStream is null) return null;

        try
        {
            // Load account entries first, grouped by clientId
            var entriesByClient = new Dictionary<string, List<AccountEntry>>(StringComparer.OrdinalIgnoreCase);
            using var entriesStream = TryOpenContent(dataDir, "client-accounts-entries.csv", verifiedFiles);
            if (entriesStream is not null)
            {
                using var entryReader = new StreamReader(entriesStream);
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
            using var reader = new StreamReader(clientsStream);
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
        string dataDir, IReadOnlyDictionary<string, byte[]>? verifiedFiles, CancellationToken ct)
    {
        using var statementsStream = TryOpenContent(dataDir, "prior-statements.csv", verifiedFiles);
        if (statementsStream is null) return null;

        try
        {
            // Load installments first, grouped by accountRef
            var installmentsByAccount = new Dictionary<string, List<InstallmentEntry>>(StringComparer.OrdinalIgnoreCase);
            using var installmentsStream = TryOpenContent(dataDir, "prior-statements-installments.csv", verifiedFiles);
            if (installmentsStream is not null)
            {
                using var instReader = new StreamReader(installmentsStream);
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
            using var reader = new StreamReader(statementsStream);
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
        string dataDir, IReadOnlyDictionary<string, byte[]>? verifiedFiles, CancellationToken ct)
    {
        using var stream = TryOpenContent(dataDir, "expected-transactions.csv", verifiedFiles);
        if (stream is null) return null;

        try
        {
            var byAccount = new Dictionary<string, List<ExpectedTransaction>>(StringComparer.OrdinalIgnoreCase);
            using var reader = new StreamReader(stream);
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
