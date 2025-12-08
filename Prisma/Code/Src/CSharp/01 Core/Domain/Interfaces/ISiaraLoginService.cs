namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Service for handling SIARA Simulator login operations.
/// </summary>
public interface ISiaraLoginService
{
    /// <summary>
    /// Performs login to SIARA Simulator using the provided credentials.
    /// </summary>
    /// <param name="agent">Browser automation agent to use for login.</param>
    /// <param name="username">Username for authentication.</param>
    /// <param name="password">Password for authentication.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure of the login operation.</returns>
    Task<Result> LoginAsync(
        IBrowserAutomationAgent agent,
        string username,
        string password,
        CancellationToken cancellationToken = default);
}
