using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Infrastructure.FileSystem;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Tests.Infrastructure.FileSystem;

/// <summary>
/// Verifies that <see cref="FileSystemExpedienteHandoffStore"/> round-trips
/// <see cref="ExxerCube.Prisma.Domain.Enum.EnumModel"/>-derived fields with full fidelity through
/// the JSON handoff artifact (ADR-011 follow-up: SmartEnum JSON converter).
/// </summary>
/// <remarks>
/// These tests complement <see cref="FileSystemExpedienteHandoffStoreContractTests"/>: the contract
/// tests confirm string/dictionary fields; these tests confirm that SmartEnum fields come back as
/// the correct singleton instance after a JSON save→load cycle. Before the
/// <c>EnumModelJsonConverterFactory</c> was registered, System.Text.Json serialized each EnumModel
/// as a JSON object and deserialised it via the parameterless constructor — producing a zero-value
/// default rather than the original singleton.
/// </remarks>
public sealed class FileSystemExpedienteHandoffStore_SmartEnumRoundTripTests
{
    private static readonly string BasePath =
        Path.Combine(Path.GetTempPath(), "prisma-handoff-smartenum-roundtrip");

    private static FileSystemExpedienteHandoffStore BuildSut() =>
        new(
            new SharedStoragePathResolver(
                Options.Create(new StorageOptions { BasePath = BasePath }),
                NullLogger<SharedStoragePathResolver>.Instance),
            NullLogger<FileSystemExpedienteHandoffStore>.Instance);

    // ── LegalSubdivisionKind (direct field on Expediente) ──────────────────────

    /// <summary>
    /// LegalSubdivisionKind.J_AS (value 5) round-trips correctly; previously the
    /// parameterless-ctor default (value 0 / Unknown) would be returned instead.
    /// </summary>
    [Fact]
    public async Task SaveThenLoad_LegalSubdivisionKind_RoundTripsCorrectSingleton()
    {
        var sut = BuildSut();
        var expediente = new Expediente
        {
            NumeroExpediente = "A/AS1-2505-000001-PHM",
            Subdivision = LegalSubdivisionKind.J_AS,
        };
        const string path = "smartenum/subdivision-jas.fusion.json";

        var save = await sut.SaveAsync(expediente, path, TestContext.Current.CancellationToken);
        save.IsSuccess.ShouldBeTrue();

        var load = await sut.LoadAsync(path, TestContext.Current.CancellationToken);
        load.IsSuccess.ShouldBeTrue();
        load.Value.ShouldNotBeNull();

        var loaded = load.Value!.Subdivision;
        loaded.ShouldBe(LegalSubdivisionKind.J_AS);
        loaded.Value.ShouldBe(LegalSubdivisionKind.J_AS.Value);
        loaded.Name.ShouldBe("J_AS");
    }

    /// <summary>
    /// LegalSubdivisionKind.Other (value 999) round-trips; the non-sequential value
    /// makes it a good regression sentinel for look-up-by-name correctness.
    /// </summary>
    [Fact]
    public async Task SaveThenLoad_LegalSubdivisionKind_Other_RoundTrips()
    {
        var sut = BuildSut();
        var expediente = new Expediente
        {
            NumeroExpediente = "A/AS1-2505-000002-PHM",
            Subdivision = LegalSubdivisionKind.Other,
        };
        const string path = "smartenum/subdivision-other.fusion.json";

        var save = await sut.SaveAsync(expediente, path, TestContext.Current.CancellationToken);
        save.IsSuccess.ShouldBeTrue();

        var load = await sut.LoadAsync(path, TestContext.Current.CancellationToken);
        load.IsSuccess.ShouldBeTrue();

        load.Value!.Subdivision.ShouldBe(LegalSubdivisionKind.Other);
        load.Value.Subdivision.Value.ShouldBe(999);
    }

    /// <summary>
    /// LegalSubdivisionKind.Unknown (the default, value 0) round-trips correctly;
    /// this is the boundary case where both old (broken) and new (correct) paths
    /// might superficially appear to work — but only the new path sets the Name.
    /// </summary>
    [Fact]
    public async Task SaveThenLoad_LegalSubdivisionKind_Unknown_PreservesNameNotJustZeroValue()
    {
        var sut = BuildSut();
        var expediente = new Expediente
        {
            NumeroExpediente = "A/AS1-2505-000003-PHM",
            Subdivision = LegalSubdivisionKind.Unknown,
        };
        const string path = "smartenum/subdivision-unknown.fusion.json";

        var save = await sut.SaveAsync(expediente, path, TestContext.Current.CancellationToken);
        save.IsSuccess.ShouldBeTrue();

        var load = await sut.LoadAsync(path, TestContext.Current.CancellationToken);
        load.IsSuccess.ShouldBeTrue();

        var loaded = load.Value!.Subdivision;
        // Value==0 would pass even with the broken path (parameterless ctor gives 0).
        // Name=="Unknown" distinguishes the correct singleton from a raw default instance.
        loaded.Name.ShouldBe("Unknown");
        loaded.ShouldBe(LegalSubdivisionKind.Unknown);
    }

    // ── Multiple SmartEnum values in one expediente ─────────────────────────────

    /// <summary>
    /// Exercises all non-trivial LegalSubdivisionKind values in a single round-trip to
    /// confirm the factory handles multiple EnumModel instances in the same object graph.
    /// </summary>
    [Theory]
    [MemberData(nameof(AllSubdivisionKinds))]
    public async Task SaveThenLoad_AllLegalSubdivisionKinds_RoundTripCorrectly(
        LegalSubdivisionKind expected)
    {
        var sut = BuildSut();
        var expediente = new Expediente
        {
            NumeroExpediente = $"A/AS1-2505-{expected.Value:D6}-PHM",
            Subdivision = expected,
        };
        var path = $"smartenum/all-subdivisions/{expected.Name}.fusion.json";

        var save = await sut.SaveAsync(expediente, path, TestContext.Current.CancellationToken);
        save.IsSuccess.ShouldBeTrue(customMessage: $"Save failed for {expected.Name}");

        var load = await sut.LoadAsync(path, TestContext.Current.CancellationToken);
        load.IsSuccess.ShouldBeTrue(customMessage: $"Load failed for {expected.Name}");

        var loaded = load.Value!.Subdivision;
        loaded.ShouldBe(expected, customMessage: $"Round-trip mismatch for {expected.Name}");
        loaded.Value.ShouldBe(expected.Value);
        loaded.Name.ShouldBe(expected.Name);
    }

    /// <summary>All public singleton instances of <see cref="LegalSubdivisionKind"/>.</summary>
    public static TheoryData<LegalSubdivisionKind> AllSubdivisionKinds() =>
        new(EnumModel.GetAll<LegalSubdivisionKind>());
}
