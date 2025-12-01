namespace ExxerCube.Prisma.Tests.Infrastructure.Classification;

/// <summary>
/// Unit tests for <see cref="FieldMatcherService{T}"/>.
/// </summary>
public class FieldMatcherServiceTests
{
    private readonly IFieldExtractor<DocxSource> _fieldExtractor;
    private readonly IMatchingPolicy _matchingPolicy;
    private readonly NameMatchingPolicy _nameMatchingPolicy;
    private readonly ILogger<FieldMatcherService<DocxSource>> _logger;
    private readonly FieldMatcherService<DocxSource> _service;

    public FieldMatcherServiceTests()
    {
        _fieldExtractor = Substitute.For<IFieldExtractor<DocxSource>>();
        _logger = Substitute.For<ILogger<FieldMatcherService<DocxSource>>>();
        
        var options = Options.Create(new MatchingPolicyOptions());
        _matchingPolicy = new MatchingPolicyService(options, Substitute.For<ILogger<MatchingPolicyService>>());
        var nameOptions = new NameMatchingOptions();
        _nameMatchingPolicy = new NameMatchingPolicy(new StaticOptionsMonitor<NameMatchingOptions>(nameOptions), Substitute.For<ILogger<NameMatchingPolicy>>());

        _service = new FieldMatcherService<DocxSource>(_fieldExtractor, _matchingPolicy, _nameMatchingPolicy, _logger);
    }

    [Fact]
    public async Task MatchFieldsAsync_WithMatchingFields_ReturnsMatchedFields()
    {
        // Arrange
        var sources = new List<DocxSource> { new DocxSource("test1.docx"), new DocxSource("test2.docx") };
        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente"),
            new FieldDefinition("Causa")
        };

        var extractedFields1 = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Test Causa"
        };

        var extractedFields2 = new ExtractedFields
        {
            Expediente = "A/AS1-2505-088637-PHM",
            Causa = "Test Causa"
        };

        _fieldExtractor.ExtractFieldsAsync(Arg.Is<DocxSource>(s => s.FilePath == "test1.docx"), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(extractedFields1));
        _fieldExtractor.ExtractFieldsAsync(Arg.Is<DocxSource>(s => s.FilePath == "test2.docx"), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(extractedFields2));

        // Act
        var result = await _service.MatchFieldsAsync(sources, fieldDefinitions);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.FieldMatches.ShouldContainKey("Expediente");
        result.Value.FieldMatches.ShouldContainKey("Causa");
        result.Value.OverallAgreement.ShouldBeGreaterThan(0.9f);
        result.Value.ConflictingFields.ShouldBeEmpty();
    }

    [Fact]
    public async Task MatchFieldsAsync_WithConflictingFields_DetectsConflicts()
    {
        // Arrange
        var sources = new List<DocxSource> { new DocxSource("test1.docx"), new DocxSource("test2.docx") };
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        var extractedFields1 = new ExtractedFields { Expediente = "A/AS1-2505-088637-PHM" };
        var extractedFields2 = new ExtractedFields { Expediente = "B/BS2-3006-099748-QRS" };

        _fieldExtractor.ExtractFieldsAsync(Arg.Is<DocxSource>(s => s.FilePath == "test1.docx"), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(extractedFields1));
        _fieldExtractor.ExtractFieldsAsync(Arg.Is<DocxSource>(s => s.FilePath == "test2.docx"), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(extractedFields2));

        // Act
        var result = await _service.MatchFieldsAsync(sources, fieldDefinitions);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ConflictingFields.ShouldContain("Expediente");
    }

    [Fact]
    public async Task MatchFieldsAsync_WithMissingFields_TracksMissingFields()
    {
        // Arrange
        var sources = new List<DocxSource> { new DocxSource("test1.docx") };
        var fieldDefinitions = new[]
        {
            new FieldDefinition("Expediente"),
            new FieldDefinition("Causa")
        };

        var extractedFields = new ExtractedFields { Expediente = "A/AS1-2505-088637-PHM" };

        _fieldExtractor.ExtractFieldsAsync(Arg.Any<DocxSource>(), Arg.Any<FieldDefinition[]>())
            .Returns(Result<ExtractedFields>.Success(extractedFields));

        // Act
        var result = await _service.MatchFieldsAsync(sources, fieldDefinitions);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.MissingFields.ShouldContain("Causa");
    }

    [Fact]
    public async Task MatchFieldsAsync_NoSources_ReturnsFailure()
    {
        // Arrange
        var sources = new List<DocxSource>();
        var fieldDefinitions = new[] { new FieldDefinition("Expediente") };

        // Act
        var result = await _service.MatchFieldsAsync(sources, fieldDefinitions);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("No sources");
    }

    [Fact]
    public async Task MatchFieldsAsync_NoFieldDefinitions_ReturnsFailure()
    {
        // Arrange
        var sources = new List<DocxSource> { new DocxSource("test.docx") };
        var fieldDefinitions = new FieldDefinition[0];

        // Act
        var result = await _service.MatchFieldsAsync(sources, fieldDefinitions);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("No field definitions");
    }

    [Fact]
    public async Task GenerateUnifiedRecordAsync_WithMatchedFields_ReturnsUnifiedRecord()
    {
        // Arrange
        var matchedFields = new MatchedFields
        {
            FieldMatches = new Dictionary<string, FieldMatchResult>
            {
                ["Expediente"] = new FieldMatchResult("Expediente", "A/AS1-2505-088637-PHM", 1.0f, "DOCX")
                {
                    AgreementLevel = 1.0f,
                    HasConflict = false
                }
            },
            OverallAgreement = 1.0f
        };

        var expediente = new Expediente { NumeroExpediente = "A/AS1-2505-088637-PHM" };
        var classification = new ClassificationResult();

        // Act
        var result = await _service.GenerateUnifiedRecordAsync(matchedFields, expediente, classification);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.Expediente.ShouldNotBeNull();
        result.Value.ExtractedFields.ShouldNotBeNull();
        result.Value.MatchedFields.ShouldBe(matchedFields);
        result.Value.Classification.ShouldBe(classification);
    }

    [Fact]
    public async Task GenerateUnifiedRecordAsync_NullMatchedFields_ReturnsFailure()
    {
        // Act
        var result = await _service.GenerateUnifiedRecordAsync(null!, null, null);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("cannot be null");
    }

    [Fact]
    public async Task ValidateCompletenessAsync_AllRequiredFieldsPresent_ReturnsSuccess()
    {
        // Arrange
        var matchedFields = new MatchedFields
        {
            FieldMatches = new Dictionary<string, FieldMatchResult>
            {
                ["Expediente"] = new FieldMatchResult("Expediente", "A/AS1-2505-088637-PHM", 1.0f, "DOCX"),
                ["Causa"] = new FieldMatchResult("Causa", "Test Causa", 1.0f, "DOCX")
            }
        };
        var requiredFields = new List<string> { "Expediente", "Causa" };

        // Act
        var result = await _service.ValidateCompletenessAsync(matchedFields, requiredFields);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public async Task ValidateCompletenessAsync_MissingRequiredFields_ReturnsFailure()
    {
        // Arrange
        var matchedFields = new MatchedFields
        {
            FieldMatches = new Dictionary<string, FieldMatchResult>
            {
                ["Expediente"] = new FieldMatchResult("Expediente", "A/AS1-2505-088637-PHM", 1.0f, "DOCX")
            }
        };
        var requiredFields = new List<string> { "Expediente", "Causa" };

        // Act
        var result = await _service.ValidateCompletenessAsync(matchedFields, requiredFields);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldNotBeNull();
        result.Error!.ShouldContain("Causa");
    }

    [Fact]
    public async Task ValidateCompletenessAsync_EmptyRequiredFields_ReturnsSuccess()
    {
        // Arrange
        var matchedFields = new MatchedFields();
        var requiredFields = new List<string>();

        // Act
        var result = await _service.ValidateCompletenessAsync(matchedFields, requiredFields);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }
}

internal sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T> where T : class, new()
{
    private readonly T _value;

    public StaticOptionsMonitor(T value)
    {
        _value = value;
    }

    public T CurrentValue => _value;

    public T Get(string? name) => _value;

    public IDisposable OnChange(Action<T, string?> listener) => NullDisposable.Instance;

    private sealed class NullDisposable : IDisposable
    {
        public static readonly NullDisposable Instance = new();
        public void Dispose()
        {
        }
    }
}

