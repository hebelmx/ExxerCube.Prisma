namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Provides synchronous access to the current user context.
/// </summary>
public interface IUserContextAccessor
{
    /// <summary>
    /// Gets the current user's identity synchronously.
    /// </summary>
    UserIdentity? Current { get; }
}