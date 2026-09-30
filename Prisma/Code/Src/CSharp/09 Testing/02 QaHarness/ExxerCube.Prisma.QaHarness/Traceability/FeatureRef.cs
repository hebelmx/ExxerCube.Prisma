// <copyright file="FeatureRef.cs" company="ExxerCube">
// Copyright (c) ExxerCube. All rights reserved.
// </copyright>

namespace ExxerCube.Prisma.QaHarness.Traceability;

/// <summary>
/// A reference to a named product feature, used to link QA artefacts to
/// the feature it exercises.
/// </summary>
/// <param name="Id">The feature identifier (e.g. <c>"F-OCR-01"</c>).</param>
/// <param name="Name">A short, human-readable name for the feature.</param>
public sealed record FeatureRef(string Id, string Name);
