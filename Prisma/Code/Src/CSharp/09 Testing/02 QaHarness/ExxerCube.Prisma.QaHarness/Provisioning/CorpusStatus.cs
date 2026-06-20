// <copyright file="CorpusStatus.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Provisioning;

/// <summary>
/// Describes the state of the document corpus after an environment provisioning attempt.
/// </summary>
public enum CorpusStatus
{
    /// <summary>The corpus was generated fresh by the Python document generator.</summary>
    Seeded,

    /// <summary>The corpus was restored from static Fixtures/ because the generator was unavailable.</summary>
    RestoredFromFixtures,

    /// <summary>No corpus is present and no generator is available; affected workflows will be skipped.</summary>
    AbsentNoGenerator,

    /// <summary>The generator was available but failed to produce the corpus; affected workflows will be skipped.</summary>
    AbsentGeneratorFailed,
}
