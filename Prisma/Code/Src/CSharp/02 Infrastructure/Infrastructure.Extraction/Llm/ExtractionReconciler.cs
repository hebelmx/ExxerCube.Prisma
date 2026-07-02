using ExxerCube.Prisma.Domain.Llm;

namespace ExxerCube.Prisma.Infrastructure.Extraction.Ocr.Llm;

/// <summary>
/// PURE merge implementation of <see cref="IExtractionReconciler"/>.
/// No I/O, no LLM calls — only in-memory field selection over the candidate list.
/// </summary>
/// <remarks>
/// <para>Per-field merge policy (applied independently for each string/scalar field):</para>
/// <list type="number">
///   <item>A candidate whose <see cref="LabelledExtraction.Source"/> is <c>"deterministic"</c>
///         and whose field is non-null/non-empty wins unconditionally.</item>
///   <item>If the deterministic value is absent, the first LLM candidate with a non-empty value
///         fills the field (or all agreeing LLM candidates yield the same fill).</item>
///   <item>If two LLM candidates disagree on a field and deterministic is absent, the field is
///         left unresolved and a <see cref="ReconciliationResult.ReviewFlags"/> entry is emitted.</item>
/// </list>
/// <para>Header fields (NumeroOficio, AreaDescripcion, etc.) are copied wholesale from the
/// deterministic candidate — they are rarely extracted by LLMs.</para>
/// </remarks>
public sealed class ExtractionReconciler : IExtractionReconciler
{
    private readonly ILogger<ExtractionReconciler> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ExtractionReconciler"/>.
    /// </summary>
    /// <param name="logger">Structured logger for reconciliation events and review flags.</param>
    public ExtractionReconciler(ILogger<ExtractionReconciler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<Result<ReconciliationResult>> ReconcileAsync(
        IReadOnlyList<LabelledExtraction> candidates,
        CancellationToken cancellationToken = default)
    {
        if (cancellationToken.IsCancellationRequested)
            return Task.FromResult(ResultExtensions.Cancelled<ReconciliationResult>());

        if (candidates is null || candidates.Count == 0)
            return Task.FromResult(
                Result<ReconciliationResult>.WithFailure(
                    "No candidates provided for reconciliation."));

        // Identify the deterministic candidate (if available and successful).
        var det = candidates.FirstOrDefault(
            c => c.Source == "deterministic"
                 && c.Status == TrackStatus.Available
                 && c.Fields is not null);

        // All other candidates that produced a result.
        var llms = candidates
            .Where(c => c.Source != "deterministic"
                        && c.Status == TrackStatus.Available
                        && c.Fields is not null)
            .ToList();

        var reviewFlags = new List<string>();

        // Start from a full copy of the deterministic Expediente — all header fields are
        // authoritative from the deterministic source (XML / DOCX extraction).
        var best = det?.Fields is not null
            ? CopyExpediente(det.Fields)
            : new Expediente();

        // ------------------------------------------------------------------
        // Per-field merge for fields that LLM extractors also populate.
        // ------------------------------------------------------------------

        // NumeroExpediente
        var mergedExpediente = MergeString(
            "NumeroExpediente",
            det?.Fields?.NumeroExpediente,
            llms,
            c => c.Fields!.NumeroExpediente,
            reviewFlags);
        if (mergedExpediente is not null)
            best.NumeroExpediente = mergedExpediente;

        // NombreSolicitante
        var mergedSolicitante = MergeString(
            "NombreSolicitante",
            det?.Fields?.NombreSolicitante,
            llms,
            c => c.Fields!.NombreSolicitante,
            reviewFlags);
        if (mergedSolicitante is not null)
            best.NombreSolicitante = mergedSolicitante;

        // SolicitudPartes — deterministic wins (already in best via CopyExpediente).
        // If deterministic had none, pick the first LLM track that extracted partes.
        if (best.SolicitudPartes.Count == 0)
        {
            var withPartes = llms.Where(c => c.Fields!.SolicitudPartes.Count > 0).ToList();
            var firstWithPartes = withPartes.FirstOrDefault();
            if (firstWithPartes?.Fields is not null)
            {
                foreach (var parte in firstWithPartes.Fields.SolicitudPartes)
                    best.SolicitudPartes.Add(parte);

                // Honesty: if another LLM track produced a different-sized party list, flag it for
                // review rather than silently discarding it (never silently pick between conflicting LLMs).
                var divergent = withPartes.Skip(1).FirstOrDefault(
                    c => c.Fields!.SolicitudPartes.Count != firstWithPartes.Fields.SolicitudPartes.Count);
                if (divergent is not null)
                {
                    reviewFlags.Add(
                        $"SolicitudPartes: '{firstWithPartes.Source}' ({firstWithPartes.Fields.SolicitudPartes.Count}) " +
                        $"vs '{divergent.Source}' ({divergent.Fields!.SolicitudPartes.Count}) disagree — used '{firstWithPartes.Source}'.");
                }
            }
        }

        // AdditionalFields scalar keys (Monto, Rfc, Curp, Cuenta) — per-field merge.
        MergeAdditionalFields(det, llms, best, reviewFlags);

        // Provenance.
        best.AdditionalFields["_ReconciliationSource"] = "ExtractionReconciler";

        if (reviewFlags.Count > 0)
        {
            _logger.LogWarning(
                "ExtractionReconciler: {Count} field conflict(s) detected. Flags: {Flags}",
                reviewFlags.Count, string.Join("; ", reviewFlags));
        }

        _logger.LogInformation(
            "ExtractionReconciler: merged {CandidateCount} candidate(s); {FlagCount} review flag(s).",
            candidates.Count, reviewFlags.Count);

        var result = new ReconciliationResult(best, candidates, reviewFlags.AsReadOnly());
        return Task.FromResult(Result<ReconciliationResult>.WithSuccess(result));
    }

    // -----------------------------------------------------------------------
    // Private helpers
    // -----------------------------------------------------------------------

    /// <summary>
    /// Per-field string merge.
    /// Returns the winning value, or <see langword="null"/> when the caller should keep
    /// the value already set on <c>best</c> (either deterministic-wins or no value anywhere).
    /// </summary>
    private static string? MergeString(
        string fieldName,
        string? detValue,
        IReadOnlyList<LabelledExtraction> llms,
        Func<LabelledExtraction, string?> getter,
        List<string> reviewFlags)
    {
        // Rule 1: non-empty deterministic value wins — caller keeps the copied value.
        if (!string.IsNullOrWhiteSpace(detValue))
            return null;

        // Collect non-empty LLM values, filtering in-place so Value is guaranteed non-null.
        var llmValues = llms
            .Select(c => (c.Source, Value: getter(c)))
            .Where(x => !string.IsNullOrWhiteSpace(x.Value))
            .Select(x => (x.Source, Value: x.Value!))
            .ToList();

        if (llmValues.Count == 0)
            return null; // no LLM has a value

        // Rule 2 + 3: check for agreement.
        var distinct = llmValues.Select(x => x.Value).Distinct(StringComparer.Ordinal).ToList();

        if (distinct.Count == 1)
            return distinct[0]; // all agree → fill

        // Rule 3: conflict → null + review flag.
        var conflictDesc = string.Join(" vs ",
            llmValues.Select(x => $"{x.Source}='{x.Value}'"));
        reviewFlags.Add($"{fieldName}: LLM candidates disagree ({conflictDesc})");
        return null;
    }

    /// <summary>
    /// Merges LLM-extractable scalar fields stored in <see cref="Expediente.AdditionalFields"/>
    /// (Monto, Rfc, Curp, Cuenta) into <paramref name="best"/> using the same three-rule policy.
    /// </summary>
    private static void MergeAdditionalFields(
        LabelledExtraction? det,
        IReadOnlyList<LabelledExtraction> llms,
        Expediente best,
        List<string> reviewFlags)
    {
        var keysToMerge = new[] { "Monto", "Rfc", "Curp", "Cuenta" };
        var detAddl = det?.Fields?.AdditionalFields;

        foreach (var key in keysToMerge)
        {
            // Rule 1: deterministic wins if it has a non-empty value for this key.
            string? detValue = null;
            if (detAddl is not null && detAddl.TryGetValue(key, out var dv)
                && !string.IsNullOrWhiteSpace(dv))
            {
                detValue = dv;
            }

            if (detValue is not null)
                continue; // already in best from CopyExpediente

            // Collect non-empty LLM values.
            var llmValues = new List<(string Source, string Value)>();
            foreach (var c in llms)
            {
                if (c.Fields!.AdditionalFields.TryGetValue(key, out var v)
                    && !string.IsNullOrWhiteSpace(v))
                {
                    llmValues.Add((c.Source, v));
                }
            }

            if (llmValues.Count == 0)
                continue;

            var distinct = llmValues.Select(x => x.Value).Distinct(StringComparer.Ordinal).ToList();

            if (distinct.Count == 1)
            {
                best.AdditionalFields[key] = distinct[0];
            }
            else
            {
                var desc = string.Join(" vs ",
                    llmValues.Select(x => $"{x.Source}='{x.Value}'"));
                reviewFlags.Add($"{key}: LLM candidates disagree ({desc})");
            }
        }
    }

    /// <summary>
    /// Shallow copies all scalar and list properties from <paramref name="source"/> into a
    /// new <see cref="Expediente"/> instance.  Lists share element references (not deep-cloned);
    /// mutations to list elements after reconciliation are the caller's responsibility.
    /// </summary>
    private static Expediente CopyExpediente(Expediente source)
    {
        var copy = new Expediente
        {
            NumeroExpediente = source.NumeroExpediente,
            NumeroOficio = source.NumeroOficio,
            SolicitudSiara = source.SolicitudSiara,
            Folio = source.Folio,
            OficioYear = source.OficioYear,
            AreaClave = source.AreaClave,
            AreaDescripcion = source.AreaDescripcion,
            Subdivision = source.Subdivision,
            FechaPublicacion = source.FechaPublicacion,
            DiasPlazo = source.DiasPlazo,
            AutoridadNombre = source.AutoridadNombre,
            AutoridadEspecificaNombre = source.AutoridadEspecificaNombre,
            NombreSolicitante = source.NombreSolicitante,
            FundamentoLegal = source.FundamentoLegal,
            MedioEnvio = source.MedioEnvio,
            EvidenciaFirma = source.EvidenciaFirma,
            OficioOrigen = source.OficioOrigen,
            AcuerdoReferencia = source.AcuerdoReferencia,
            Referencia = source.Referencia,
            Referencia1 = source.Referencia1,
            Referencia2 = source.Referencia2,
            TieneAseguramiento = source.TieneAseguramiento,
            FechaRecepcion = source.FechaRecepcion,
            FechaRegistro = source.FechaRegistro,
            FechaEstimadaConclusion = source.FechaEstimadaConclusion,
            BodyText = source.BodyText,
            OcrConfidence = source.OcrConfidence,
            LawMandatedFields = source.LawMandatedFields,
            SemanticAnalysis = source.SemanticAnalysis,
        };

        foreach (var parte in source.SolicitudPartes)
            copy.SolicitudPartes.Add(parte);

        foreach (var esp in source.SolicitudEspecificas)
            copy.SolicitudEspecificas.Add(esp);

        foreach (var kv in source.AdditionalFields)
            copy.AdditionalFields[kv.Key] = kv.Value;

        return copy;
    }
}
