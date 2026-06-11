using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Domain.Entities;
using ExxerCube.Prisma.Domain.Interfaces;
using IndQuestResults;
using IndQuestResults.Operations;
using NSubstitute;

namespace ExxerCube.Prisma.Testing.Contracts;

/// <summary>
/// Builds the contract-conforming NSubstitute mock that backs the blueprint instance
/// of <see cref="TemplateRepositoryContract"/>.
/// </summary>
/// <remarks>
/// <para>
/// The contract asserts real persistence outcomes (save validation, version ordering,
/// active-template selection, activate-deactivates-others), which canned stubs cannot
/// satisfy. The factory therefore evolved into a <em>reference fake</em> — an in-memory
/// store implementing the documented <see cref="ITemplateRepository"/> semantics, the
/// same algorithm the production EF-backed <c>TemplateRepository</c> implements. Each
/// call returns a fresh, isolated store. This is exactly the evolution ADR-005 §6 expects.
/// </para>
/// </remarks>
public static class TemplateRepositoryMockFactory
{
    /// <summary>
    /// Creates an <see cref="ITemplateRepository"/> mock that satisfies every test in
    /// <see cref="TemplateRepositoryContract"/>.
    /// </summary>
    /// <returns>The configured mock backed by a fresh in-memory store.</returns>
    public static ITemplateRepository CreateContractConformingMock()
    {
        var store = new List<TemplateDefinition>();
        var mock = Substitute.For<ITemplateRepository>();

        // Non-Result getters mirror the production impl's pragmatic cancellation (null / empty, no throw);
        // the Result-returning commands return a Cancelled Result.
        mock.GetTemplateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<CancellationToken>(2).IsCancellationRequested
                ? null
                : GetTemplate(store, call.ArgAt<string>(0), call.ArgAt<string>(1))));

        mock.GetLatestTemplateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<CancellationToken>(1).IsCancellationRequested
                ? null
                : GetLatest(store, call.ArgAt<string>(0))));

        mock.GetAllTemplateVersionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<CancellationToken>(1).IsCancellationRequested
                ? (IReadOnlyList<TemplateDefinition>)Array.Empty<TemplateDefinition>()
                : GetAllVersions(store, call.ArgAt<string>(0))));

        mock.SaveTemplateAsync(Arg.Any<TemplateDefinition>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<CancellationToken>(1).IsCancellationRequested
                ? ResultExtensions.Cancelled()
                : Save(store, call.ArgAt<TemplateDefinition>(0))));

        mock.DeleteTemplateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<CancellationToken>(2).IsCancellationRequested
                ? ResultExtensions.Cancelled()
                : Delete(store, call.ArgAt<string>(0), call.ArgAt<string>(1))));

        mock.ActivateTemplateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.ArgAt<CancellationToken>(2).IsCancellationRequested
                ? ResultExtensions.Cancelled()
                : Activate(store, call.ArgAt<string>(0), call.ArgAt<string>(1))));

        return mock;
    }

    private static TemplateDefinition? GetTemplate(List<TemplateDefinition> store, string templateType, string version)
        => store.FirstOrDefault(t => t.TemplateType == templateType && t.Version == version);

    private static TemplateDefinition? GetLatest(List<TemplateDefinition> store, string templateType)
    {
        var now = DateTime.UtcNow;
        return store
            .Where(t =>
                t.TemplateType == templateType &&
                t.IsActive &&
                t.EffectiveDate <= now &&
                (t.ExpirationDate == null || t.ExpirationDate > now))
            .OrderByDescending(t => t.EffectiveDate)
            .FirstOrDefault();
    }

    private static IReadOnlyList<TemplateDefinition> GetAllVersions(List<TemplateDefinition> store, string templateType)
        => store
            .Where(t => t.TemplateType == templateType)
            .OrderByDescending(t => t.Version, StringComparer.Ordinal)
            .ToList()
            .AsReadOnly();

    private static Result Save(List<TemplateDefinition> store, TemplateDefinition template)
    {
        if (template == null)
        {
            return Result.Failure("Template cannot be null");
        }

        if (string.IsNullOrWhiteSpace(template.TemplateType))
        {
            return Result.Failure("TemplateType is required");
        }

        if (string.IsNullOrWhiteSpace(template.Version))
        {
            return Result.Failure("Version is required");
        }

        if (template.FieldMappings == null || template.FieldMappings.Count == 0)
        {
            return Result.Failure("FieldMappings are required");
        }

        if (store.Any(t => t.TemplateId == template.TemplateId))
        {
            return Result.Failure($"Template with ID {template.TemplateId} already exists");
        }

        if (store.Any(t => t.TemplateType == template.TemplateType && t.Version == template.Version))
        {
            return Result.Failure($"Template {template.TemplateType} v{template.Version} already exists");
        }

        template.CreatedAt = DateTime.UtcNow;
        template.ModifiedAt = DateTime.UtcNow;
        store.Add(template);

        return Result.Success();
    }

    private static Result Delete(List<TemplateDefinition> store, string templateType, string version)
    {
        if (string.IsNullOrWhiteSpace(templateType))
        {
            return Result.Failure("TemplateType is required");
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            return Result.Failure("Version is required");
        }

        var template = store.FirstOrDefault(t => t.TemplateType == templateType && t.Version == version);

        if (template == null)
        {
            return Result.Failure($"Template not found: {templateType} v{version}");
        }

        if (template.IsActive)
        {
            return Result.Failure($"Cannot delete active template: {templateType} v{version}. Deactivate it first.");
        }

        store.Remove(template);
        return Result.Success();
    }

    private static Result Activate(List<TemplateDefinition> store, string templateType, string version)
    {
        if (string.IsNullOrWhiteSpace(templateType))
        {
            return Result.Failure("TemplateType is required");
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            return Result.Failure("Version is required");
        }

        var templateToActivate = store.FirstOrDefault(t => t.TemplateType == templateType && t.Version == version);

        if (templateToActivate == null)
        {
            return Result.Failure($"Template not found: {templateType} v{version}");
        }

        foreach (var template in store.Where(t => t.TemplateType == templateType))
        {
            template.IsActive = false;
            template.ModifiedAt = DateTime.UtcNow;
        }

        templateToActivate.IsActive = true;
        templateToActivate.ModifiedAt = DateTime.UtcNow;

        return Result.Success();
    }
}
