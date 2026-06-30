using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ExxerCube.Prisma.Veriqan.Orchestration.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace ExxerCube.Prisma.Veriqan.Orchestration.Tests;

/// <summary>
/// Unit tests for <see cref="VeriqanConfigurationValidator"/>.
/// Verifies that the startup validator detects absent critical keys and emits the
/// expected structured warnings — without crashing the process.
/// </summary>
public sealed class VeriqanConfigurationValidatorTests
{
    // ---------------------------------------------------------------------------
    // 1. All keys absent → VeriqanDb warning is emitted + FindMissingKeys returns all keys
    // ---------------------------------------------------------------------------

    /// <summary>
    /// When <c>ConnectionStrings:VeriqanDb</c> is absent the validator must log a structured
    /// Warning containing the literal phrase "IN-MEMORY" so operators can query for it.
    /// </summary>
    [Fact]
    public async Task StartAsync_VeriqanDbAbsent_LogsInMemoryWarning()
    {
        var ct = TestContext.Current.CancellationToken;

        // Empty config — no keys present.
        var config = new ConfigurationBuilder().Build();

        var logger = Substitute.For<ILogger<VeriqanConfigurationValidator>>();
        var validator = new VeriqanConfigurationValidator(config, logger);

        // Act
        await validator.StartAsync(ct);

        // Assert — at least one Warning call must have been made.
        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<System.Exception?>(),
            Arg.Any<Func<object, System.Exception?, string>>());
    }

    /// <summary>
    /// When <c>ConnectionStrings:VeriqanDb</c> is absent, <see cref="VeriqanConfigurationValidator.FindMissingKeys"/>
    /// must include it in the returned list.
    /// </summary>
    [Fact]
    public void FindMissingKeys_VeriqanDbAbsent_IncludesConnectionStringKey()
    {
        var config = new ConfigurationBuilder().Build();
        var logger = Substitute.For<ILogger<VeriqanConfigurationValidator>>();
        var validator = new VeriqanConfigurationValidator(config, logger);

        // Act
        var missingKeys = validator.FindMissingKeys();

        // Assert
        missingKeys.ShouldContain("ConnectionStrings:VeriqanDb",
            "The DB connection string key must be flagged when absent.");
    }

    // ---------------------------------------------------------------------------
    // 2. All critical keys present → no warnings, all-clear log at Information
    // ---------------------------------------------------------------------------

    /// <summary>
    /// When every critical key has a non-empty value the validator must NOT emit any
    /// Warning-level log entry.
    /// </summary>
    [Fact]
    public async Task StartAsync_AllCriticalKeysPresent_NoWarningLogged()
    {
        var ct = TestContext.Current.CancellationToken;

        // Provide valid (non-empty) values for every critical key.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:VeriqanDb"] = "Server=localhost;Database=VeriqanDb;",
                ["Veriqan:CsvReferenceData:RootDirectory"] = "/opt/reference-data/csv/",
                ["Veriqan:Smtp:Host"] = "smtp.example.com",
                ["Veriqan:LegalBaseline:EncryptionKey"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
                ["Veriqan:Auth:Jwt:SigningKey"] = "jwt-signing-key-at-least-32-chars-long-for-hmac256",
            })
            .Build();

        var logger = Substitute.For<ILogger<VeriqanConfigurationValidator>>();
        var validator = new VeriqanConfigurationValidator(config, logger);

        // Act
        await validator.StartAsync(ct);

        // Assert — no Warning calls.
        logger.DidNotReceive().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<System.Exception?>(),
            Arg.Any<Func<object, System.Exception?, string>>());
    }

    /// <summary>
    /// When every critical key is present, <see cref="VeriqanConfigurationValidator.FindMissingKeys"/>
    /// must return an empty list.
    /// </summary>
    [Fact]
    public void FindMissingKeys_AllKeysPresent_ReturnsEmpty()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:VeriqanDb"] = "Server=localhost;Database=VeriqanDb;",
                ["Veriqan:CsvReferenceData:RootDirectory"] = "/opt/reference-data/csv/",
                ["Veriqan:Smtp:Host"] = "smtp.example.com",
                ["Veriqan:LegalBaseline:EncryptionKey"] = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=",
                ["Veriqan:Auth:Jwt:SigningKey"] = "jwt-signing-key-at-least-32-chars-long-for-hmac256",
            })
            .Build();

        var logger = Substitute.For<ILogger<VeriqanConfigurationValidator>>();
        var validator = new VeriqanConfigurationValidator(config, logger);

        // Act
        var missingKeys = validator.FindMissingKeys();

        // Assert
        missingKeys.ShouldBeEmpty(
            "FindMissingKeys must return empty when all critical keys are supplied.");
    }

    // ---------------------------------------------------------------------------
    // 3. Cancelled token — StartAsync returns without doing work
    // ---------------------------------------------------------------------------

    /// <summary>
    /// When the host cancellation token is already cancelled before <c>StartAsync</c>
    /// the validator must return without logging anything — honouring the cancellation
    /// contract.
    /// </summary>
    [Fact]
    public async Task StartAsync_CancelledToken_ReturnsImmediatelyWithoutLogging()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var config = new ConfigurationBuilder().Build();
        var logger = Substitute.For<ILogger<VeriqanConfigurationValidator>>();
        var validator = new VeriqanConfigurationValidator(config, logger);

        // Act
        await validator.StartAsync(cts.Token);

        // Assert — no log calls at all.
        logger.DidNotReceive().Log(
            Arg.Any<LogLevel>(),
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<System.Exception?>(),
            Arg.Any<Func<object, System.Exception?, string>>());
    }

    // ---------------------------------------------------------------------------
    // 4. Partial config — only some keys present
    // ---------------------------------------------------------------------------

    /// <summary>
    /// When only some critical keys are present the missing-keys list must contain
    /// exactly the absent ones and a warning must be emitted per missing key.
    /// </summary>
    [Fact]
    public void FindMissingKeys_PartialConfig_ReturnsOnlyAbsentKeys()
    {
        // Provide DB and SMTP host but leave RootDirectory and EncryptionKey absent.
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:VeriqanDb"] = "Server=localhost;Database=VeriqanDb;",
                ["Veriqan:Smtp:Host"] = "smtp.example.com",
            })
            .Build();

        var logger = Substitute.For<ILogger<VeriqanConfigurationValidator>>();
        var validator = new VeriqanConfigurationValidator(config, logger);

        // Act
        var missingKeys = validator.FindMissingKeys();

        // Assert
        missingKeys.ShouldNotContain("ConnectionStrings:VeriqanDb",
            "VeriqanDb is supplied — must not appear in the missing list.");
        missingKeys.ShouldNotContain("Veriqan:Smtp:Host",
            "Smtp:Host is supplied — must not appear in the missing list.");
        missingKeys.ShouldContain("Veriqan:CsvReferenceData:RootDirectory",
            "RootDirectory is absent — must be flagged.");
        missingKeys.ShouldContain("Veriqan:LegalBaseline:EncryptionKey",
            "EncryptionKey is absent — must be flagged.");
        missingKeys.ShouldContain("Veriqan:Auth:Jwt:SigningKey",
            "JWT signing key is absent — must be flagged (Story 6.3).");
    }
}
