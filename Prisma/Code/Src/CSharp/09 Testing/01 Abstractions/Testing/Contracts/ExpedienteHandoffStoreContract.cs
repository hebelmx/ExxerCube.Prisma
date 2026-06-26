using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using Shouldly;
using Xunit;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Behavioral contract for <see cref="IExpedienteHandoffStore"/> — every implementation (the reference
/// <see cref="FakeExpedienteHandoffStore"/> and the production <c>FileSystemExpedienteHandoffStore</c>) must
/// pass these tests unchanged (ADR-005, ADR-011 Reconciliator edge).
/// </summary>
/// <remarks>
/// <para>
/// Uses the ADR-005 §3 default <strong>injected-<see cref="Sut"/></strong> mechanism. The class is
/// <c>abstract</c>, so xUnit does not discover it; each inherited test runs once per deriving class.
/// </para>
/// <para>
/// Scope rule (ADR-005 §5): only behavior <em>any</em> correct store must exhibit — round-trip of a saved
/// expediente, and fail-closed on null input, blank/escaping paths, and a missing artifact. The round-trip
/// asserts only reliably-serializable string/dictionary fields so the contract holds for the JSON-backed real
/// store as well as the in-memory fake; deeper serialization fidelity is the real SUT's own concern.
/// </para>
/// </remarks>
public abstract class ExpedienteHandoffStoreContract
{
    /// <summary>Initializes the contract with the store under test.</summary>
    /// <param name="sut">The <see cref="IExpedienteHandoffStore"/> implementation to verify.</param>
    protected ExpedienteHandoffStoreContract(IExpedienteHandoffStore sut)
    {
        ArgumentNullException.ThrowIfNull(sut);
        Sut = sut;
    }

    /// <summary>Gets the store under test.</summary>
    protected IExpedienteHandoffStore Sut { get; }

    private static Expediente SampleExpediente() => new()
    {
        NumeroExpediente = "A/AS1-2505-088637-PHM",
        NumeroOficio = "214-1-18714972/2025",
        AutoridadNombre = "CNBV",
        Referencia1 = "Bloqueo de cuentas",
        AdditionalFields = { ["NumeroOficio"] = "214-1-18714972/2025", ["Causa"] = "PLD" },
    };

    /// <summary>Contract: a saved expediente loads back with its core fields intact (round-trip).</summary>
    [Fact]
    public async Task SaveThenLoad_ValidExpediente_RoundTripsCoreFields()
    {
        var expediente = SampleExpediente();
        const string relative = "2026/06/12/round-trip.fusion.json";

        var save = await Sut.SaveAsync(expediente, relative, TestContext.Current.CancellationToken);
        save.IsSuccess.ShouldBeTrue();

        var load = await Sut.LoadAsync(relative, TestContext.Current.CancellationToken);

        load.IsSuccess.ShouldBeTrue();
        load.Value.ShouldNotBeNull();
        load.Value!.NumeroExpediente.ShouldBe(expediente.NumeroExpediente);
        load.Value.NumeroOficio.ShouldBe(expediente.NumeroOficio);
        load.Value.AutoridadNombre.ShouldBe(expediente.AutoridadNombre);
        load.Value.Referencia1.ShouldBe(expediente.Referencia1);
        load.Value.AdditionalFields.ShouldContainKeyAndValue("Causa", "PLD");
    }

    /// <summary>Contract: saving a null expediente fails closed (a failure Result, never an exception).</summary>
    [Fact]
    public async Task SaveAsync_NullExpediente_ReturnsFailure()
    {
        var result = await Sut.SaveAsync(null!, "2026/06/12/null.fusion.json", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>Contract: a blank relative path fails closed on both save and load.</summary>
    /// <param name="blank">A blank relative path.</param>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task BlankRelativePath_ReturnsFailure(string blank)
    {
        var save = await Sut.SaveAsync(SampleExpediente(), blank, TestContext.Current.CancellationToken);
        save.IsFailure.ShouldBeTrue();

        var load = await Sut.LoadAsync(blank, TestContext.Current.CancellationToken);
        load.IsFailure.ShouldBeTrue();
    }

    /// <summary>Contract: a relative path that would escape the storage base fails closed.</summary>
    /// <param name="escaping">A traversal/escape path.</param>
    [Theory]
    [InlineData("../../../../etc/passwd")]
    [InlineData("../outside.fusion.json")]
    [InlineData("2026/../../escape.fusion.json")]
    public async Task BaseEscapingPath_ReturnsFailure(string escaping)
    {
        var save = await Sut.SaveAsync(SampleExpediente(), escaping, TestContext.Current.CancellationToken);
        save.IsFailure.ShouldBeTrue();

        var load = await Sut.LoadAsync(escaping, TestContext.Current.CancellationToken);
        load.IsFailure.ShouldBeTrue();
    }

    /// <summary>Contract: loading a path with no stored artifact fails closed (never an exception).</summary>
    [Fact]
    public async Task LoadAsync_MissingArtifact_ReturnsFailure()
    {
        var result = await Sut.LoadAsync("2026/06/12/never-saved.fusion.json", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.IsFailure.ShouldBeTrue();
    }

    /// <summary>
    /// Contract: an expediente saved with a non-null <see cref="Expediente.BodyText"/> loads back with
    /// <c>BodyText</c> intact (Story 2.1b round-trip — the field must survive JSON serialization so the
    /// Reconciliator can read the OCR body text carried from the Extractor over the process boundary).
    /// </summary>
    [Fact]
    public async Task SaveThenLoad_WithBodyText_BodyTextRoundTrips()
    {
        var expediente = new Expediente
        {
            NumeroExpediente = "A/AS1-2505-BODY-TST",
            BodyText = "Este oficio notifica ASEGURAMIENTO de bienes por disposición judicial."
        };
        const string relative = "2026/06/25/body-text-round-trip.fusion.json";

        var save = await Sut.SaveAsync(expediente, relative, TestContext.Current.CancellationToken);
        save.IsSuccess.ShouldBeTrue();

        var load = await Sut.LoadAsync(relative, TestContext.Current.CancellationToken);

        load.IsSuccess.ShouldBeTrue();
        load.Value.ShouldNotBeNull();
        load.Value!.BodyText.ShouldBe(expediente.BodyText);
    }
}
