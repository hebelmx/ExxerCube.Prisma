namespace ExxerCube.Prisma.Domain.Interfaces.Navigation;

/// <summary>
/// Defines a navigation target for browser automation.
/// </summary>
public interface INavigationTarget
{
    /// <summary>
    /// Gets the unique identifier for this navigation target.
    /// </summary>
    string Id { get; }

    /// <summary>
    /// Gets the display name for this navigation target.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Gets the description of this navigation target.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Gets the base URL for this navigation target.
    /// </summary>
    string BaseUrl { get; }

    /// <summary>
    /// Navigates to the target using the provided browser automation agent.
    /// </summary>
    /// <param name="agent">The browser automation agent to use for navigation.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> NavigateAsync(IBrowserAutomationAgent agent, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves documents from the navigation target.
    /// </summary>
    /// <param name="agent">The browser automation agent to use.</param>
    /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
    /// <returns>A result containing the list of downloadable files.</returns>
    Task<Result<List<DownloadableFile>>> RetrieveDocumentsAsync(
        IBrowserAutomationAgent agent,
        CancellationToken cancellationToken = default);
}
