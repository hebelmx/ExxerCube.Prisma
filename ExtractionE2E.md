
  Constraints & Decisions

  - ✓ Comparison: Exact match → Fuzzy fallback (existing class)
  - ✓ Processing: Sequential only (one at a time)
  - ✓ Storage: In-memory only (no persistence)
  - ✓ Batch Size: 4 documents max (not 25)
  - ✓ OCR Confidence: 75% (adjust during dev/testing)
  - ✓ Bulk Processing: Interface stubs only (Red phase, no implementation)
  - ✓ Focus: Stakeholder presentation ready

  ---
  DETAILED IMPLEMENTATION PLAN

  PHASE 1: Renaming & UI Alignment ⚡ (30 min)

  TDD: Not applicable (UI refactoring)

  Tasks:
  1. Rename file: OCRDemo.razor → DocumentProcessing.razor
  2. Update all text references:
    - PageTitle: "Document Processing"
    - Header: "Document Processing"
    - Section headers: "XML Processing" / "OCR Processing"
  3. Find and update NavMenu references (if any)

  Files Modified:
  - OCRDemo.razor → DocumentProcessing.razor
  - Navigation menu (TBD - need to find it)

  Acceptance: No "Demo" text visible, all menus/titles say "Document Processing"

  ---
  PHASE 2: OCR Twin Buttons 🔧 (3 hours)

  TDD Approach: Write E2E tests first, then implement

  Step 2.1: Write E2E Tests (Red Phase)

  File: Tests.Infrastructure.BrowserAutomation.E2E/OcrExtractionE2ETests.cs

  public class OcrExtractionE2ETests : IAsyncLifetime
  {
      // Test fixtures directory exists
      [Fact] public void Fixtures_PRP1Directory_ShouldContainPdfFiles()

      // Test OCR extraction for each of 4 PDFs
      [Fact] public async Task ExtractFromPdf_PRP1_222AAA_ShouldExtractAllFields()
      [Fact] public async Task ExtractFromPdf_PRP1_333BBB_ShouldExtractAllFields()
      [Fact] public async Task ExtractFromPdf_PRP1_333ccc_ShouldExtractAllFields()
      [Fact] public async Task ExtractFromPdf_PRP1_555CCC_ShouldExtractAllFields()

      // Test field extraction patterns
      [Fact] public async Task ExtractFromPdf_NumeroExpediente_ShouldMatchPattern()
      [Fact] public async Task ExtractFromPdf_NumeroOficio_ShouldMatchPattern()
      [Fact] public async Task ExtractFromPdf_SolicitudSiara_ShouldMatchPattern()

      // Test confidence threshold
      [Fact] public async Task ExtractFromPdf_Confidence_ShouldBeAbove75Percent()

      // Smoke test all 4 PDFs
      [Fact] public async Task ExtractFromPdf_AllPRP1Fixtures_ShouldProcessWithoutErrors()
  }

  Expected: All tests FAIL (Red) - OCR functionality doesn't exist yet

  Step 2.2: Implement OCR Service Integration (Green Phase)

  File: DocumentProcessing.razor

  New Code Section (after XML section):
  <!-- OCR Processing Section -->
  <MudCard Class="mb-4">
      <MudCardHeader>
          <MudText Typo="Typo.h6">
              <MudIcon Icon="@Icons.Material.Filled.PictureAsPdf" Class="mr-2" />
              OCR Processing - PDF Documents
          </MudText>
      </MudCardHeader>
      <MudCardContent>
          <MudText Typo="Typo.body2" Class="mb-3">
              Click any PDF fixture to extract text with OCR (Tesseract):
          </MudText>

          <MudGrid>
              <!-- 4 buttons matching XML twins -->
              <MudItem xs="12" sm="6" md="3">
                  <MudButton Variant="Variant.Filled" Color="Color.Primary"
                             OnClick="@(() => LoadPdfFixture("222AAA-44444444442025.pdf"))"
                             Disabled="isProcessing">
                      PRP1: 222AAA PDF
                  </MudButton>
              </MudItem>
              <!-- Repeat for 333BBB, 333ccc, 555CCC -->
          </MudGrid>

          <!-- Display OCR results if available -->
          @if (ocrExpediente != null) { /* Display logic */ }
      </MudCardContent>
  </MudCard>

  New Method:
  private Expediente? ocrExpediente;
  private ProcessingResult? ocrResult;

  private async Task LoadPdfFixture(string pdfFileName)
  {
      // 1. Load PDF from PRP1 fixtures
      var pdfPath = Path.Combine(GetFixturesPath(), pdfFileName);
      var pdfBytes = await File.ReadAllBytesAsync(pdfPath);

      // 2. Process with OCR service
      var imageData = new ImageData { Data = pdfBytes, SourcePath = pdfFileName };
      var config = new ProcessingConfig {
          OCRConfig = new OCRConfig {
              ConfidenceThreshold = 75.0f, // Updated threshold
              Language = "spa"
          }
      };

      var result = await OcrService.ProcessDocumentAsync(imageData, config);

      // 3. Parse OCR text into Expediente domain object
      ocrResult = result.Value;
      ocrExpediente = ParseOcrToExpediente(ocrResult.OCRResult.Text);

      StateHasChanged();
  }

  Expected: All OcrExtractionE2ETests PASS (Green)

  ---
  PHASE 3: Comparison Feature 🔍 (4 hours)

  TDD Approach: Service tests → E2E tests → Implementation

  Step 3.1: Find Existing Fuzzy Comparison Class

  Action: Search codebase for fuzzy string matching

  grep -r "Levenshtein\|FuzzyMatch\|StringSimilarity" --include="*.cs"

  Expected: Find existing fuzzy comparison utility

  Step 3.2: Write Service Tests (Red Phase)

  File: Tests.Infrastructure.Extraction/DocumentComparisonServiceTests.cs

  public class DocumentComparisonServiceTests
  {
      [Fact] public void CompareFields_ExactMatch_ReturnsMatchStatus()
      [Fact] public void CompareFields_DifferentCase_UsesFuzzyMatch()
      [Fact] public void CompareFields_MinorTypo_UsesFuzzyMatch()
      [Fact] public void CompareFields_CompletelyDifferent_ReturnsDifferentStatus()
      [Fact] public void CompareExpedientes_AllFieldsMatch_Returns100PercentSimilarity()
      [Fact] public void CompareExpedientes_HalfFieldsMatch_Returns50PercentSimilarity()
  }

  Step 3.3: Implement Comparison Service (Green Phase)

  File: Infrastructure.Extraction/DocumentComparisonService.cs

  public interface IDocumentComparisonService
  {
      ComparisonResult CompareExpedientes(Expediente xml, Expediente ocr);
      FieldComparison CompareField(string fieldName, string xmlValue, string ocrValue, float? ocrConfidence = null);
  }

  public class DocumentComparisonService : IDocumentComparisonService
  {
      private readonly IFuzzyMatcher _fuzzyMatcher; // Existing class

      public ComparisonResult CompareExpedientes(Expediente xml, Expediente ocr)
      {
          var comparisons = new List<FieldComparison>();

          // Compare each field
          comparisons.Add(CompareField("NumeroExpediente", xml.NumeroExpediente, ocr.NumeroExpediente));
          comparisons.Add(CompareField("NumeroOficio", xml.NumeroOficio, ocr.NumeroOficio));
          // ... all fields

          return new ComparisonResult {
              FieldComparisons = comparisons,
              OverallSimilarity = CalculateOverallSimilarity(comparisons),
              MatchCount = comparisons.Count(c => c.Status == "Match"),
              TotalFields = comparisons.Count
          };
      }

      public FieldComparison CompareField(string fieldName, string xmlValue, string ocrValue, float? ocrConfidence =
   null)
      {
          // 1. Exact match first
          if (string.Equals(xmlValue?.Trim(), ocrValue?.Trim(), StringComparison.Ordinal))
          {
              return new FieldComparison {
                  FieldName = fieldName,
                  XmlValue = xmlValue,
                  OcrValue = ocrValue,
                  Status = "Match",
                  Similarity = 1.0f,
                  OcrConfidence = ocrConfidence
              };
          }

          // 2. Fuzzy match fallback
          var similarity = _fuzzyMatcher.CalculateSimilarity(xmlValue, ocrValue);

          return new FieldComparison {
              FieldName = fieldName,
              XmlValue = xmlValue,
              OcrValue = ocrValue,
              Status = similarity > 0.8f ? "Partial" : "Different",
              Similarity = similarity,
              OcrConfidence = ocrConfidence
          };
      }
  }

  Models:
  public class ComparisonResult
  {
      public List<FieldComparison> FieldComparisons { get; set; } = new();
      public float OverallSimilarity { get; set; }
      public int MatchCount { get; set; }
      public int TotalFields { get; set; }
  }

  public class FieldComparison
  {
      public string FieldName { get; set; } = string.Empty;
      public string XmlValue { get; set; } = string.Empty;
      public string OcrValue { get; set; } = string.Empty;
      public string Status { get; set; } = "Pending"; // Match, Partial, Different, Missing
      public float Similarity { get; set; }
      public float? OcrConfidence { get; set; }
  }

  Step 3.4: Write E2E Tests (Red → Green)

  File: Tests.Infrastructure.BrowserAutomation.E2E/ComparisonE2ETests.cs

  public class ComparisonE2ETests : IAsyncLifetime
  {
      [Fact] public async Task Compare_222AAA_XmlVsOcr_ShouldMatchMajorityOfFields()
      [Fact] public async Task Compare_AllPRP1Fixtures_ShouldAchieveMinimum70PercentMatch()
      [Fact] public async Task Compare_WithMissingFields_ShouldHandleGracefully()
      [Fact] public async Task Compare_WithNullValues_ShouldHandleGracefully()
  }

  Step 3.5: UI Implementation

  File: DocumentProcessing.razor

  New Section:
  <!-- Comparison Section -->
  <MudCard Class="mb-4" Style="@(canCompare ? "" : "opacity: 0.5;")">
      <MudCardHeader>
          <MudText Typo="Typo.h6">
              <MudIcon Icon="@Icons.Material.Filled.Compare" Class="mr-2" />
              XML vs OCR Comparison
          </MudText>
      </MudCardHeader>
      <MudCardContent>
          <MudButton Variant="Variant.Filled" Color="Color.Info"
                     OnClick="CompareResults"
                     Disabled="@(!canCompare || isProcessing)">
              <MudIcon Icon="@Icons.Material.Filled.CompareArrows" Class="mr-2" />
              Compare XML vs OCR
          </MudButton>

          @if (comparisonResult != null)
          {
              <!-- Summary Stats -->
              <MudGrid Class="mt-4">
                  <MudItem xs="12" sm="4">
                      <MudPaper Class="pa-4" Style="background: linear-gradient(135deg, #43e97b 0%, #38f9d7 100%);">
                          <MudText Typo="Typo.h3" Align="Align.Center">
                              @comparisonResult.MatchCount / @comparisonResult.TotalFields
                          </MudText>
                          <MudText Typo="Typo.body1" Align="Align.Center">Exact Matches</MudText>
                      </MudPaper>
                  </MudItem>
                  <MudItem xs="12" sm="4">
                      <MudPaper Class="pa-4" Style="background: linear-gradient(135deg, #fa709a 0%, #fee140 100%);">
                          <MudText Typo="Typo.h3" Align="Align.Center">
                              @($"{comparisonResult.OverallSimilarity:P0}")
                          </MudText>
                          <MudText Typo="Typo.body1" Align="Align.Center">Overall Similarity</MudText>
                      </MudPaper>
                  </MudItem>
              </MudGrid>

              <!-- Detailed Field Comparison Table -->
              <MudTable Items="@comparisonResult.FieldComparisons" Class="mt-4" Dense="true">
                  <HeaderContent>
                      <MudTh>Field</MudTh>
                      <MudTh>XML Value</MudTh>
                      <MudTh>OCR Value</MudTh>
                      <MudTh>Similarity</MudTh>
                      <MudTh>Status</MudTh>
                  </HeaderContent>
                  <RowTemplate>
                      <MudTd>@context.FieldName</MudTd>
                      <MudTd><code>@context.XmlValue</code></MudTd>
                      <MudTd><code>@context.OcrValue</code></MudTd>
                      <MudTd>@($"{context.Similarity:P0}")</MudTd>
                      <MudTd>
                          <MudChip Color="@GetStatusColor(context.Status)" Size="Size.Small">
                              @context.Status
                          </MudChip>
                      </MudTd>
                  </RowTemplate>
              </MudTable>
          }
      </MudCardContent>
  </MudCard>

  @code {
      private bool canCompare => xmlExpediente != null && ocrExpediente != null;
      private ComparisonResult? comparisonResult;

      private async Task CompareResults()
      {
          if (!canCompare) return;

          isProcessing = true;
          comparisonResult = await ComparisonService.CompareExpedientes(xmlExpediente, ocrExpediente);
          isProcessing = false;

          Snackbar.Add($"Comparison complete: {comparisonResult.MatchCount}/{comparisonResult.TotalFields} exact
  matches", Severity.Info);
          StateHasChanged();
      }

      private Color GetStatusColor(string status) => status switch {
          "Match" => Color.Success,
          "Partial" => Color.Warning,
          "Different" => Color.Error,
          _ => Color.Default
      };
  }

  ---
  PHASE 4: Bulk Processing (Interface Stubs Only) 📋 (1.5 hours)

  TDD Approach: RED PHASE ONLY - Tests that fail, no implementation

  Step 4.1: Create Interface Stubs

  File: Domain/Interfaces/IBulkProcessingService.cs

  public interface IBulkProcessingService
  {
      /// <summary>
      /// Gets random sample of documents from bulk directory.
      /// </summary>
      /// <param name="count">Number of documents (max 4)</param>
      Task<Result<List<BulkDocument>>> GetRandomSampleAsync(int count, CancellationToken cancellationToken =
  default);

      /// <summary>
      /// Processes a single document: XML + OCR + Comparison.
      /// </summary>
      Task<Result<BulkProcessingResult>> ProcessDocumentAsync(BulkDocument document, CancellationToken
  cancellationToken = default);
  }

  public class BulkDocument
  {
      public string Id { get; set; } = string.Empty; // Directory name
      public string XmlPath { get; set; } = string.Empty;
      public string PdfPath { get; set; } = string.Empty;
      public BulkProcessingStatus Status { get; set; } = BulkProcessingStatus.Pending;
  }

  public enum BulkProcessingStatus
  {
      Pending,
      ProcessingXml,
      ProcessingOcr,
      Comparing,
      Complete,
      Error
  }

  public class BulkProcessingResult
  {
      public string DocumentId { get; set; } = string.Empty;
      public Expediente? XmlExpediente { get; set; }
      public Expediente? OcrExpediente { get; set; }
      public ComparisonResult? Comparison { get; set; }
      public bool Success { get; set; }
      public string? ErrorMessage { get; set; }
  }

  Step 4.2: Write RED Tests

  File: Tests.Infrastructure.BrowserAutomation.E2E/BulkProcessingE2ETests.cs

  public class BulkProcessingE2ETests : IAsyncLifetime
  {
      // These tests will FAIL - no implementation
      [Fact] public async Task GetRandomSample_RequestingFourDocuments_ReturnsFourDocuments()
      [Fact] public async Task GetRandomSample_MaxFourDocuments_EnforcesLimit()
      [Fact] public async Task ProcessDocument_ValidDocument_ReturnsCompleteResult()
      [Fact] public async Task ProcessDocument_Sequential_ProcessesOneAtATime()

      [Fact(Skip = "Implementation pending - stakeholder demo only")]
      public async Task ProcessBatch_FourDocuments_CompletesSuccessfully()
  }

  Expected: All tests FAIL with NotImplementedException

  Step 4.3: UI Stub (Compiles but doesn't work)

  File: DocumentProcessing.razor

  <!-- Bulk Processing Section (STUB - Not Functional) -->
  <MudExpansionPanels Class="mb-4">
      <MudExpansionPanel Text="Bulk Processing (Preview - Coming Soon)"
  Icon="@Icons.Material.Filled.BatchPrediction" Disabled="true">
          <MudAlert Severity="Severity.Info">
              Bulk processing feature is under development. This preview shows the planned interface.
          </MudAlert>

          <MudButton Variant="Variant.Outlined" Color="Color.Primary" Disabled="true">
              <MudIcon Icon="@Icons.Material.Filled.Refresh" Class="mr-2" />
              Load Random Sample (Max 4)
          </MudButton>

          <MudTable Items="@bulkDocuments" Class="mt-4" Dense="true" Disabled="true">
              <HeaderContent>
                  <MudTh>Document ID</MudTh>
                  <MudTh>XML Status</MudTh>
                  <MudTh>OCR Status</MudTh>
                  <MudTh>Comparison</MudTh>
              </HeaderContent>
              <RowTemplate>
                  <MudTd>@context.Id</MudTd>
                  <MudTd><MudChip Size="Size.Small">@context.Status</MudChip></MudTd>
                  <MudTd>-</MudTd>
                  <MudTd>-</MudTd>
              </RowTemplate>
          </MudTable>
      </MudExpansionPanel>
  </MudExpansionPanels>

  @code {
      private List<BulkDocument> bulkDocuments = new(); // Empty - no implementation
  }

  ---
  PHASE 5: Integration & Polish ✨ (2 hours)

  Tasks:
  1. Dependency Injection:
    - Register IDocumentComparisonService in DI container
    - Register IBulkProcessingService (stub)
  2. Error Handling:
    - Graceful OCR failures (fallback messages)
    - Comparison null checks
  3. UI Polish:
    - Loading spinners during OCR processing
    - Clear results button
    - Responsive layout adjustments
  4. Performance:
    - Verify OCR < 10s per PDF (Tesseract only, no GOT-OCR fallback for presentation)
    - Test with all 4 PRP1 PDFs
  5. Documentation:
    - Update README with new features
    - Add stakeholder demo script

  ---
  Test Execution Plan

  Run Tests After Each Phase:

  # Phase 2: OCR Tests
  dotnet test --filter "FullyQualifiedName~OcrExtractionE2ETests"

  # Phase 3: Comparison Tests
  dotnet test --filter "FullyQualifiedName~DocumentComparisonServiceTests"
  dotnet test --filter "FullyQualifiedName~ComparisonE2ETests"

  # Phase 4: Bulk (expect failures)
  dotnet test --filter "FullyQualifiedName~BulkProcessingE2ETests"

  # All E2E Tests
  dotnet test --filter "FullyQualifiedName~E2E"

  ---
  Deliverables Checklist

  Phase 1-3 (Fully Functional):

  - ✅ Renamed to "Document Processing" (no "Demo")
  - ✅ 4 XML processing buttons (existing)
  - ✅ 4 OCR processing buttons (new, twin PDFs)
  - ✅ Comparison button with side-by-side results
  - ✅ Exact match → Fuzzy fallback comparison
  - ✅ All E2E tests GREEN (except bulk)
  - ✅ 75% OCR confidence threshold
  - ✅ Sequential processing only

  Phase 4 (Stub Only):

  - ✅ IBulkProcessingService interface
  - ✅ Bulk models (BulkDocument, BulkProcessingResult)
  - ✅ RED tests (failing, no implementation)
  - ✅ UI stub (disabled, "Coming Soon" message)
  - ✅ Max 4 documents enforced in interface

  ---
  Risk Mitigation

  1. OCR Accuracy: If < 75% confidence on PRP1 PDFs, adjust threshold or improve preprocessing
  2. Fuzzy Matcher: If not found, implement simple Levenshtein distance
  3. Performance: If OCR > 10s, optimize preprocessing or skip multi-page PDFs for demo
  4. Time: If running behind, skip bulk stub (Phase 4) entirely

  ---
  Ready to Proceed?

  This plan follows TDD strictly:
  - RED: Write failing tests
  - GREEN: Implement minimum code to pass
  - REFACTOR: Clean up after tests pass

  Phase 4 stays in RED (stakeholder preview only).

  Shall I proceed with Phase 1: Renaming & UI Alignment?

  Todos
  ☒ Gather intelligence and understand current implementation
  ☒ Break down user requirements with detailed analysis
  ☐ Create comprehensive implementation plan with updated constraints
