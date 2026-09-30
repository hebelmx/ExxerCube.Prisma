using ExxerCube.Prisma.Domain.Enum;
using ExxerCube.Prisma.Domain.ValueObjects;

namespace ExxerCube.Prisma.Tests.Infrastructure.FileStorage;

/// <summary>
/// Unit tests for <see cref="SafeFileNamerService"/>.
/// </summary>
public class SafeFileNamerServiceTests
{
    private readonly ILogger<SafeFileNamerService> _logger;
    private readonly SafeFileNamerService _service;

    /// <summary>
    /// Initializes a new instance of the <see cref="SafeFileNamerServiceTests"/> class.
    /// </summary>
    public SafeFileNamerServiceTests()
    {
        _logger = Substitute.For<ILogger<SafeFileNamerService>>();
        _service = new SafeFileNamerService(_logger);
    }

    /// <summary>
    /// Tests that safe file name is generated with classification prefix.
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_WithClassification_ReturnsSafeFileName()
    {
        // Arrange
        var originalFileName = "test.pdf";
        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Aseguramiento,
            Level2 = ClassificationLevel2.Especial
        };

        // Act
        var result = await _service.GenerateSafeFileNameAsync(originalFileName, classification, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNullOrEmpty();
        result.Value.ShouldContain("ASEGURAMIENTO");
        result.Value.ShouldContain("ESPECIAL");
        result.Value.ShouldEndWith(".pdf");
    }

    /// <summary>
    /// Tests that expediente number is included in safe file name when available.
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_WithExpediente_IncludesExpediente()
    {
        // Arrange
        var originalFileName = "test.pdf";
        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Aseguramiento
        };
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                NumeroExpediente = "A/AS1-2505-088637-PHM"
            }
        };

        // Act
        var result = await _service.GenerateSafeFileNameAsync(originalFileName, classification, metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldContain("A_AS1-2505-088637-PHM");
    }

    /// <summary>
    /// Tests that invalid characters are sanitized in file name.
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_WithInvalidCharacters_SanitizesFileName()
    {
        // Arrange
        var originalFileName = "test<file>.pdf";
        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Documentacion
        };
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente
            {
                NumeroExpediente = "A/AS1-2505-088637-PHM"
            }
        };

        // Act
        var result = await _service.GenerateSafeFileNameAsync(originalFileName, classification, metadata, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value.ShouldNotContain("<");
        result.Value.ShouldNotContain(">");
    }

    /// <summary>
    /// Tests that timestamp is included in safe file name.
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_Always_IncludesTimestamp()
    {
        // Arrange
        var originalFileName = "test.pdf";
        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Aseguramiento
        };

        // Act
        var result = await _service.GenerateSafeFileNameAsync(originalFileName, classification, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        // Should contain date format yyyyMMdd-HHmmss
        result.Value.ShouldNotBeNull();
        System.Text.RegularExpressions.Regex.IsMatch(result.Value, @"\d{8}-\d{6}").ShouldBeTrue();
    }

    // ── Mutation-killing exact-value tests (Unit 34) ──────────────────────────

    /// <summary>
    /// The Level1 prefix is upper-cased (kills ToUpperInvariant removal / ToLower swap)
    /// and starts the name (kills the join-separator and component-order mutations).
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_NoLevel2NoMetadata_HasExactStructure()
    {
        // Arrange — Aseguramiento has DisplayName == Name (no accent) → clean upper-case.
        var classification = new ClassificationResult { Level1 = ClassificationLevel1.Aseguramiento };

        // Act
        var result = await _service.GenerateSafeFileNameAsync("input.pdf", classification, null, TestContext.Current.CancellationToken);

        // Assert — exactly: ASEGURAMIENTO_<yyyyMMdd-HHmmss>.pdf  (no Level2, no expediente, single separators).
        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        System.Text.RegularExpressions.Regex.IsMatch(result.Value!, @"^ASEGURAMIENTO_\d{8}-\d{6}\.pdf$").ShouldBeTrue(result.Value);
        result.Value.ShouldNotContain("__"); // no empty component leaked in
    }

    /// <summary>
    /// Level1 is upper-cased, not left as-is or lower-cased.
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_Level1_IsUpperCased()
    {
        var classification = new ClassificationResult { Level1 = ClassificationLevel1.Desembargo };

        var result = await _service.GenerateSafeFileNameAsync("x.txt", classification, null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.StartsWith("DESEMBARGO_", StringComparison.Ordinal).ShouldBeTrue(result.Value);
        result.Value.Contains("Desembargo", StringComparison.Ordinal).ShouldBeFalse(); // would survive if ToUpperInvariant were removed
        result.Value.Contains("desembargo", StringComparison.Ordinal).ShouldBeFalse(); // would survive if ToLowerInvariant were substituted
    }

    /// <summary>
    /// When Level2 is present it is appended (upper-cased) after Level1 and before the timestamp
    /// (kills the <c>Level2 is not null</c> negation / block-removal and the ToUpperInvariant on Level2).
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_WithLevel2_HasExactOrderedStructure()
    {
        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Aseguramiento,
            Level2 = ClassificationLevel2.Judicial,
        };

        var result = await _service.GenerateSafeFileNameAsync("doc.pdf", classification, null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        System.Text.RegularExpressions.Regex.IsMatch(result.Value!, @"^ASEGURAMIENTO_JUDICIAL_\d{8}-\d{6}\.pdf$").ShouldBeTrue(result.Value);
        result.Value!.Contains("Judicial", StringComparison.Ordinal).ShouldBeFalse(); // Level2 must be upper-cased too
    }

    /// <summary>
    /// When Level2 is null it must NOT appear — exactly one classification component precedes the timestamp.
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_NullLevel2_OmitsSecondComponent()
    {
        var classification = new ClassificationResult
        {
            Level1 = ClassificationLevel1.Transferencia,
            Level2 = null,
        };

        var result = await _service.GenerateSafeFileNameAsync("doc.pdf", classification, null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        System.Text.RegularExpressions.Regex.IsMatch(result.Value!, @"^TRANSFERENCIA_\d{8}-\d{6}\.pdf$").ShouldBeTrue(result.Value);
    }

    /// <summary>
    /// A non-empty expediente is sanitized and inserted between Level1 and the timestamp.
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_WithExpediente_HasExactOrderedStructure()
    {
        var classification = new ClassificationResult { Level1 = ClassificationLevel1.Aseguramiento };
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente { NumeroExpediente = "AS1-2505-088637-PHM" },
        };

        var result = await _service.GenerateSafeFileNameAsync("doc.pdf", classification, metadata, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        // Hyphens/digits are valid filename chars → preserved verbatim between the two separators.
        System.Text.RegularExpressions.Regex.IsMatch(result.Value!, @"^ASEGURAMIENTO_AS1-2505-088637-PHM_\d{8}-\d{6}\.pdf$").ShouldBeTrue(result.Value);
    }

    /// <summary>
    /// An empty expediente number must NOT add a component (kills the <c>&amp;&amp;</c>→<c>||</c>
    /// and the <c>!IsNullOrEmpty</c> mutations — either would inject an empty "__" component).
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_EmptyExpedienteNumber_OmitsExpedienteComponent()
    {
        var classification = new ClassificationResult { Level1 = ClassificationLevel1.Aseguramiento };
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente { NumeroExpediente = string.Empty },
        };

        var result = await _service.GenerateSafeFileNameAsync("doc.pdf", classification, metadata, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        System.Text.RegularExpressions.Regex.IsMatch(result.Value!, @"^ASEGURAMIENTO_\d{8}-\d{6}\.pdf$").ShouldBeTrue(result.Value);
        result.Value.ShouldNotContain("__");
    }

    /// <summary>
    /// A null Expediente object must NOT add a component (kills the <c>metadata?.Expediente != null</c> guard).
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_NullExpedienteObject_OmitsExpedienteComponent()
    {
        var classification = new ClassificationResult { Level1 = ClassificationLevel1.Aseguramiento };
        var metadata = new ExtractedMetadata { Expediente = null };

        var result = await _service.GenerateSafeFileNameAsync("doc.pdf", classification, metadata, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        System.Text.RegularExpressions.Regex.IsMatch(result.Value!, @"^ASEGURAMIENTO_\d{8}-\d{6}\.pdf$").ShouldBeTrue(result.Value);
    }

    /// <summary>
    /// Whitespace inside the expediente becomes an underscore (kills the <c>char.IsWhiteSpace</c> branch:
    /// if removed, the space would fall through to the else and be appended verbatim).
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_ExpedienteWithWhitespace_ReplacesWithUnderscore()
    {
        var classification = new ClassificationResult { Level1 = ClassificationLevel1.Aseguramiento };
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente { NumeroExpediente = "AB CD" },
        };

        var result = await _service.GenerateSafeFileNameAsync("doc.pdf", classification, metadata, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ShouldContain("_AB_CD_");
        result.Value.ShouldNotContain("AB CD"); // no raw space survived
    }

    /// <summary>
    /// An invalid filename char inside the expediente becomes an underscore (kills the
    /// <c>invalidChars.Contains(c)</c> branch — ':' is invalid on the path).
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_ExpedienteWithInvalidChar_ReplacesWithUnderscore()
    {
        var classification = new ClassificationResult { Level1 = ClassificationLevel1.Aseguramiento };
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente { NumeroExpediente = "AB:CD" },
        };

        var result = await _service.GenerateSafeFileNameAsync("doc.pdf", classification, metadata, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ShouldContain("_AB_CD_");
        result.Value.ShouldNotContain(":");
    }

    /// <summary>
    /// Ordinary alphanumeric characters in the expediente are preserved verbatim
    /// (kills the else-branch removal that would drop or replace valid chars).
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_ExpedienteWithValidChars_PreservesThem()
    {
        var classification = new ClassificationResult { Level1 = ClassificationLevel1.Aseguramiento };
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente { NumeroExpediente = "Exp123XYZ" },
        };

        var result = await _service.GenerateSafeFileNameAsync("doc.pdf", classification, metadata, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ShouldContain("_Exp123XYZ_");
    }

    /// <summary>
    /// A name longer than 200 characters is truncated to exactly 200 before the extension is appended
    /// (kills the <c>Length &gt; 200</c> guard direction and the <c>Substring(0, 200)</c> constant).
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_OverlongName_TruncatedToExactly200PlusExtension()
    {
        var classification = new ClassificationResult { Level1 = ClassificationLevel1.Aseguramiento };
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente { NumeroExpediente = new string('A', 300) },
        };

        var result = await _service.GenerateSafeFileNameAsync("doc.pdf", classification, metadata, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        // 200 (truncated base) + ".pdf" (4) = 204.
        result.Value!.Length.ShouldBe(204);
        result.Value.ShouldEndWith(".pdf");
        result.Value[..200].ShouldNotContain("."); // extension is appended after truncation, not inside it
    }

    /// <summary>
    /// A short name (well under 200) is NOT truncated — full content is preserved
    /// (kills mutations that always truncate, e.g. <c>Length &gt; 200</c> → <c>Length &gt; 0</c>).
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_ShortName_NotTruncated()
    {
        var classification = new ClassificationResult { Level1 = ClassificationLevel1.Aseguramiento };
        var metadata = new ExtractedMetadata
        {
            Expediente = new Expediente { NumeroExpediente = "SHORT123" },
        };

        var result = await _service.GenerateSafeFileNameAsync("doc.pdf", classification, metadata, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ShouldContain("SHORT123"); // full, untruncated
        result.Value.Length.ShouldBeLessThan(204);
    }

    /// <summary>
    /// A null Level1 makes <c>Level1.ToString()</c> throw inside the try; the catch converts it
    /// to a failure result (covers and kills the catch-block log + failure-message mutants).
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_NullLevel1_ReturnsFailureFromCatch()
    {
        var classification = new ClassificationResult { Level1 = null! };

        var result = await _service.GenerateSafeFileNameAsync("doc.pdf", classification, null, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldContain("Error generating safe file name");
    }

    /// <summary>
    /// The original file's extension is carried over onto the generated name.
    /// </summary>
    [Fact]
    public async Task GenerateSafeFileNameAsync_PreservesOriginalExtension()
    {
        var classification = new ClassificationResult { Level1 = ClassificationLevel1.Aseguramiento };

        var result = await _service.GenerateSafeFileNameAsync("report.docx", classification, null, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotBeNull();
        result.Value!.ShouldEndWith(".docx");
        result.Value.ShouldNotEndWith(".pdf");
    }
}

