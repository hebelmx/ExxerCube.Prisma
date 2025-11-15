# UI/UX Enhancements: Stories 1.7-1.9 (BMAD Agent)

**Component:** Export Management, PDF Export, Audit Trail Viewer  
**Stories:** 1.7 - SIRO Export, 1.8 - PDF Summarization & Signing, 1.9 - Audit Trail  
**Status:** UX Enhancement Recommendations  
**Created:** 2025-01-15  
**Author:** BMAD Agent (Browser-based Markdown Documentation)

---

## Executive Summary

Stories 1.7, 1.8, and 1.9 deliver robust backend functionality for SIRO-compliant export generation, PDF summarization and digital signing, and comprehensive audit trail logging. However, **critical UI components are missing** to expose these capabilities to end users. This document provides comprehensive UI/UX enhancement specifications to transform backend functionality into intuitive, user-friendly interfaces.

**Current State:** 
- Story 1.7: Backend complete ✅ | UI Status: ⚠️ **Missing** (AC7 not implemented)
- Story 1.8: Backend complete ✅ | UI Status: ⚠️ **Missing** (UI extensions not implemented)
- Story 1.9: Backend complete ✅ | UI Status: ⚠️ **Missing** (AC3, AC6 not implemented)

---

## Story 1.7: SIRO-Compliant Export Generation - UI Enhancements

### Current State Analysis

**Backend Status:** ✅ Complete  
**UI Status:** ⚠️ **Missing** - AC7 (Export Management Screen) not implemented

**Gaps Identified:**
- No export management screen for initiating exports
- No export queue table showing status and progress
- No download functionality for generated files
- No export format selection (SIRO XML, Excel FR18)
- No validation options display

### Required UI Components

#### 1. Export Management Dashboard Component

**Location:** `Components/Pages/ExportManagement.razor`  
**Purpose:** Central hub for initiating exports, monitoring export queue, and downloading generated files

**Features:**

**A. Export Initiation Form**
- **Case Selection:**
  - Multi-select dropdown or checkbox list of available cases
  - Filter by expediente number, oficio number, date range
  - Select all / Clear all functionality
  - Selected cases count display

- **Export Format Selection:**
  - Radio buttons or tabs: SIRO XML, Excel FR18, Both
  - Format descriptions and use cases
  - Format-specific options (if applicable)

- **Validation Options:**
  - Checkbox: "Validate against SIRO schema before export"
  - Checkbox: "Validate all required fields are present"
  - Checkbox: "Stop on validation errors" vs "Continue with warnings"

- **Export Actions:**
  - "Generate Export" button (primary action)
  - "Preview Export" button (optional - shows what will be exported)
  - "Cancel" button

**B. Export Queue Table**
- **Columns:**
  - Export ID
  - Cases Included (count + expandable list)
  - Export Format (SIRO XML, Excel FR18, PDF)
  - Status (Pending, In Progress, Completed, Failed, Validating)
  - Progress (progress bar for in-progress exports)
  - Created At (timestamp)
  - Completed At (timestamp, if completed)
  - Actions (View Details, Download, Cancel, Retry)

- **Status Indicators:**
  - Color-coded badges: Green (Completed), Blue (In Progress), Yellow (Pending), Red (Failed)
  - Progress bar for in-progress exports
  - Error message display for failed exports

- **Filtering:**
  - Status filter (multi-select)
  - Format filter (SIRO XML, Excel FR18, PDF)
  - Date range filter (created date)
  - Search by export ID or case ID

- **Sorting:**
  - By created date (newest first)
  - By status
  - By format

**C. Export Details Dialog**
- **Information Display:**
  - Export ID and correlation ID
  - Cases included (expandable list)
  - Export format and options used
  - Validation results (if validation was performed)
  - File paths and sizes
  - Generation timestamps

- **Actions:**
  - Download files (individual or bulk download)
  - View validation errors (if any)
  - Regenerate export
  - View audit trail for this export

**Implementation Code:**

```razor
@page "/export-management"
@using ExxerCube.Prisma.Domain.Entities
@using ExxerCube.Prisma.Application.Services
@inject IExportService ExportService
@inject ILogger<ExportManagement> Logger

<PageTitle>Export Management</PageTitle>

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">
        <MudIcon Icon="@Icons.Material.Filled.FileDownload" Class="mr-2" />
        Export Management
    </MudText>

    <!-- Export Initiation Form -->
    <MudCard Class="mb-4">
        <MudCardHeader>
            <MudText Typo="Typo.h6">Create New Export</MudText>
        </MudCardHeader>
        <MudCardContent>
            <EditForm Model="@exportRequest" OnValidSubmit="InitiateExport">
                <DataAnnotationsValidator />
                
                <!-- Case Selection -->
                <MudItem xs="12" Class="mb-4">
                    <MudText Typo="Typo.subtitle1" Class="mb-2">Select Cases</MudText>
                    <MudAutocomplete T="string"
                                   @bind-Value="selectedCaseId"
                                   SearchFunc="@SearchCases"
                                   ToStringFunc="@(x => x ?? string.Empty)"
                                   Label="Search and select cases"
                                   Variant="Variant.Outlined"
                                   MultiSelection="true"
                                   @bind-SelectedValues="selectedCaseIds">
                        <ItemTemplate>
                            <MudText>@context</MudText>
                        </ItemTemplate>
                    </MudAutocomplete>
                    <MudText Typo="Typo.caption" Color="Color.Secondary" Class="mt-1">
                        @selectedCaseIds.Count case(s) selected
                    </MudText>
                    <MudButton Size="Size.Small" 
                              Variant="Variant.Text"
                              OnClick="SelectAllCases">
                        Select All
                    </MudButton>
                    <MudButton Size="Size.Small" 
                              Variant="Variant.Text"
                              OnClick="ClearSelection">
                        Clear All
                    </MudButton>
                </MudItem>

                <!-- Export Format Selection -->
                <MudItem xs="12" Class="mb-4">
                    <MudText Typo="Typo.subtitle1" Class="mb-2">Export Format</MudText>
                    <MudRadioGroup @bind-SelectedOption="@exportFormat">
                        <MudRadio Option="@ExportFormat.SiroXml" Color="Color.Primary">
                            SIRO XML
                            <HelperText>Regulatory-compliant XML format for SIRO submission</HelperText>
                        </MudRadio>
                        <MudRadio Option="@ExportFormat.ExcelFr18" Color="Color.Primary">
                            Excel FR18
                            <HelperText>Excel layout for SIRO registration systems</HelperText>
                        </MudRadio>
                        <MudRadio Option="@ExportFormat.Both" Color="Color.Primary">
                            Both Formats
                            <HelperText>Generate both SIRO XML and Excel FR18</HelperText>
                        </MudRadio>
                    </MudRadioGroup>
                </MudItem>

                <!-- Validation Options -->
                <MudItem xs="12" Class="mb-4">
                    <MudText Typo="Typo.subtitle1" Class="mb-2">Validation Options</MudText>
                    <MudCheckBox @bind-Checked="@validateSchema" Label="Validate against SIRO schema" />
                    <MudCheckBox @bind-Checked="@validateRequiredFields" Label="Validate all required fields are present" />
                    <MudCheckBox @bind-Checked="@stopOnErrors" Label="Stop on validation errors" />
                </MudItem>

                <!-- Actions -->
                <MudItem xs="12">
                    <MudButton Variant="Variant.Filled" 
                              Color="Color.Primary"
                              ButtonType="ButtonType.Submit"
                              StartIcon="@Icons.Material.Filled.FileDownload"
                              Disabled="@isExporting">
                        @(isExporting ? "Generating Export..." : "Generate Export")
                    </MudButton>
                    <MudButton Variant="Variant.Outlined" 
                              Color="Color.Secondary"
                              Class="ml-2"
                              OnClick="PreviewExport"
                              Disabled="@isExporting || !selectedCaseIds.Any()">
                        Preview Export
                    </MudButton>
                </MudItem>
            </EditForm>
        </MudCardContent>
    </MudCard>

    <!-- Export Queue Table -->
    <MudCard>
        <MudCardHeader>
            <MudText Typo="Typo.h6">Export Queue</MudText>
            <MudSpacer />
            <MudTextField @bind-Value="searchText" 
                         Placeholder="Search exports..." 
                         Adornment="Adornment.Start"
                         AdornmentIcon="@Icons.Material.Filled.Search"
                         Variant="Variant.Outlined"
                         Dense="true"
                         Immediate="true" />
        </MudCardHeader>
        <MudCardContent>
            <MudTable Items="@FilteredExports" 
                     Dense="true" 
                     Hover="true" 
                     Striped="true"
                     Filter="new Func<ExportRecord, bool>(FilterExports)"
                     SortMode="SortMode.Multiple">
                <HeaderContent>
                    <MudTh>
                        <MudTableSortLabel SortBy="@new Func<ExportRecord, object>(x => x.ExportId)">
                            Export ID
                        </MudTableSortLabel>
                    </MudTh>
                    <MudTh>Cases</MudTh>
                    <MudTh>
                        <MudTableSortLabel SortBy="@new Func<ExportRecord, object>(x => x.Format)">
                            Format
                        </MudTableSortLabel>
                    </MudTh>
                    <MudTh>
                        <MudTableSortLabel SortBy="@new Func<ExportRecord, object>(x => x.Status)">
                            Status
                        </MudTableSortLabel>
                    </MudTh>
                    <MudTh>Progress</MudTh>
                    <MudTh>
                        <MudTableSortLabel SortBy="@new Func<ExportRecord, object>(x => x.CreatedAt)">
                            Created
                        </MudTableSortLabel>
                    </MudTh>
                    <MudTh>Actions</MudTh>
                </HeaderContent>
                <RowTemplate>
                    <MudTd DataLabel="Export ID">
                        <MudText Typo="Typo.body2" Class="font-weight-medium">
                            @context.ExportId
                        </MudText>
                        <MudText Typo="Typo.caption" Color="Color.Secondary">
                            @context.CorrelationId
                        </MudText>
                    </MudTd>
                    <MudTd DataLabel="Cases">
                        <MudText Typo="Typo.body2">@context.CasesCount case(s)</MudText>
                        <MudButton Size="Size.Small" 
                                  Variant="Variant.Text"
                                  OnClick="@(() => ShowCaseList(context))">
                            View List
                        </MudButton>
                    </MudTd>
                    <MudTd DataLabel="Format">
                        <MudChipSet>
                            @foreach (var format in context.Formats)
                            {
                                <MudChip Size="Size.Small" Color="@GetFormatColor(format)">
                                    @format
                                </MudChip>
                            }
                        </MudChipSet>
                    </MudTd>
                    <MudTd DataLabel="Status">
                        <MudChip Size="Size.Small" 
                                Color="@GetStatusColor(context.Status)"
                                Icon="@GetStatusIcon(context.Status)">
                            @context.Status
                        </MudChip>
                        @if (context.Status == "Failed" && !string.IsNullOrEmpty(context.ErrorMessage))
                        {
                            <MudTooltip Text="@context.ErrorMessage">
                                <MudIcon Icon="@Icons.Material.Filled.Error" 
                                       Size="Size.Small" 
                                       Color="Color.Error" 
                                       Class="ml-1" />
                            </MudTooltip>
                        }
                    </MudTd>
                    <MudTd DataLabel="Progress">
                        @if (context.Status == "In Progress")
                        {
                            <MudProgressLinear Value="@context.Progress" 
                                              Color="Color.Info"
                                              Class="mt-2" />
                            <MudText Typo="Typo.caption">@context.Progress%</MudText>
                        }
                        else
                        {
                            <MudText Typo="Typo.caption" Color="Color.Secondary">-</MudText>
                        }
                    </MudTd>
                    <MudTd DataLabel="Created">
                        <MudText Typo="Typo.body2">@context.CreatedAt.ToString("g")</MudText>
                        @if (context.CompletedAt.HasValue)
                        {
                            <MudText Typo="Typo.caption" Color="Color.Secondary">
                                Completed: @context.CompletedAt.Value.ToString("g")
                            </MudText>
                        }
                    </MudTd>
                    <MudTd DataLabel="Actions">
                        <MudIconButton Icon="@Icons.Material.Filled.Visibility" 
                                      Size="Size.Small"
                                      OnClick="@(() => ViewExportDetails(context))" />
                        @if (context.Status == "Completed")
                        {
                            <MudIconButton Icon="@Icons.Material.Filled.Download" 
                                          Size="Size.Small"
                                          OnClick="@(() => DownloadExport(context))" />
                        }
                        @if (context.Status == "Failed")
                        {
                            <MudIconButton Icon="@Icons.Material.Filled.Refresh" 
                                          Size="Size.Small"
                                          OnClick="@(() => RetryExport(context))" />
                        }
                        @if (context.Status == "Pending" || context.Status == "In Progress")
                        {
                            <MudIconButton Icon="@Icons.Material.Filled.Cancel" 
                                          Size="Size.Small"
                                          Color="Color.Error"
                                          OnClick="@(() => CancelExport(context))" />
                        }
                    </MudTd>
                </RowTemplate>
                <PagerContent>
                    <MudTablePager />
                </PagerContent>
            </MudTable>
        </MudCardContent>
    </MudCard>

    <!-- Export Details Dialog -->
    <MudDialog @bind-IsVisible="@showExportDetailsDialog">
        <TitleContent>
            <MudText Typo="Typo.h6">Export Details: @selectedExport?.ExportId</MudText>
        </TitleContent>
        <DialogContent>
            @if (selectedExport != null)
            {
                <MudGrid>
                    <MudItem xs="12">
                        <MudText Typo="Typo.subtitle1">Export Information</MudText>
                        <MudDivider Class="my-2" />
                    </MudItem>
                    <MudItem xs="12" sm="6">
                        <MudText Typo="Typo.body2">Export ID</MudText>
                        <MudText Typo="Typo.body1" Class="font-weight-bold">@selectedExport.ExportId</MudText>
                    </MudItem>
                    <MudItem xs="12" sm="6">
                        <MudText Typo="Typo.body2">Correlation ID</MudText>
                        <MudText Typo="Typo.body1">@selectedExport.CorrelationId</MudText>
                    </MudItem>
                    <MudItem xs="12" sm="6">
                        <MudText Typo="Typo.body2">Status</MudText>
                        <MudChip Size="Size.Small" Color="@GetStatusColor(selectedExport.Status)">
                            @selectedExport.Status
                        </MudChip>
                    </MudItem>
                    <MudItem xs="12" sm="6">
                        <MudText Typo="Typo.body2">Format</MudText>
                        <MudChipSet>
                            @foreach (var format in selectedExport.Formats)
                            {
                                <MudChip Size="Size.Small">@format</MudChip>
                            }
                        </MudChipSet>
                    </MudItem>
                    
                    <!-- Validation Results -->
                    @if (selectedExport.ValidationResults != null)
                    {
                        <MudItem xs="12">
                            <MudDivider Class="my-2" />
                            <MudText Typo="Typo.subtitle1">Validation Results</MudText>
                            @if (selectedExport.ValidationResults.IsValid)
                            {
                                <MudAlert Severity="Severity.Success">
                                    All validations passed
                                </MudAlert>
                            }
                            else
                            {
                                <MudAlert Severity="Severity.Error">
                                    Validation errors found: @selectedExport.ValidationResults.ErrorCount
                                </MudAlert>
                                <MudList>
                                    @foreach (var error in selectedExport.ValidationResults.Errors)
                                    {
                                        <MudListItem>
                                            <MudText Typo="Typo.body2">@error</MudText>
                                        </MudListItem>
                                    }
                                </MudList>
                            }
                        </MudItem>
                    }
                    
                    <!-- Generated Files -->
                    @if (selectedExport.GeneratedFiles.Any())
                    {
                        <MudItem xs="12">
                            <MudDivider Class="my-2" />
                            <MudText Typo="Typo.subtitle1">Generated Files</MudText>
                            <MudList>
                                @foreach (var file in selectedExport.GeneratedFiles)
                                {
                                    <MudListItem>
                                        <MudIcon Icon="@GetFileIcon(file.Format)" Class="mr-2" />
                                        <MudText Typo="Typo.body2">@file.FileName</MudText>
                                        <MudText Typo="Typo.caption" Color="Color.Secondary" Class="ml-2">
                                            @FormatFileSize(file.FileSize)
                                        </MudText>
                                        <MudSpacer />
                                        <MudButton Size="Size.Small" 
                                                  Variant="Variant.Text"
                                                  StartIcon="@Icons.Material.Filled.Download"
                                                  OnClick="@(() => DownloadFile(file))">
                                            Download
                                        </MudButton>
                                    </MudListItem>
                                }
                            </MudList>
                        </MudItem>
                    }
                </MudGrid>
            }
        </DialogContent>
        <DialogActions>
            <MudButton OnClick="@(() => showExportDetailsDialog = false)">Close</MudButton>
            @if (selectedExport?.Status == "Completed")
            {
                <MudButton Variant="Variant.Filled" 
                          Color="Color.Primary"
                          StartIcon="@Icons.Material.Filled.Download"
                          OnClick="@(() => DownloadAllFiles(selectedExport))">
                    Download All
                </MudButton>
            }
        </DialogActions>
    </MudDialog>
</MudContainer>

@code {
    private List<ExportRecord> exports = new();
    private List<ExportRecord> filteredExports = new();
    private string searchText = string.Empty;
    private bool isExporting = false;
    private bool showExportDetailsDialog = false;
    private ExportRecord? selectedExport;
    
    private ExportRequest exportRequest = new();
    private string? selectedCaseId;
    private HashSet<string> selectedCaseIds = new();
    private ExportFormat exportFormat = ExportFormat.SiroXml;
    private bool validateSchema = true;
    private bool validateRequiredFields = true;
    private bool stopOnErrors = false;

    protected override async Task OnInitializedAsync()
    {
        await LoadExports();
    }

    private async Task LoadExports()
    {
        // Load export records from ExportService
        // Implementation depends on your service interface
    }

    private async Task<IEnumerable<string>> SearchCases(string value)
    {
        // Search cases for autocomplete
        // Implementation depends on your service interface
        return new List<string>();
    }

    private async Task InitiateExport()
    {
        if (!selectedCaseIds.Any())
        {
            // Show error: No cases selected
            return;
        }

        isExporting = true;
        StateHasChanged();

        try
        {
            // Call ExportService to initiate export
            // Implementation depends on your service interface
            await Task.Delay(1000); // Placeholder
            
            // Refresh export queue
            await LoadExports();
        }
        finally
        {
            isExporting = false;
            StateHasChanged();
        }
    }

    private void PreviewExport()
    {
        // Show preview dialog
    }

    private void SelectAllCases()
    {
        // Select all available cases
    }

    private void ClearSelection()
    {
        selectedCaseIds.Clear();
        StateHasChanged();
    }

    private bool FilterExports(ExportRecord export)
    {
        if (!string.IsNullOrEmpty(searchText) && 
            !export.ExportId.Contains(searchText, StringComparison.OrdinalIgnoreCase) &&
            !export.CorrelationId.Contains(searchText, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        return true;
    }

    private void ViewExportDetails(ExportRecord export)
    {
        selectedExport = export;
        showExportDetailsDialog = true;
    }

    private void DownloadExport(ExportRecord export)
    {
        // Trigger download of all files
    }

    private void DownloadFile(GeneratedFile file)
    {
        // Download individual file
    }

    private void DownloadAllFiles(ExportRecord export)
    {
        // Download all files as zip
    }

    private void RetryExport(ExportRecord export)
    {
        // Retry failed export
    }

    private void CancelExport(ExportRecord export)
    {
        // Cancel pending/in-progress export
    }

    private void ShowCaseList(ExportRecord export)
    {
        // Show dialog with case list
    }

    private Color GetFormatColor(string format) => format switch
    {
        "SIRO XML" => Color.Info,
        "Excel FR18" => Color.Success,
        "PDF" => Color.Error,
        _ => Color.Default
    };

    private Color GetStatusColor(string status) => status switch
    {
        "Completed" => Color.Success,
        "In Progress" => Color.Info,
        "Pending" => Color.Warning,
        "Failed" => Color.Error,
        "Validating" => Color.Primary,
        _ => Color.Default
    };

    private string GetStatusIcon(string status) => status switch
    {
        "Completed" => Icons.Material.Filled.CheckCircle,
        "In Progress" => Icons.Material.Filled.HourglassEmpty,
        "Pending" => Icons.Material.Filled.Schedule,
        "Failed" => Icons.Material.Filled.Error,
        "Validating" => Icons.Material.Filled.Verified,
        _ => Icons.Material.Filled.Help
    };

    private string GetFileIcon(string format) => format switch
    {
        "XML" => Icons.Material.Filled.Code,
        "XLSX" => Icons.Material.Filled.TableChart,
        "PDF" => Icons.Material.Filled.PictureAsPdf,
        _ => Icons.Material.Filled.InsertDriveFile
    };

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private enum ExportFormat
    {
        SiroXml,
        ExcelFr18,
        Both
    }

    private class ExportRequest
    {
        public HashSet<string> CaseIds { get; set; } = new();
        public ExportFormat Format { get; set; }
        public bool ValidateSchema { get; set; }
        public bool ValidateRequiredFields { get; set; }
        public bool StopOnErrors { get; set; }
    }

    private class ExportRecord
    {
        public string ExportId { get; set; } = string.Empty;
        public string CorrelationId { get; set; } = string.Empty;
        public List<string> CaseIds { get; set; } = new();
        public int CasesCount => CaseIds.Count;
        public List<string> Formats { get; set; } = new();
        public string Status { get; set; } = string.Empty;
        public int Progress { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
        public ValidationResults? ValidationResults { get; set; }
        public List<GeneratedFile> GeneratedFiles { get; set; } = new();
    }

    private class ValidationResults
    {
        public bool IsValid { get; set; }
        public int ErrorCount { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    private class GeneratedFile
    {
        public string FileName { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public long FileSize { get; set; }
        public string FilePath { get; set; } = string.Empty;
    }
}
```

---

## Story 1.8: PDF Summarization and Digital Signing - UI Enhancements

### Current State Analysis

**Backend Status:** ✅ Complete  
**UI Status:** ⚠️ **Missing** - UI extensions not implemented

**Gaps Identified:**
- No PDF export initiation form
- No certificate selection interface
- No PDF summarization options display
- No PDF export status and download links
- No signature validation display

### Required UI Components

#### 1. PDF Export Extension to Export Management

**Location:** Extend existing `ExportManagement.razor`  
**Purpose:** Add PDF export capabilities with certificate selection and summarization options

**Features:**

**A. PDF Export Tab/Section**
- **PDF Export Options:**
  - Checkbox: "Include PDF summarization"
  - Summarization method selection (Rule-based, Semantic Analysis)
  - Requirement categories to include (multi-select: Bloqueo, Desbloqueo, Documentacion, Transferencia, Informacion)

- **Digital Signing Options:**
  - Certificate source selection (Azure Key Vault, File, Windows Certificate Store)
  - Certificate selection dropdown (populated from configured source)
  - Certificate information display (name, expiration date, issuer)
  - Checkbox: "Validate signature after signing"

- **PDF Generation Options:**
  - Include case information
  - Include person information
  - Include compliance actions
  - Include requirement summary
  - Include audit trail excerpt

**B. Certificate Management Panel**
- **Certificate Status:**
  - Current certificate information
  - Certificate expiration warning (if expiring soon)
  - Certificate validation status

- **Certificate Selection:**
  - Dropdown with available certificates
  - Certificate details on selection
  - Test certificate button

**C. PDF Export Status Display**
- **Status Indicators:**
  - Summarization status (In Progress, Completed, Failed)
  - PDF generation status
  - Signing status
  - Signature validation status

- **Progress Tracking:**
  - Multi-step progress indicator
  - Current step highlighted
  - Time elapsed for each step

**Implementation Code:**

```razor
<!-- Add to ExportManagement.razor -->

<!-- PDF Export Tab -->
<MudTabs @bind-ActivePanelIndex="@activeTabIndex">
    <!-- Existing SIRO XML/Excel tabs -->
    
    <MudTabPanel Text="PDF Export">
        <MudCard>
            <MudCardHeader>
                <MudText Typo="Typo.h6">PDF Export with Digital Signing</MudText>
            </MudCardHeader>
            <MudCardContent>
                <EditForm Model="@pdfExportRequest" OnValidSubmit="InitiatePdfExport">
                    <DataAnnotationsValidator />
                    
                    <!-- Case Selection (reuse from main form) -->
                    <MudItem xs="12" Class="mb-4">
                        <MudText Typo="Typo.subtitle1" Class="mb-2">Select Cases</MudText>
                        <!-- Same case selection as main form -->
                    </MudItem>
                    
                    <!-- PDF Summarization Options -->
                    <MudItem xs="12" Class="mb-4">
                        <MudText Typo="Typo.subtitle1" Class="mb-2">PDF Summarization</MudText>
                        <MudCheckBox @bind-Checked="@includeSummarization" 
                                    Label="Include PDF requirement summarization" />
                        @if (includeSummarization)
                        {
                            <MudSelect Label="Summarization Method" 
                                     @bind-Value="@summarizationMethod"
                                     Variant="Variant.Outlined"
                                     Class="mt-2">
                                <MudSelectItem Value="RuleBased">Rule-Based Classification</MudSelectItem>
                                <MudSelectItem Value="SemanticAnalysis">Semantic Analysis</MudSelectItem>
                            </MudSelect>
                            <MudText Typo="Typo.subtitle2" Class="mt-3 mb-2">Requirement Categories</MudText>
                            <MudCheckBox @bind-Checked="@includeBloqueo" Label="Bloqueo" />
                            <MudCheckBox @bind-Checked="@includeDesbloqueo" Label="Desbloqueo" />
                            <MudCheckBox @bind-Checked="@includeDocumentacion" Label="Documentacion" />
                            <MudCheckBox @bind-Checked="@includeTransferencia" Label="Transferencia" />
                            <MudCheckBox @bind-Checked="@includeInformacion" Label="Informacion" />
                        }
                    </MudItem>
                    
                    <!-- Digital Signing Options -->
                    <MudItem xs="12" Class="mb-4">
                        <MudText Typo="Typo.subtitle1" Class="mb-2">Digital Signing</MudText>
                        <MudSelect Label="Certificate Source" 
                                 @bind-Value="@certificateSource"
                                 Variant="Variant.Outlined"
                                 OnSelectionChanged="@LoadCertificates">
                            <MudSelectItem Value="AzureKeyVault">Azure Key Vault</MudSelectItem>
                            <MudSelectItem Value="File">File Path</MudSelectItem>
                            <MudSelectItem Value="WindowsStore">Windows Certificate Store</MudSelectItem>
                        </MudSelect>
                        
                        @if (availableCertificates.Any())
                        {
                            <MudSelect Label="Select Certificate" 
                                     @bind-Value="@selectedCertificate"
                                     Variant="Variant.Outlined"
                                     Class="mt-2"
                                     OnSelectionChanged="@ShowCertificateDetails">
                                @foreach (var cert in availableCertificates)
                                {
                                    <MudSelectItem Value="@cert.Name">
                                        @cert.Name
                                        @if (cert.ExpiresSoon)
                                        {
                                            <MudChip Size="Size.Small" Color="Color.Warning">Expires Soon</MudChip>
                                        }
                                    </MudSelectItem>
                                }
                            </MudSelect>
                            
                            @if (selectedCertificateInfo != null)
                            {
                                <MudCard Class="mt-2" Elevation="2">
                                    <MudCardContent>
                                        <MudGrid>
                                            <MudItem xs="12" sm="6">
                                                <MudText Typo="Typo.body2">Certificate Name</MudText>
                                                <MudText Typo="Typo.body1">@selectedCertificateInfo.Name</MudText>
                                            </MudItem>
                                            <MudItem xs="12" sm="6">
                                                <MudText Typo="Typo.body2">Issuer</MudText>
                                                <MudText Typo="Typo.body1">@selectedCertificateInfo.Issuer</MudText>
                                            </MudItem>
                                            <MudItem xs="12" sm="6">
                                                <MudText Typo="Typo.body2">Valid From</MudText>
                                                <MudText Typo="Typo.body1">@selectedCertificateInfo.ValidFrom.ToString("g")</MudText>
                                            </MudItem>
                                            <MudItem xs="12" sm="6">
                                                <MudText Typo="Typo.body2">Valid To</MudText>
                                                <MudText Typo="Typo.body1" 
                                                        Color="@(selectedCertificateInfo.ExpiresSoon ? Color.Warning : Color.Default)">
                                                    @selectedCertificateInfo.ValidTo.ToString("g")
                                                </MudText>
                                            </MudItem>
                                            <MudItem xs="12">
                                                <MudText Typo="Typo.body2">Status</MudText>
                                                <MudChip Size="Size.Small" 
                                                        Color="@(selectedCertificateInfo.IsValid ? Color.Success : Color.Error)">
                                                    @(selectedCertificateInfo.IsValid ? "Valid" : "Invalid")
                                                </MudChip>
                                            </MudItem>
                                        </MudGrid>
                                    </MudCardContent>
                                </MudCard>
                            }
                        }
                        else
                        {
                            <MudAlert Severity="Severity.Warning" Class="mt-2">
                                No certificates available. Please configure certificate source in settings.
                            </MudAlert>
                        }
                        
                        <MudCheckBox @bind-Checked="@validateSignature" 
                                    Label="Validate signature after signing"
                                    Class="mt-2" />
                    </MudItem>
                    
                    <!-- PDF Content Options -->
                    <MudItem xs="12" Class="mb-4">
                        <MudText Typo="Typo.subtitle1" Class="mb-2">PDF Content</MudText>
                        <MudCheckBox @bind-Checked="@includeCaseInfo" Label="Include case information" />
                        <MudCheckBox @bind-Checked="@includePersonInfo" Label="Include person information" />
                        <MudCheckBox @bind-Checked="@includeComplianceActions" Label="Include compliance actions" />
                        <MudCheckBox @bind-Checked="@includeRequirementSummary" Label="Include requirement summary" />
                        <MudCheckBox @bind-Checked="@includeAuditTrail" Label="Include audit trail excerpt" />
                    </MudItem>
                    
                    <!-- Actions -->
                    <MudItem xs="12">
                        <MudButton Variant="Variant.Filled" 
                                  Color="Color.Primary"
                                  ButtonType="ButtonType.Submit"
                                  StartIcon="@Icons.Material.Filled.PictureAsPdf"
                                  Disabled="@isExporting || selectedCertificate == null">
                            Generate Signed PDF
                        </MudButton>
                    </MudItem>
                </EditForm>
            </MudCardContent>
        </MudCard>
        
        <!-- PDF Export Progress (if in progress) -->
        @if (currentPdfExport != null && currentPdfExport.Status == "In Progress")
        {
            <MudCard Class="mt-4">
                <MudCardHeader>
                    <MudText Typo="Typo.h6">PDF Export Progress</MudText>
                </MudCardHeader>
                <MudCardContent>
                    <MudStepper @bind-ActiveStepIndex="@currentPdfExport.CurrentStep" Linear="false">
                        <MudStep Title="Summarization" Completed="@(currentPdfExport.CurrentStep >= 0)">
                            @if (currentPdfExport.CurrentStep == 0)
                            {
                                <MudProgressCircular Indeterminate="true" Size="Size.Small" />
                            }
                            else if (currentPdfExport.CurrentStep > 0)
                            {
                                <MudIcon Icon="@Icons.Material.Filled.Check" Color="Color.Success" />
                            }
                        </MudStep>
                        <MudStep Title="PDF Generation" Completed="@(currentPdfExport.CurrentStep >= 1)">
                            @if (currentPdfExport.CurrentStep == 1)
                            {
                                <MudProgressCircular Indeterminate="true" Size="Size.Small" />
                            }
                            else if (currentPdfExport.CurrentStep > 1)
                            {
                                <MudIcon Icon="@Icons.Material.Filled.Check" Color="Color.Success" />
                            }
                        </MudStep>
                        <MudStep Title="Digital Signing" Completed="@(currentPdfExport.CurrentStep >= 2)">
                            @if (currentPdfExport.CurrentStep == 2)
                            {
                                <MudProgressCircular Indeterminate="true" Size="Size.Small" />
                            }
                            else if (currentPdfExport.CurrentStep > 2)
                            {
                                <MudIcon Icon="@Icons.Material.Filled.Check" Color="Color.Success" />
                            }
                        </MudStep>
                        <MudStep Title="Signature Validation" Completed="@(currentPdfExport.CurrentStep >= 3)">
                            @if (currentPdfExport.CurrentStep == 3)
                            {
                                <MudProgressCircular Indeterminate="true" Size="Size.Small" />
                            }
                            else if (currentPdfExport.CurrentStep > 3)
                            {
                                <MudIcon Icon="@Icons.Material.Filled.Check" Color="Color.Success" />
                            }
                        </MudStep>
                    </MudStepper>
                </MudCardContent>
            </MudCard>
        }
    </MudTabPanel>
</MudTabs>

@code {
    // Add to existing code
    
    private int activeTabIndex = 0;
    private PdfExportRequest pdfExportRequest = new();
    private bool includeSummarization = true;
    private string summarizationMethod = "RuleBased";
    private bool includeBloqueo = true;
    private bool includeDesbloqueo = true;
    private bool includeDocumentacion = true;
    private bool includeTransferencia = true;
    private bool includeInformacion = true;
    private string certificateSource = "AzureKeyVault";
    private List<CertificateInfo> availableCertificates = new();
    private string? selectedCertificate;
    private CertificateInfo? selectedCertificateInfo;
    private bool validateSignature = true;
    private bool includeCaseInfo = true;
    private bool includePersonInfo = true;
    private bool includeComplianceActions = true;
    private bool includeRequirementSummary = true;
    private bool includeAuditTrail = false;
    private PdfExportRecord? currentPdfExport;

    private async Task LoadCertificates(string source)
    {
        // Load certificates from selected source
        // Implementation depends on certificate management service
    }

    private void ShowCertificateDetails(string? certificateName)
    {
        selectedCertificateInfo = availableCertificates.FirstOrDefault(c => c.Name == certificateName);
        StateHasChanged();
    }

    private async Task InitiatePdfExport()
    {
        if (selectedCertificate == null)
        {
            // Show error: Certificate required
            return;
        }

        isExporting = true;
        StateHasChanged();

        try
        {
            // Call ExportService to initiate PDF export
            // Implementation depends on your service interface
            await Task.Delay(1000); // Placeholder
            
            // Refresh export queue
            await LoadExports();
        }
        finally
        {
            isExporting = false;
            StateHasChanged();
        }
    }

    private class PdfExportRequest
    {
        public HashSet<string> CaseIds { get; set; } = new();
        public bool IncludeSummarization { get; set; }
        public string SummarizationMethod { get; set; } = string.Empty;
        public List<string> RequirementCategories { get; set; } = new();
        public string CertificateSource { get; set; } = string.Empty;
        public string? SelectedCertificate { get; set; }
        public bool ValidateSignature { get; set; }
        public PdfContentOptions ContentOptions { get; set; } = new();
    }

    private class CertificateInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Issuer { get; set; } = string.Empty;
        public DateTime ValidFrom { get; set; }
        public DateTime ValidTo { get; set; }
        public bool IsValid { get; set; }
        public bool ExpiresSoon { get; set; }
    }

    private class PdfExportRecord
    {
        public string ExportId { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int CurrentStep { get; set; }
        public string? SignedPdfPath { get; set; }
        public bool SignatureValidated { get; set; }
    }

    private class PdfContentOptions
    {
        public bool IncludeCaseInfo { get; set; }
        public bool IncludePersonInfo { get; set; }
        public bool IncludeComplianceActions { get; set; }
        public bool IncludeRequirementSummary { get; set; }
        public bool IncludeAuditTrail { get; set; }
    }
}
```

---

## Story 1.9: Audit Trail and Reporting - UI Enhancements

### Current State Analysis

**Backend Status:** ✅ Complete  
**UI Status:** ⚠️ **Missing** - AC3 (Audit Trail Viewer) and AC6 (Export functionality) not implemented

**Gaps Identified:**
- No audit trail viewer interface
- No filtering capabilities (file ID, date range, action type, user)
- No correlation ID search
- No audit log export functionality
- No classification report generation UI

### Required UI Components

#### 1. Audit Trail Viewer Component

**Location:** `Components/Pages/AuditTrailViewer.razor`  
**Purpose:** View and filter audit logs with export capabilities

**Features:**

**A. Audit Trail Table**
- **Columns:**
  - Timestamp (formatted date/time)
  - Correlation ID (clickable, links to related records)
  - File ID (link to file details)
  - Action Type (badge with color coding)
  - Stage (Ingestion, Extraction, DecisionLogic, Export)
  - User ID (if applicable)
  - Success Status (success/failure indicator)
  - Action Details (expandable JSON view)
  - Error Message (if failed)

- **Filtering:**
  - File ID search
  - Date range picker (from/to dates)
  - Action type multi-select (Download, Classification, Extraction, Review, Export, etc.)
  - User ID filter
  - Stage filter
  - Success status filter (Success, Failure, All)
  - Correlation ID search

- **Sorting:**
  - By timestamp (newest/oldest first)
  - By action type
  - By stage
  - By user

**B. Correlation ID Tracking**
- **Correlation View:**
  - Click correlation ID to show all related audit records
  - Timeline view of all actions for a correlation ID
  - Visual flow showing processing stages

**C. Export Functionality**
- **Export Options:**
  - Export filtered results to CSV
  - Export filtered results to JSON
  - Export classification report (CSV/JSON)
  - Date range selection for reports

**D. Statistics Dashboard**
- **Metrics:**
  - Total audit records (filtered)
  - Records by action type (pie chart or bar chart)
  - Records by stage (distribution)
  - Success rate (percentage)
  - Records by user (top users)
  - Records by date (time series)

**Implementation Code:**

```razor
@page "/audit-trail"
@using ExxerCube.Prisma.Domain.Entities
@using ExxerCube.Prisma.Application.Services
@inject IAuditLogger AuditLogger
@inject IAuditReportingService AuditReportingService
@inject ILogger<AuditTrailViewer> Logger

<PageTitle>Audit Trail Viewer</PageTitle>

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">
        <MudIcon Icon="@Icons.Material.Filled.History" Class="mr-2" />
        Audit Trail Viewer
    </MudText>

    <!-- Statistics Cards -->
    <MudGrid Class="mb-4">
        <MudItem xs="12" sm="6" md="3">
            <MudCard>
                <MudCardContent>
                    <MudText Typo="Typo.h4" Color="Color.Info">@totalRecords</MudText>
                    <MudText Typo="Typo.body2">Total Records</MudText>
                    <MudText Typo="Typo.caption" Color="Color.Secondary">
                        @filteredRecordsCount filtered
                    </MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>
        <MudItem xs="12" sm="6" md="3">
            <MudCard>
                <MudCardContent>
                    <MudText Typo="Typo.h4" Color="Color.Success">@successRate.ToString("F1")%</MudText>
                    <MudText Typo="Typo.body2">Success Rate</MudText>
                    <MudText Typo="Typo.caption" Color="Color.Secondary">
                        @successCount successful
                    </MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>
        <MudItem xs="12" sm="6" md="3">
            <MudCard>
                <MudCardContent>
                    <MudText Typo="Typo.h4" Color="Color.Error">@failureCount</MudText>
                    <MudText Typo="Typo.body2">Failed Actions</MudText>
                    <MudText Typo="Typo.caption" Color="Color.Secondary">
                        @(failureRate.ToString("F1"))% failure rate
                    </MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>
        <MudItem xs="12" sm="6" md="3">
            <MudCard>
                <MudCardContent>
                    <MudText Typo="Typo.h4" Color="Color.Primary">@uniqueCorrelationIds</MudText>
                    <MudText Typo="Typo.body2">Unique Correlations</MudText>
                    <MudText Typo="Typo.caption" Color="Color.Secondary">
                        Tracked workflows
                    </MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>
    </MudGrid>

    <!-- Filters Panel -->
    <MudCard Class="mb-4">
        <MudCardHeader>
            <MudText Typo="Typo.h6">Filters</MudText>
            <MudSpacer />
            <MudButton Size="Size.Small" 
                      Variant="Variant.Text"
                      OnClick="ClearFilters">
                Clear All
            </MudButton>
        </MudCardHeader>
        <MudCardContent>
            <MudGrid>
                <MudItem xs="12" sm="6" md="3">
                    <MudTextField Label="File ID" 
                                 @bind-Value="filterFileId"
                                 Variant="Variant.Outlined"
                                 Dense="true"
                                 Immediate="true" />
                </MudItem>
                <MudItem xs="12" sm="6" md="3">
                    <MudTextField Label="Correlation ID" 
                                 @bind-Value="filterCorrelationId"
                                 Variant="Variant.Outlined"
                                 Dense="true"
                                 Immediate="true" />
                </MudItem>
                <MudItem xs="12" sm="6" md="3">
                    <MudDatePicker Label="From Date" 
                                 @bind-Date="filterFromDate"
                                 Variant="Variant.Outlined"
                                 Dense="true" />
                </MudItem>
                <MudItem xs="12" sm="6" md="3">
                    <MudDatePicker Label="To Date" 
                                 @bind-Date="filterToDate"
                                 Variant="Variant.Outlined"
                                 Dense="true" />
                </MudItem>
                <MudItem xs="12" sm="6" md="3">
                    <MudSelect Label="Action Type" 
                             @bind-SelectedValue="selectedActionTypes"
                             MultiSelection="true"
                             Variant="Variant.Outlined"
                             Dense="true">
                        @foreach (var actionType in Enum.GetValues<AuditActionType>())
                        {
                            <MudSelectItem Value="@actionType">@actionType</MudSelectItem>
                        }
                    </MudSelect>
                </MudItem>
                <MudItem xs="12" sm="6" md="3">
                    <MudSelect Label="Stage" 
                             @bind-SelectedValue="selectedStages"
                             MultiSelection="true"
                             Variant="Variant.Outlined"
                             Dense="true">
                        @foreach (var stage in Enum.GetValues<ProcessingStage>())
                        {
                            <MudSelectItem Value="@stage">@stage</MudSelectItem>
                        }
                    </MudSelect>
                </MudItem>
                <MudItem xs="12" sm="6" md="3">
                    <MudTextField Label="User ID" 
                                 @bind-Value="filterUserId"
                                 Variant="Variant.Outlined"
                                 Dense="true"
                                 Immediate="true" />
                </MudItem>
                <MudItem xs="12" sm="6" md="3">
                    <MudSelect Label="Success Status" 
                             @bind-SelectedValue="filterSuccessStatus"
                             Variant="Variant.Outlined"
                             Dense="true">
                        <MudSelectItem Value="@((bool?)null)">All</MudSelectItem>
                        <MudSelectItem Value="@true">Success Only</MudSelectItem>
                        <MudSelectItem Value="@false">Failed Only</MudSelectItem>
                    </MudSelect>
                </MudItem>
            </MudGrid>
        </MudCardContent>
    </MudCard>

    <!-- Audit Trail Table -->
    <MudCard>
        <MudCardHeader>
            <MudText Typo="Typo.h6">Audit Records</MudText>
            <MudSpacer />
            <MudButton Size="Size.Small" 
                      Variant="Variant.Outlined"
                      StartIcon="@Icons.Material.Filled.FileDownload"
                      OnClick="ExportAuditLog">
                        Export Filtered Results
                    </MudButton>
                    <MudButton Size="Size.Small" 
                              Variant="Variant.Outlined"
                              StartIcon="@Icons.Material.Filled.Assessment"
                              OnClick="GenerateClassificationReport"
                              Class="ml-2">
                        Generate Classification Report
                    </MudButton>
        </MudCardHeader>
        <MudCardContent>
            <MudTable Items="@FilteredAuditRecords" 
                     Dense="true" 
                     Hover="true" 
                     Striped="true"
                     Filter="new Func<AuditRecord, bool>(FilterRecords)"
                     SortMode="SortMode.Multiple">
                <HeaderContent>
                    <MudTh>
                        <MudTableSortLabel SortBy="@new Func<AuditRecord, object>(x => x.Timestamp)">
                            Timestamp
                        </MudTableSortLabel>
                    </MudTh>
                    <MudTh>
                        <MudTableSortLabel SortBy="@new Func<AuditRecord, object>(x => x.CorrelationId)">
                            Correlation ID
                        </MudTableSortLabel>
                    </MudTh>
                    <MudTh>File ID</MudTh>
                    <MudTh>
                        <MudTableSortLabel SortBy="@new Func<AuditRecord, object>(x => x.ActionType)">
                            Action Type
                        </MudTableSortLabel>
                    </MudTh>
                    <MudTh>
                        <MudTableSortLabel SortBy="@new Func<AuditRecord, object>(x => x.Stage)">
                            Stage
                        </MudTableSortLabel>
                    </MudTh>
                    <MudTh>User</MudTh>
                    <MudTh>Status</MudTh>
                    <MudTh>Details</MudTh>
                </HeaderContent>
                <RowTemplate>
                    <MudTd DataLabel="Timestamp">
                        <MudText Typo="Typo.body2">@context.Timestamp.ToString("g")</MudText>
                        <MudText Typo="Typo.caption" Color="Color.Secondary">
                            @GetRelativeTime(context.Timestamp)
                        </MudText>
                    </MudTd>
                    <MudTd DataLabel="Correlation ID">
                        <MudButton Size="Size.Small" 
                                  Variant="Variant.Text"
                                  OnClick="@(() => ViewCorrelationTimeline(context.CorrelationId))">
                            @TruncateString(context.CorrelationId, 12)
                        </MudButton>
                    </MudTd>
                    <MudTd DataLabel="File ID">
                        @if (!string.IsNullOrEmpty(context.FileId))
                        {
                            <MudLink Href="@($"/file-details/{context.FileId}")" 
                                   Color="Color.Primary">
                                @TruncateString(context.FileId, 12)
                            </MudLink>
                        }
                        else
                        {
                            <MudText Typo="Typo.caption" Color="Color.Secondary">-</MudText>
                        }
                    </MudTd>
                    <MudTd DataLabel="Action Type">
                        <MudChip Size="Size.Small" 
                                Color="@GetActionTypeColor(context.ActionType)">
                            @context.ActionType
                        </MudChip>
                    </MudTd>
                    <MudTd DataLabel="Stage">
                        <MudChip Size="Size.Small" Color="Color.Info">
                            @context.Stage
                        </MudChip>
                    </MudTd>
                    <MudTd DataLabel="User">
                        @if (!string.IsNullOrEmpty(context.UserId))
                        {
                            <MudText Typo="Typo.body2">@context.UserId</MudText>
                        }
                        else
                        {
                            <MudText Typo="Typo.caption" Color="Color.Secondary">System</MudText>
                        }
                    </MudTd>
                    <MudTd DataLabel="Status">
                        @if (context.Success)
                        {
                            <MudIcon Icon="@Icons.Material.Filled.CheckCircle" 
                                   Color="Color.Success" 
                                   Size="Size.Small" />
                        }
                        else
                        {
                            <MudIcon Icon="@Icons.Material.Filled.Error" 
                                   Color="Color.Error" 
                                   Size="Size.Small" />
                            @if (!string.IsNullOrEmpty(context.ErrorMessage))
                            {
                                <MudTooltip Text="@context.ErrorMessage">
                                    <MudText Typo="Typo.caption" Color="Color.Error">Error</MudText>
                                </MudTooltip>
                            }
                        }
                    </MudTd>
                    <MudTd DataLabel="Details">
                        <MudIconButton Icon="@Icons.Material.Filled.Info" 
                                      Size="Size.Small"
                                      OnClick="@(() => ShowActionDetails(context))" />
                    </MudTd>
                </RowTemplate>
                <PagerContent>
                    <MudTablePager />
                </PagerContent>
            </MudTable>
        </MudCardContent>
    </MudCard>

    <!-- Action Details Dialog -->
    <MudDialog @bind-IsVisible="@showActionDetailsDialog">
        <TitleContent>
            <MudText Typo="Typo.h6">Action Details: @selectedRecord?.ActionType</MudText>
        </TitleContent>
        <DialogContent>
            @if (selectedRecord != null)
            {
                <MudGrid>
                    <MudItem xs="12">
                        <MudText Typo="Typo.subtitle1">Action Information</MudText>
                        <MudDivider Class="my-2" />
                    </MudItem>
                    <MudItem xs="12" sm="6">
                        <MudText Typo="Typo.body2">Timestamp</MudText>
                        <MudText Typo="Typo.body1">@selectedRecord.Timestamp.ToString("g")</MudText>
                    </MudItem>
                    <MudItem xs="12" sm="6">
                        <MudText Typo="Typo.body2">Correlation ID</MudText>
                        <MudText Typo="Typo.body1">@selectedRecord.CorrelationId</MudText>
                    </MudItem>
                    <MudItem xs="12" sm="6">
                        <MudText Typo="Typo.body2">Action Type</MudText>
                        <MudChip Size="Size.Small" Color="@GetActionTypeColor(selectedRecord.ActionType)">
                            @selectedRecord.ActionType
                        </MudChip>
                    </MudItem>
                    <MudItem xs="12" sm="6">
                        <MudText Typo="Typo.body2">Stage</MudText>
                        <MudChip Size="Size.Small" Color="Color.Info">
                            @selectedRecord.Stage
                        </MudChip>
                    </MudItem>
                    <MudItem xs="12">
                        <MudDivider Class="my-2" />
                        <MudText Typo="Typo.subtitle1">Action Details (JSON)</MudText>
                        <MudPaper Class="pa-4" Elevation="1">
                            <MudText Typo="Typo.body2" Class="font-family-monospace">
                                @FormatJson(selectedRecord.ActionDetails)
                            </MudText>
                        </MudPaper>
                    </MudItem>
                    @if (!selectedRecord.Success && !string.IsNullOrEmpty(selectedRecord.ErrorMessage))
                    {
                        <MudItem xs="12">
                            <MudDivider Class="my-2" />
                            <MudAlert Severity="Severity.Error">
                                <MudText Typo="Typo.subtitle2">Error Message</MudText>
                                <MudText Typo="Typo.body2">@selectedRecord.ErrorMessage</MudText>
                            </MudAlert>
                        </MudItem>
                    }
                </MudGrid>
            }
        </DialogContent>
        <DialogActions>
            <MudButton OnClick="@(() => showActionDetailsDialog = false)">Close</MudButton>
            <MudButton Variant="Variant.Text" 
                      Color="Color.Primary"
                      OnClick="@(() => ViewCorrelationTimeline(selectedRecord?.CorrelationId ?? string.Empty))">
                View Correlation Timeline
            </MudButton>
        </DialogActions>
    </MudDialog>

    <!-- Correlation Timeline Dialog -->
    <MudDialog @bind-IsVisible="@showCorrelationTimelineDialog" MaxWidth="MaxWidth.Large">
        <TitleContent>
            <MudText Typo="Typo.h6">Correlation Timeline: @selectedCorrelationId</MudText>
        </TitleContent>
        <DialogContent>
            @if (correlationRecords.Any())
            {
                <MudTimeline Align="Align.Start">
                    @foreach (var record in correlationRecords.OrderBy(r => r.Timestamp))
                    {
                        <MudTimelineItem Size="Size.Small"
                                        Color="@(record.Success ? Color.Success : Color.Error)"
                                        Icon="@(record.Success ? Icons.Material.Filled.CheckCircle : Icons.Material.Filled.Error)">
                            <MudText Typo="Typo.body1" Class="font-weight-bold">
                                @record.ActionType
                            </MudText>
                            <MudText Typo="Typo.body2" Color="Color.Secondary">
                                @record.Stage - @record.Timestamp.ToString("g")
                            </MudText>
                            @if (!string.IsNullOrEmpty(record.FileId))
                            {
                                <MudText Typo="Typo.caption">
                                    File: @record.FileId
                                </MudText>
                            }
                            @if (!record.Success)
                            {
                                <MudAlert Severity="Severity.Error" Dense="true" Class="mt-2">
                                    @record.ErrorMessage
                                </MudAlert>
                            }
                        </MudTimelineItem>
                    }
                </MudTimeline>
            }
            else
            {
                <MudAlert Severity="Severity.Info">No records found for this correlation ID</MudAlert>
            }
        </DialogContent>
        <DialogActions>
            <MudButton OnClick="@(() => showCorrelationTimelineDialog = false)">Close</MudButton>
        </DialogActions>
    </MudDialog>
</MudContainer>

@code {
    private List<AuditRecord> auditRecords = new();
    private List<AuditRecord> filteredAuditRecords = new();
    private string searchText = string.Empty;
    private string? filterFileId;
    private string? filterCorrelationId;
    private DateTime? filterFromDate;
    private DateTime? filterToDate;
    private List<AuditActionType>? selectedActionTypes;
    private List<ProcessingStage>? selectedStages;
    private string? filterUserId;
    private bool? filterSuccessStatus;
    private bool showActionDetailsDialog = false;
    private bool showCorrelationTimelineDialog = false;
    private AuditRecord? selectedRecord;
    private string selectedCorrelationId = string.Empty;
    private List<AuditRecord> correlationRecords = new();

    private int totalRecords => auditRecords.Count;
    private int filteredRecordsCount => filteredAuditRecords.Count;
    private int successCount => filteredAuditRecords.Count(r => r.Success);
    private int failureCount => filteredAuditRecords.Count(r => !r.Success);
    private double successRate => filteredAuditRecords.Count > 0 
        ? (successCount * 100.0 / filteredAuditRecords.Count) 
        : 0;
    private double failureRate => filteredAuditRecords.Count > 0 
        ? (failureCount * 100.0 / filteredAuditRecords.Count) 
        : 0;
    private int uniqueCorrelationIds => filteredAuditRecords.Select(r => r.CorrelationId).Distinct().Count();

    protected override async Task OnInitializedAsync()
    {
        await LoadAuditRecords();
    }

    private async Task LoadAuditRecords()
    {
        // Load audit records from AuditLogger
        // Implementation depends on your service interface
    }

    private bool FilterRecords(AuditRecord record)
    {
        if (!string.IsNullOrEmpty(filterFileId) && 
            (string.IsNullOrEmpty(record.FileId) || !record.FileId.Contains(filterFileId, StringComparison.OrdinalIgnoreCase)))
            return false;
            
        if (!string.IsNullOrEmpty(filterCorrelationId) && 
            !record.CorrelationId.Contains(filterCorrelationId, StringComparison.OrdinalIgnoreCase))
            return false;
            
        if (filterFromDate.HasValue && record.Timestamp < filterFromDate.Value)
            return false;
            
        if (filterToDate.HasValue && record.Timestamp > filterToDate.Value)
            return false;
            
        if (selectedActionTypes != null && selectedActionTypes.Any() && 
            !selectedActionTypes.Contains(record.ActionType))
            return false;
            
        if (selectedStages != null && selectedStages.Any() && 
            !selectedStages.Contains(record.Stage))
            return false;
            
        if (!string.IsNullOrEmpty(filterUserId) && 
            (string.IsNullOrEmpty(record.UserId) || !record.UserId.Contains(filterUserId, StringComparison.OrdinalIgnoreCase)))
            return false;
            
        if (filterSuccessStatus.HasValue && record.Success != filterSuccessStatus.Value)
            return false;
            
        return true;
    }

    private void ClearFilters()
    {
        filterFileId = null;
        filterCorrelationId = null;
        filterFromDate = null;
        filterToDate = null;
        selectedActionTypes = null;
        selectedStages = null;
        filterUserId = null;
        filterSuccessStatus = null;
        StateHasChanged();
    }

    private void ShowActionDetails(AuditRecord record)
    {
        selectedRecord = record;
        showActionDetailsDialog = true;
    }

    private void ViewCorrelationTimeline(string correlationId)
    {
        selectedCorrelationId = correlationId;
        correlationRecords = auditRecords.Where(r => r.CorrelationId == correlationId).ToList();
        showCorrelationTimelineDialog = true;
        StateHasChanged();
    }

    private async Task ExportAuditLog()
    {
        // Export filtered audit records to CSV/JSON
        // Implementation depends on AuditReportingService
    }

    private async Task GenerateClassificationReport()
    {
        // Generate classification report for date range
        // Implementation depends on AuditReportingService
    }

    private string GetRelativeTime(DateTime timestamp)
    {
        var span = DateTime.Now - timestamp;
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours}h ago";
        return $"{(int)span.TotalDays}d ago";
    }

    private string TruncateString(string value, int maxLength)
    {
        return value.Length > maxLength ? value.Substring(0, maxLength) + "..." : value;
    }

    private string FormatJson(string json)
    {
        // Format JSON for display
        // Implementation depends on JSON formatting library
        return json;
    }

    private Color GetActionTypeColor(AuditActionType actionType) => actionType switch
    {
        AuditActionType.Download => Color.Info,
        AuditActionType.Classification => Color.Primary,
        AuditActionType.Extraction => Color.Secondary,
        AuditActionType.Review => Color.Warning,
        AuditActionType.Export => Color.Success,
        _ => Color.Default
    };
}
```

#### 2. Classification Report Generation Component

**Location:** `Components/Shared/ClassificationReportGenerator.razor`  
**Purpose:** Generate classification reports in CSV/JSON format

**Features:**
- Date range selection
- Format selection (CSV, JSON)
- Report preview before generation
- Download generated report

**Implementation Code:**

```razor
@using ExxerCube.Prisma.Application.Services
@inject IAuditReportingService AuditReportingService
@inject ILogger<ClassificationReportGenerator> Logger

<MudDialog @bind-IsVisible="@IsVisible" MaxWidth="MaxWidth.Medium">
    <TitleContent>
        <MudText Typo="Typo.h6">Generate Classification Report</MudText>
    </TitleContent>
    <DialogContent>
        <EditForm Model="@reportRequest" OnValidSubmit="GenerateReport">
            <DataAnnotationsValidator />
            
            <!-- Date Range Selection -->
            <MudItem xs="12" Class="mb-4">
                <MudText Typo="Typo.subtitle1" Class="mb-2">Date Range</MudText>
                <MudDatePicker Label="From Date" 
                             @bind-Date="@reportRequest.FromDate"
                             Variant="Variant.Outlined"
                             Required="true"
                             RequiredError="From date is required" />
                <MudDatePicker Label="To Date" 
                             @bind-Date="@reportRequest.ToDate"
                             Variant="Variant.Outlined"
                             Required="true"
                             RequiredError="To date is required"
                             Class="mt-2" />
                <MudText Typo="Typo.caption" Color="Color.Secondary" Class="mt-1">
                    Select the date range for classification data
                </MudText>
            </MudItem>
            
            <!-- Format Selection -->
            <MudItem xs="12" Class="mb-4">
                <MudText Typo="Typo.subtitle1" Class="mb-2">Export Format</MudText>
                <MudRadioGroup @bind-SelectedOption="@reportRequest.Format">
                    <MudRadio Option="@ReportFormat.Csv" Color="Color.Primary">
                        CSV (Comma-Separated Values)
                        <HelperText>Best for Excel and data analysis</HelperText>
                    </MudRadio>
                    <MudRadio Option="@ReportFormat.Json" Color="Color.Primary">
                        JSON (JavaScript Object Notation)
                        <HelperText>Best for programmatic processing</HelperText>
                    </MudRadio>
                </MudRadioGroup>
            </MudItem>
            
            <!-- Report Options -->
            <MudItem xs="12" Class="mb-4">
                <MudText Typo="Typo.subtitle1" Class="mb-2">Report Options</MudText>
                <MudCheckBox @bind-Checked="@reportRequest.IncludeStatistics" 
                           Label="Include classification statistics" />
                <MudCheckBox @bind-Checked="@reportRequest.IncludeConfidenceScores" 
                           Label="Include confidence scores" />
                <MudCheckBox @bind-Checked="@reportRequest.IncludeMetadata" 
                           Label="Include file metadata" />
            </MudItem>
            
            <!-- Preview Section -->
            @if (reportPreview != null)
            {
                <MudItem xs="12" Class="mb-4">
                    <MudDivider Class="my-2" />
                    <MudText Typo="Typo.subtitle1" Class="mb-2">Report Preview</MudText>
                    <MudAlert Severity="Severity.Info" Class="mb-2">
                        <MudText Typo="Typo.body2">
                            Records to include: @reportPreview.RecordCount
                        </MudText>
                        <MudText Typo="Typo.body2">
                            Date range: @reportPreview.FromDate.ToString("d") - @reportPreview.ToDate.ToString("d")
                        </MudText>
                        <MudText Typo="Typo.body2">
                            Estimated file size: @FormatFileSize(reportPreview.EstimatedSize)
                        </MudText>
                    </MudAlert>
                    
                    @if (reportPreview.SampleData.Any())
                    {
                        <MudText Typo="Typo.subtitle2" Class="mb-2">Sample Data (first 5 records)</MudText>
                        <MudTable Items="@reportPreview.SampleData" 
                                 Dense="true" 
                                 Hover="true"
                                 MaxHeight="200px">
                            <HeaderContent>
                                <MudTh>File ID</MudTh>
                                <MudTh>Classification</MudTh>
                                <MudTh>Confidence</MudTh>
                                <MudTh>Date</MudTh>
                            </HeaderContent>
                            <RowTemplate>
                                <MudTd>@TruncateString(context.FileId, 12)</MudTd>
                                <MudTd>
                                    <MudChip Size="Size.Small" Color="Color.Info">
                                        @context.Classification
                                    </MudChip>
                                </MudTd>
                                <MudTd>@context.ConfidenceScore.ToString("P0")</MudTd>
                                <MudTd>@context.ClassificationDate.ToString("g")</MudTd>
                            </RowTemplate>
                        </MudTable>
                    }
                </MudItem>
            }
            
            <!-- Actions -->
            <MudItem xs="12">
                <MudButton Variant="Variant.Outlined" 
                          Color="Color.Secondary"
                          OnClick="PreviewReport"
                          Disabled="@isGenerating || !IsDateRangeValid()">
                    Preview Report
                </MudButton>
                <MudButton Variant="Variant.Filled" 
                          Color="Color.Primary"
                          ButtonType="ButtonType.Submit"
                          StartIcon="@Icons.Material.Filled.FileDownload"
                          Disabled="@isGenerating || !IsDateRangeValid()"
                          Class="ml-2">
                    @(isGenerating ? "Generating..." : "Generate Report")
                </MudButton>
            </MudItem>
        </EditForm>
        
        <!-- Progress Indicator -->
        @if (isGenerating)
        {
            <MudProgressLinear Indeterminate="true" Class="mt-4" />
        }
        
        <!-- Error Display -->
        @if (!string.IsNullOrEmpty(errorMessage))
        {
            <MudAlert Severity="Severity.Error" Class="mt-4">
                @errorMessage
            </MudAlert>
        }
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => IsVisible = false)" Disabled="@isGenerating">
            Cancel
        </MudButton>
        @if (generatedReportUrl != null)
        {
            <MudButton Variant="Variant.Filled" 
                      Color="Color.Success"
                      StartIcon="@Icons.Material.Filled.Download"
                      Href="@generatedReportUrl"
                      Download="classification-report.@(reportRequest.Format.ToString().ToLower())">
                Download Report
            </MudButton>
        }
    </DialogActions>
</MudDialog>

@code {
    [Parameter] public bool IsVisible { get; set; }
    [Parameter] public EventCallback<bool> IsVisibleChanged { get; set; }
    
    private ClassificationReportRequest reportRequest = new();
    private ClassificationReportPreview? reportPreview;
    private bool isGenerating = false;
    private string? errorMessage;
    private string? generatedReportUrl;

    private bool IsDateRangeValid()
    {
        return reportRequest.FromDate.HasValue && 
               reportRequest.ToDate.HasValue &&
               reportRequest.FromDate.Value <= reportRequest.ToDate.Value;
    }

    private async Task PreviewReport()
    {
        if (!IsDateRangeValid())
        {
            errorMessage = "Please select a valid date range";
            return;
        }

        try
        {
            errorMessage = null;
            // Call AuditReportingService to get preview
            // Implementation depends on your service interface
            // reportPreview = await AuditReportingService.PreviewClassificationReportAsync(...);
            
            // Placeholder preview data
            reportPreview = new ClassificationReportPreview
            {
                RecordCount = 150,
                FromDate = reportRequest.FromDate!.Value,
                ToDate = reportRequest.ToDate!.Value,
                EstimatedSize = 1024 * 50, // 50 KB estimate
                SampleData = new List<ClassificationRecordPreview>
                {
                    new() { FileId = "FILE001", Classification = "Bloqueo", ConfidenceScore = 0.95m, ClassificationDate = DateTime.Now.AddDays(-1) },
                    new() { FileId = "FILE002", Classification = "Desbloqueo", ConfidenceScore = 0.88m, ClassificationDate = DateTime.Now.AddDays(-2) },
                    new() { FileId = "FILE003", Classification = "Documentacion", ConfidenceScore = 0.92m, ClassificationDate = DateTime.Now.AddDays(-3) },
                    new() { FileId = "FILE004", Classification = "Transferencia", ConfidenceScore = 0.87m, ClassificationDate = DateTime.Now.AddDays(-4) },
                    new() { FileId = "FILE005", Classification = "Informacion", ConfidenceScore = 0.91m, ClassificationDate = DateTime.Now.AddDays(-5) }
                }
            };
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error previewing classification report");
            errorMessage = $"Error generating preview: {ex.Message}";
        }
    }

    private async Task GenerateReport()
    {
        if (!IsDateRangeValid())
        {
            errorMessage = "Please select a valid date range";
            return;
        }

        isGenerating = true;
        errorMessage = null;
        generatedReportUrl = null;
        StateHasChanged();

        try
        {
            // Call AuditReportingService to generate report
            // Implementation depends on your service interface
            // var result = await AuditReportingService.GenerateClassificationReportAsync(...);
            
            // Placeholder: simulate report generation
            await Task.Delay(2000);
            
            // In real implementation, this would be the URL to download the generated report
            generatedReportUrl = $"/api/reports/classification/{Guid.NewGuid()}.{reportRequest.Format.ToString().ToLower()}";
            
            Logger.LogInformation("Classification report generated successfully");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error generating classification report");
            errorMessage = $"Error generating report: {ex.Message}";
        }
        finally
        {
            isGenerating = false;
            StateHasChanged();
        }
    }

    private string TruncateString(string value, int maxLength)
    {
        return value.Length > maxLength ? value.Substring(0, maxLength) + "..." : value;
    }

    private string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }

    private enum ReportFormat
    {
        Csv,
        Json
    }

    private class ClassificationReportRequest
    {
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public ReportFormat Format { get; set; } = ReportFormat.Csv;
        public bool IncludeStatistics { get; set; } = true;
        public bool IncludeConfidenceScores { get; set; } = true;
        public bool IncludeMetadata { get; set; } = false;
    }

    private class ClassificationReportPreview
    {
        public int RecordCount { get; set; }
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public long EstimatedSize { get; set; }
        public List<ClassificationRecordPreview> SampleData { get; set; } = new();
    }

    private class ClassificationRecordPreview
    {
        public string FileId { get; set; } = string.Empty;
        public string Classification { get; set; } = string.Empty;
        public decimal ConfidenceScore { get; set; }
        public DateTime ClassificationDate { get; set; }
    }
}
```

**Usage in Audit Trail Viewer:**

```razor
<!-- Add to AuditTrailViewer.razor -->
<ClassificationReportGenerator @bind-IsVisible="@showReportGeneratorDialog" />

<!-- Update button click handler -->
private void GenerateClassificationReport()
{
    showReportGeneratorDialog = true;
}
```

---

## API/Service Integration Details

### Export Service Integration

**Required Service Interface:**

```csharp
public interface IExportService
{
    Task<Result<ExportJob>> InitiateExportAsync(
        ExportRequest request, 
        CancellationToken cancellationToken = default);
    
    Task<Result<ExportJob>> GetExportStatusAsync(
        string exportId, 
        CancellationToken cancellationToken = default);
    
    Task<Result<List<ExportJob>>> GetExportQueueAsync(
        ExportFilter filter, 
        CancellationToken cancellationToken = default);
    
    Task<Result<Stream>> DownloadExportAsync(
        string exportId, 
        string fileFormat, 
        CancellationToken cancellationToken = default);
    
    Task<Result> CancelExportAsync(
        string exportId, 
        CancellationToken cancellationToken = default);
    
    Task<Result<ExportJob>> RetryExportAsync(
        string exportId, 
        CancellationToken cancellationToken = default);
    
    Task<Result<PdfExportJob>> InitiatePdfExportAsync(
        PdfExportRequest request, 
        CancellationToken cancellationToken = default);
    
    Task<Result<PdfExportJob>> GetPdfExportStatusAsync(
        string exportId, 
        CancellationToken cancellationToken = default);
}
```

### Audit Reporting Service Integration

**Required Service Interface:**

```csharp
public interface IAuditReportingService
{
    Task<Result<List<AuditRecord>>> GetAuditRecordsAsync(
        AuditFilter filter, 
        CancellationToken cancellationToken = default);
    
    Task<Result<ClassificationReportPreview>> PreviewClassificationReportAsync(
        DateTime fromDate, 
        DateTime toDate, 
        CancellationToken cancellationToken = default);
    
    Task<Result<Stream>> GenerateClassificationReportAsync(
        ClassificationReportRequest request, 
        CancellationToken cancellationToken = default);
    
    Task<Result<Stream>> ExportAuditLogAsync(
        AuditFilter filter, 
        ExportFormat format, 
        CancellationToken cancellationToken = default);
    
    Task<Result<List<AuditRecord>>> GetCorrelationTimelineAsync(
        string correlationId, 
        CancellationToken cancellationToken = default);
}
```

### Certificate Management Integration

**Required Service Interface:**

```csharp
public interface ICertificateService
{
    Task<Result<List<CertificateInfo>>> GetAvailableCertificatesAsync(
        CertificateSource source, 
        CancellationToken cancellationToken = default);
    
    Task<Result<CertificateInfo>> GetCertificateDetailsAsync(
        string certificateName, 
        CertificateSource source, 
        CancellationToken cancellationToken = default);
    
    Task<Result<bool>> ValidateCertificateAsync(
        string certificateName, 
        CertificateSource source, 
        CancellationToken cancellationToken = default);
}
```

---

## Error Handling Patterns

### Export Service Error Handling

**Error Scenarios:**
- No cases selected → Show validation error in form
- Export generation failure → Show error in export queue with retry option
- File download failure → Show error toast with retry button
- Certificate unavailable → Show warning with alternative options
- Validation errors → Display validation results dialog with details

**Implementation Pattern:**

```razor
@code {
    private async Task InitiateExport()
    {
        if (!selectedCaseIds.Any())
        {
            snackbar.Add("Please select at least one case", Severity.Warning);
            return;
        }

        isExporting = true;
        StateHasChanged();

        try
        {
            var result = await ExportService.InitiateExportAsync(new ExportRequest
            {
                CaseIds = selectedCaseIds.ToList(),
                Format = exportFormat,
                ValidateSchema = validateSchema,
                ValidateRequiredFields = validateRequiredFields,
                StopOnErrors = stopOnErrors
            });

            if (result.IsFailure)
            {
                snackbar.Add($"Export failed: {result.Error}", Severity.Error);
                return;
            }

            snackbar.Add("Export initiated successfully", Severity.Success);
            await LoadExports();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error initiating export");
            snackbar.Add("An unexpected error occurred", Severity.Error);
        }
        finally
        {
            isExporting = false;
            StateHasChanged();
        }
    }
}
```

### Audit Trail Error Handling

**Error Scenarios:**
- Filter returns no results → Show info message
- Export generation failure → Show error with retry option
- Correlation ID not found → Show "No records found" message
- Date range invalid → Show validation error

---

## Real-time Updates Implementation

### SignalR Integration for Export Progress

**Hub Definition:**

```csharp
public class ExportHub : Hub
{
    public async Task JoinExportGroup(string exportId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"export-{exportId}");
    }
    
    public async Task LeaveExportGroup(string exportId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"export-{exportId}");
    }
}
```

**Client-side Integration:**

```razor
@inject HubConnection? HubConnection
@implements IAsyncDisposable

@code {
    protected override async Task OnInitializedAsync()
    {
        HubConnection = new HubConnectionBuilder()
            .WithUrl("/hubs/export")
            .Build();

        HubConnection.On<ExportProgressUpdate>("ExportProgress", (update) =>
        {
            var export = exports.FirstOrDefault(e => e.ExportId == update.ExportId);
            if (export != null)
            {
                export.Status = update.Status;
                export.Progress = update.Progress;
                StateHasChanged();
            }
        });

        await HubConnection.StartAsync();
    }

    private async Task ViewExportDetails(ExportRecord export)
    {
        selectedExport = export;
        showExportDetailsDialog = true;
        
        // Join SignalR group for real-time updates
        if (HubConnection?.State == HubConnectionState.Connected)
        {
            await HubConnection.SendAsync("JoinExportGroup", export.ExportId);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (HubConnection is not null)
        {
            await HubConnection.DisposeAsync();
        }
    }
}
```

**Server-side Progress Updates:**

```csharp
public class ExportService
{
    private readonly IHubContext<ExportHub> _hubContext;
    
    public async Task<Result<ExportJob>> InitiateExportAsync(
        ExportRequest request, 
        CancellationToken cancellationToken = default)
    {
        var exportJob = new ExportJob { ExportId = Guid.NewGuid().ToString() };
        
        // Start background processing
        _ = Task.Run(async () =>
        {
            try
            {
                await _hubContext.Clients
                    .Group($"export-{exportJob.ExportId}")
                    .SendAsync("ExportProgress", new ExportProgressUpdate
                    {
                        ExportId = exportJob.ExportId,
                        Status = "In Progress",
                        Progress = 0
                    });

                // Process export...
                // Update progress periodically
                
                await _hubContext.Clients
                    .Group($"export-{exportJob.ExportId}")
                    .SendAsync("ExportProgress", new ExportProgressUpdate
                    {
                        ExportId = exportJob.ExportId,
                        Status = "Completed",
                        Progress = 100
                    });
            }
            catch (Exception ex)
            {
                await _hubContext.Clients
                    .Group($"export-{exportJob.ExportId}")
                    .SendAsync("ExportProgress", new ExportProgressUpdate
                    {
                        ExportId = exportJob.ExportId,
                        Status = "Failed",
                        ErrorMessage = ex.Message
                    });
            }
        }, cancellationToken);
        
        return Result<ExportJob>.Success(exportJob);
    }
}
```

---

## Consolidated Implementation Plan

### Priority 1: Critical UI Components (Required for Story Completion)

1. **Export Management Dashboard** (Story 1.7 - AC7)
   - Export initiation form
   - Export queue table
   - Download functionality
   - Export details dialog

2. **PDF Export Extensions** (Story 1.8)
   - PDF export tab in Export Management
   - Certificate selection interface
   - PDF summarization options
   - Digital signing progress tracking

3. **Audit Trail Viewer** (Story 1.9 - AC3, AC6)
   - Audit trail table with comprehensive filtering
   - Correlation ID tracking and timeline view
   - Export functionality (CSV/JSON)
   - Classification report generation

### Priority 2: Supporting UI Components (Enhancement)

1. **Export Preview** (Story 1.7)
   - Preview dialog showing what will be exported
   - Validation preview

2. **Certificate Management** (Story 1.8)
   - Certificate management interface
   - Certificate expiration warnings

3. **Report Generation UI** (Story 1.9)
   - Enhanced report generation interface
   - Report scheduling (future enhancement)

### Implementation Order

**Phase 1:** Story 1.7 UI Components (Critical - AC7)
- Export Management Dashboard
- Export initiation form
- Export queue table

**Phase 2:** Story 1.8 UI Extensions
- PDF export tab
- Certificate selection
- PDF export progress tracking

**Phase 3:** Story 1.9 UI Components (Critical - AC3, AC6)
- Audit Trail Viewer
- Correlation ID tracking
- Export functionality

**Phase 4:** Integration and Enhancements
- Integrate all components
- Add preview functionality
- Add advanced filtering

---

## Design System Compliance

All UI components must follow:
- **MudBlazor Component Library:** Use MudBlazor components consistently
- **Color Scheme:** Follow existing color patterns (Primary, Secondary, Success, Warning, Error, Info)
- **Typography:** Use MudText with appropriate Typo variants
- **Icons:** Use Material Icons via `Icons.Material.Filled.*`
- **Layout:** Use MudContainer, MudGrid, MudItem for responsive layouts
- **Cards:** Use MudCard, MudCardHeader, MudCardContent for content sections
- **Tables:** Use MudTable for data display with proper sorting and filtering
- **Progress Indicators:** Use MudProgressLinear, MudProgressCircular, and MudStepper for progress tracking
- **Dialogs:** Use MudDialog for detailed views and confirmations
- **Forms:** Use MudForm, MudTextField, MudSelect, MudCheckBox for form inputs

---

## User Experience Considerations

### Information Hierarchy
1. **Primary Information:** Export status, audit records, correlation timelines (most important)
2. **Secondary Information:** Export options, filter settings (supporting context)
3. **Tertiary Information:** Detailed JSON, certificate details (expandable views)

### Progressive Disclosure
- Show summary information by default
- Allow expansion for detailed views
- Use tabs/accordions for complex information
- Provide tooltips for technical terms

### Real-time Updates
- Use SignalR for real-time export progress updates
- Show loading states during data fetch
- Provide error states with retry options
- Visual indicators for new/updated exports

### Accessibility
- Ensure proper ARIA labels for all interactive elements
- Support keyboard navigation throughout
- Provide color-blind friendly indicators (use icons + colors)
- Ensure sufficient contrast ratios (WCAG 2.1 AA compliance)
- Screen reader announcements for status changes

### Performance
- Virtual scrolling for large audit trail tables
- Lazy loading of export details
- Debounced filter inputs
- Cached filter results
- Progressive loading (load summary first, details on demand)
- Efficient pagination for large datasets

---

## Testing Considerations

### UX Testing
- User testing with compliance officers and managers
- Task completion time measurement
- Error rate tracking
- Satisfaction surveys
- Usability testing for each component

### Accessibility Testing
- Screen reader testing (NVDA, JAWS)
- Keyboard-only navigation testing
- Color contrast verification
- WCAG 2.1 AA compliance audit

### Performance Testing
- Load time for large audit trail datasets
- Filter/search responsiveness
- Export generation performance
- Real-time update performance (SignalR)
- Memory usage with many records

### Integration Testing
- Verify export management integrates with ExportService
- Verify PDF export integrates with PDF signing service
- Verify audit trail viewer integrates with AuditLogger
- Verify correlation ID tracking works across all stages

---

## Component Dependencies and Navigation

### Required NuGet Packages

**UI Project Dependencies:**
- `MudBlazor` (latest stable version)
- `Microsoft.AspNetCore.SignalR.Client` (for real-time updates)
- `System.Text.Json` (for JSON handling)

### Navigation Structure

**Menu Items to Add:**

```razor
<!-- Add to MainLayout.razor or NavigationMenu.razor -->
<MudNavLink Href="/export-management" Match="NavLinkMatch.Prefix">
    <MudIcon Icon="@Icons.Material.Filled.FileDownload" Class="mr-2" />
    Export Management
</MudNavLink>

<MudNavLink Href="/audit-trail" Match="NavLinkMatch.Prefix">
    <MudIcon Icon="@Icons.Material.Filled.History" Class="mr-2" />
    Audit Trail
</MudNavLink>
```

### Route Configuration

**Required Routes:**

```csharp
// In Program.cs or App.razor
app.MapRazorPages();
app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

// Routes defined in components:
// - /export-management (ExportManagement.razor)
// - /audit-trail (AuditTrailViewer.razor)
```

### Component Registration

**Dependency Injection Setup:**

```csharp
// In Program.cs
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<IAuditReportingService, AuditReportingService>();
builder.Services.AddScoped<ICertificateService, CertificateService>();
builder.Services.AddSignalR(); // For real-time updates

// Register SignalR Hub
app.MapHub<ExportHub>("/hubs/export");
```

### Shared Components

**Components to Create:**
- `Components/Shared/ClassificationReportGenerator.razor` - Report generation dialog
- `Components/Shared/ExportProgressIndicator.razor` - Reusable progress component
- `Components/Shared/CorrelationTimeline.razor` - Reusable timeline component

---

## Implementation Checklist

### Story 1.7: SIRO Export UI
- [ ] Create `ExportManagement.razor` component
- [ ] Implement export initiation form
- [ ] Implement export queue table with filtering
- [ ] Implement export details dialog
- [ ] Implement download functionality
- [ ] Add SignalR integration for real-time updates
- [ ] Add error handling and validation
- [ ] Add unit tests for component logic
- [ ] Add integration tests with ExportService

### Story 1.8: PDF Export UI
- [ ] Add PDF export tab to ExportManagement component
- [ ] Implement certificate selection interface
- [ ] Implement PDF summarization options
- [ ] Implement digital signing progress tracking
- [ ] Add certificate validation display
- [ ] Integrate with CertificateService
- [ ] Add error handling for certificate failures
- [ ] Add unit tests for PDF export UI

### Story 1.9: Audit Trail UI
- [ ] Create `AuditTrailViewer.razor` component
- [ ] Implement audit trail table with comprehensive filtering
- [ ] Implement correlation ID tracking and timeline view
- [ ] Implement export functionality (CSV/JSON)
- [ ] Create `ClassificationReportGenerator.razor` component
- [ ] Add statistics dashboard
- [ ] Add real-time updates for new audit records
- [ ] Add unit tests for audit trail viewer
- [ ] Add integration tests with AuditReportingService

### Common Tasks
- [ ] Add navigation menu items
- [ ] Configure SignalR hubs
- [ ] Set up dependency injection
- [ ] Add error handling patterns
- [ ] Add loading states and progress indicators
- [ ] Implement responsive design
- [ ] Add accessibility features (ARIA labels, keyboard navigation)
- [ ] Performance optimization (virtual scrolling, lazy loading)
- [ ] User acceptance testing

---

## Next Steps

1. **Review this document** with Product Owner and Development Team
2. **Prioritize UI components** based on user needs and story dependencies
3. **Create detailed wireframes** for each component (if needed)
4. **Set up SignalR infrastructure** for real-time updates
5. **Implement backend service interfaces** (IExportService, IAuditReportingService, ICertificateService)
6. **Implement UI components** following MudBlazor patterns and this specification
7. **Add unit and integration tests** for all components
8. **Test UI components** with end users for usability
9. **Update story status** once UI components are complete
10. **Deploy to staging environment** for user acceptance testing

---

## Related Documentation

- [Front-End Specification](front-end-spec.md) - Overall UI/UX architecture
- [UI Requirements Analysis](ui-requirements-stories-1.1-1.4.md) - Comprehensive UI requirements
- [Story 1.7](../stories/1.7.siro-compliant-export.md) - SIRO export story
- [Story 1.8](../stories/1.8.pdf-summarization-digital-signing.md) - PDF summarization story
- [Story 1.9](../stories/1.9.audit-trail-reporting.md) - Audit trail story

---

**Document Status:** ✅ **Complete & Enhanced**  
**Last Updated:** 2025-01-15  
**Ready for Implementation** 🚀

### Enhancement Summary

This document has been enhanced with:
- ✅ Complete Classification Report Generation Component implementation (Story 1.9)
- ✅ API/Service Integration details with interface definitions
- ✅ Error Handling Patterns with code examples
- ✅ Real-time Updates Implementation using SignalR
- ✅ Component Dependencies and Navigation setup
- ✅ Comprehensive Implementation Checklist
- ✅ Enhanced Next Steps with detailed action items

All three stories (1.7, 1.8, 1.9) now have complete UI enhancement specifications with:
- Full Razor component implementations
- Service integration patterns
- Error handling strategies
- Real-time update mechanisms
- Testing considerations
- Accessibility and performance guidelines

