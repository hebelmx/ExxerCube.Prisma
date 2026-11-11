using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Tests.Domain.ValueObjects;

/// <summary>
/// Tests for SemanticAnalysis value object (the 5 situations).
/// Minimal coverage - proves the structure exists and can represent requirements.
/// </summary>
public class SemanticAnalysisTests
{
    [Fact]
    public void Can_Create_Semantic_Analysis_With_Bloqueo_Requirement()
    {
        // Arrange & Act
        var analysis = new SemanticAnalysis
        {
            RequiereBloqueo = new BloqueoRequirement
            {
                EsRequerido = true,
                EsParcial = false,
                Monto = 50000.00m,
                Moneda = "MXN",
                CuentasEspecificas = new List<string> { "9876543210" },
                ProductosEspecificos = new List<string> { "Cuenta de Ahorro" }
            }
        };

        // Assert
        analysis.RequiereBloqueo.ShouldNotBeNull();
        analysis.RequiereBloqueo.EsRequerido.ShouldBeTrue();
        analysis.RequiereBloqueo.EsParcial.ShouldBeFalse();
        analysis.RequiereBloqueo.Monto.ShouldBe(50000.00m);
        analysis.RequiereBloqueo.Moneda.ShouldBe("MXN");
        analysis.RequiereBloqueo.CuentasEspecificas.ShouldContain("9876543210");
    }

    [Fact]
    public void Can_Create_Semantic_Analysis_With_Desbloqueo_Requirement()
    {
        // Arrange & Act
        var analysis = new SemanticAnalysis
        {
            RequiereDesbloqueo = new DesbloqueoRequirement
            {
                EsRequerido = true,
                ExpedienteBloqueoOriginal = "A/AS1-2505-088637-PHM"
            }
        };

        // Assert
        analysis.RequiereDesbloqueo.ShouldNotBeNull();
        analysis.RequiereDesbloqueo.EsRequerido.ShouldBeTrue();
        analysis.RequiereDesbloqueo.ExpedienteBloqueoOriginal.ShouldBe("A/AS1-2505-088637-PHM");
    }

    [Fact]
    public void Can_Create_Semantic_Analysis_With_Documentacion_Requirement()
    {
        // Arrange & Act
        var analysis = new SemanticAnalysis
        {
            RequiereDocumentacion = new DocumentacionRequirement
            {
                EsRequerido = true,
                TiposDocumento = new List<DocumentoRequerido>
                {
                    new DocumentoRequerido
                    {
                        Tipo = "Estado de cuenta",
                        PeriodoInicio = new DateTime(2024, 1, 1),
                        PeriodoFin = new DateTime(2024, 12, 31)
                    },
                    new DocumentoRequerido
                    {
                        Tipo = "ID del cliente"
                    }
                }
            }
        };

        // Assert
        analysis.RequiereDocumentacion.ShouldNotBeNull();
        analysis.RequiereDocumentacion.EsRequerido.ShouldBeTrue();
        analysis.RequiereDocumentacion.TiposDocumento.Count.ShouldBe(2);
        analysis.RequiereDocumentacion.TiposDocumento[0].Tipo.ShouldBe("Estado de cuenta");
        analysis.RequiereDocumentacion.TiposDocumento[0].PeriodoInicio.ShouldBe(new DateTime(2024, 1, 1));
    }

    [Fact]
    public void Can_Create_Semantic_Analysis_With_Transferencia_Requirement()
    {
        // Arrange & Act
        var analysis = new SemanticAnalysis
        {
            RequiereTransferencia = new TransferenciaRequirement
            {
                EsRequerido = true,
                CuentaDestino = "1234567890",
                Monto = 10000.00m
            }
        };

        // Assert
        analysis.RequiereTransferencia.ShouldNotBeNull();
        analysis.RequiereTransferencia.EsRequerido.ShouldBeTrue();
        analysis.RequiereTransferencia.CuentaDestino.ShouldBe("1234567890");
        analysis.RequiereTransferencia.Monto.ShouldBe(10000.00m);
    }

    [Fact]
    public void Can_Create_Semantic_Analysis_With_Informacion_General_Requirement()
    {
        // Arrange & Act
        var analysis = new SemanticAnalysis
        {
            RequiereInformacionGeneral = new InformacionGeneralRequirement
            {
                EsRequerido = true,
                InformacionSolicitada = "Historial de transacciones del último año"
            }
        };

        // Assert
        analysis.RequiereInformacionGeneral.ShouldNotBeNull();
        analysis.RequiereInformacionGeneral.EsRequerido.ShouldBeTrue();
        analysis.RequiereInformacionGeneral.InformacionSolicitada.ShouldBe("Historial de transacciones del último año");
    }

    [Fact]
    public void All_Requirements_Are_Nullable_By_Default()
    {
        // Arrange & Act
        var analysis = new SemanticAnalysis();

        // Assert - all should be null until analysis is performed
        analysis.RequiereBloqueo.ShouldBeNull();
        analysis.RequiereDesbloqueo.ShouldBeNull();
        analysis.RequiereDocumentacion.ShouldBeNull();
        analysis.RequiereTransferencia.ShouldBeNull();
        analysis.RequiereInformacionGeneral.ShouldBeNull();
    }

    [Fact]
    public void Can_Represent_Multiple_Requirements_Simultaneously()
    {
        // Arrange & Act - real case might require bloqueo AND documentación
        var analysis = new SemanticAnalysis
        {
            RequiereBloqueo = new BloqueoRequirement { EsRequerido = true },
            RequiereDocumentacion = new DocumentacionRequirement { EsRequerido = true }
        };

        // Assert
        analysis.RequiereBloqueo.ShouldNotBeNull();
        analysis.RequiereBloqueo.EsRequerido.ShouldBeTrue();
        analysis.RequiereDocumentacion.ShouldNotBeNull();
        analysis.RequiereDocumentacion.EsRequerido.ShouldBeTrue();
    }

    [Fact]
    public void Has_Validation_State()
    {
        // Arrange & Act
        var analysis = new SemanticAnalysis();

        // Assert
        analysis.Validation.ShouldNotBeNull();
    }
}
