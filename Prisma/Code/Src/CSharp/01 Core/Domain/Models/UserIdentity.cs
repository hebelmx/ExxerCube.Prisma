namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Represents a user's identity with ID, username, and roles.
/// </summary>
/// <param name="UserId">The unique identifier for the user.</param>
/// <param name="UserName">The user's display name or username.</param>
/// <param name="Roles">A read-only collection of role names assigned to the user.</param>
public sealed record UserIdentity(string UserId, string UserName, IReadOnlyCollection<string> Roles);