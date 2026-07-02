using ExxerCube.Prisma.Infrastructure.Classification.Llm;
using ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Llm;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.DependencyInjection;

/// <summary>
/// Extension methods for registering extraction services in the dependency injection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds extraction services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddExtractionServices(this IServiceCollection services)
    {
        services.AddScoped<IFileTypeIdentifier, FileTypeIdentifierService>();
        services.AddScoped<IXmlNullableParser<Domain.Entities.Expediente>, XmlExpedienteParser>();

        // Register PDF-to-image converter port (rasterization for the PDF OCR field extractor)
        services.AddScoped<IPdfToImageConverter, Teseract.PdfToImageConverter>();

        // Register format-specific extractors
        services.AddScoped<XmlMetadataExtractor>();
        services.AddScoped<DocxMetadataExtractor>();
        services.AddScoped<PdfMetadataExtractor>();

        // Register composite extractor that delegates to format-specific ones
        services.AddScoped<IMetadataExtractor, CompositeMetadataExtractor>();

        // NOTE: Adaptive DOCX extraction system is registered at the application layer (API/Host)
        // Infrastructure projects should NOT depend on each other.
        // Call services.AddAdaptiveDocxExtraction() in your API/Host Startup/Program.cs after calling AddExtractionServices()

        // Register generic field extractors for Story 1.3
        // DocxFieldExtractor accepts an optional IOcrExecutor (D2 image-OCR path).
        // The executor is resolved from the container so the existing TesseractOcrExecutor singleton/scoped
        // instance is REUSED — do NOT new up a second engine (Tesseract same-process second-init DEADLOCK).
        services.AddScoped<IFieldExtractor<DocxSource>>(sp =>
            new Teseract.DocxFieldExtractor(
                sp.GetRequiredService<ILogger<Teseract.DocxFieldExtractor>>(),
                sp.GetService<IOcrExecutor>()));
        services.AddScoped<IFieldExtractor<PdfSource>, PdfOcrFieldExtractor>();
        // Register XML field extractor for CNBV/PRP1 structured XML documents
        services.AddScoped<IFieldExtractor<XmlSource>, XmlFieldExtractor>();

        // Register OCR executors with keyed services for runtime selection.
        // SINGLETON lifetime is MANDATORY for TesseractOcrExecutor — see its XML doc for details.
        // Short version: TesseractEngine may only be instantiated ONCE per process (native global state
        // deadlocks on a second init). Scoped/Transient lifetimes would create a new instance (and a new
        // engine) per scope/call, triggering the deadlock on any second OCR use in the same process.
        //
        // CRITICAL: register ONE concrete TesseractOcrExecutor singleton and FORWARD both the unkeyed
        // IOcrExecutor and the keyed "Tesseract" IOcrExecutor to that SAME instance. Registering the
        // concrete type twice (once keyed, once unkeyed) would build TWO executors → TWO engines → the
        // exact second-init deadlock this guards against, if both are ever resolved in one process.
        services.AddSingleton<Teseract.TesseractOcrExecutor>();
        services.AddKeyedSingleton<IOcrExecutor>(
            "Tesseract",
            (sp, _) => sp.GetRequiredService<Teseract.TesseractOcrExecutor>());

        // GOT-OCR2: Transformer-based, slower but more accurate (140s, 88%+ confidence)
        // DISABLED: Requires IPythonEnvironment which is not configured
        // services.AddKeyedScoped<IOcrExecutor, GotOcr2.GotOcr2OcrExecutor>("GotOcr2");

        // Default: Use Tesseract as primary (fast), fallback to GOT-OCR2 for low confidence
        services.AddSingleton<IOcrExecutor>(sp => sp.GetRequiredService<Teseract.TesseractOcrExecutor>());

        // Register comparison service
        services.AddScoped<IDocumentComparisonService, DocumentComparisonService>();

        // Register bulk processing service
        services.AddScoped<IBulkProcessingService, BulkProcessingService>();

        // Register OCR processing service (used by BulkProcessingService)
        services.AddScoped<IOcrProcessingService, Execution.OcrProcessingService>();

        // OCR text cleaning (raw + normalized forms retained)
        services.AddSingleton<ITextSanitizer, TextSanitizer>();
        services.AddSingleton<OcrSanitizationService>();

        // OCR session repository for data collection and model retraining
        services.AddSingleton<IOcrSessionRepository, Repositories.OcrSessionRepository>();

        // ----------------------------------------------------------------
        // S2: LLM vision track + reconciler — registered DARK.
        // LlmVisionFieldExtractor is a concrete scoped type (not bound to IFieldExtractor<ImageSource>)
        // so the deterministic pipeline is untouched until an orchestrator explicitly routes images to it.
        // IExtractionReconciler is wired but nothing calls it until S3 wiring.
        // ----------------------------------------------------------------
        services.AddScoped<LlmVisionFieldExtractor>();

        // Expose LlmVisionFieldExtractor as ILlmExpedienteExtractor<ImageSource> so
        // HybridExtractionService can inject by interface (mockable) and receive the full
        // Expediente with SolicitudPartes. Forwards to the same scoped instance.
        services.AddScoped<ILlmExpedienteExtractor<ImageSource>>(
            sp => sp.GetRequiredService<LlmVisionFieldExtractor>());

        services.AddScoped<IExtractionReconciler, ExtractionReconciler>();

        // ----------------------------------------------------------------
        // S3a: HybridExtractionService — orchestrates deterministic + LLM tracks.
        // Ships DARK: LlmProviders:TextExtractorEnabled and VisionExtractorEnabled both default false.
        // Injects ILlmExpedienteExtractor<T> (not IFieldExtractor<T>) for LLM tracks so the full
        // Expediente (including SolicitudPartes) is preserved without an ExtractedFields round-trip.
        // ----------------------------------------------------------------
        services.AddScoped<IHybridExtractionService>(sp => new HybridExtractionService(
            sp.GetRequiredService<IFieldExtractor<PdfSource>>(),
            sp.GetRequiredService<ILlmExpedienteExtractor<TxtSource>>(),
            sp.GetRequiredService<ILlmExpedienteExtractor<ImageSource>>(),
            sp.GetRequiredService<IPdfToImageConverter>(),
            sp.GetRequiredService<IExtractionReconciler>(),
            sp.GetRequiredService<IOptionsMonitor<LlmProvidersOptions>>(),
            sp.GetRequiredService<ILogger<HybridExtractionService>>()));

        return services;
    }
}