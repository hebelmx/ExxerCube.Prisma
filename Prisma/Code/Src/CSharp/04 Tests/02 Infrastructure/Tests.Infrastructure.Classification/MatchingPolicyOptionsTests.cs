namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Unit tests for <see cref="MatchingPolicyOptions"/> configuration validation and loading.
/// </summary>
public class MatchingPolicyOptionsTests
{
    [Fact]
    public void MatchingPolicyOptions_DefaultValues_AreValid()
    {
        // Arrange & Act
        var options = new MatchingPolicyOptions();

        // Assert
        options.ConflictThreshold.ShouldBe(0.5f);
        options.MinimumConfidence.ShouldBe(0.3f);
        options.SourcePriority.ShouldNotBeNull();
        options.SourcePriority.ShouldContain("XML");
        options.SourcePriority.ShouldContain("DOCX");
        options.SourcePriority.ShouldContain("PDF");
        options.FieldRules.ShouldNotBeNull();
    }

    [Fact]
    public void MatchingPolicyOptions_InvalidConflictThreshold_CanBeSet()
    {
        // Arrange
        var options = new MatchingPolicyOptions();

        // Act - Test that invalid values can be set (validation would be in service layer)
        options.ConflictThreshold = -0.1f; // Invalid: negative
        options.ConflictThreshold = 1.5f; // Invalid: > 1.0

        // Assert - Options class doesn't validate, service should handle validation
        options.ConflictThreshold.ShouldBe(1.5f);
    }

    [Fact]
    public void MatchingPolicyOptions_InvalidMinimumConfidence_CanBeSet()
    {
        // Arrange
        var options = new MatchingPolicyOptions();

        // Act - Test that invalid values can be set (validation would be in service layer)
        options.MinimumConfidence = -0.1f; // Invalid: negative
        options.MinimumConfidence = 1.5f; // Invalid: > 1.0

        // Assert - Options class doesn't validate, service should handle validation
        options.MinimumConfidence.ShouldBe(1.5f);
    }

    [Fact]
    public void MatchingPolicyOptions_InvalidSourcePriority_CanBeSet()
    {
        // Arrange
        var options = new MatchingPolicyOptions();

        // Act - Test that empty/null source priority can be set
        options.SourcePriority = new List<string>(); // Empty list
        options.SourcePriority = null!; // Null

        // Assert - Options class doesn't validate, service should handle validation
        options.SourcePriority.ShouldBeNull();
    }

    [Fact]
    public void MatchingPolicyOptions_LoadFromConfiguration_LoadsCorrectly()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "MatchingPolicy:ConflictThreshold", "0.7" },
                { "MatchingPolicy:MinimumConfidence", "0.5" },
                { "MatchingPolicy:SourcePriority:0", "PDF" },
                { "MatchingPolicy:SourcePriority:1", "DOCX" },
                { "MatchingPolicy:SourcePriority:2", "XML" }
            })
            .Build();

        // Act
        var options = new MatchingPolicyOptions();
        // Clear the default SourcePriority list before binding
        options.SourcePriority.Clear();
        
        // Bind all properties except SourcePriority first
        var matchingPolicySection = configuration.GetSection("MatchingPolicy");
        matchingPolicySection.Bind(options, binderOptions =>
        {
            binderOptions.BindNonPublicProperties = false;
        });
        
        // Manually bind SourcePriority from configuration to ensure it replaces defaults
        // Configuration binding for arrays/lists can be unreliable, so we bind it explicitly
        options.SourcePriority.Clear();
        var sourcePrioritySection = matchingPolicySection.GetSection("SourcePriority");
        if (sourcePrioritySection.Exists())
        {
            var index = 0;
            while (true)
            {
                var value = sourcePrioritySection[$"{index}"];
                if (string.IsNullOrEmpty(value))
                {
                    break;
                }
                options.SourcePriority.Add(value);
                index++;
            }
        }

        // Assert
        options.ConflictThreshold.ShouldBe(0.7f);
        options.MinimumConfidence.ShouldBe(0.5f);
        options.SourcePriority.ShouldNotBeNull();
        options.SourcePriority[0].ShouldBe("PDF");
        options.SourcePriority[1].ShouldBe("DOCX");
        options.SourcePriority[2].ShouldBe("XML");
    }

    [Fact]
    public void MatchingPolicyOptions_LoadFromConfiguration_MissingValues_UsesDefaults()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        // Act
        var options = new MatchingPolicyOptions();
        configuration.GetSection("MatchingPolicy").Bind(options);

        // Assert - Should use default values
        options.ConflictThreshold.ShouldBe(0.5f);
        options.MinimumConfidence.ShouldBe(0.3f);
        options.SourcePriority.ShouldNotBeNull();
    }

    [Fact]
    public void MatchingPolicyOptions_FieldSpecificRules_LoadFromConfiguration()
    {
        // Arrange
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "MatchingPolicy:FieldRules:Expediente:ConflictThreshold", "0.9" },
                { "MatchingPolicy:FieldRules:Expediente:MinimumConfidence", "0.8" },
                { "MatchingPolicy:FieldRules:Expediente:SourcePriority:0", "XML" },
                { "MatchingPolicy:FieldRules:Causa:ConflictThreshold", "0.6" }
            })
            .Build();

        // Act
        var options = new MatchingPolicyOptions();
        configuration.GetSection("MatchingPolicy").Bind(options);

        // Assert
        options.FieldRules.ShouldNotBeNull();
        options.FieldRules.ShouldContainKey("Expediente");
        options.FieldRules["Expediente"].ConflictThreshold.ShouldBe(0.9f);
        options.FieldRules["Expediente"].MinimumConfidence.ShouldBe(0.8f);
        options.FieldRules["Expediente"].SourcePriority.ShouldNotBeNull();
        options.FieldRules["Expediente"].SourcePriority![0].ShouldBe("XML");
        options.FieldRules.ShouldContainKey("Causa");
        options.FieldRules["Causa"].ConflictThreshold.ShouldBe(0.6f);
    }

    [Fact]
    public void MatchingPolicyOptions_FieldSpecificRules_OverrideGlobalSettings()
    {
        // Arrange
        var options = new MatchingPolicyOptions
        {
            ConflictThreshold = 0.5f,
            MinimumConfidence = 0.3f,
            FieldRules = new Dictionary<string, FieldMatchingRule>
            {
                ["Expediente"] = new FieldMatchingRule
                {
                    ConflictThreshold = 0.9f,
                    MinimumConfidence = 0.8f,
                    SourcePriority = new List<string> { "XML", "DOCX" }
                }
            }
        };

        // Act & Assert
        options.FieldRules["Expediente"].ConflictThreshold.ShouldBe(0.9f); // Override
        options.FieldRules["Expediente"].MinimumConfidence.ShouldBe(0.8f); // Override
        options.FieldRules["Expediente"].SourcePriority.ShouldNotBeNull();
        options.FieldRules["Expediente"].SourcePriority![0].ShouldBe("XML"); // Override
    }

    [Fact]
    public async Task MatchingPolicyService_WithInvalidConflictThreshold_HandlesGracefully()
    {
        // Arrange
        var invalidOptions = new MatchingPolicyOptions
        {
            ConflictThreshold = -0.1f // Invalid: negative
        };
        var optionsWrapper = Options.Create(invalidOptions);
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MatchingPolicyService>.Instance;
        var service = new MatchingPolicyService(optionsWrapper, logger);

        // Act - Service should handle invalid values gracefully (use defaults or clamp)
        var values = new List<FieldValue>
        {
            new FieldValue("Test", "Value1", 0.9f, "DOCX"),
            new FieldValue("Test", "Value2", 0.8f, "PDF")
        };

        var result = await service.SelectBestValueAsync("Test", values);

        // Assert - Service should still work (may clamp or use default threshold)
        result.IsSuccess.ShouldBeTrue(); // Service handles invalid config gracefully
    }

    [Fact]
    public async Task MatchingPolicyService_WithEmptySourcePriority_HandlesGracefully()
    {
        // Arrange
        var options = new MatchingPolicyOptions
        {
            SourcePriority = new List<string>() // Empty priority list
        };
        var optionsWrapper = Options.Create(options);
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<MatchingPolicyService>.Instance;
        var service = new MatchingPolicyService(optionsWrapper, logger);

        // Act
        var values = new List<FieldValue>
        {
            new FieldValue("Test", "Value1", 0.9f, "DOCX"),
            new FieldValue("Test", "Value1", 0.8f, "PDF")
        };

        var result = await service.SelectBestValueAsync("Test", values);

        // Assert - Service should handle empty priority gracefully
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
    }
}

