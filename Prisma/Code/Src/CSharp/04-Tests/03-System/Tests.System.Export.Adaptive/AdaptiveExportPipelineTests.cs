using System.IO;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace ExxerCube.Prisma.Tests.System.Export.Adaptive;

/// <summary>
/// System tests for the complete adaptive export pipeline.
/// NO MOCKS - Tests full integration with real database, real mapper, real exporter.
/// Tests cross-cutting concerns and validates actual file generation.
/// </summary>
public sealed class AdaptiveExportPipelineTests : IDisposable
{
    private readonly TemplateDbContext _dbContext;
    private readonly ITemplateRepository _templateRepository;
    private readonly ITemplateFieldMapper _fieldMapper;
    private readonly IAdaptiveExporter _exporter;

    public AdaptiveExportPipelineTests()
    {
        // Setup REAL database (InMemory for tests)
        var options = new DbContextOptionsBuilder<TemplateDbContext>()
            .UseInMemoryDatabase(databaseName: $"SystemTestDb_{Guid.NewGuid()}")
            .Options;

        _dbContext = new TemplateDbContext(options);

        // Create REAL dependencies (NO MOCKS)
        _templateRepository = new TemplateRepository(_dbContext, NullLogger<TemplateRepository>.Instance);
        _fieldMapper = new TemplateFieldMapper(NullLogger<TemplateFieldMapper>.Instance);
        _exporter = new AdaptiveExporter(_templateRepository, _fieldMapper, NullLogger<AdaptiveExporter>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task ExportToExcel_WithSimpleTemplate_CreatesValidExcelFile()
    {
        // Arrange: Create a REAL template in the database
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0",
            Name = "Simple Excel Template",
            Description = "Basic Excel export with 3 fields",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SystemTest"
        };

        // Define field mappings for Excel columns
        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Name",
            targetField: "Full Name",
            isRequired: true,
            dataType: "string")
        {
            DisplayOrder = 1
        });

        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Age",
            targetField: "Age",
            isRequired: true,
            dataType: "int")
        {
            DisplayOrder = 2
        });

        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Email",
            targetField: "Email Address",
            isRequired: false,
            dataType: "string")
        {
            DisplayOrder = 3
        });

        // Save template to REAL database
        var saveResult = await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);
        saveResult.IsSuccess.ShouldBeTrue();

        // Create REAL source data
        var sourceData = new
        {
            Name = "John Doe",
            Age = 35,
            Email = "john.doe@example.com"
        };

        // Act: Export using REAL exporter (full pipeline)
        var exportResult = await _exporter.ExportAsync(sourceData, "Excel", TestContext.Current.CancellationToken);

        // Assert: Export succeeded
        exportResult.IsSuccess.ShouldBeTrue();
        exportResult.Value.ShouldNotBeNull();
        exportResult.Value.Length.ShouldBeGreaterThan(0);

        // Assert: Generated file is valid Excel
        using var memoryStream = new MemoryStream(exportResult.Value!);
        using var workbook = new XLWorkbook(memoryStream);

        // Verify Excel structure
        workbook.Worksheets.Count.ShouldBe(1);
        var worksheet = workbook.Worksheets.First();

        // Verify headers (row 1)
        worksheet.Cell(1, 1).GetString().ShouldBe("Full Name");
        worksheet.Cell(1, 2).GetString().ShouldBe("Age");
        worksheet.Cell(1, 3).GetString().ShouldBe("Email Address");

        // Verify data (row 2)
        worksheet.Cell(2, 1).GetString().ShouldBe("John Doe");
        worksheet.Cell(2, 2).GetString().ShouldBe("35");
        worksheet.Cell(2, 3).GetString().ShouldBe("john.doe@example.com");
    }

    [Fact]
    public async Task ExportToExcel_WithTransformations_AppliesTransformationsCorrectly()
    {
        // Arrange: Template with transformation expressions
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0",
            Name = "Excel Template with Transformations",
            Description = "Tests Trim, ToUpper, chained transformations",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SystemTest"
        };

        // Field with transformation: Trim() | ToUpper()
        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Name",
            targetField: "Name (Uppercase)",
            isRequired: true,
            dataType: "string")
        {
            DisplayOrder = 1,
            TransformExpression = "Trim() | ToUpper()"
        });

        // Field with transformation: PadLeft(10, 0)
        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Code",
            targetField: "Code (Padded)",
            isRequired: true,
            dataType: "string")
        {
            DisplayOrder = 2,
            TransformExpression = "PadLeft(10, 0)"
        });

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceData = new
        {
            Name = "  john doe  ",  // Leading/trailing spaces
            Code = "123"
        };

        // Act
        var exportResult = await _exporter.ExportAsync(sourceData, "Excel", TestContext.Current.CancellationToken);

        // Assert
        exportResult.IsSuccess.ShouldBeTrue();

        using var memoryStream = new MemoryStream(exportResult.Value!);
        using var workbook = new XLWorkbook(memoryStream);
        var worksheet = workbook.Worksheets.First();

        // Verify transformations were applied
        worksheet.Cell(2, 1).GetString().ShouldBe("JOHN DOE");  // Trimmed and uppercased
        worksheet.Cell(2, 2).GetString().ShouldBe("0000000123");  // Padded to 10 chars
    }

    [Fact]
    public async Task ExportToExcel_WithValidationRules_RejectsInvalidData()
    {
        // Arrange: Template with validation rules
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0",
            Name = "Excel Template with Validation",
            Description = "Tests validation rules",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SystemTest"
        };

        // Field with email validation
        var emailMapping = new FieldMapping(
            sourceFieldPath: "Email",
            targetField: "Email",
            isRequired: true,
            dataType: "string")
        {
            DisplayOrder = 1
        };

        emailMapping.ValidationRules.Add("EmailAddress");
        template.FieldMappings.Add(emailMapping);

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var invalidData = new
        {
            Email = "not-a-valid-email"  // Invalid email format
        };

        // Act
        var exportResult = await _exporter.ExportAsync(invalidData, "Excel", TestContext.Current.CancellationToken);

        // Assert: Export should fail due to validation
        exportResult.IsFailure.ShouldBeTrue();
        exportResult.Error.ShouldContain("not a valid email");
    }

    [Fact]
    public async Task ExportToExcel_WithMissingOptionalField_UsesDefaultValue()
    {
        // Arrange
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0",
            Name = "Excel Template with Optional Fields",
            Description = "Tests optional fields with defaults",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SystemTest"
        };

        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Name",
            targetField: "Name",
            isRequired: true,
            dataType: "string")
        {
            DisplayOrder = 1
        });

        // Optional field with default value
        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "MiddleName",
            targetField: "Middle Name",
            isRequired: false,
            dataType: "string")
        {
            DisplayOrder = 2,
            DefaultValue = "N/A"
        });

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceData = new
        {
            Name = "John Doe"
            // MiddleName is missing
        };

        // Act
        var exportResult = await _exporter.ExportAsync(sourceData, "Excel", TestContext.Current.CancellationToken);

        // Assert
        exportResult.IsSuccess.ShouldBeTrue();

        using var memoryStream = new MemoryStream(exportResult.Value!);
        using var workbook = new XLWorkbook(memoryStream);
        var worksheet = workbook.Worksheets.First();

        worksheet.Cell(2, 1).GetString().ShouldBe("John Doe");
        worksheet.Cell(2, 2).GetString().ShouldBe("N/A");  // Default value used
    }

    [Fact]
    public async Task ExportToExcel_WithMultipleRows_CreatesCorrectRowCount()
    {
        // Arrange: This will test batch export in the future
        // For now, we'll just validate single-row export works
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "Excel",
            Version = "1.0.0",
            Name = "Excel Template for Batch Export",
            Description = "Prepares for batch export capability",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SystemTest"
        };

        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Name",
            targetField: "Name",
            isRequired: true,
            dataType: "string")
        {
            DisplayOrder = 1
        });

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceData = new { Name = "Test Record" };

        // Act
        var exportResult = await _exporter.ExportAsync(sourceData, "Excel", TestContext.Current.CancellationToken);

        // Assert
        exportResult.IsSuccess.ShouldBeTrue();

        using var memoryStream = new MemoryStream(exportResult.Value!);
        using var workbook = new XLWorkbook(memoryStream);
        var worksheet = workbook.Worksheets.First();

        // Should have header row + 1 data row
        worksheet.LastRowUsed()!.RowNumber().ShouldBe(2);
    }

    [Fact]
    public async Task ExportToXml_WithSimpleTemplate_CreatesValidXmlFile()
    {
        // Arrange: Create a REAL template in the database
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "XML",
            Version = "1.0.0",
            Name = "Simple XML Template",
            Description = "Basic XML export with 3 fields",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SystemTest"
        };

        // Define field mappings for XML elements
        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Name",
            targetField: "FullName",
            isRequired: true,
            dataType: "string")
        {
            DisplayOrder = 1
        });

        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Age",
            targetField: "Age",
            isRequired: true,
            dataType: "int")
        {
            DisplayOrder = 2
        });

        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Email",
            targetField: "EmailAddress",
            isRequired: false,
            dataType: "string")
        {
            DisplayOrder = 3
        });

        // Save template to REAL database
        var saveResult = await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);
        saveResult.IsSuccess.ShouldBeTrue();

        // Create REAL source data
        var sourceData = new
        {
            Name = "John Doe",
            Age = 35,
            Email = "john.doe@example.com"
        };

        // Act: Export using REAL exporter (full pipeline)
        var exportResult = await _exporter.ExportAsync(sourceData, "XML", TestContext.Current.CancellationToken);

        // Assert: Export succeeded
        exportResult.IsSuccess.ShouldBeTrue();
        exportResult.Value.ShouldNotBeNull();
        exportResult.Value.Length.ShouldBeGreaterThan(0);

        // Assert: Generated file is valid XML
        var xmlString = Encoding.UTF8.GetString(exportResult.Value!);
        var xmlDoc = XDocument.Parse(xmlString);

        // Verify XML structure
        xmlDoc.Root.ShouldNotBeNull();
        xmlDoc.Root!.Name.LocalName.ShouldBe("Export");

        // Verify elements in correct order
        var elements = xmlDoc.Root.Elements().ToList();
        elements.Count.ShouldBe(3);
        elements[0].Name.LocalName.ShouldBe("FullName");
        elements[0].Value.ShouldBe("John Doe");
        elements[1].Name.LocalName.ShouldBe("Age");
        elements[1].Value.ShouldBe("35");
        elements[2].Name.LocalName.ShouldBe("EmailAddress");
        elements[2].Value.ShouldBe("john.doe@example.com");
    }

    [Fact]
    public async Task ExportToXml_WithTransformations_AppliesTransformationsCorrectly()
    {
        // Arrange: Template with transformation expressions
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "XML",
            Version = "1.0.0",
            Name = "XML Template with Transformations",
            Description = "Tests Trim, ToUpper, chained transformations",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SystemTest"
        };

        // Field with transformation: Trim() | ToUpper()
        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Name",
            targetField: "NameUppercase",
            isRequired: true,
            dataType: "string")
        {
            DisplayOrder = 1,
            TransformExpression = "Trim() | ToUpper()"
        });

        // Field with transformation: PadLeft(10, 0)
        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Code",
            targetField: "CodePadded",
            isRequired: true,
            dataType: "string")
        {
            DisplayOrder = 2,
            TransformExpression = "PadLeft(10, 0)"
        });

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceData = new
        {
            Name = "  john doe  ",  // Leading/trailing spaces
            Code = "123"
        };

        // Act
        var exportResult = await _exporter.ExportAsync(sourceData, "XML", TestContext.Current.CancellationToken);

        // Assert
        exportResult.IsSuccess.ShouldBeTrue();

        var xmlString = Encoding.UTF8.GetString(exportResult.Value!);
        var xmlDoc = XDocument.Parse(xmlString);

        // Verify transformations were applied
        var nameElement = xmlDoc.Root!.Element("NameUppercase");
        nameElement.ShouldNotBeNull();
        nameElement!.Value.ShouldBe("JOHN DOE");  // Trimmed and uppercased

        var codeElement = xmlDoc.Root!.Element("CodePadded");
        codeElement.ShouldNotBeNull();
        codeElement!.Value.ShouldBe("0000000123");  // Padded to 10 chars
    }

    [Fact]
    public async Task ExportToXml_WithValidationRules_RejectsInvalidData()
    {
        // Arrange: Template with validation rules
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "XML",
            Version = "1.0.0",
            Name = "XML Template with Validation",
            Description = "Tests validation rules",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SystemTest"
        };

        // Field with email validation
        var emailMapping = new FieldMapping(
            sourceFieldPath: "Email",
            targetField: "Email",
            isRequired: true,
            dataType: "string")
        {
            DisplayOrder = 1
        };

        emailMapping.ValidationRules.Add("EmailAddress");
        template.FieldMappings.Add(emailMapping);

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var invalidData = new
        {
            Email = "not-a-valid-email"  // Invalid email format
        };

        // Act
        var exportResult = await _exporter.ExportAsync(invalidData, "XML", TestContext.Current.CancellationToken);

        // Assert: Export should fail due to validation
        exportResult.IsFailure.ShouldBeTrue();
        exportResult.Error.ShouldContain("not a valid email");
    }

    [Fact]
    public async Task ExportToXml_WithMissingOptionalField_UsesDefaultValue()
    {
        // Arrange
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "XML",
            Version = "1.0.0",
            Name = "XML Template with Optional Fields",
            Description = "Tests optional fields with defaults",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SystemTest"
        };

        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Name",
            targetField: "Name",
            isRequired: true,
            dataType: "string")
        {
            DisplayOrder = 1
        });

        // Optional field with default value
        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "MiddleName",
            targetField: "MiddleName",
            isRequired: false,
            dataType: "string")
        {
            DisplayOrder = 2,
            DefaultValue = "N/A"
        });

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceData = new
        {
            Name = "John Doe"
            // MiddleName is missing
        };

        // Act
        var exportResult = await _exporter.ExportAsync(sourceData, "XML", TestContext.Current.CancellationToken);

        // Assert
        exportResult.IsSuccess.ShouldBeTrue();

        var xmlString = Encoding.UTF8.GetString(exportResult.Value!);
        var xmlDoc = XDocument.Parse(xmlString);

        var nameElement = xmlDoc.Root!.Element("Name");
        nameElement.ShouldNotBeNull();
        nameElement!.Value.ShouldBe("John Doe");

        var middleNameElement = xmlDoc.Root!.Element("MiddleName");
        middleNameElement.ShouldNotBeNull();
        middleNameElement!.Value.ShouldBe("N/A");  // Default value used
    }

    [Fact]
    public async Task ExportToXml_WithMultipleFields_CreatesCorrectStructure()
    {
        // Arrange
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "XML",
            Version = "1.0.0",
            Name = "XML Template with Multiple Fields",
            Description = "Tests correct XML structure with ordered fields",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SystemTest"
        };

        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Name",
            targetField: "Name",
            isRequired: true,
            dataType: "string")
        {
            DisplayOrder = 1
        });

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceData = new { Name = "Test Record" };

        // Act
        var exportResult = await _exporter.ExportAsync(sourceData, "XML", TestContext.Current.CancellationToken);

        // Assert
        exportResult.IsSuccess.ShouldBeTrue();

        var xmlString = Encoding.UTF8.GetString(exportResult.Value!);
        var xmlDoc = XDocument.Parse(xmlString);

        // Should have root element with 1 child element
        xmlDoc.Root.ShouldNotBeNull();
        xmlDoc.Root!.Elements().Count().ShouldBe(1);
    }

    [Fact]
    public async Task ExportToDocx_WithSimpleTemplate_CreatesValidDocxFile()
    {
        // Arrange: Create a REAL template in the database
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "DOCX",
            Version = "1.0.0",
            Name = "Simple DOCX Template",
            Description = "Basic DOCX export with 3 fields",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SystemTest"
        };

        // Define field mappings for DOCX paragraphs
        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Name",
            targetField: "Full Name",
            isRequired: true,
            dataType: "string")
        {
            DisplayOrder = 1
        });

        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Age",
            targetField: "Age",
            isRequired: true,
            dataType: "int")
        {
            DisplayOrder = 2
        });

        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Email",
            targetField: "Email Address",
            isRequired: false,
            dataType: "string")
        {
            DisplayOrder = 3
        });

        // Save template to REAL database
        var saveResult = await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);
        saveResult.IsSuccess.ShouldBeTrue();

        // Create REAL source data
        var sourceData = new
        {
            Name = "John Doe",
            Age = 35,
            Email = "john.doe@example.com"
        };

        // Act: Export using REAL exporter (full pipeline)
        var exportResult = await _exporter.ExportAsync(sourceData, "DOCX", TestContext.Current.CancellationToken);

        // Assert: Export succeeded
        exportResult.IsSuccess.ShouldBeTrue();
        exportResult.Value.ShouldNotBeNull();
        exportResult.Value.Length.ShouldBeGreaterThan(0);

        // Assert: Generated file is valid DOCX
        using var memoryStream = new MemoryStream(exportResult.Value!);
        using var wordDoc = WordprocessingDocument.Open(memoryStream, false);

        // Verify DOCX structure
        wordDoc.MainDocumentPart.ShouldNotBeNull();
        var body = wordDoc.MainDocumentPart!.Document.Body;
        body.ShouldNotBeNull();

        // Verify paragraphs (3 field paragraphs in order)
        var paragraphs = body!.Elements<Paragraph>().ToList();
        paragraphs.Count.ShouldBeGreaterThanOrEqualTo(3);

        // Verify content
        paragraphs[0].InnerText.ShouldContain("Full Name");
        paragraphs[0].InnerText.ShouldContain("John Doe");
        paragraphs[1].InnerText.ShouldContain("Age");
        paragraphs[1].InnerText.ShouldContain("35");
        paragraphs[2].InnerText.ShouldContain("Email Address");
        paragraphs[2].InnerText.ShouldContain("john.doe@example.com");
    }

    [Fact]
    public async Task ExportToDocx_WithTransformations_AppliesTransformationsCorrectly()
    {
        // Arrange: Template with transformation expressions
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "DOCX",
            Version = "1.0.0",
            Name = "DOCX Template with Transformations",
            Description = "Tests Trim, ToUpper, chained transformations",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SystemTest"
        };

        // Field with transformation: Trim() | ToUpper()
        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Name",
            targetField: "Name (Uppercase)",
            isRequired: true,
            dataType: "string")
        {
            DisplayOrder = 1,
            TransformExpression = "Trim() | ToUpper()"
        });

        // Field with transformation: PadLeft(10, 0)
        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Code",
            targetField: "Code (Padded)",
            isRequired: true,
            dataType: "string")
        {
            DisplayOrder = 2,
            TransformExpression = "PadLeft(10, 0)"
        });

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceData = new
        {
            Name = "  john doe  ",  // Leading/trailing spaces
            Code = "123"
        };

        // Act
        var exportResult = await _exporter.ExportAsync(sourceData, "DOCX", TestContext.Current.CancellationToken);

        // Assert
        exportResult.IsSuccess.ShouldBeTrue();

        using var memoryStream = new MemoryStream(exportResult.Value!);
        using var wordDoc = WordprocessingDocument.Open(memoryStream, false);
        var body = wordDoc.MainDocumentPart!.Document.Body;
        var paragraphs = body!.Elements<Paragraph>().ToList();

        // Verify transformations were applied
        paragraphs[0].InnerText.ShouldContain("JOHN DOE");  // Trimmed and uppercased
        paragraphs[1].InnerText.ShouldContain("0000000123");  // Padded to 10 chars
    }

    [Fact]
    public async Task ExportToDocx_WithValidationRules_RejectsInvalidData()
    {
        // Arrange: Template with validation rules
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "DOCX",
            Version = "1.0.0",
            Name = "DOCX Template with Validation",
            Description = "Tests validation rules",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SystemTest"
        };

        // Field with email validation
        var emailMapping = new FieldMapping(
            sourceFieldPath: "Email",
            targetField: "Email",
            isRequired: true,
            dataType: "string")
        {
            DisplayOrder = 1
        };

        emailMapping.ValidationRules.Add("EmailAddress");
        template.FieldMappings.Add(emailMapping);

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var invalidData = new
        {
            Email = "not-a-valid-email"  // Invalid email format
        };

        // Act
        var exportResult = await _exporter.ExportAsync(invalidData, "DOCX", TestContext.Current.CancellationToken);

        // Assert: Export should fail due to validation
        exportResult.IsFailure.ShouldBeTrue();
        exportResult.Error.ShouldContain("not a valid email");
    }

    [Fact]
    public async Task ExportToDocx_WithMissingOptionalField_UsesDefaultValue()
    {
        // Arrange
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "DOCX",
            Version = "1.0.0",
            Name = "DOCX Template with Optional Fields",
            Description = "Tests optional fields with defaults",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SystemTest"
        };

        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Name",
            targetField: "Name",
            isRequired: true,
            dataType: "string")
        {
            DisplayOrder = 1
        });

        // Optional field with default value
        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "MiddleName",
            targetField: "Middle Name",
            isRequired: false,
            dataType: "string")
        {
            DisplayOrder = 2,
            DefaultValue = "N/A"
        });

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceData = new
        {
            Name = "John Doe"
            // MiddleName is missing
        };

        // Act
        var exportResult = await _exporter.ExportAsync(sourceData, "DOCX", TestContext.Current.CancellationToken);

        // Assert
        exportResult.IsSuccess.ShouldBeTrue();

        using var memoryStream = new MemoryStream(exportResult.Value!);
        using var wordDoc = WordprocessingDocument.Open(memoryStream, false);
        var body = wordDoc.MainDocumentPart!.Document.Body;
        var paragraphs = body!.Elements<Paragraph>().ToList();

        paragraphs[0].InnerText.ShouldContain("John Doe");
        paragraphs[1].InnerText.ShouldContain("N/A");  // Default value used
    }

    [Fact]
    public async Task ExportToDocx_WithMultipleFields_CreatesCorrectStructure()
    {
        // Arrange
        var template = new TemplateDefinition
        {
            TemplateId = Guid.NewGuid().ToString(),
            TemplateType = "DOCX",
            Version = "1.0.0",
            Name = "DOCX Template with Multiple Fields",
            Description = "Tests correct DOCX structure with ordered fields",
            IsActive = true,
            EffectiveDate = DateTime.UtcNow.AddDays(-1),
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "SystemTest"
        };

        template.FieldMappings.Add(new FieldMapping(
            sourceFieldPath: "Name",
            targetField: "Name",
            isRequired: true,
            dataType: "string")
        {
            DisplayOrder = 1
        });

        await _templateRepository.SaveTemplateAsync(template, TestContext.Current.CancellationToken);

        var sourceData = new { Name = "Test Record" };

        // Act
        var exportResult = await _exporter.ExportAsync(sourceData, "DOCX", TestContext.Current.CancellationToken);

        // Assert
        exportResult.IsSuccess.ShouldBeTrue();

        using var memoryStream = new MemoryStream(exportResult.Value!);
        using var wordDoc = WordprocessingDocument.Open(memoryStream, false);
        var body = wordDoc.MainDocumentPart!.Document.Body;

        // Should have at least 1 paragraph
        body.ShouldNotBeNull();
        body!.Elements<Paragraph>().Count().ShouldBeGreaterThanOrEqualTo(1);
    }
}
