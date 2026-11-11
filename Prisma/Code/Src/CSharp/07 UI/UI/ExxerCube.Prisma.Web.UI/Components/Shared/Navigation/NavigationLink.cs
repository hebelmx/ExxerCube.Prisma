namespace ExxerCube.Prisma.Web.UI.Components.Shared.Navigation;

internal sealed record NavigationLink(
    string Title,
    string Href,
    string Icon,
    string Description,
    NavLinkMatch Match = NavLinkMatch.All,
    string[]? Tags = null,
    string[]? RequiredRoles = null,
    bool RequiresAuthentication = false,
    string? Policy = null,
    bool IncludeInPrimaryNavigation = true,
    string? DevelopmentSamplePath = null,
    bool IsExample = false)
{
    public string? RolesCsv => RequiredRoles is { Length: > 0 } ? string.Join(',', RequiredRoles) : null;

    public bool RequiresAuthorization => RequiresAuthentication || (RequiredRoles is { Length: > 0 }) || !string.IsNullOrWhiteSpace(Policy);

    public string EffectiveDevelopmentHref => DevelopmentSamplePath ?? Href;
}