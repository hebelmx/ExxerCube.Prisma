using System;
using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Ocr;
using ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.Resolution;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ExxerCube.Prisma.Veriqan.Infrastructure.Extraction.DependencyInjection;

/// <summary>
/// <see cref="IServiceCollection"/> extension methods for the Veriqan extraction infrastructure.
/// </summary>
public static class VeriqanExtractionExtensions
{
    /// <summary>
    /// Registers the PdfPig-based <see cref="IStatementFieldExtractor"/> implementation,
    /// the <see cref="PdfExtractionOptions"/> configuration, the <see cref="IPasswordProvider"/>
    /// (null no-op when no password configuration is present), and any supporting services
    /// required by the Veriqan extraction pipeline.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <param name="config">
    /// Optional application configuration used to bind <c>Veriqan:PdfExtraction</c> options.
    /// When <see langword="null"/> the defaults defined in <see cref="PdfExtractionOptions"/>
    /// are used (50 MB limit, 30 s timeout).
    /// </param>
    /// <returns>The same <paramref name="services"/> instance for chaining.</returns>
    public static IServiceCollection AddVeriqanExtraction(
        this IServiceCollection services,
        IConfiguration? config = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Bind PDF-extraction options from configuration (or use defaults when config is absent).
        if (config is not null)
        {
            services.Configure<PdfExtractionOptions>(
                config.GetSection(PdfExtractionOptions.Section));
        }
        else
        {
            services.Configure<PdfExtractionOptions>(_ => { /* defaults applied by class initialiser */ });
        }

        // Password provider — null (no-op) unless a custom implementation is registered before
        // AddVeriqanExtraction is called (TryAdd semantics: first registration wins).
        services.TryAddSingleton<IPasswordProvider, NullPasswordProvider>();

        // Stage 1 (positional) — registered as its own concrete singleton so the strangler-fig
        // decorator below can wrap it directly without resolving IStatementFieldExtractor
        // recursively through itself.
        //
        // VERIQAN C1.3/C1.4/C1.7/C2.1b: the geometric-plausibility confidence killswitch
        // (PdfExtractionOptions.EmitGeometricConfidence, default TRUE / ARMED since the C1.7
        // owner ruling 2026-07-17, see that property's remarks) is threaded through here
        // explicitly rather than relying on the extractor ctor's own `emitGeometricConfidence =
        // false` default — that ctor default stays behaviour-neutral for direct/test
        // construction; arming for production happens at this composition root by reading
        // configuration, so it can be flipped OFF via Veriqan:PdfExtraction:EmitGeometricConfidence
        // without a code change or redeploy. ONE flag arms all four slices together: Tasa/Cat (C1.2),
        // the 7 RESUMEN money fields (C1.4), the DESGLOSE total row (C1.6), and the C2.1b
        // recompute-operand HeaderMoney fields (SaldoCargosRegulares / PagoParaNoGenerarIntereses).
        services.TryAddSingleton(sp =>
        {
            var pdfOptions = sp.GetRequiredService<IOptions<PdfExtractionOptions>>();
            return new PdfPigStatementFieldExtractor(
                sp.GetRequiredService<ILogger<PdfPigStatementFieldExtractor>>(),
                pdfOptions,
                sp.GetRequiredService<IPasswordProvider>(),
                timeProvider: null,
                enableCatalogImageHashing: false,
                emitGeometricConfidence: pdfOptions.Value.EmitGeometricConfidence);
        });

        // Per-field progressive fallback-extraction chain (E1): every FieldKind ladder is empty
        // (positional-only) until a later epic registers higher stages, so the decorator below is
        // behavior-neutral — it returns PdfPigStatementFieldExtractor's own result unchanged.
        services.TryAddSingleton<IFieldEscalationLadderRegistry, FieldEscalationLadderRegistry>();

        // Header-image OCR engine (E7.S7.2/S7.3): SINGLETON — the native TesseractEngine must be
        // created at most once per process (deadlock on a second concurrent instantiation) and is
        // not thread-safe; TesseractHeaderProductOcrEngine serializes access internally.
        services.TryAddSingleton<IHeaderProductOcrEngine, TesseractHeaderProductOcrEngine>();

        // §-anchor OCR escalation ladder (RC1.S6): shares the SAME singleton OCR engine above —
        // its semaphore already serializes all native Tesseract access, so this stage adds no new
        // engine instance and no new deadlock risk. No-op (reference-preserving pass-through) for
        // every document whose text layer alone finds ≥ 2 present sections (all synthetic/demo
        // fixtures), so registering it unconditionally is safe.
        services.TryAddSingleton<SectionAnchorOcrEscalationStage>();

        // Stage-provider seam (E2.2): resolves the concrete higher-stage implementations for a
        // field. DefaultFieldStageProvider registers a fuzzy label-anchor stage for
        // FieldKind.PaymentDueDate and (E7.S7.2/S7.3) a header-image OCR stage for
        // FieldKind.Product (each field's ladder — see FieldEscalationLadderRegistry — only
        // escalates to its higher stage when the positional extractor found nothing); every
        // other field still returns no stages, so the orchestrator remains behavior-neutral for
        // them. Registered as a singleton so the OCR engine it wraps is not re-resolved per call.
        services.TryAddSingleton<IFieldStageProvider, DefaultFieldStageProvider>();
        services.TryAddSingleton<FieldResolutionOrchestrator>();

        // IProductResolver (Story 3.3a) must already be registered — callers compose this
        // extraction registration alongside AddVeriqanBinding() (Application layer), which is
        // where IProductResolver is registered as a singleton; this factory only runs once the
        // full container is resolved, so registration order between the two calls does not matter.
        services.TryAddSingleton<IStatementFieldExtractor>(sp => new EscalatingStatementFieldExtractor(
            sp.GetRequiredService<PdfPigStatementFieldExtractor>(),
            sp.GetRequiredService<FieldResolutionOrchestrator>(),
            sp.GetRequiredService<IProductResolver>(),
            sp.GetRequiredService<SectionAnchorOcrEscalationStage>(),
            sp.GetRequiredService<ILogger<EscalatingStatementFieldExtractor>>()));

        return services;
    }
}
