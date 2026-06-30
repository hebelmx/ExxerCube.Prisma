using ExxerCube.Prisma.Veriqan.Application.Ports;
using ExxerCube.Prisma.Veriqan.Infrastructure.Reporting;
using ExxerCube.Prisma.Veriqan.Orchestration.Secrets;
using IndQuestResults;
using IndQuestResults.Operations;
using NSubstitute;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// Unit tests for <see cref="SmtpPasswordSecretPopulator"/> (Story 6.3).
/// Verifies that the SMTP password is sourced from <see cref="ISecretProvider"/> when not
/// already supplied by configuration binding, and that a config-supplied value is preserved.
/// </summary>
public sealed class SmtpPasswordSecretPopulatorTests
{
    // ─────────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────────

    private static SecretValue MakeSecret(string value) =>
        new(value, "config:Veriqan:Smtp:Password", "v1", System.DateTimeOffset.UtcNow);

    // ─────────────────────────────────────────────────────────────────────────
    // 1. Password absent → provider value used
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When <see cref="SmtpOptions.Password"/> is null the populator must call the secret
    /// provider and set the password from the returned value.
    /// </summary>
    [Fact]
    public void PostConfigure_PasswordAbsent_SetsPasswordFromProvider()
    {
        var secretProvider = Substitute.For<ISecretProvider>();
        secretProvider
            .GetSecretAsync(SmtpPasswordSecretPopulator.SecretLogicalName, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<SecretValue>.WithSuccess(MakeSecret("smtp-pass-from-vault"))));

        var options = new SmtpOptions(); // Password is null by default
        var sut = new SmtpPasswordSecretPopulator(secretProvider);

        sut.PostConfigure(null, options);

        options.Password.ShouldBe("smtp-pass-from-vault",
            "SmtpOptions.Password must be populated from the secret provider when absent.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 2. Password already set by config → preserved (config wins)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When <see cref="SmtpOptions.Password"/> is already populated (e.g. from appsettings
    /// or env vars) the populator must leave it unchanged — configuration always wins.
    /// </summary>
    [Fact]
    public void PostConfigure_PasswordAlreadySet_PreservesExistingValue()
    {
        var secretProvider = Substitute.For<ISecretProvider>();

        var options = new SmtpOptions { Password = "already-configured" };
        var sut = new SmtpPasswordSecretPopulator(secretProvider);

        sut.PostConfigure(null, options);

        options.Password.ShouldBe("already-configured",
            "A password already set by configuration must not be overwritten by the provider.");

        // Secret provider must NOT be consulted when the password is already set.
        secretProvider.DidNotReceive()
            .GetSecretAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 3. Provider returns failure → password stays null (graceful degradation)
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// When the secret provider returns a failure result the populator must leave
    /// <see cref="SmtpOptions.Password"/> null — SmtpEmailSender handles unauthenticated
    /// relay gracefully, so a missing password is not fatal.
    /// </summary>
    [Fact]
    public void PostConfigure_ProviderReturnsFailure_PasswordRemainsNull()
    {
        var secretProvider = Substitute.For<ISecretProvider>();
        secretProvider
            .GetSecretAsync(SmtpPasswordSecretPopulator.SecretLogicalName, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Result<SecretValue>.WithFailure("Secret not configured.")));

        var options = new SmtpOptions(); // Password null
        var sut = new SmtpPasswordSecretPopulator(secretProvider);

        sut.PostConfigure(null, options);

        options.Password.ShouldBeNull(
            "When the provider returns a failure the SMTP password must remain null — " +
            "unauthenticated relay is valid for local/dev configurations.");
    }

    // ─────────────────────────────────────────────────────────────────────────
    // 4. Config fallback: the logical name matches the config key path
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The <see cref="SmtpPasswordSecretPopulator.SecretLogicalName"/> must equal the
    /// configuration path used for the SMTP password section, ensuring the config-backed
    /// default implementation reads from the same key.
    /// </summary>
    [Fact]
    public void SecretLogicalName_MatchesSmtpOptionsPasswordConfigPath()
    {
        // Veriqan:Smtp is the SectionKey; Password is the property name → path = Veriqan:Smtp:Password
        SmtpPasswordSecretPopulator.SecretLogicalName
            .ShouldBe("Veriqan:Smtp:Password",
                "The logical name must match the IConfiguration key path so the default " +
                "ConfigurationSecretProvider reads the correct environment variable.");
    }
}
