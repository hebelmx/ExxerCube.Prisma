namespace ExxerCube.Prisma.Domain.Models;

/// <summary>
/// Represents the result of a token validation operation.
/// </summary>
/// <param name="IsValid">Indicates whether the token is valid.</param>
/// <param name="Identity">The user identity extracted from the token, if valid.</param>
/// <param name="Error">An optional error message if validation failed.</param>
public sealed record TokenValidationResult(bool IsValid, UserIdentity? Identity, string? Error = null);