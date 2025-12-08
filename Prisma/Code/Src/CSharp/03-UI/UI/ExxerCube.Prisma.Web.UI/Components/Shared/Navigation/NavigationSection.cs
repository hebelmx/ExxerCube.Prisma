namespace ExxerCube.Prisma.Web.UI.Components.Shared.Navigation;

internal sealed record NavigationSection(string Title, IReadOnlyList<NavigationLink> Links);