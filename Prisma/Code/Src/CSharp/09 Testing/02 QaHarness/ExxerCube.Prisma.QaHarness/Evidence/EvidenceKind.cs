// <copyright file="EvidenceKind.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Evidence;

/// <summary>
/// Classifies the type of artefact stored in an <see cref="EvidenceItem"/>.
/// </summary>
public enum EvidenceKind
{
    /// <summary>A PNG or JPEG screenshot captured from a Playwright browser page.</summary>
    Screenshot,

    /// <summary>A structured log snapshot captured from the Serilog pipeline during a workflow run.</summary>
    LogSnapshot,

    /// <summary>A file produced by the Prisma pipeline (e.g. <c>.siro.xml</c>, <c>.fusion.json</c>, <c>.xlsx</c>).</summary>
    GeneratedFile,

    /// <summary>An HTTP Archive (HAR) file capturing network traffic recorded by Playwright.</summary>
    NetworkHar,

    /// <summary>A diagnostic dump such as a Playwright trace ZIP or a memory snapshot.</summary>
    Diagnostic,

    /// <summary>A Playwright video recording of a browser workflow execution.</summary>
    Video,
}
