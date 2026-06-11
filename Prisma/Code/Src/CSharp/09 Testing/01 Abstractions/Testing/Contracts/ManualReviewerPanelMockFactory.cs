using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.Interfaces;
using ExxerCube.Prisma.Domain.Models;
using ExxerCube.Prisma.Domain.ValueObjects;
using IndQuestResults;
using IndQuestResults.Operations;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Builds the contract-conforming reference fake that backs the blueprint instance of
/// <see cref="ManualReviewerPanelContract"/>.
/// </summary>
/// <remarks>
/// Per ADR-005 §6 the factory is a <em>reference fake</em>: a hand-written in-memory
/// <see cref="IManualReviewerPanel"/> implementing the documented semantics (validation, the
/// review-case identification rules, decision submission with case lookup + duplicate prevention),
/// the same logic the production <c>ManualReviewerService</c> applies minus the database, logging and
/// transaction plumbing. It is the executable design specification.
/// </remarks>
public static class ManualReviewerPanelMockFactory
{
    /// <summary>
    /// Creates an <see cref="IManualReviewerPanel"/> reference fake that satisfies every test in
    /// <see cref="ManualReviewerPanelContract"/>.
    /// </summary>
    /// <returns>The configured reference fake backed by a fresh in-memory store.</returns>
    public static IManualReviewerPanel CreateContractConformingMock()
        => new ReferenceManualReviewerPanel();

    private sealed class ReferenceManualReviewerPanel : IManualReviewerPanel
    {
        private readonly List<ReviewCase> _cases = new();
        private readonly List<ReviewDecision> _decisions = new();

        public Task<Result<List<ReviewCase>>> GetReviewCasesAsync(
            ReviewFilters? filters,
            int pageNumber = 1,
            int pageSize = 50,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(ResultExtensions.Cancelled<List<ReviewCase>>());
            }

            if (pageNumber < 1)
            {
                return Task.FromResult(Result<List<ReviewCase>>.WithFailure("Page number must be greater than 0"));
            }

            if (pageSize < 1 || pageSize > 1000)
            {
                return Task.FromResult(Result<List<ReviewCase>>.WithFailure("Page size must be between 1 and 1000"));
            }

            IEnumerable<ReviewCase> query = _cases;

            if (filters != null)
            {
                if (filters.MinConfidenceLevel.HasValue)
                {
                    query = query.Where(c => c.ConfidenceLevel >= filters.MinConfidenceLevel.Value);
                }

                if (filters.MaxConfidenceLevel.HasValue)
                {
                    query = query.Where(c => c.ConfidenceLevel <= filters.MaxConfidenceLevel.Value);
                }

                if (filters.ClassificationAmbiguity.HasValue)
                {
                    query = query.Where(c => c.ClassificationAmbiguity == filters.ClassificationAmbiguity.Value);
                }

                if (filters.ReviewReason is not null)
                {
                    query = query.Where(c => c.RequiresReviewReason == filters.ReviewReason);
                }

                if (filters.Status is not null)
                {
                    query = query.Where(c => c.Status == filters.Status);
                }

                if (!string.IsNullOrWhiteSpace(filters.AssignedTo))
                {
                    query = query.Where(c => c.AssignedTo == filters.AssignedTo);
                }
            }

            var skip = (pageNumber - 1) * pageSize;
            var cases = query.OrderByDescending(c => c.CreatedAt).Skip(skip).Take(pageSize).ToList();

            return Task.FromResult(Result<List<ReviewCase>>.Success(cases));
        }

        public Task<Result> SubmitReviewDecisionAsync(
            string caseId,
            ReviewDecision decision,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(ResultExtensions.Cancelled());
            }

            if (string.IsNullOrWhiteSpace(caseId))
            {
                return Task.FromResult(Result.WithFailure("CaseId cannot be null or empty"));
            }

            if (decision == null)
            {
                return Task.FromResult(Result.WithFailure("Decision cannot be null"));
            }

            if ((decision.OverriddenFields?.Count > 0 || decision.OverriddenClassification != null)
                && string.IsNullOrWhiteSpace(decision.Notes))
            {
                return Task.FromResult(Result.WithFailure("Notes are required when overriding fields or classification"));
            }

            var reviewCase = _cases.FirstOrDefault(c => c.CaseId == caseId);
            if (reviewCase == null)
            {
                return Task.FromResult(Result.WithFailure($"Review case not found: {caseId}"));
            }

            if (_decisions.Any(d => d.CaseId == caseId))
            {
                return Task.FromResult(Result.WithFailure("A decision has already been submitted for this case"));
            }

            if (string.IsNullOrWhiteSpace(decision.CaseId))
            {
                decision.CaseId = caseId;
            }

            if (decision.ReviewedAt == default)
            {
                decision.ReviewedAt = DateTime.UtcNow;
            }

            if (string.IsNullOrWhiteSpace(decision.DecisionId))
            {
                decision.DecisionId = $"DEC-{Guid.NewGuid():N}";
            }

            _decisions.Add(decision);

            reviewCase.Status = decision.DecisionType.Value switch
            {
                0 => ReviewStatus.Completed,
                1 => ReviewStatus.Rejected,
                2 => ReviewStatus.Pending,
                _ => reviewCase.Status,
            };

            return Task.FromResult(Result.Success());
        }

        public Task<Result<FieldAnnotations>> GetFieldAnnotationsAsync(
            string caseId,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(ResultExtensions.Cancelled<FieldAnnotations>());
            }

            if (string.IsNullOrWhiteSpace(caseId))
            {
                return Task.FromResult(Result<FieldAnnotations>.WithFailure("CaseId cannot be null or empty"));
            }

            var reviewCase = _cases.FirstOrDefault(c => c.CaseId == caseId);
            if (reviewCase == null)
            {
                return Task.FromResult(Result<FieldAnnotations>.WithFailure($"Review case not found: {caseId}"));
            }

            var annotations = new FieldAnnotations
            {
                CaseId = caseId,
                FieldAnnotationsDict = new Dictionary<string, FieldAnnotation>
                {
                    ["ConfidenceLevel"] = new FieldAnnotation
                    {
                        FieldName = "ConfidenceLevel",
                        Value = reviewCase.ConfidenceLevel,
                        Confidence = reviewCase.ConfidenceLevel,
                        Source = "Classification",
                        HasConflict = reviewCase.ClassificationAmbiguity,
                        AgreementLevel = reviewCase.ClassificationAmbiguity ? 0.5f : 1.0f
                    }
                }
            };

            return Task.FromResult(Result<FieldAnnotations>.Success(annotations));
        }

        public Task<Result<List<ReviewCase>>> IdentifyReviewCasesAsync(
            string fileId,
            UnifiedMetadataRecord metadata,
            ClassificationResult classification,
            CancellationToken cancellationToken = default)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                return Task.FromResult(ResultExtensions.Cancelled<List<ReviewCase>>());
            }

            if (string.IsNullOrWhiteSpace(fileId))
            {
                return Task.FromResult(Result<List<ReviewCase>>.WithFailure("FileId cannot be null or empty"));
            }

            if (metadata == null)
            {
                return Task.FromResult(Result<List<ReviewCase>>.WithFailure("Metadata cannot be null"));
            }

            if (classification == null)
            {
                return Task.FromResult(Result<List<ReviewCase>>.WithFailure("Classification cannot be null"));
            }

            var existing = _cases.Where(c => c.FileId == fileId && c.Status != ReviewStatus.Completed).ToList();
            if (existing.Count > 0)
            {
                return Task.FromResult(Result<List<ReviewCase>>.Success(existing));
            }

            var reviewCases = new List<ReviewCase>();

            if (classification.Confidence < 80)
            {
                reviewCases.Add(NewCase(fileId, ReviewReason.LowConfidence, classification.Confidence, ambiguity: false));
            }

            var isAmbiguous = classification.Level2 == null || (metadata.MatchedFields?.ConflictingFields?.Count > 0);
            if (isAmbiguous)
            {
                reviewCases.Add(NewCase(fileId, ReviewReason.AmbiguousClassification, classification.Confidence, ambiguity: true));
            }

            if (metadata.MatchedFields != null)
            {
                var hasExtractionErrors = (metadata.MatchedFields.ConflictingFields?.Count > 0)
                                          || (metadata.MatchedFields.MissingFields?.Count > 0);
                if (hasExtractionErrors)
                {
                    reviewCases.Add(NewCase(fileId, ReviewReason.ExtractionError, classification.Confidence, ambiguity: false));
                }
            }

            _cases.AddRange(reviewCases);

            return Task.FromResult(Result<List<ReviewCase>>.Success(reviewCases));
        }

        private static ReviewCase NewCase(string fileId, ReviewReason reason, int confidence, bool ambiguity)
            => new()
            {
                CaseId = $"CASE-{Guid.NewGuid():N}",
                FileId = fileId,
                RequiresReviewReason = reason,
                ConfidenceLevel = confidence,
                ClassificationAmbiguity = ambiguity,
                Status = ReviewStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };
    }
}
