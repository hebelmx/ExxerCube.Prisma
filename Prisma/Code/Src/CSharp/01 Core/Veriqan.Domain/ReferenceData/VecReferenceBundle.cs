using System.Text.Json.Serialization;

namespace ExxerCube.Prisma.Veriqan.Domain.ReferenceData;

/// <summary>
/// Source-agnostic reference/ground-truth bundle for VEC statement verification.
/// Mirrors the JSON Schema contract at <c>vec-reference-bundle.schema.json</c> (draft 2020-12, version 1.0.0).
/// Any section except <see cref="BundleMetadata"/> may be null; absent sections produce
/// <c>INSUFFICIENT_REFERENCE_DATA</c> findings rather than false results (graceful degradation).
/// </summary>
/// <param name="BundleMetadata">Required metadata identifying the bundle, institution, and schema version.</param>
/// <param name="Products">Product catalog with tariffs, aliases, and image references.</param>
/// <param name="InterestRates">TASA table: annual ordinary fixed rate per product per period.</param>
/// <param name="MandatoryLegends">Legends that must appear in the statement (checklist item 46).</param>
/// <param name="SequentialImages">Ordered images expected between DESGLOSE and PROMOCIONES sections (item 47).</param>
/// <param name="Promotions">Promotional inserts with validity windows (item 49).</param>
/// <param name="ClientAccounts">Client master data for header field validation (items 2–8).</param>
/// <param name="PriorStatements">Closing values of the previous month (items 17, 36, 40).</param>
/// <param name="ExpectedTransactions">Ground-truth transaction detail for items 45/58.</param>
/// <param name="ToleranceConfig">Tolerance bands (currency ±MXN, points ±pts).</param>
/// <param name="ValidationConstants">Constants such as required font family and banking-year days.</param>
public sealed record VecReferenceBundle(
    [property: JsonPropertyName("bundleMetadata")] BundleMetadata BundleMetadata,
    [property: JsonPropertyName("products")] IReadOnlyList<VecProduct>? Products,
    [property: JsonPropertyName("interestRates")] IReadOnlyList<InterestRateEntry>? InterestRates,
    [property: JsonPropertyName("mandatoryLegends")] IReadOnlyList<MandatoryLegend>? MandatoryLegends,
    [property: JsonPropertyName("sequentialImages")] IReadOnlyList<SequentialImage>? SequentialImages,
    [property: JsonPropertyName("promotions")] IReadOnlyList<Promotion>? Promotions,
    [property: JsonPropertyName("clientAccounts")] IReadOnlyList<ClientAccount>? ClientAccounts,
    [property: JsonPropertyName("priorStatements")] IReadOnlyList<PriorStatement>? PriorStatements,
    [property: JsonPropertyName("expectedTransactions")] IReadOnlyList<ExpectedTransactionGroup>? ExpectedTransactions,
    [property: JsonPropertyName("toleranceConfig")] ToleranceConfig? ToleranceConfig,
    [property: JsonPropertyName("validationConstants")] ValidationConstants? ValidationConstants
);

/// <summary>Metadata identifying this bundle instance.</summary>
/// <param name="SchemaVersion">Must be <c>"1.0.0"</c> (const in schema).</param>
/// <param name="Institution">Bank / issuer name (e.g. "Demo Bank").</param>
/// <param name="BundleId">Optional unique id for this bundle instance.</param>
/// <param name="GeneratedAt">ISO-8601 date-time when the bundle was generated.</param>
/// <param name="Period">Optional date range this bundle covers.</param>
/// <param name="Source">Optional provenance: mechanism, reference, notes.</param>
public sealed record BundleMetadata(
    [property: JsonPropertyName("schemaVersion")] string SchemaVersion,
    [property: JsonPropertyName("institution")] string Institution,
    [property: JsonPropertyName("bundleId")] string? BundleId,
    [property: JsonPropertyName("generatedAt")] string? GeneratedAt,
    [property: JsonPropertyName("period")] PeriodRange? Period,
    [property: JsonPropertyName("source")] BundleSource? Source
);

/// <summary>A date range (label + ISO-8601 start/end dates).</summary>
/// <param name="Label">Human-readable label, e.g. <c>"Sep-Oct 2025"</c>.</param>
/// <param name="Start">ISO-8601 date string for range start.</param>
/// <param name="End">ISO-8601 date string for range end.</param>
public sealed record PeriodRange(
    [property: JsonPropertyName("label")] string? Label,
    [property: JsonPropertyName("start")] string? Start,
    [property: JsonPropertyName("end")] string? End
);

/// <summary>Provenance information for the bundle.</summary>
/// <param name="Mechanism">Delivery mechanism: csv, database, api, manual, or mixed.</param>
/// <param name="Reference">Connection string id / file set / endpoint (no secrets).</param>
/// <param name="Notes">Free-text notes.</param>
public sealed record BundleSource(
    [property: JsonPropertyName("mechanism")] string? Mechanism,
    [property: JsonPropertyName("reference")] string? Reference,
    [property: JsonPropertyName("notes")] string? Notes
);

/// <summary>A credit-card product with tariffs, aliases, and image references.</summary>
/// <param name="ProductId">Canonical product identifier (e.g. <c>"TC-NL"</c>).</param>
/// <param name="ProductName">Full display name (e.g. <c>"Tarjeta de Crédito NL"</c>).</param>
/// <param name="Aliases">Alternative names the bank uses for this product (e.g. <c>["NL"]</c>).</param>
/// <param name="HasRewardsProgram">Whether the product participates in a rewards program.</param>
/// <param name="CardImage">Reference to the product card image (catalog/cards/...).</param>
/// <param name="ImportantMessageImage">Reference to the product's important-message image.</param>
/// <param name="Tariffs">Annual commission and other charges for this product.</param>
public sealed record VecProduct(
    [property: JsonPropertyName("productId")] string ProductId,
    [property: JsonPropertyName("productName")] string ProductName,
    [property: JsonPropertyName("aliases")] IReadOnlyList<string>? Aliases,
    [property: JsonPropertyName("hasRewardsProgram")] bool? HasRewardsProgram,
    [property: JsonPropertyName("cardImage")] ImageRef? CardImage,
    [property: JsonPropertyName("importantMessageImage")] ImageRef? ImportantMessageImage,
    [property: JsonPropertyName("tariffs")] ProductTariffs? Tariffs
);

/// <summary>Reference to a catalog image (URI + optional hashes).</summary>
/// <param name="ImageId">Stable catalog id for the image.</param>
/// <param name="Uri">Path or URL; resolved at runtime, not embedded.</param>
/// <param name="Sha256">SHA-256 hex digest of the image file.</param>
/// <param name="PerceptualHash">pHash/dHash for fuzzy visual comparison.</param>
/// <param name="Width">Pixel width.</param>
/// <param name="Height">Pixel height.</param>
/// <param name="Description">Human-readable description.</param>
public sealed record ImageRef(
    [property: JsonPropertyName("imageId")] string? ImageId,
    [property: JsonPropertyName("uri")] string? Uri,
    [property: JsonPropertyName("sha256")] string? Sha256,
    [property: JsonPropertyName("perceptualHash")] string? PerceptualHash,
    [property: JsonPropertyName("width")] int? Width,
    [property: JsonPropertyName("height")] int? Height,
    [property: JsonPropertyName("description")] string? Description
);

/// <summary>Annual commission and other charges for a product.</summary>
/// <param name="AnnualCommission">Annual commission in the specified currency.</param>
/// <param name="Currency">Currency code (default <c>"MXN"</c>).</param>
/// <param name="OtherCharges">Additional named charges.</param>
public sealed record ProductTariffs(
    [property: JsonPropertyName("annualCommission")] decimal? AnnualCommission,
    [property: JsonPropertyName("currency")] string? Currency,
    [property: JsonPropertyName("otherCharges")] IReadOnlyList<OtherCharge>? OtherCharges
);

/// <summary>An additional named charge on a product.</summary>
/// <param name="Name">Charge name.</param>
/// <param name="Amount">Charge amount.</param>
public sealed record OtherCharge(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("amount")] decimal Amount
);

/// <summary>TASA (interest rate) entries for one product across multiple periods.</summary>
/// <param name="ProductId">Canonical product id this rate table applies to.</param>
/// <param name="RatesByPeriod">Ordered rate entries, one per billing period.</param>
public sealed record InterestRateEntry(
    [property: JsonPropertyName("productId")] string ProductId,
    [property: JsonPropertyName("ratesByPeriod")] IReadOnlyList<RateByPeriod> RatesByPeriod
);

/// <summary>Annual ordinary fixed rate for one billing period.</summary>
/// <param name="AnnualOrdinaryFixedRate">Decimal fraction (e.g. 0.1975 = 19.75%).</param>
/// <param name="PeriodLabel">Human-readable period label, e.g. <c>"Sep Oct"</c>.</param>
/// <param name="PeriodStart">ISO-8601 date string for period start.</param>
/// <param name="PeriodEnd">ISO-8601 date string for period end.</param>
public sealed record RateByPeriod(
    [property: JsonPropertyName("annualOrdinaryFixedRate")] decimal AnnualOrdinaryFixedRate,
    [property: JsonPropertyName("periodLabel")] string? PeriodLabel,
    [property: JsonPropertyName("periodStart")] string? PeriodStart,
    [property: JsonPropertyName("periodEnd")] string? PeriodEnd
);

/// <summary>A mandatory legend that must appear in the statement (checklist item 46).</summary>
/// <param name="LegendId">Stable identifier for the legend.</param>
/// <param name="Text">Expected text content.</param>
/// <param name="MatchMode">How to compare: <c>exact</c>, <c>normalized</c>, or <c>contains</c>.</param>
/// <param name="AppliesToProducts">Product ids this legend applies to; absent/empty = all products.</param>
/// <param name="Section">Section of the statement where the legend should appear.</param>
/// <param name="Required">Whether absence of this legend constitutes a defect (default true).</param>
public sealed record MandatoryLegend(
    [property: JsonPropertyName("legendId")] string LegendId,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("matchMode")] string? MatchMode,
    [property: JsonPropertyName("appliesToProducts")] IReadOnlyList<string>? AppliesToProducts,
    [property: JsonPropertyName("section")] string? Section,
    [property: JsonPropertyName("required")] bool? Required
);

/// <summary>One entry in the ordered sequential image list (checklist item 47).</summary>
/// <param name="Order">1-based position in the sequence.</param>
/// <param name="Image">Image reference.</param>
/// <param name="ProductId">Product this entry applies to; absent = all products.</param>
public sealed record SequentialImage(
    [property: JsonPropertyName("order")] int Order,
    [property: JsonPropertyName("image")] ImageRef Image,
    [property: JsonPropertyName("productId")] string? ProductId
);

/// <summary>A promotional insert with a validity window (checklist item 49).</summary>
/// <param name="PromotionId">Stable identifier for the promotion.</param>
/// <param name="Image">Image reference for the promotional insert.</param>
/// <param name="ValidFrom">ISO-8601 date from which the promotion is valid.</param>
/// <param name="ValidTo">ISO-8601 date until which the promotion is valid.</param>
/// <param name="AppliesToProducts">Product ids this promotion applies to; absent/empty = all products.</param>
public sealed record Promotion(
    [property: JsonPropertyName("promotionId")] string PromotionId,
    [property: JsonPropertyName("image")] ImageRef Image,
    [property: JsonPropertyName("validFrom")] string ValidFrom,
    [property: JsonPropertyName("validTo")] string ValidTo,
    [property: JsonPropertyName("appliesToProducts")] IReadOnlyList<string>? AppliesToProducts
);

/// <summary>Client master data for statement header field validation (items 2–8).</summary>
/// <param name="ClientId">Internal client identifier.</param>
/// <param name="ClientName">Structured name (first names, last names, full).</param>
/// <param name="Rfc">Tax registration number (RFC).</param>
/// <param name="ClientNumber">Bank-assigned client number.</param>
/// <param name="Address">Mailing address.</param>
/// <param name="Accounts">Accounts associated with this client.</param>
public sealed record ClientAccount(
    [property: JsonPropertyName("clientId")] string ClientId,
    [property: JsonPropertyName("clientName")] ClientName? ClientName,
    [property: JsonPropertyName("rfc")] string? Rfc,
    [property: JsonPropertyName("clientNumber")] string? ClientNumber,
    [property: JsonPropertyName("address")] Address? Address,
    [property: JsonPropertyName("accounts")] IReadOnlyList<AccountEntry>? Accounts
);

/// <summary>Structured client name.</summary>
/// <param name="FirstNames">Given name(s).</param>
/// <param name="LastNames">Family name(s).</param>
/// <param name="Full">Full formatted name.</param>
public sealed record ClientName(
    [property: JsonPropertyName("firstNames")] string? FirstNames,
    [property: JsonPropertyName("lastNames")] string? LastNames,
    [property: JsonPropertyName("full")] string? Full
);

/// <summary>A physical or mailing address.</summary>
/// <param name="Street">Street name.</param>
/// <param name="Number">Street number.</param>
/// <param name="Neighborhood">Neighborhood / colonia.</param>
/// <param name="PostalCode">5-digit postal code.</param>
/// <param name="State">State name.</param>
public sealed record Address(
    [property: JsonPropertyName("street")] string? Street,
    [property: JsonPropertyName("number")] string? Number,
    [property: JsonPropertyName("neighborhood")] string? Neighborhood,
    [property: JsonPropertyName("postalCode")] string? PostalCode,
    [property: JsonPropertyName("state")] string? State
);

/// <summary>An account entry within a client record.</summary>
/// <param name="AccountRef">Internal join key used to link prior statements and transactions.</param>
/// <param name="ProductId">Canonical product id for this account.</param>
/// <param name="CardNumber">Card number (may be masked; matcher honors masking).</param>
/// <param name="Clabe">CLABE interbank account number.</param>
/// <param name="BranchNumber">Branch number.</param>
/// <param name="CreditLine">Approved credit line amount.</param>
/// <param name="AccountOpenDate">ISO-8601 date when the account was opened.</param>
public sealed record AccountEntry(
    [property: JsonPropertyName("accountRef")] string AccountRef,
    [property: JsonPropertyName("productId")] string ProductId,
    [property: JsonPropertyName("cardNumber")] string? CardNumber,
    [property: JsonPropertyName("clabe")] string? Clabe,
    [property: JsonPropertyName("branchNumber")] string? BranchNumber,
    [property: JsonPropertyName("creditLine")] decimal? CreditLine,
    [property: JsonPropertyName("accountOpenDate")] string? AccountOpenDate
);

/// <summary>Prior-month closing values for cross-period checks (items 17, 36, 40).</summary>
/// <param name="AccountRef">Account this statement belongs to.</param>
/// <param name="Period">Period covered by this prior statement.</param>
/// <param name="ClosingBalances">Closing balances at end of the prior period.</param>
/// <param name="Installments">Open MSI (meses sin intereses) purchases carried over from this period.</param>
/// <param name="DocumentRef">Reference to the prior statement PDF.</param>
public sealed record PriorStatement(
    [property: JsonPropertyName("accountRef")] string AccountRef,
    [property: JsonPropertyName("period")] PeriodRange Period,
    [property: JsonPropertyName("closingBalances")] ClosingBalances? ClosingBalances,
    [property: JsonPropertyName("installments")] IReadOnlyList<InstallmentEntry>? Installments,
    [property: JsonPropertyName("documentRef")] DocumentRef? DocumentRef
);

/// <summary>Closing balances from the previous billing cycle.</summary>
/// <param name="PagoParaNoGenerarIntereses">Payment required to avoid interest charges.</param>
/// <param name="SaldoDeudorTotal">Total outstanding balance.</param>
/// <param name="RewardsPointsBalance">Rewards points balance.</param>
/// <param name="RewardsPesosBalance">Rewards pesos (cash-equivalent) balance.</param>
public sealed record ClosingBalances(
    [property: JsonPropertyName("pagoParaNoGenerarIntereses")] decimal? PagoParaNoGenerarIntereses,
    [property: JsonPropertyName("saldoDeudorTotal")] decimal? SaldoDeudorTotal,
    [property: JsonPropertyName("rewardsPointsBalance")] decimal? RewardsPointsBalance,
    [property: JsonPropertyName("rewardsPesosBalance")] decimal? RewardsPesosBalance
);

/// <summary>An open MSI installment purchase carried from the prior period (items 40/41).</summary>
/// <param name="PurchaseId">Stable identifier for the original purchase.</param>
/// <param name="Description">Merchant / purchase description.</param>
/// <param name="SaldoPendiente">Remaining balance on the installment plan.</param>
/// <param name="PagoRequerido">This month's required payment.</param>
/// <param name="NumeroDePago">Current payment number (e.g. 5 of 12).</param>
/// <param name="TotalPagos">Total number of installment payments.</param>
public sealed record InstallmentEntry(
    [property: JsonPropertyName("purchaseId")] string PurchaseId,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("saldoPendiente")] decimal? SaldoPendiente,
    [property: JsonPropertyName("pagoRequerido")] decimal? PagoRequerido,
    [property: JsonPropertyName("numeroDePago")] int? NumeroDePago,
    [property: JsonPropertyName("totalPagos")] int? TotalPagos
);

/// <summary>Reference to a document (URI + optional SHA-256).</summary>
/// <param name="Uri">Relative or absolute URI to the document.</param>
/// <param name="Sha256">SHA-256 hex digest of the document.</param>
public sealed record DocumentRef(
    [property: JsonPropertyName("uri")] string? Uri,
    [property: JsonPropertyName("sha256")] string? Sha256
);

/// <summary>Ground-truth transaction entries for one account (items 45/58).</summary>
/// <param name="AccountRef">Account these transactions belong to.</param>
/// <param name="Transactions">Ordered list of expected transactions.</param>
public sealed record ExpectedTransactionGroup(
    [property: JsonPropertyName("accountRef")] string AccountRef,
    [property: JsonPropertyName("transactions")] IReadOnlyList<ExpectedTransaction> Transactions
);

/// <summary>A single expected transaction (from the Excel "Detalle de operaciones").</summary>
/// <param name="Description">Transaction description as it should appear on the statement.</param>
/// <param name="Amount">Transaction amount (absolute value).</param>
/// <param name="OperationDate">ISO-8601 date when the operation occurred.</param>
/// <param name="ChargeDate">ISO-8601 date when the charge was applied.</param>
/// <param name="Sign"><c>"+"</c> for credits, <c>"-"</c> for debits.</param>
public sealed record ExpectedTransaction(
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("amount")] decimal Amount,
    [property: JsonPropertyName("operationDate")] string? OperationDate,
    [property: JsonPropertyName("chargeDate")] string? ChargeDate,
    [property: JsonPropertyName("sign")] string? Sign
);

/// <summary>Tolerance bands for verification checks (green vs. red thresholds from the Excel).</summary>
/// <param name="CurrencyToleranceMxn">Currency tolerance in MXN (default 0.50).</param>
/// <param name="PointsTolerance">Points tolerance (default 1.00).</param>
/// <param name="RewardsPesosToleranceMxn">Rewards-pesos tolerance in MXN (default 1.00).</param>
/// <param name="PointsToPesosExchangeRate">Exchange rate for converting points to pesos (default 0.10).</param>
public sealed record ToleranceConfig(
    [property: JsonPropertyName("currencyToleranceMxn")] decimal? CurrencyToleranceMxn,
    [property: JsonPropertyName("pointsTolerance")] decimal? PointsTolerance,
    [property: JsonPropertyName("rewardsPesosToleranceMxn")] decimal? RewardsPesosToleranceMxn,
    [property: JsonPropertyName("pointsToPesosExchangeRate")] decimal? PointsToPesosExchangeRate
);

/// <summary>Constants used across validation checks.</summary>
/// <param name="RequiredFontFamily">Font family that must appear in the statement (default <c>"Aptos"</c>).</param>
/// <param name="BankingYearDays">Days in the banking year for interest calculations (default 360).</param>
/// <param name="CatAnnualCommissionMxn">Annual commission amount used in CAT calculation (default 1500).</param>
public sealed record ValidationConstants(
    [property: JsonPropertyName("requiredFontFamily")] string? RequiredFontFamily,
    [property: JsonPropertyName("bankingYearDays")] int? BankingYearDays,
    [property: JsonPropertyName("catAnnualCommissionMxn")] decimal? CatAnnualCommissionMxn
);
