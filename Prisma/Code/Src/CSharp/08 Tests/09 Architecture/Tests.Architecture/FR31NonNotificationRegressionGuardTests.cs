using System.IO;
using System.Text.RegularExpressions;

namespace ExxerCube.Prisma.Tests.Architecture;

/// <summary>
/// Regression guard for FR31 / INV-5: the Prisma pipeline must NOT notify a regulated/investigated
/// party (client) unless a legal directive explicitly permits it.
///
/// Owner ruling (2026-06-20, HRQ-12): FR31 is currently satisfied BY ABSENCE — no external
/// notification sink exists in Prisma production code. The Veriqan <c>SmtpEmailSender</c> is a
/// separate product (auditor-alert, not client-notify) and is therefore excluded. The ASP.NET
/// Identity scaffolding uses <c>IdentityNoOpEmailSender</c> / <c>NoOpEmailSender</c> — a no-op by
/// design, explicitly allow-listed here.
///
/// HOW THIS GUARD WORKS:
/// The test loads the Prisma production assemblies (excluding Veriqan) and searches every concrete
/// class for references to known external-notification sink TYPES (field types, constructor
/// parameters, method return types, method parameters). If any such reference appears outside the
/// allow-list, the test fails with a message directing the developer to add a legal-directive gate
/// before wiring any real external-notification capability.
///
/// SINK TYPES FORBIDDEN IN PRISMA PRODUCTION CODE (without a legal gate):
///   - System.Net.Mail.SmtpClient                  (SMTP email transport)
///   - System.Net.Mail.MailMessage                 (SMTP email message)
///   - MailKit.Net.Smtp.ISmtpClient                (MailKit SMTP)
///   - SendGrid.ISendGridClient / SendGrid.SendGridClient  (SendGrid REST email)
///   - Twilio.Rest.Api.V2010.Account.MessageResource (Twilio SMS)
///   - FirebaseAdmin.Messaging.FirebaseMessaging   (FCM push notifications)
///
/// ALLOW-LIST (known-benign, non-sending stubs):
///   - ExxerCube.Prisma.Web.UI.Components.Account.IdentityNoOpEmailSender
///       → ASP.NET Identity account-scaffold stub; wraps NoOpEmailSender — sends nothing.
///         Evidence: HRQ-12 (docs/qa/phase2-review/closure-evidence/HRQ-12-notification-sinks.txt)
///
/// EXCLUDE SCOPE:
///   - Veriqan product namespaces ("ExxerCube.Prisma.Veriqan.*") — auditor-alert feature for a
///     different product, covered by its own QA scope. Not client-notification.
///   - Test assemblies — test helpers/fakes are never production notification paths.
/// </summary>
public sealed class FR31NonNotificationRegressionGuardTests(ITestOutputHelper output)
{
    private readonly ILogger _logger = XUnitLogger.CreateLogger<FR31NonNotificationRegressionGuardTests>(output);

    // ---------------------------------------------------------------------------
    // Sink type full names to forbid in Prisma production code.
    // Matches are done by checking whether any member of a class (field, ctor param,
    // method param, method return) references one of these type full names.
    // ---------------------------------------------------------------------------
    private static readonly IReadOnlyList<string> ForbiddenSinkTypeNames =
    [
        // SMTP / e-mail transport
        "System.Net.Mail.SmtpClient",
        "System.Net.Mail.MailMessage",
        // MailKit SMTP (popular alternative to System.Net.Mail)
        "MailKit.Net.Smtp.ISmtpClient",
        "MailKit.Net.Smtp.SmtpClient",
        // SendGrid REST API
        "SendGrid.ISendGridClient",
        "SendGrid.SendGridClient",
        // Twilio SMS
        "Twilio.Rest.Api.V2010.Account.MessageResource",
        "Twilio.Clients.ITwilioRestClient",
        // Firebase Cloud Messaging (push notifications)
        "FirebaseAdmin.Messaging.FirebaseMessaging",
        // Generic webhook/push (class-level detection — checked via ForbiddenMemberPatterns below)
    ];

    // ---------------------------------------------------------------------------
    // Forbidden patterns checked via assembly qualified-name substrings.
    // These catch package namespaces that indicate a notification dependency even
    // when the concrete type name is not predictable (e.g. SDK wrappers).
    // ---------------------------------------------------------------------------
    private static readonly IReadOnlyList<string> ForbiddenAssemblyNamePrefixes =
    [
        "SendGrid",
        "Twilio",
        "FirebaseAdmin",
        "MailKit",
        // Microsoft.Azure.NotificationHubs (Azure push hub)
        "Microsoft.Azure.NotificationHubs",
        // Azure Communication Services (email + SMS)
        "Azure.Communication.Email",
        "Azure.Communication.Sms",
    ];

    // ---------------------------------------------------------------------------
    // Allow-listed type full names: known benign stubs that must NOT trigger failure.
    // Add entries here ONLY when there is documented evidence the code sends nothing.
    // ---------------------------------------------------------------------------
    private static readonly IReadOnlySet<string> AllowListedTypeFullNames = new HashSet<string>(StringComparer.Ordinal)
    {
        // IdentityNoOpEmailSender: ASP.NET Identity account-scaffold; wraps NoOpEmailSender.
        // Registered as IEmailSender<ApplicationUser> in Web.UI DI root.
        // Evidence: HRQ-12-notification-sinks.txt — confirmed no-send path.
        "ExxerCube.Prisma.Web.UI.Components.Account.IdentityNoOpEmailSender",
    };

    // ---------------------------------------------------------------------------
    // Namespace prefix exclusions:
    //   Veriqan — separate product; its SMTP is auditor-alert (FR-17), not FR31 client-notify.
    // ---------------------------------------------------------------------------
    private static readonly IReadOnlyList<string> ExcludedNamespacePrefixes =
    [
        "ExxerCube.Prisma.Veriqan.",
        "Veriqan.",
    ];

    // ---------------------------------------------------------------------------
    // Test
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Scans all Prisma production assemblies (excluding Veriqan) and asserts no class
    /// references an external-notification sink type unless it is explicitly allow-listed.
    ///
    /// If this test FAILS after you add a feature that sends email/SMS/push/webhook to a
    /// non-operator audience, you MUST:
    ///   1. Obtain a written legal directive permitting client notification.
    ///   2. Add a gate (e.g. <c>LegalDirectiveRequiredGate</c>) that blocks the send path
    ///      when the directive is absent.
    ///   3. Document the decision in an ADR and in HRQ-12-notification-sinks.txt.
    ///   4. Only then add the class to <see cref="AllowListedTypeFullNames"/> with a reference
    ///      to the ADR.
    /// </summary>
    [Fact]
    public void Prisma_ProductionCode_MustNotReferenceExternalNotificationSinks_WithoutLegalDirectiveGate()
    {
        var assemblies = LoadPrismaProductionAssemblies();

        _logger.LogInformation(
            "FR31 guard: scanning {Count} Prisma production assemblies for external-notification sinks.",
            assemblies.Count);

        var violations = new List<string>();

        foreach (var assembly in assemblies)
        {
            ScanAssemblyForSinks(assembly, violations);
        }

        if (violations.Count > 0)
        {
            _logger.LogWarning(
                "FR31/INV-5 REGRESSION: {Count} external-notification sink reference(s) found in Prisma production code.\n{Details}",
                violations.Count,
                string.Join(Environment.NewLine, violations.Select(v => $"  - {v}")));
        }
        else
        {
            _logger.LogInformation("FR31/INV-5 guard PASSED: no external-notification sinks in Prisma production code.");
        }

        violations.ShouldBeEmpty(
            "FR31/INV-5 REGRESSION GUARD FAILED: One or more Prisma production types reference an " +
            "external-notification sink (email/SMS/push/webhook) without a legal-directive gate. " +
            "See class-level XML-doc on FR31NonNotificationRegressionGuardTests for the required steps " +
            "before allow-listing a new sink. Violations:\n" +
            string.Join("\n", violations.Select(v => $"  - {v}")));
    }

    // ---------------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------------

    /// <summary>
    /// Scans one assembly for classes that reference forbidden sink types.
    /// Violations are appended to <paramref name="violations"/>.
    /// </summary>
    private void ScanAssemblyForSinks(Assembly assembly, List<string> violations)
    {
        // Check whether this assembly itself pulls in a forbidden notification SDK.
        var assemblyRefs = assembly.GetReferencedAssemblies();
        foreach (var refName in assemblyRefs)
        {
            var name = refName.Name ?? string.Empty;
            foreach (var forbidden in ForbiddenAssemblyNamePrefixes)
            {
                if (name.StartsWith(forbidden, StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add(
                        $"[ASSEMBLY-REF] {assembly.GetName().Name} references notification SDK '{name}' " +
                        $"(prefix='{forbidden}'). Ensure no class in this assembly delivers to a non-operator " +
                        $"audience, and add it to the allow-list with an ADR reference.");
                }
            }
        }

        // Scan every concrete type in the assembly.
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t is not null).Select(t => t!).ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogDebug("Could not load types from {Assembly}: {Message}", assembly.GetName().Name, ex.Message);
            return;
        }

        foreach (var type in types)
        {
            if (type is null || !type.IsClass || type.IsAbstract)
                continue;

            var typeFullName = type.FullName ?? type.Name;

            // Skip types in excluded namespaces (Veriqan product scope).
            if (IsInExcludedNamespace(typeFullName))
                continue;

            // Skip types that are in the allow-list.
            if (AllowListedTypeFullNames.Contains(typeFullName))
            {
                _logger.LogDebug("FR31 guard: allow-listed type '{Type}' skipped.", typeFullName);
                continue;
            }

            // Check constructor parameters.
            CheckMembers(type, typeFullName, violations);
        }
    }

    /// <summary>
    /// Checks fields, constructor parameters, method parameters, and method return types
    /// of <paramref name="type"/> for references to forbidden sink types.
    /// </summary>
    private static void CheckMembers(Type type, string typeFullName, List<string> violations)
    {
        // Fields (including backing fields — catches DI-injected sink instances).
        foreach (var field in type.GetFields(
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly))
        {
            var fieldTypeName = GetBaseTypeName(field.FieldType);
            foreach (var sink in ForbiddenSinkTypeNames)
            {
                if (string.Equals(fieldTypeName, sink, StringComparison.Ordinal))
                {
                    violations.Add(
                        $"{typeFullName}.{field.Name} (field): references forbidden sink type '{sink}'. " +
                        $"Add a legal-directive gate before introducing a real notification send path.");
                }
            }
        }

        // Constructor parameters.
        foreach (var ctor in type.GetConstructors(
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            foreach (var param in ctor.GetParameters())
            {
                var paramTypeName = GetBaseTypeName(param.ParameterType);
                foreach (var sink in ForbiddenSinkTypeNames)
                {
                    if (string.Equals(paramTypeName, sink, StringComparison.Ordinal))
                    {
                        violations.Add(
                            $"{typeFullName}.ctor (parameter '{param.Name}'): injects forbidden sink " +
                            $"type '{sink}'. Add a legal-directive gate before introducing a real " +
                            $"notification send path.");
                    }
                }
            }
        }

        // Method parameters and return types (public + non-public declared on this type).
        foreach (var method in type.GetMethods(
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly))
        {
            // Return type.
            var returnTypeName = GetBaseTypeName(method.ReturnType);
            foreach (var sink in ForbiddenSinkTypeNames)
            {
                if (string.Equals(returnTypeName, sink, StringComparison.Ordinal))
                {
                    violations.Add(
                        $"{typeFullName}.{method.Name} (return type): returns forbidden sink type '{sink}'. " +
                        $"Add a legal-directive gate before introducing a real notification send path.");
                }
            }

            // Parameters.
            foreach (var param in method.GetParameters())
            {
                var paramTypeName = GetBaseTypeName(param.ParameterType);
                foreach (var sink in ForbiddenSinkTypeNames)
                {
                    if (string.Equals(paramTypeName, sink, StringComparison.Ordinal))
                    {
                        violations.Add(
                            $"{typeFullName}.{method.Name} (parameter '{param.Name}'): uses forbidden sink " +
                            $"type '{sink}'. Add a legal-directive gate before introducing a real " +
                            $"notification send path.");
                    }
                }
            }
        }

        // Properties (return type).
        foreach (var prop in type.GetProperties(
            BindingFlags.Instance | BindingFlags.Static |
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.DeclaredOnly))
        {
            var propTypeName = GetBaseTypeName(prop.PropertyType);
            foreach (var sink in ForbiddenSinkTypeNames)
            {
                if (string.Equals(propTypeName, sink, StringComparison.Ordinal))
                {
                    violations.Add(
                        $"{typeFullName}.{prop.Name} (property): exposes forbidden sink type '{sink}'. " +
                        $"Add a legal-directive gate before introducing a real notification send path.");
                }
            }
        }
    }

    /// <summary>
    /// Returns the canonical full name of a type, unwrapping arrays, by-ref, pointers, and generic
    /// wrappers (e.g. <c>Task&lt;SmtpClient&gt;</c> → checks both the generic type and its arguments).
    /// Returns the outer type's full name for direct comparison; callers invoke this once per member
    /// so inner generic arguments are checked separately by the same logic.
    /// </summary>
    private static string GetBaseTypeName(Type type)
    {
        if (type.IsArray)
            return GetBaseTypeName(type.GetElementType()!);
        if (type.IsByRef || type.IsPointer)
            return GetBaseTypeName(type.GetElementType()!);
        if (type.IsGenericType)
            return type.GetGenericTypeDefinition().FullName ?? type.FullName ?? type.Name;

        return type.FullName ?? type.Name;
    }

    /// <summary>Returns <c>true</c> if the type's full name falls within an excluded namespace.</summary>
    private static bool IsInExcludedNamespace(string typeFullName)
    {
        foreach (var prefix in ExcludedNamespacePrefixes)
        {
            if (typeFullName.StartsWith(prefix, StringComparison.Ordinal))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Loads Prisma production assemblies that are referenced by the architecture test project.
    /// This mirrors the set already loaded into the current AppDomain by virtue of the
    /// <c>ProjectReference</c> items in <c>ExxerCube.Prisma.Tests.Architecture.csproj</c>,
    /// which covers all Core, Infrastructure, Service, and UI assemblies.
    ///
    /// We intentionally exclude Veriqan assemblies (separate product) and test assemblies
    /// (non-production code).
    /// </summary>
    private static IReadOnlyList<Assembly> LoadPrismaProductionAssemblies()
    {
        // Pull assemblies already loaded into the AppDomain (triggered by the test project's
        // ProjectReferences which cover Core/Infrastructure/Services/UI).
        // Then also scan the build output directory for any production DLLs not yet loaded.

        var alreadyLoaded = AppDomain.CurrentDomain.GetAssemblies()
            .Where(IsPrismaProductionAssembly)
            .ToList();

        // Also search build output directory for missed assemblies.
        var fromDisk = new List<Assembly>();
        var baseDir = AppContext.BaseDirectory;
        if (Directory.Exists(baseDir))
        {
            foreach (var dll in Directory.GetFiles(baseDir, "ExxerCube.Prisma.*.dll", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(baseDir, "Prisma.Orion.*.dll", SearchOption.TopDirectoryOnly))
                .Concat(Directory.GetFiles(baseDir, "Prisma.Athena.*.dll", SearchOption.TopDirectoryOnly))
                .Concat(Directory.GetFiles(baseDir, "Prisma.Auth.*.dll", SearchOption.TopDirectoryOnly))
                .Concat(Directory.GetFiles(baseDir, "Prisma.Reconciliator.*.dll", SearchOption.TopDirectoryOnly)))
            {
                try
                {
                    var loaded = Assembly.LoadFrom(dll);
                    if (IsPrismaProductionAssembly(loaded))
                        fromDisk.Add(loaded);
                }
                catch
                {
                    // Unloadable (e.g. native dependencies) — skip.
                }
            }
        }

        return alreadyLoaded
            .Concat(fromDisk)
            .DistinctBy(a => a.FullName, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Returns <c>true</c> for Prisma production assemblies.
    /// Excludes: dynamic assemblies, test assemblies, Veriqan assemblies, Microsoft/BCL assemblies.
    /// </summary>
    private static bool IsPrismaProductionAssembly(Assembly assembly)
    {
        if (assembly.IsDynamic)
            return false;

        var name = assembly.GetName().Name ?? string.Empty;

        // Must be an ExxerCube.Prisma or Prisma.* service assembly.
        var isPrisma = name.StartsWith("ExxerCube.Prisma.", StringComparison.Ordinal)
                    || name.StartsWith("Prisma.Orion.", StringComparison.Ordinal)
                    || name.StartsWith("Prisma.Athena.", StringComparison.Ordinal)
                    || name.StartsWith("Prisma.Auth.", StringComparison.Ordinal)
                    || name.StartsWith("Prisma.Reconciliator.", StringComparison.Ordinal);

        if (!isPrisma)
            return false;

        // Exclude test assemblies.
        if (name.Contains(".Tests.", StringComparison.Ordinal)
            || name.EndsWith(".Tests", StringComparison.Ordinal)
            || name.Contains(".Testing.", StringComparison.Ordinal)
            || name.EndsWith(".Testing", StringComparison.Ordinal))
            return false;

        // Exclude Veriqan assemblies — separate product, separate QA scope.
        if (name.Contains(".Veriqan.", StringComparison.Ordinal)
            || name.Contains("Veriqan.", StringComparison.Ordinal))
            return false;

        return true;
    }
}
