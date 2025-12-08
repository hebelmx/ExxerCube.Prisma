namespace ExxerCube.Prisma.Tests.UI.Navigation;

public class NavigationRegistryTests
{
    [Fact]
    public void EveryPageRouteIsRepresentedInNavigationRegistry()
    {
        var assembly = typeof(UiProgram).Assembly;

        var routedComponents = assembly
            .GetTypes()
            .Select(type => new
            {
                Type = type,
                Routes = type.GetCustomAttributes<RouteAttribute>(inherit: false).Select(attr => NormalizeRoute(attr.Template)).ToArray(),
                Hidden = type.IsDefined(typeof(HideFromNavigationAttribute), inherit: false)
            })
            .Where(entry => entry.Routes.Length > 0);

        var pageRoutes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in routedComponents)
        {
            if (component.Hidden)
            {
                continue;
            }

            foreach (var route in component.Routes)
            {
                pageRoutes.Add(route);
            }
        }

        var registryRoutes = NavigationRegistry.AllLinks
            .Select(link => NormalizeRoute(link.Href))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missingInRegistry = pageRoutes.Except(registryRoutes).ToList();
        var missingInPages = registryRoutes.Except(pageRoutes).ToList();

        missingInRegistry.ShouldBeEmpty(
            missingInRegistry.Any()
                ? $"Add these routes to NavigationRegistry: {string.Join(", ", missingInRegistry)}"
                : null);

        missingInPages.ShouldBeEmpty(
            missingInPages.Any()
                ? $"Remove or create components for these NavigationRegistry entries: {string.Join(", ", missingInPages)}"
                : null);
    }

    private static string NormalizeRoute(string? route)
    {
        var normalized = (route ?? string.Empty).Trim();
        if (normalized.Length == 0)
        {
            return "/";
        }

        if (!normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = "/" + normalized;
        }

        if (normalized.Length > 1 && normalized.EndsWith("/", StringComparison.Ordinal))
        {
            normalized = normalized.TrimEnd('/');
        }

        return normalized.ToLowerInvariant();
    }
}