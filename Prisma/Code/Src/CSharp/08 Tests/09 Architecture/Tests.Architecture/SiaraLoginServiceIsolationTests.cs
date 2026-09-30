namespace ExxerCube.Prisma.Tests.Architecture;

/// <summary>
/// Structural guardrails for ADR-010 <strong>P6</strong>: the raw-credential SIARA login driver
/// (<c>ISiaraLoginService</c>) must be isolated from <em>persisted</em> credentials by structure, not by
/// convention. These reflection tests fail the build if a future change lets the login driver read
/// configuration directly, or wires it into any consumer other than the credential-source-fed
/// <c>AutomatedLogin</c> provider.
/// </summary>
public sealed class SiaraLoginServiceIsolationTests(ITestOutputHelper output)
{
    private readonly ILogger logger = XUnitLogger.CreateLogger<SiaraLoginServiceIsolationTests>(output);

    private static readonly Type LoginServiceInterface =
        typeof(ExxerCube.Prisma.Domain.Interfaces.ISiaraLoginService);

    private static readonly Assembly BrowserAutomationAssembly =
        typeof(ExxerCube.Prisma.Infrastructure.BrowserAutomation.Services.SiaraLoginService).Assembly;

    /// <summary>
    /// P6: every <c>ISiaraLoginService</c> implementation must take <strong>no</strong> raw configuration
    /// dependency (<c>IConfiguration</c>/<c>IConfigurationSection</c>). It therefore cannot read persisted
    /// credentials itself — credentials can only reach it as transient method parameters supplied by the
    /// credential source.
    /// </summary>
    [Fact]
    public void LoginService_Implementations_DoNotDependOnRawConfiguration()
    {
        var implementations = BrowserAutomationAssembly.GetTypes()
            .Where(t => t.IsClass && !t.IsAbstract && LoginServiceInterface.IsAssignableFrom(t))
            .ToList();

        implementations.ShouldNotBeEmpty(
            "there must be a production ISiaraLoginService implementation to verify (P6 isolation)");

        var violations = new List<string>();
        foreach (var impl in implementations)
        {
            foreach (var ctor in impl.GetConstructors())
            {
                foreach (var parameter in ctor.GetParameters())
                {
                    var typeName = parameter.ParameterType.FullName ?? parameter.ParameterType.Name;
                    if (typeName.StartsWith("Microsoft.Extensions.Configuration.IConfiguration", StringComparison.Ordinal))
                    {
                        violations.Add($"{impl.Name}.ctor depends on {parameter.ParameterType.Name}");
                    }
                }
            }
        }

        if (violations.Any())
        {
            logger.LogWarning("Rule: ISiaraLoginService must not read raw configuration. Count={Count}\n{Details}",
                violations.Count, string.Join(Environment.NewLine, violations.Select(v => $" - {v}")));
        }

        violations.ShouldBeEmpty(
            "The SIARA login driver must not depend on IConfiguration/IConfigurationSection (ADR-010 P6): " +
            $"it could then read persisted credentials. Violations: {string.Join(", ", violations)}");
    }

    /// <summary>
    /// P6: the only type that may receive an <c>ISiaraLoginService</c> via constructor injection is the
    /// credential-source-fed <c>AutomatedLogin</c> provider. This pins the set of consumers structurally so
    /// a future composition root cannot quietly wire app-config credentials through a new consumer.
    /// </summary>
    [Fact]
    public void Only_AutomatedLoginProvider_Injects_SiaraLoginService()
    {
        var allowedConsumers = new HashSet<string>(StringComparer.Ordinal)
        {
            "AutomatedLoginSiaraSessionProvider",
        };

        var consumers = BrowserAutomationAssembly.GetTypes()
            .Where(t => t.IsClass)
            .Where(t => t.GetConstructors()
                .Any(c => c.GetParameters().Any(p => p.ParameterType == LoginServiceInterface)))
            .Select(t => t.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var unexpected = consumers.Where(name => !allowedConsumers.Contains(name)).ToList();

        if (unexpected.Any())
        {
            logger.LogWarning("Rule: only AutomatedLogin may consume ISiaraLoginService. Unexpected={Count}\n{Details}",
                unexpected.Count, string.Join(Environment.NewLine, unexpected.Select(v => $" - {v}")));
        }

        unexpected.ShouldBeEmpty(
            "Only AutomatedLoginSiaraSessionProvider may inject ISiaraLoginService (ADR-010 P6 isolation). " +
            $"Unexpected consumers: {string.Join(", ", unexpected)}");
    }
}
