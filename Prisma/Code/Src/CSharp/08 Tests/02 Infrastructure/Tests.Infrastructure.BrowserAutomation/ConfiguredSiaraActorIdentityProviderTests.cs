namespace ExxerCube.Prisma.Tests.Infrastructure.BrowserAutomation;

/// <summary>
/// Mode-specific mechanics for <see cref="ConfiguredSiaraActorIdentityProvider"/> that the universal
/// <see cref="SiaraActorIdentityProviderContract"/> leaves to the implementation (ADR-005 §5): the
/// fail-closed path when ActorId is not configured, and the success path returning a ServiceAccount
/// actor with the configured id and optional display name.
/// </summary>
public sealed class ConfiguredSiaraActorIdentityProviderTests
{
    private static ConfiguredSiaraActorIdentityProvider CreateProvider(string actorId, string? displayName = null) =>
        new(
            Options.Create(new SiaraAuthOptions
            {
                Actor = new SiaraActorOptions { ActorId = actorId, DisplayName = displayName },
            }),
            Substitute.For<ILogger<ConfiguredSiaraActorIdentityProvider>>());

    [Fact]
    public async Task GetCurrentActorAsync_WhenActorIdIsEmpty_FailsClosed()
    {
        var sut = CreateProvider(actorId: string.Empty);

        var result = await sut.GetCurrentActorAsync(TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetCurrentActorAsync_WithConfiguredActorId_ReturnsServiceAccountActor()
    {
        var sut = CreateProvider(actorId: "orion-downloader-svc");

        var result = await sut.GetCurrentActorAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ActorId.ShouldBe("orion-downloader-svc");
        result.Value.ActorType.ShouldBe(SiaraActorType.ServiceAccount);
    }

    [Fact]
    public async Task GetCurrentActorAsync_WithDisplayName_PropagatesDisplayName()
    {
        var sut = CreateProvider(actorId: "orion-downloader-svc", displayName: "Orion Downloader");

        var result = await sut.GetCurrentActorAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.DisplayName.ShouldBe("Orion Downloader");
    }

    [Fact]
    public async Task GetCurrentActorAsync_WithNoDisplayName_DisplayNameIsNull()
    {
        var sut = CreateProvider(actorId: "orion-downloader-svc", displayName: null);

        var result = await sut.GetCurrentActorAsync(TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value!.DisplayName.ShouldBeNull();
    }
}
