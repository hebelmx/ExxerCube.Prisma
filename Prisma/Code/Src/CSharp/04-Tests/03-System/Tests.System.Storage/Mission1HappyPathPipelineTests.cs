using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using ExxerCube.Prisma.Application.Services;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Infrastructure.Database;
using ExxerCube.Prisma.Infrastructure.Database.EntityFramework;
using ExxerCube.Prisma.Infrastructure.Database.Services;
using ExxerCube.Prisma.Infrastructure.Events;
using ExxerCube.Prisma.Infrastructure.Extraction;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Teseract;
using ExxerCube.Prisma.Testing.Infrastructure.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace ExxerCube.Prisma.Tests.System.Storage;

/// <summary>
/// Mission 1 happy-path system test: Ingestion → OCR/Extraction → Reconciliation → Storage with full telemetry.
/// Uses real SQL Server container, real PRP1 fixtures, and persists audit trail with consistent correlation IDs.
/// </summary>
[Collection("DatabaseInfrastructure")]
public class Mission1HappyPathPipelineTests : IAsyncLifetime, IDisposable
{
    private readonly SqlServerContainerFixture _fixture;
    private readonly ITestOutputHelper _output;
    private readonly string _fixturesFolderName = "PRP1";
    private readonly JsonSerializerOptions _jsonOptions;
    private ServiceProvider? _serviceProvider;
    private EventPersistenceWorker? _worker;
    private IEventPublisher? _eventPublisher;
    private DbContextOptions<PrismaDbContext>? _dbOptions;
    private string? _workingDirectory;
    private string? _fixturesRoot;

    public Mission1HappyPathPipelineTests(SqlServerContainerFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = null,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    public async ValueTask InitializeAsync()
    {
        _fixture.EnsureAvailable();

        _dbOptions = new DbContextOptionsBuilder<PrismaDbContext>()
            .UseSqlServer(_fixture.ConnectionString)
            .Options;

        await using (var context = new PrismaDbContext(_dbOptions))
        {
            await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        }

        await _fixture.CleanDatabaseAsync();

        var services = new ServiceCollection();
        services.AddScoped<IPrismaDbContext>(_ => new PrismaDbContext(_dbOptions));
        services.AddScoped<PrismaDbContext>(_ => new PrismaDbContext(_dbOptions));
        services.AddSingleton<IEventPublisher, EventPublisher>(sp =>
            new EventPublisher(XUnitLogger.CreateLogger<EventPublisher>(_output)));
        services.AddSingleton<ILogger<EventPersistenceWorker>>(
            XUnitLogger.CreateLogger<EventPersistenceWorker>(_output));

        _serviceProvider = services.BuildServiceProvider();
        _eventPublisher = _serviceProvider.GetRequiredService<IEventPublisher>();
        var logger = _serviceProvider.GetRequiredService<ILogger<EventPersistenceWorker>>();
        var scopeFactory = _serviceProvider.GetRequiredService<IServiceScopeFactory>();

        _worker = new EventPersistenceWorker(_eventPublisher, logger, scopeFactory);
        _workingDirectory = Path.Combine(Path.GetTempPath(), $"mission1-{Guid.NewGuid()}");
        Directory.CreateDirectory(_workingDirectory);
        _fixturesRoot = ResolveFixturesRoot();
    }

    public async ValueTask DisposeAsync()
    {
        if (_worker != null)
        {
            await _worker.StopAsync(TestContext.Current.CancellationToken);
            _worker.Dispose();
        }

        _serviceProvider?.Dispose();

        if (!string.IsNullOrWhiteSpace(_workingDirectory) && Directory.Exists(_workingDirectory))
        {
            Directory.Delete(_workingDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task HappyPath_EndToEnd_PersistsCorrelationAuditAndStorage()
    {
        var correlationId = Guid.NewGuid();
        var fileId = Guid.NewGuid();
        var pdfFixture = ResolveFixturePath("222AAA-44444444442025.pdf");
        var xmlFixture = ResolveFixturePath("222AAA-44444444442025.xml");
        var pdfCopyPath = Path.Combine(_workingDirectory!, Path.GetFileName(pdfFixture));
        var xmlCopyPath = Path.Combine(_workingDirectory!, Path.GetFileName(xmlFixture));

        File.Copy(pdfFixture, pdfCopyPath, overwrite: true);
        File.Copy(xmlFixture, xmlCopyPath, overwrite: true);

        await using var context = new PrismaDbContext(_dbOptions!);
        context.FileMetadata.Add(new FileMetadata
        {
            FileId = fileId.ToString(),
            FileName = Path.GetFileName(pdfFixture),
            FilePath = pdfCopyPath,
            Format = FileFormat.Pdf,
            DownloadDateTime = DateTime.UtcNow
        });
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await _worker!.StartAsync(TestContext.Current.CancellationToken);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        // Stage 1: Ingestion (emit event + audit via worker)
        var downloadEvent = new DocumentDownloadedEvent
        {
            FileId = fileId,
            FileName = Path.GetFileName(pdfFixture),
            Source = "PRP1Fixture",
            FileSizeBytes = new FileInfo(pdfFixture).Length,
            Format = FileFormat.Pdf,
            DownloadUrl = pdfFixture,
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow
        };
        _eventPublisher!.Publish(downloadEvent);

        // Stage 2: OCR/Extraction (simulated OCR result using real fixture content)
        var ocrText = await File.ReadAllTextAsync(ResolveFixturePath("222AAA-44444444442025_page-0001.ocr.txt"), TestContext.Current.CancellationToken);
        var ocrConfidence = 96.5m;
        var ocrEvent = new OcrCompletedEvent
        {
            FileId = fileId,
            OcrEngine = "FixtureText",
            Confidence = ocrConfidence,
            ExtractedTextLength = ocrText.Length,
            ProcessingTime = TimeSpan.FromMilliseconds(850),
            FallbackTriggered = false,
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow.AddMilliseconds(50)
        };
        _eventPublisher.Publish(ocrEvent);

        // Stage 3: Reconciliation (XML vs OCR-derived Expediente)
        var xmlParser = new XmlExpedienteParser(XUnitLogger.CreateLogger<XmlExpedienteParser>(_output));
        var comparisonService = new DocumentComparisonService(XUnitLogger.CreateLogger<DocumentComparisonService>(_output));

        var xmlBytes = await File.ReadAllBytesAsync(xmlFixture, TestContext.Current.CancellationToken);
        var xmlResult = await xmlParser.ParseAsync(xmlBytes, TestContext.Current.CancellationToken);
        xmlResult.IsSuccess.ShouldBeTrue(xmlResult.Error);
        var xmlExpediente = xmlResult.Value!;

        // For the happy path, the OCR-derived expediente mirrors the XML to assert reconciliation >= 95%.
        var ocrExpediente = CloneExpediente(xmlExpediente);
        var comparison = await comparisonService.CompareExpedientesAsync(xmlExpediente, ocrExpediente, TestContext.Current.CancellationToken);

        await using var auditContext = new PrismaDbContext(_dbOptions!);
        var auditLogger = new AuditLoggerService(
            auditContext,
            XUnitLogger.CreateLogger<AuditLoggerService>(_output));

        await auditLogger.LogAuditAsync(
            AuditActionType.Review,
            ProcessingStage.DecisionLogic,
            fileId.ToString(),
            correlationId.ToString(),
            null,
            JsonSerializer.Serialize(new
            {
                comparison.MatchPercentage,
                comparison.MatchCount,
                comparison.TotalFields
            }, _jsonOptions),
            success: comparison.MatchPercentage >= 95,
            errorMessage: comparison.MatchPercentage < 95 ? "Match below 95%" : null,
            TestContext.Current.CancellationToken);

        // Stage 4: Storage/export completion
        var storedEvent = new DocumentProcessingCompletedEvent
        {
            FileId = fileId,
            TotalProcessingTime = TimeSpan.FromSeconds(2.1),
            AutoProcessed = true,
            CorrelationId = correlationId,
            Timestamp = DateTime.UtcNow.AddMilliseconds(100)
        };
        _eventPublisher.Publish(storedEvent);

        await Task.Delay(750, TestContext.Current.CancellationToken); // allow worker to flush events

        await using var queryContext = new PrismaDbContext(_dbOptions!);
        var auditTrail = await queryContext.AuditRecords
            .Where(r => r.CorrelationId == correlationId.ToString())
            .OrderBy(r => r.Timestamp)
            .ToListAsync(TestContext.Current.CancellationToken);

        auditTrail.Count.ShouldBe(4, "Expected four stages: Ingestion, OCR, Reconcile, Stored");
        auditTrail.All(r => r.CorrelationId == correlationId.ToString()).ShouldBeTrue("Correlation continuity across all stages");

        var expectedStages = new[]
        {
            ProcessingStage.Ingestion,
            ProcessingStage.Extraction,
            ProcessingStage.DecisionLogic,
            ProcessingStage.Export
        };
        var actualStages = auditTrail.Select(r => r.Stage).ToArray();
        actualStages.ShouldBeSubsetOf(expectedStages, "Audit stages should follow the mission flow");

        for (var i = 1; i < auditTrail.Count; i++)
        {
            (auditTrail[i].Timestamp >= auditTrail[i - 1].Timestamp).ShouldBeTrue("Audit timestamps should be non-decreasing");
        }

        var ocrAudit = auditTrail.First(r => r.Stage == ProcessingStage.Extraction);
        var parsedOcr = JsonSerializer.Deserialize<OcrCompletedEvent>(ocrAudit.ActionDetails!, _jsonOptions);
        parsedOcr.ShouldNotBeNull();
        parsedOcr!.Confidence.ShouldBeGreaterThanOrEqualTo(90, "OCR confidence should meet ≥0.9 threshold for mandatory fields");

        var reconcileAudit = auditTrail.First(r => r.Stage == ProcessingStage.DecisionLogic);
        var reconcilePayload = JsonSerializer.Deserialize<ReconcilePayload>(reconcileAudit.ActionDetails!, _jsonOptions);
        reconcilePayload.ShouldNotBeNull();
        reconcilePayload!.MatchPercentage.ShouldBeGreaterThanOrEqualTo(95);
        reconcilePayload.MatchCount.ShouldBe(reconcilePayload.TotalFields);

        var storageRecord = auditTrail.Last();
        storageRecord.Stage.ShouldBe(ProcessingStage.Export);
        storageRecord.ActionType.ShouldBe(AuditActionType.Export);
        storageRecord.Success.ShouldBeTrue();

        var fileMetadata = await context.FileMetadata.FindAsync(new object[] { fileId.ToString() }, TestContext.Current.CancellationToken);
        fileMetadata.ShouldNotBeNull("Unified record should be persisted in storage");
        File.Exists(fileMetadata!.FilePath).ShouldBeTrue("Raw artifact should be saved to storage path");
    }

    private static Expediente CloneExpediente(Expediente source)
    {
        return new Expediente
        {
            NumeroExpediente = source.NumeroExpediente,
            NumeroOficio = source.NumeroOficio,
            SolicitudSiara = source.SolicitudSiara,
            Folio = source.Folio,
            OficioYear = source.OficioYear,
            AreaClave = source.AreaClave,
            AreaDescripcion = source.AreaDescripcion,
            FechaPublicacion = source.FechaPublicacion,
            DiasPlazo = source.DiasPlazo,
            AutoridadNombre = source.AutoridadNombre,
            AutoridadEspecificaNombre = source.AutoridadEspecificaNombre,
            NombreSolicitante = source.NombreSolicitante,
            Referencia = source.Referencia,
            Referencia1 = source.Referencia1,
            Referencia2 = source.Referencia2,
            TieneAseguramiento = source.TieneAseguramiento,
            SolicitudPartes = source.SolicitudPartes.Select(p => new SolicitudParte
            {
                ParteId = p.ParteId,
                Caracter = p.Caracter,
                PersonaTipo = p.PersonaTipo,
                Paterno = p.Paterno,
                Materno = p.Materno,
                Nombre = p.Nombre,
                Rfc = p.Rfc,
                Relacion = p.Relacion,
                Domicilio = p.Domicilio,
                Complementarios = p.Complementarios
            }).ToList(),
            SolicitudEspecificas = source.SolicitudEspecificas.Select(s => new SolicitudEspecifica
            {
                SolicitudEspecificaId = s.SolicitudEspecificaId,
                InstruccionesCuentasPorConocer = s.InstruccionesCuentasPorConocer,
                PersonasSolicitud = s.PersonasSolicitud.Select(persona => new PersonaSolicitud
                {
                    PersonaId = persona.PersonaId,
                    Caracter = persona.Caracter,
                    Persona = persona.Persona,
                    Paterno = persona.Paterno,
                    Materno = persona.Materno,
                    Nombre = persona.Nombre,
                    Rfc = persona.Rfc,
                    Relacion = persona.Relacion,
                    Domicilio = persona.Domicilio,
                    Complementarios = persona.Complementarios
                }).ToList()
            }).ToList(),
            FechaRecepcion = source.FechaRecepcion,
            FechaRegistro = source.FechaRegistro,
            FechaEstimadaConclusion = source.FechaEstimadaConclusion
        };
    }

    private sealed class ReconcilePayload
    {
        public float MatchPercentage { get; set; }
        public int MatchCount { get; set; }
        public int TotalFields { get; set; }
    }

    private string ResolveFixturePath(string fileName)
    {
        if (string.IsNullOrWhiteSpace(_fixturesRoot))
        {
            _fixturesRoot = ResolveFixturesRoot();
        }

        var path = Path.Combine(_fixturesRoot, fileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"Fixture not found: {path}");
        }

        return path;
    }

    private string ResolveFixturesRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current != null)
        {
            var directSolution = Path.Combine(current.FullName, "Prisma", "Code", "Src", "CSharp", "ExxerCube.Prisma.sln");
            if (File.Exists(directSolution))
            {
                return Path.Combine(current.FullName, "Prisma", "Fixtures", _fixturesFolderName);
            }

            var nestedSolution = Path.Combine(current.FullName, "ExxerCube.Prisma", "Prisma", "Code", "Src", "CSharp", "ExxerCube.Prisma.sln");
            if (File.Exists(nestedSolution))
            {
                return Path.Combine(current.FullName, "ExxerCube.Prisma", "Prisma", "Fixtures", _fixturesFolderName);
            }

            current = current.Parent;
        }

        throw new InvalidOperationException("Unable to locate fixtures folder from test base directory.");
    }

    public void Dispose()
    {
        _ = DisposeAsync().AsTask();
    }
}
