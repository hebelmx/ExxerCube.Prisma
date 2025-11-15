# UI/UX Enhancements: Stories 1.1-1.3 (BMAD Agent)

**Component:** Document Processing Dashboard, Classification Display, Field Matching Visualization  
**Stories:** 1.1 - Browser Automation, 1.2 - Metadata Extraction, 1.3 - Field Matching  
**Status:** UX Enhancement Recommendations  
**Created:** 2025-01-15  
**Author:** BMAD Agent (Browser-based Markdown Documentation)

---

## Executive Summary

Stories 1.1, 1.2, and 1.3 deliver robust backend functionality for document ingestion, metadata extraction, classification, and field matching. However, **critical UI components are missing** to expose these capabilities to end users. This document provides comprehensive UI/UX enhancement specifications to transform backend functionality into intuitive, user-friendly interfaces.

**Current State:** Backend complete ✅ | UI Status: ⚠️ **Partial/Missing**

---

## Story 1.1: Browser Automation and Document Download - UI Enhancements

### Current State Analysis

**Backend Status:** ✅ Complete (QA Approved)  
**UI Status:** ⚠️ **Partial** - Basic infrastructure exists, enhanced dashboard needed

**Gaps Identified:**
- No dedicated UI for browser automation download results
- No visibility into automated download history
- No real-time download status updates
- No duplicate detection visualization
- No download statistics dashboard

### Required UI Components

#### 1. Document Ingestion Dashboard

**Location:** Create `DocumentIngestionDashboard.razor` or enhance existing `OCRDemo.razor`  
**Purpose:** Central hub for monitoring automated document downloads and manual uploads

**Features:**

**A. Download History Table**
- **Columns:**
  - File Name (with icon based on format)
  - Source URL (truncated, clickable link)
  - Download Timestamp (relative time + absolute)
  - File Size (formatted: KB/MB)
  - Format Badge (PDF/XML/DOCX/ZIP)
  - Status Badge (Success/Duplicate/Failed)
  - Checksum (truncated, expandable)
  - Actions (View Metadata, Download, View Processing Status)

- **Filtering:**
  - Date range picker
  - Format filter (multi-select)
  - Status filter (Success/Duplicate/Failed)
  - Search by filename or URL

- **Sorting:**
  - By timestamp (newest/oldest first)
  - By file name (A-Z)
  - By file size (largest/smallest)
  - By status

**B. Download Statistics Cards**
- **Metrics:**
  - Total files downloaded (today/week/month/all-time)
  - Duplicate detection count (with percentage)
  - Failed downloads count (with error breakdown)
  - Average download time
  - Total storage used
  - Files pending processing

- **Visual Design:**
  - Large number with trend indicator (↑/↓)
  - Mini chart showing trend over time
  - Clickable cards that filter the table

**C. Browser Automation Status Panel**
- **Information:**
  - Last automation run timestamp
  - Next scheduled run (if configured)
  - Automation health status (success rate, error count)
  - Current browser session status (if running)
  - Configuration summary (website URL, file patterns)

- **Actions:**
  - Manual trigger button (with confirmation)
  - View automation logs
  - Configure automation settings

**D. Recent Downloads Feed**
- Real-time updates via SignalR
- Shows last 10-20 downloads with status indicators
- Expandable to show full history
- Visual indicators for new downloads

**Implementation Code:**

```razor
@page "/document-ingestion"
@using ExxerCube.Prisma.Domain.Entities
@using ExxerCube.Prisma.Application.Services
@inject IDocumentIngestionService DocumentIngestionService
@inject IFileMetadataLogger FileMetadataLogger
@inject ILogger<DocumentIngestionDashboard> Logger

<PageTitle>Document Ingestion Dashboard</PageTitle>

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">
        <MudIcon Icon="@Icons.Material.Filled.CloudDownload" Class="mr-2" />
        Document Ingestion Dashboard
    </MudText>

    <!-- Statistics Cards -->
    <MudGrid Class="mb-4">
        <MudItem xs="12" sm="6" md="3">
            <MudCard OnClick="@(() => FilterByStatus("Success"))" Class="cursor-pointer">
                <MudCardContent>
                    <MudText Typo="Typo.h4" Color="Color.Success">@totalDownloads</MudText>
                    <MudText Typo="Typo.body2">Total Downloads</MudText>
                    <MudText Typo="Typo.caption" Color="Color.Secondary">
                        @(duplicateCount) duplicates detected
                    </MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>
        <MudItem xs="12" sm="6" md="3">
            <MudCard OnClick="@(() => FilterByStatus("Failed"))" Class="cursor-pointer">
                <MudCardContent>
                    <MudText Typo="Typo.h4" Color="Color.Error">@failedDownloads</MudText>
                    <MudText Typo="Typo.body2">Failed Downloads</MudText>
                    <MudText Typo="Typo.caption" Color="Color.Secondary">
                        @(failedPercentage.ToString("F1"))% failure rate
                    </MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>
        <MudItem xs="12" sm="6" md="3">
            <MudCard>
                <MudCardContent>
                    <MudText Typo="Typo.h4" Color="Color.Info">@FormatFileSize(totalStorageUsed)</MudText>
                    <MudText Typo="Typo.body2">Total Storage Used</MudText>
                    <MudText Typo="Typo.caption" Color="Color.Secondary">
                        @(averageFileSize) avg file size
                    </MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>
        <MudItem xs="12" sm="6" md="3">
            <MudCard>
                <MudCardContent>
                    <MudText Typo="Typo.h4" Color="Color.Warning">@pendingProcessing</MudText>
                    <MudText Typo="Typo.body2">Pending Processing</MudText>
                    <MudText Typo="Typo.caption" Color="Color.Secondary">
                        Awaiting extraction
                    </MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>
    </MudGrid>

    <!-- Browser Automation Status -->
    <MudCard Class="mb-4">
        <MudCardHeader>
            <MudText Typo="Typo.h6">
                <MudIcon Icon="@Icons.Material.Filled.Settings" Class="mr-2" />
                Browser Automation Status
            </MudText>
        </MudCardHeader>
        <MudCardContent>
            <MudGrid>
                <MudItem xs="12" sm="6" md="3">
                    <MudText Typo="Typo.subtitle2">Last Run</MudText>
                    <MudText Typo="Typo.body1">
                        @(lastRunTime?.ToString("g") ?? "Never")
                    </MudText>
                </MudItem>
                <MudItem xs="12" sm="6" md="3">
                    <MudText Typo="Typo.subtitle2">Success Rate</MudText>
                    <MudProgressLinear Value="@successRate" Color="@GetSuccessRateColor(successRate)" />
                    <MudText Typo="Typo.body2">@(successRate.ToString("F1"))%</MudText>
                </MudItem>
                <MudItem xs="12" sm="6" md="3">
                    <MudText Typo="Typo.subtitle2">Status</MudText>
                    <MudChip Size="Size.Small" Color="@GetStatusColor(automationStatus)">
                        @automationStatus
                    </MudChip>
                </MudItem>
                <MudItem xs="12" sm="6" md="3">
                    <MudButton Variant="Variant.Filled" 
                               Color="Color.Primary"
                               StartIcon="@Icons.Material.Filled.PlayArrow"
                               OnClick="TriggerManualDownload"
                               Disabled="@isDownloading">
                        @(isDownloading ? "Downloading..." : "Trigger Download")
                    </MudButton>
                </MudItem>
            </MudGrid>
        </MudCardContent>
    </MudCard>

    <!-- Download History Table -->
    <MudCard>
        <MudCardHeader>
            <MudText Typo="Typo.h6">Download History</MudText>
            <MudSpacer />
            <MudTextField @bind-Value="searchText" 
                         Placeholder="Search files..." 
                         Adornment="Adornment.Start"
                         AdornmentIcon="@Icons.Material.Filled.Search"
                         Variant="Variant.Outlined"
                         Dense="true"
                         Immediate="true" />
        </MudCardHeader>
        <MudCardContent>
            <MudTable Items="@filteredDownloads" 
                     Dense="true" 
                     Hover="true" 
                     Striped="true"
                     Filter="new Func<FileMetadata, bool>(FilterDownloads)"
                     SortMode="SortMode.Multiple">
                <HeaderContent>
                    <MudTh>
                        <MudTableSortLabel SortBy="@new Func<FileMetadata, object>(x => x.FileName)">
                            File Name
                        </MudTableSortLabel>
                    </MudTh>
                    <MudTh>
                        <MudTableSortLabel SortBy="@new Func<FileMetadata, object>(x => x.Url)">
                            Source URL
                        </MudTableSortLabel>
                    </MudTh>
                    <MudTh>
                        <MudTableSortLabel SortBy="@new Func<FileMetadata, object>(x => x.DownloadTimestamp)">
                            Downloaded
                        </MudTableSortLabel>
                    </MudTh>
                    <MudTh>
                        <MudTableSortLabel SortBy="@new Func<FileMetadata, object>(x => x.FileSize)">
                            Size
                        </MudTableSortLabel>
                    </MudTh>
                    <MudTh>Format</MudTh>
                    <MudTh>Status</MudTh>
                    <MudTh>Actions</MudTh>
                </HeaderContent>
                <RowTemplate>
                    <MudTd DataLabel="File Name">
                        <MudIcon Icon="@GetFormatIcon(context.Format)" Class="mr-2" />
                        <MudText Typo="Typo.body2">@context.FileName</MudText>
                    </MudTd>
                    <MudTd DataLabel="Source URL">
                        @if (!string.IsNullOrEmpty(context.Url))
                        {
                            <MudLink Href="@context.Url" Target="_blank" Color="Color.Primary">
                                @TruncateUrl(context.Url, 40)
                            </MudLink>
                        }
                        else
                        {
                            <MudText Typo="Typo.body2" Color="Color.Secondary">Manual Upload</MudText>
                        }
                    </MudTd>
                    <MudTd DataLabel="Downloaded">
                        <MudText Typo="Typo.body2">@context.DownloadTimestamp.ToString("g")</MudText>
                        <MudText Typo="Typo.caption" Color="Color.Secondary">
                            @GetRelativeTime(context.DownloadTimestamp)
                        </MudText>
                    </MudTd>
                    <MudTd DataLabel="Size">
                        <MudText Typo="Typo.body2">@FormatFileSize(context.FileSize)</MudText>
                    </MudTd>
                    <MudTd DataLabel="Format">
                        <MudChip Size="Size.Small" Color="@GetFormatColor(context.Format)">
                            @context.Format
                        </MudChip>
                    </MudTd>
                    <MudTd DataLabel="Status">
                        <MudChip Size="Size.Small" 
                                Color="@GetStatusColor(context.Status)"
                                Icon="@GetStatusIcon(context.Status)">
                            @context.Status
                        </MudChip>
                    </MudTd>
                    <MudTd DataLabel="Actions">
                        <MudIconButton Icon="@Icons.Material.Filled.Visibility" 
                                      Size="Size.Small"
                                      OnClick="@(() => ViewMetadata(context))" />
                        <MudIconButton Icon="@Icons.Material.Filled.Download" 
                                      Size="Size.Small"
                                      OnClick="@(() => DownloadFile(context))" />
                        <MudIconButton Icon="@Icons.Material.Filled.Info" 
                                      Size="Size.Small"
                                      OnClick="@(() => ViewProcessingStatus(context))" />
                    </MudTd>
                </RowTemplate>
                <PagerContent>
                    <MudTablePager />
                </PagerContent>
            </MudTable>
        </MudCardContent>
    </MudCard>

    <!-- Filters Panel -->
    <MudExpansionPanels Class="mt-4">
        <MudExpansionPanel Text="Filters">
            <MudGrid>
                <MudItem xs="12" sm="6" md="3">
                    <MudDatePicker Label="From Date" @bind-Date="filterFromDate" />
                </MudItem>
                <MudItem xs="12" sm="6" md="3">
                    <MudDatePicker Label="To Date" @bind-Date="filterToDate" />
                </MudItem>
                <MudItem xs="12" sm="6" md="3">
                    <MudSelect Label="Format" @bind-SelectedValue="selectedFormat" MultiSelection="true">
                        @foreach (var format in Enum.GetValues<FileFormat>())
                        {
                            <MudSelectItem Value="@format">@format</MudSelectItem>
                        }
                    </MudSelect>
                </MudItem>
                <MudItem xs="12" sm="6" md="3">
                    <MudSelect Label="Status" @bind-SelectedValue="selectedStatus" MultiSelection="true">
                        <MudSelectItem Value="Success">Success</MudSelectItem>
                        <MudSelectItem Value="Duplicate">Duplicate</MudSelectItem>
                        <MudSelectItem Value="Failed">Failed</MudSelectItem>
                    </MudSelect>
                </MudItem>
            </MudGrid>
        </MudExpansionPanel>
    </MudExpansionPanels>
</MudContainer>

@code {
    private List<FileMetadata> downloads = new();
    private List<FileMetadata> filteredDownloads = new();
    private string searchText = string.Empty;
    private DateTime? filterFromDate;
    private DateTime? filterToDate;
    private FileFormat? selectedFormat;
    private string? selectedStatus;
    private bool isDownloading = false;
    private string automationStatus = "Idle";
    private DateTime? lastRunTime;
    private double successRate = 0;
    
    private int totalDownloads => downloads.Count;
    private int duplicateCount => downloads.Count(d => d.Status == "Duplicate");
    private int failedDownloads => downloads.Count(d => d.Status == "Failed");
    private double failedPercentage => totalDownloads > 0 ? (failedDownloads * 100.0 / totalDownloads) : 0;
    private long totalStorageUsed => downloads.Sum(d => d.FileSize);
    private double averageFileSize => totalDownloads > 0 ? downloads.Average(d => d.FileSize) : 0;
    private int pendingProcessing => downloads.Count(d => d.Status == "Success" && !d.IsProcessed);

    protected override async Task OnInitializedAsync()
    {
        await LoadDownloadHistory();
    }

    private async Task LoadDownloadHistory()
    {
        // Load download history from FileMetadataLogger
        // Implementation depends on your service interface
    }

    private bool FilterDownloads(FileMetadata file)
    {
        if (!string.IsNullOrEmpty(searchText) && 
            !file.FileName.Contains(searchText, StringComparison.OrdinalIgnoreCase) &&
            !(file.Url?.Contains(searchText, StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return false;
        }
        
        if (filterFromDate.HasValue && file.DownloadTimestamp < filterFromDate.Value)
            return false;
            
        if (filterToDate.HasValue && file.DownloadTimestamp > filterToDate.Value)
            return false;
            
        if (selectedFormat.HasValue && file.Format != selectedFormat.Value)
            return false;
            
        if (!string.IsNullOrEmpty(selectedStatus) && file.Status != selectedStatus)
            return false;
            
        return true;
    }

    private async Task TriggerManualDownload()
    {
        isDownloading = true;
        StateHasChanged();
        
        try
        {
            // Trigger manual download via DocumentIngestionService
            // Implementation depends on your service interface
            await Task.Delay(1000); // Placeholder
        }
        finally
        {
            isDownloading = false;
            await LoadDownloadHistory();
            StateHasChanged();
        }
    }

    private void ViewMetadata(FileMetadata file)
    {
        // Navigate to metadata viewer or open dialog
    }

    private void DownloadFile(FileMetadata file)
    {
        // Trigger file download
    }

    private void ViewProcessingStatus(FileMetadata file)
    {
        // Navigate to processing status page
    }

    private void FilterByStatus(string status)
    {
        selectedStatus = status;
        StateHasChanged();
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

    private string TruncateUrl(string url, int maxLength)
    {
        return url.Length > maxLength ? url.Substring(0, maxLength) + "..." : url;
    }

    private string GetRelativeTime(DateTime timestamp)
    {
        var span = DateTime.Now - timestamp;
        if (span.TotalMinutes < 1) return "Just now";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes}m ago";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours}h ago";
        return $"{(int)span.TotalDays}d ago";
    }

    private string GetFormatIcon(FileFormat format) => format switch
    {
        FileFormat.PDF => Icons.Material.Filled.PictureAsPdf,
        FileFormat.XML => Icons.Material.Filled.Code,
        FileFormat.DOCX => Icons.Material.Filled.Description,
        FileFormat.ZIP => Icons.Material.Filled.Archive,
        _ => Icons.Material.Filled.InsertDriveFile
    };

    private Color GetFormatColor(FileFormat format) => format switch
    {
        FileFormat.PDF => Color.Error,
        FileFormat.XML => Color.Info,
        FileFormat.DOCX => Color.Primary,
        FileFormat.ZIP => Color.Warning,
        _ => Color.Default
    };

    private Color GetStatusColor(string status) => status switch
    {
        "Success" => Color.Success,
        "Duplicate" => Color.Info,
        "Failed" => Color.Error,
        _ => Color.Default
    };

    private string GetStatusIcon(string status) => status switch
    {
        "Success" => Icons.Material.Filled.CheckCircle,
        "Duplicate" => Icons.Material.Filled.ContentCopy,
        "Failed" => Icons.Material.Filled.Error,
        _ => Icons.Material.Filled.Help
    };

    private Color GetSuccessRateColor(double rate) => rate switch
    {
        >= 95 => Color.Success,
        >= 80 => Color.Warning,
        _ => Color.Error
    };
}
```

#### 2. File Metadata Viewer Component

**Location:** `Components/Shared/FileMetadataViewer.razor`  
**Purpose:** Display detailed file metadata in a dialog or dedicated page

**Features:**
- File details card (all FileMetadata properties)
- Related documents link (if file was processed)
- Download history (show if file was previously downloaded)
- Checksum verification status
- Processing pipeline status

---

## Story 1.2: Enhanced Metadata Extraction and File Classification - UI Enhancements

### Current State Analysis

**Backend Status:** ✅ Complete (QA Approved)  
**UI Status:** ⚠️ **Missing** - No UI to display classification results

**Gaps Identified:**
- No display of classification results (Level 1/Level 2 categories)
- No confidence score visualization
- No metadata extraction details (XML/DOCX/PDF extraction)
- No processing stage status indicator
- No classification decision audit trail

### Required UI Components

#### 1. Classification Results Display Component

**Location:** `Components/Shared/ClassificationResultsCard.razor`  
**Purpose:** Show document classification with confidence scores and detailed breakdown

**Features:**

**A. Classification Summary Card**
- **Level 1 Category:**
  - Large badge with category name (Aseguramiento, Desembargo, Documentacion, etc.)
  - Color-coded by category type
  - Icon representing category

- **Level 2/3 Subcategory:**
  - Smaller badge showing subcategory (Especial, Judicial, Hacendario)
  - Only shown if applicable

- **Confidence Score:**
  - Circular progress indicator (0-100%)
  - Color coding: Green (>90%), Yellow (80-90%), Orange (70-80%), Red (<70%)
  - Large percentage display

- **Classification Scores Breakdown:**
  - List showing individual category scores
  - Progress bars for each category
  - Tooltip showing scoring rationale

**B. Metadata Extraction Details Tabs**
- **Tabbed View:**
  - XML Metadata tab
  - DOCX Metadata tab
  - PDF Metadata tab
  - Classification tab

- **Per-Format Display:**
  - Extracted fields with values
  - Source indicators
  - Extraction confidence per field
  - Highlight source of each field value

**C. Processing Stage Pipeline**
- **Visual Pipeline:**
  - Stage 1: Ingestion (Browser automation and file download)
  - Stage 2: Extraction (Metadata extraction and classification)
  - Stage 3: Decision Logic (Identity resolution and compliance actions)

- **Status Indicators:**
  - Completed (green checkmark)
  - In Progress (spinner)
  - Pending (gray)
  - Failed (red X)

- **Timestamps:**
  - Completion time for each stage
  - Click to view stage details

**Implementation Code:**

```razor
@using ExxerCube.Prisma.Domain.Entities
@using ExxerCube.Prisma.Application.Services

<MudCard Class="mb-4">
    <MudCardHeader>
        <MudText Typo="Typo.h6">
            <MudIcon Icon="@Icons.Material.Filled.Category" Class="mr-2" />
            Document Classification
        </MudText>
    </MudCardHeader>
    <MudCardContent>
        <MudGrid>
            <!-- Level 1 Category -->
            <MudItem xs="12" md="6">
                <MudText Typo="Typo.subtitle1" Class="mb-2">Level 1 Category</MudText>
                <MudChip Size="Size.Large" 
                        Color="@GetCategoryColor(Classification.Level1)"
                        Icon="@GetCategoryIcon(Classification.Level1)">
                    @Classification.Level1
                </MudChip>
            </MudItem>
            
            <!-- Confidence Score -->
            <MudItem xs="12" md="6">
                <MudText Typo="Typo.subtitle1" Class="mb-2">Confidence Score</MudText>
                <div class="d-flex align-center">
                    <MudProgressCircular Value="@Classification.Confidence" 
                                       Color="@GetConfidenceColor(Classification.Confidence)"
                                       Size="Size.Large" 
                                       Class="mr-3" />
                    <MudText Typo="Typo.h5">@Classification.Confidence%</MudText>
                </div>
            </MudItem>
            
            <!-- Level 2/3 Subcategory -->
            @if (Classification.Level2.HasValue)
            {
                <MudItem xs="12">
                    <MudText Typo="Typo.subtitle1" Class="mb-2">Level 2/3 Subcategory</MudText>
                    <MudChip Size="Size.Medium" Color="Color.Secondary">
                        @Classification.Level2
                    </MudChip>
                </MudItem>
            }
        </MudGrid>
        
        <!-- Classification Scores Breakdown -->
        <MudDivider Class="my-4" />
        <MudText Typo="Typo.subtitle2" Class="mb-3">Score Breakdown</MudText>
        <MudList>
            @foreach (var score in GetScoreBreakdown())
            {
                <MudListItem>
                    <MudGrid>
                        <MudItem xs="12" sm="4">
                            <MudText Typo="Typo.body2">@score.Category</MudText>
                        </MudItem>
                        <MudItem xs="12" sm="8">
                            <MudProgressLinear Value="@score.Value" 
                                              Color="@GetScoreColor(score.Value)" 
                                              Class="mt-1" />
                            <MudText Typo="Typo.caption" Color="Color.Secondary">
                                @score.Value%
                            </MudText>
                        </MudItem>
                    </MudGrid>
                </MudListItem>
            }
        </MudList>
    </MudCardContent>
</MudCard>

<!-- Processing Stage Pipeline -->
<MudCard Class="mb-4">
    <MudCardHeader>
        <MudText Typo="Typo.h6">Processing Pipeline</MudText>
    </MudCardHeader>
    <MudCardContent>
        <MudStepper @bind-ActiveStepIndex="@currentStage" Linear="false">
            <MudStep Title="Stage 1: Ingestion" Completed="@(currentStage >= 0)">
                <MudText Typo="Typo.body2">Browser automation and file download</MudText>
                @if (currentStage >= 0)
                {
                    <MudChip Size="Size.Small" Color="Color.Success" Class="mt-2">
                        <MudIcon Icon="@Icons.Material.Filled.Check" Size="Size.Small" Class="mr-1" />
                        Completed @ingestionTimestamp?.ToString("g")
                    </MudChip>
                }
            </MudStep>
            <MudStep Title="Stage 2: Extraction" Completed="@(currentStage >= 1)">
                <MudText Typo="Typo.body2">Metadata extraction and classification</MudText>
                @if (currentStage >= 1)
                {
                    <MudChip Size="Size.Small" Color="Color.Success" Class="mt-2">
                        <MudIcon Icon="@Icons.Material.Filled.Check" Size="Size.Small" Class="mr-1" />
                        Completed @extractionTimestamp?.ToString("g")
                    </MudChip>
                }
                else if (currentStage == 0)
                {
                    <MudChip Size="Size.Small" Color="Color.Info" Class="mt-2">
                        <MudProgressCircular Size="Size.Small" Indeterminate="true" Class="mr-1" />
                        In Progress
                    </MudChip>
                }
            </MudStep>
            <MudStep Title="Stage 3: Decision Logic" Completed="@(currentStage >= 2)">
                <MudText Typo="Typo.body2">Identity resolution and compliance actions</MudText>
                @if (currentStage >= 2)
                {
                    <MudChip Size="Size.Small" Color="Color.Success" Class="mt-2">
                        <MudIcon Icon="@Icons.Material.Filled.Check" Size="Size.Small" Class="mr-1" />
                        Completed @decisionTimestamp?.ToString("g")
                    </MudChip>
                }
                else
                {
                    <MudChip Size="Size.Small" Color="Color.Default" Class="mt-2">
                        Pending
                    </MudChip>
                }
            </MudStep>
        </MudStepper>
    </MudCardContent>
</MudCard>

<!-- Metadata Extraction Details Tabs -->
<MudCard>
    <MudCardHeader>
        <MudText Typo="Typo.h6">Metadata Extraction Details</MudText>
    </MudCardHeader>
    <MudCardContent>
        <MudTabs @bind-ActivePanelIndex="@activeTabIndex">
            <MudTabPanel Text="XML Metadata">
                @if (XmlMetadata != null)
                {
                    <MudTable Items="@GetXmlFields()" Dense="true" Hover="true">
                        <HeaderContent>
                            <MudTh>Field</MudTh>
                            <MudTh>Value</MudTh>
                            <MudTh>Confidence</MudTh>
                        </HeaderContent>
                        <RowTemplate>
                            <MudTd>@context.FieldName</MudTd>
                            <MudTd>@context.Value</MudTd>
                            <MudTd>
                                <MudProgressCircular Value="@context.Confidence" 
                                                   Size="Size.Small"
                                                   Color="@GetConfidenceColor(context.Confidence)" />
                                <MudText Typo="Typo.caption">@context.Confidence%</MudText>
                            </MudTd>
                        </RowTemplate>
                    </MudTable>
                }
                else
                {
                    <MudAlert Severity="Severity.Info">No XML metadata available</MudAlert>
                }
            </MudTabPanel>
            <MudTabPanel Text="DOCX Metadata">
                @if (DocxMetadata != null)
                {
                    <!-- Similar table structure for DOCX -->
                }
                else
                {
                    <MudAlert Severity="Severity.Info">No DOCX metadata available</MudAlert>
                }
            </MudTabPanel>
            <MudTabPanel Text="PDF Metadata">
                @if (PdfMetadata != null)
                {
                    <!-- Similar table structure for PDF -->
                }
                else
                {
                    <MudAlert Severity="Severity.Info">No PDF metadata available</MudAlert>
                }
            </MudTabPanel>
            <MudTabPanel Text="Classification Details">
                <MudExpansionPanels>
                    <MudExpansionPanel Text="Classification Rules Applied">
                        <MudList>
                            @foreach (var rule in Classification.Scores.GetAppliedRules())
                            {
                                <MudListItem>
                                    <MudText>@rule.Description</MudText>
                                    <MudText Typo="Typo.caption" Color="Color.Secondary">
                                        Score: @rule.Score
                                    </MudText>
                                </MudListItem>
                            }
                        </MudList>
                    </MudExpansionPanel>
                </MudExpansionPanels>
            </MudTabPanel>
        </MudTabs>
    </MudCardContent>
</MudCard>

@code {
    [Parameter] public ClassificationResult Classification { get; set; } = default!;
    [Parameter] public ExtractedMetadata? XmlMetadata { get; set; }
    [Parameter] public ExtractedMetadata? DocxMetadata { get; set; }
    [Parameter] public ExtractedMetadata? PdfMetadata { get; set; }
    [Parameter] public int CurrentStage { get; set; } = 0;
    [Parameter] public DateTime? IngestionTimestamp { get; set; }
    [Parameter] public DateTime? ExtractionTimestamp { get; set; }
    [Parameter] public DateTime? DecisionTimestamp { get; set; }

    private int activeTabIndex = 0;
    private int currentStage = 0;

    private Color GetCategoryColor(ClassificationLevel1 category) => category switch
    {
        ClassificationLevel1.Aseguramiento => Color.Error,
        ClassificationLevel1.Desembargo => Color.Warning,
        ClassificationLevel1.Documentacion => Color.Info,
        ClassificationLevel1.Informacion => Color.Primary,
        ClassificationLevel1.Transferencia => Color.Secondary,
        ClassificationLevel1.OperacionesIlicitas => Color.Dark,
        _ => Color.Default
    };

    private string GetCategoryIcon(ClassificationLevel1 category) => category switch
    {
        ClassificationLevel1.Aseguramiento => Icons.Material.Filled.Lock,
        ClassificationLevel1.Desembargo => Icons.Material.Filled.LockOpen,
        ClassificationLevel1.Documentacion => Icons.Material.Filled.Description,
        ClassificationLevel1.Informacion => Icons.Material.Filled.Info,
        ClassificationLevel1.Transferencia => Icons.Material.Filled.SwapHoriz,
        ClassificationLevel1.OperacionesIlicitas => Icons.Material.Filled.Warning,
        _ => Icons.Material.Filled.Help
    };

    private Color GetConfidenceColor(int confidence) => confidence switch
    {
        >= 90 => Color.Success,
        >= 80 => Color.Warning,
        >= 70 => Color.Info,
        _ => Color.Error
    };

    private Color GetScoreColor(int score) => score switch
    {
        >= 80 => Color.Success,
        >= 60 => Color.Warning,
        _ => Color.Error
    };

    private List<(string Category, int Value)> GetScoreBreakdown()
    {
        // Extract score breakdown from Classification.Scores
        // Implementation depends on ClassificationScores structure
        return new();
    }

    private List<(string FieldName, string Value, int Confidence)> GetXmlFields()
    {
        // Extract fields from XmlMetadata
        // Implementation depends on ExtractedMetadata structure
        return new();
    }
}
```

---

## Story 1.3: Field Matching and Unified Metadata Generation - UI Enhancements

### Current State Analysis

**Backend Status:** ✅ Complete (QA Approved)  
**UI Status:** ⚠️ **Missing** - No UI to display field matching results

**Gaps Identified:**
- No display of field matching across XML/DOCX/PDF sources
- No visualization of field conflicts or agreements
- No unified metadata record display
- No confidence score visualization per field
- No source comparison view

### Required UI Components

#### 1. Field Matching Visualization Component

**Location:** `Components/Shared/FieldMatchingView.razor`  
**Purpose:** Show field matching results with source comparison and conflict detection

**Features:**

**A. Source Comparison Table**
- **Columns:**
  - Field Name
  - XML Value (with source indicator)
  - DOCX Value (with source indicator)
  - PDF Value (with source indicator)
  - Unified Value (bold, with source badge)
  - Confidence Score (circular progress)
  - Status Badge (Agreed/Conflict/Missing)

- **Color Coding:**
  - Green row: All sources agree
  - Yellow row: Sources conflict (warning icon)
  - Gray row: Field missing from some sources

- **Expandable Rows:**
  - Click to see detailed matching logic
  - Show confidence calculation details
  - Show source priority explanation

**B. Field Agreement Summary Card**
- **Metrics:**
  - Overall agreement percentage (0-100%)
  - Conflicting fields count
  - Missing fields count
  - Agreed fields count

- **Visual Indicators:**
  - Large progress bar showing agreement level
  - Color-coded metrics (green/yellow/red)
  - Clickable metrics that filter the table

**C. Unified Metadata Record Display**
- **Tabbed View:**
  - Expediente tab
  - Personas tab
  - Oficio tab
  - Requirements tab
  - Matched Fields tab

- **Field-Level Annotations:**
  - Source indicator (XML/DOCX/PDF icon)
  - Confidence badge
  - Conflict warning if applicable
  - Tooltip showing all source values

**Implementation Code:**

```razor
@using ExxerCube.Prisma.Domain.Entities
@using ExxerCube.Prisma.Application.Services

<MudCard Class="mb-4">
    <MudCardHeader>
        <MudText Typo="Typo.h6">
            <MudIcon Icon="@Icons.Material.Filled.CompareArrows" Class="mr-2" />
            Field Matching Results
        </MudText>
    </MudCardHeader>
    <MudCardContent>
        <!-- Field Agreement Summary -->
        <MudGrid Class="mb-4">
            <MudItem xs="12">
                <MudText Typo="Typo.subtitle1" Class="mb-2">Overall Agreement</MudText>
                <MudProgressLinear Value="@MatchedFields.OverallAgreement * 100" 
                                  Color="@GetAgreementColor(MatchedFields.OverallAgreement)"
                                  Class="mt-2" />
                <MudText Typo="Typo.h5">@((MatchedFields.OverallAgreement * 100).ToString("F1"))%</MudText>
            </MudItem>
            <MudItem xs="12" sm="4">
                <MudCard OnClick="@(() => FilterByStatus("Conflict"))" Class="cursor-pointer">
                    <MudCardContent>
                        <MudText Typo="Typo.h4" Color="Color.Warning">
                            @MatchedFields.ConflictingFields.Count
                        </MudText>
                        <MudText Typo="Typo.body2">Conflicting Fields</MudText>
                    </MudCardContent>
                </MudCard>
            </MudItem>
            <MudItem xs="12" sm="4">
                <MudCard OnClick="@(() => FilterByStatus("Missing"))" Class="cursor-pointer">
                    <MudCardContent>
                        <MudText Typo="Typo.h4" Color="Color.Default">
                            @MatchedFields.MissingFields.Count
                        </MudText>
                        <MudText Typo="Typo.body2">Missing Fields</MudText>
                    </MudCardContent>
                </MudCard>
            </MudItem>
            <MudItem xs="12" sm="4">
                <MudCard OnClick="@(() => FilterByStatus("Agreed"))" Class="cursor-pointer">
                    <MudCardContent>
                        <MudText Typo="Typo.h4" Color="Color.Success">
                            @AgreedFieldsCount
                        </MudText>
                        <MudText Typo="Typo.body2">Agreed Fields</MudText>
                    </MudCardContent>
                </MudCard>
            </MudItem>
        </MudGrid>

        <!-- Source Comparison Table -->
        <MudTable Items="@FilteredFieldMatches" 
                 Dense="true" 
                 Hover="true" 
                 Striped="true"
                 Filter="new Func<FieldMatchRow, bool>(FilterFields)">
            <HeaderContent>
                <MudTh>
                    <MudTableSortLabel SortBy="@new Func<FieldMatchRow, object>(x => x.FieldName)">
                        Field Name
                    </MudTableSortLabel>
                </MudTh>
                <MudTh>XML</MudTh>
                <MudTh>DOCX</MudTh>
                <MudTh>PDF</MudTh>
                <MudTh>Unified Value</MudTh>
                <MudTh>
                    <MudTableSortLabel SortBy="@new Func<FieldMatchRow, object>(x => x.Confidence)">
                        Confidence
                    </MudTableSortLabel>
                </MudTh>
                <MudTh>Status</MudTh>
                <MudTh>Details</MudTh>
            </HeaderContent>
            <RowTemplate>
                <MudTd DataLabel="Field Name">
                    <MudText Typo="Typo.body2" Class="font-weight-medium">
                        @context.FieldName
                    </MudText>
                </MudTd>
                <MudTd DataLabel="XML">
                    <div class="d-flex align-center">
                        @if (!string.IsNullOrEmpty(context.XmlValue))
                        {
                            <MudIcon Icon="@Icons.Material.Filled.CheckCircle" 
                                   Size="Size.Small" 
                                   Color="Color.Success" 
                                   Class="mr-1" />
                            <MudText Typo="Typo.body2">@context.XmlValue</MudText>
                        }
                        else
                        {
                            <MudText Typo="Typo.body2" Color="Color.Secondary">-</MudText>
                        }
                    </div>
                </MudTd>
                <MudTd DataLabel="DOCX">
                    <div class="d-flex align-center">
                        @if (!string.IsNullOrEmpty(context.DocxValue))
                        {
                            <MudIcon Icon="@Icons.Material.Filled.CheckCircle" 
                                   Size="Size.Small" 
                                   Color="Color.Success" 
                                   Class="mr-1" />
                            <MudText Typo="Typo.body2">@context.DocxValue</MudText>
                        }
                        else
                        {
                            <MudText Typo="Typo.body2" Color="Color.Secondary">-</MudText>
                        }
                    </div>
                </MudTd>
                <MudTd DataLabel="PDF">
                    <div class="d-flex align-center">
                        @if (!string.IsNullOrEmpty(context.PdfValue))
                        {
                            <MudIcon Icon="@Icons.Material.Filled.CheckCircle" 
                                   Size="Size.Small" 
                                   Color="Color.Success" 
                                   Class="mr-1" />
                            <MudText Typo="Typo.body2">@context.PdfValue</MudText>
                        }
                        else
                        {
                            <MudText Typo="Typo.body2" Color="Color.Secondary">-</MudText>
                        }
                    </div>
                </MudTd>
                <MudTd DataLabel="Unified Value">
                    <div class="d-flex align-center">
                        <MudText Typo="Typo.body2" Class="font-weight-bold">
                            @context.UnifiedValue
                        </MudText>
                        @if (!string.IsNullOrEmpty(context.Source))
                        {
                            <MudChip Size="Size.Small" Color="Color.Info" Class="ml-2">
                                @context.Source
                            </MudChip>
                        }
                    </div>
                </MudTd>
                <MudTd DataLabel="Confidence">
                    <div class="d-flex align-center">
                        <MudProgressCircular Value="@context.Confidence" 
                                           Size="Size.Small"
                                           Color="@GetConfidenceColor(context.Confidence)" />
                        <MudText Typo="Typo.caption" Class="ml-2">
                            @context.Confidence%
                        </MudText>
                    </div>
                </MudTd>
                <MudTd DataLabel="Status">
                    @if (context.HasConflict)
                    {
                        <MudChip Size="Size.Small" 
                                Color="Color.Warning" 
                                Icon="@Icons.Material.Filled.Warning">
                            Conflict
                        </MudChip>
                    }
                    else if (context.IsMissing)
                    {
                        <MudChip Size="Size.Small" Color="Color.Default">
                            Missing
                        </MudChip>
                    }
                    else
                    {
                        <MudChip Size="Size.Small" 
                                Color="Color.Success" 
                                Icon="@Icons.Material.Filled.Check">
                            Agreed
                        </MudChip>
                    }
                </MudTd>
                <MudTd DataLabel="Details">
                    <MudIconButton Icon="@Icons.Material.Filled.Info" 
                                  Size="Size.Small"
                                  OnClick="@(() => ShowFieldDetails(context))" />
                </MudTd>
            </RowTemplate>
            <PagerContent>
                <MudTablePager />
            </PagerContent>
        </MudTable>
    </MudCardContent>
</MudCard>

<!-- Unified Metadata Record Display -->
<MudCard>
    <MudCardHeader>
        <MudText Typo="Typo.h6">
            <MudIcon Icon="@Icons.Material.Filled.Folder" Class="mr-2" />
            Unified Metadata Record
        </MudText>
    </MudCardHeader>
    <MudCardContent>
        <MudTabs @bind-ActivePanelIndex="@activeTabIndex">
            <MudTabPanel Text="Expediente">
                <MudGrid>
                    @foreach (var field in GetExpedienteFields())
                    {
                        <MudItem xs="12" sm="6" md="4">
                            <MudTextField Label="@field.Label" 
                                         Value="@field.Value"
                                         Variant="Variant.Outlined"
                                         ReadOnly="true">
                                <Adornment Start>
                                    <MudChip Size="Size.Small" Color="Color.Info">
                                        @field.Source
                                    </MudChip>
                                </Adornment>
                                <Adornment End>
                                    <MudProgressCircular Value="@field.Confidence" 
                                                       Size="Size.Small" />
                                </Adornment>
                            </MudTextField>
                        </MudItem>
                    }
                </MudGrid>
            </MudTabPanel>
            <MudTabPanel Text="Personas">
                @foreach (var persona in UnifiedRecord.Personas)
                {
                    <MudCard Class="mb-3">
                        <MudCardContent>
                            <MudGrid>
                                <MudItem xs="12" sm="6">
                                    <MudText Typo="Typo.subtitle2">Name</MudText>
                                    <MudText Typo="Typo.body1">@persona.FullName</MudText>
                                </MudItem>
                                <MudItem xs="12" sm="6">
                                    <MudText Typo="Typo.subtitle2">RFC</MudText>
                                    <MudText Typo="Typo.body1">@persona.Rfc</MudText>
                                </MudItem>
                            </MudGrid>
                        </MudCardContent>
                    </MudCard>
                }
            </MudTabPanel>
            <MudTabPanel Text="Oficio">
                <!-- Oficio fields display -->
            </MudTabPanel>
            <MudTabPanel Text="Requirements">
                <!-- Requirements display -->
            </MudTabPanel>
            <MudTabPanel Text="Matched Fields">
                <!-- Detailed matched fields view -->
            </MudTabPanel>
        </MudTabs>
    </MudCardContent>
</MudCard>

<!-- Field Details Dialog -->
<MudDialog @bind-IsVisible="@showFieldDetailsDialog">
    <TitleContent>
        <MudText Typo="Typo.h6">Field Details: @selectedField?.FieldName</MudText>
    </TitleContent>
    <DialogContent>
        @if (selectedField != null)
        {
            <MudGrid>
                <MudItem xs="12">
                    <MudText Typo="Typo.subtitle1">Source Values</MudText>
                    <MudList>
                        @if (!string.IsNullOrEmpty(selectedField.XmlValue))
                        {
                            <MudListItem>
                                <MudText>XML: @selectedField.XmlValue</MudText>
                            </MudListItem>
                        }
                        @if (!string.IsNullOrEmpty(selectedField.DocxValue))
                        {
                            <MudListItem>
                                <MudText>DOCX: @selectedField.DocxValue</MudText>
                            </MudListItem>
                        }
                        @if (!string.IsNullOrEmpty(selectedField.PdfValue))
                        {
                            <MudListItem>
                                <MudText>PDF: @selectedField.PdfValue</MudText>
                            </MudListItem>
                        }
                    </MudList>
                </MudItem>
                <MudItem xs="12">
                    <MudText Typo="Typo.subtitle1">Matching Logic</MudText>
                    <MudText Typo="Typo.body2">@selectedField.MatchingLogic</MudText>
                </MudItem>
                <MudItem xs="12">
                    <MudText Typo="Typo.subtitle1">Confidence Calculation</MudText>
                    <MudText Typo="Typo.body2">@selectedField.ConfidenceExplanation</MudText>
                </MudItem>
            </MudGrid>
        }
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => showFieldDetailsDialog = false)">Close</MudButton>
    </DialogActions>
</MudDialog>

@code {
    [Parameter] public MatchedFields MatchedFields { get; set; } = default!;
    [Parameter] public UnifiedMetadataRecord UnifiedRecord { get; set; } = default!;

    private List<FieldMatchRow> fieldMatches = new();
    private List<FieldMatchRow> filteredFieldMatches = new();
    private int activeTabIndex = 0;
    private bool showFieldDetailsDialog = false;
    private FieldMatchRow? selectedField;
    private string? filterStatus;

    private int AgreedFieldsCount => fieldMatches.Count(f => !f.HasConflict && !f.IsMissing);

    protected override void OnParametersSet()
    {
        LoadFieldMatches();
    }

    private void LoadFieldMatches()
    {
        fieldMatches = MatchedFields.FieldMatches.Select(kvp => new FieldMatchRow
        {
            FieldName = kvp.Key,
            MatchResult = kvp.Value,
            // Populate from match result
        }).ToList();
        
        filteredFieldMatches = fieldMatches;
    }

    private bool FilterFields(FieldMatchRow field)
    {
        if (!string.IsNullOrEmpty(filterStatus))
        {
            return filterStatus switch
            {
                "Conflict" => field.HasConflict,
                "Missing" => field.IsMissing,
                "Agreed" => !field.HasConflict && !field.IsMissing,
                _ => true
            };
        }
        return true;
    }

    private void FilterByStatus(string status)
    {
        filterStatus = filterStatus == status ? null : status;
        filteredFieldMatches = fieldMatches.Where(FilterFields).ToList();
        StateHasChanged();
    }

    private void ShowFieldDetails(FieldMatchRow field)
    {
        selectedField = field;
        showFieldDetailsDialog = true;
    }

    private Color GetAgreementColor(double agreement) => agreement switch
    {
        >= 0.9 => Color.Success,
        >= 0.7 => Color.Warning,
        _ => Color.Error
    };

    private Color GetConfidenceColor(int confidence) => confidence switch
    {
        >= 90 => Color.Success,
        >= 70 => Color.Warning,
        _ => Color.Error
    };

    private List<(string Label, string Value, string Source, int Confidence)> GetExpedienteFields()
    {
        // Extract expediente fields from UnifiedRecord
        return new();
    }

    private class FieldMatchRow
    {
        public string FieldName { get; set; } = string.Empty;
        public string? XmlValue { get; set; }
        public string? DocxValue { get; set; }
        public string? PdfValue { get; set; }
        public string UnifiedValue { get; set; } = string.Empty;
        public string? Source { get; set; }
        public int Confidence { get; set; }
        public bool HasConflict { get; set; }
        public bool IsMissing { get; set; }
        public FieldMatchResult MatchResult { get; set; } = default!;
        public string MatchingLogic { get; set; } = string.Empty;
        public string ConfidenceExplanation { get; set; } = string.Empty;
    }
}
```

---

## Consolidated Implementation Plan

### Priority 1: Critical UI Components (Required for Story Completion)

1. **Document Ingestion Dashboard** (Story 1.1)
   - Download history table with filtering and sorting
   - Statistics cards with clickable filters
   - Browser automation status panel
   - Real-time download feed

2. **Classification Results Display** (Story 1.2)
   - Classification summary card with confidence scores
   - Processing stage pipeline visualization
   - Metadata extraction details tabs
   - Classification scores breakdown

3. **Field Matching Visualization** (Story 1.3)
   - Source comparison table
   - Field agreement summary card
   - Unified metadata record display
   - Field details dialog

### Priority 2: Supporting UI Components (Enhancement)

1. **File Metadata Viewer** (Story 1.1)
   - Standalone component for viewing file metadata
   - Integration with download history

2. **Review Case Detail Enhancement** (All Stories)
   - Integrate all three components into case detail view
   - Add navigation between related documents

### Implementation Order

**Phase 1:** Story 1.1 UI Components
- Document Ingestion Dashboard
- File Metadata Viewer

**Phase 2:** Story 1.2 UI Components
- Classification Results Display
- Processing Pipeline Visualization

**Phase 3:** Story 1.3 UI Components
- Field Matching Visualization
- Unified Metadata Record Display

**Phase 4:** Integration
- Integrate all components into Review Case Detail
- Add navigation and linking between components

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
- **Progress Indicators:** Use MudProgressLinear and MudProgressCircular for confidence/agreement visualization
- **Dialogs:** Use MudDialog for detailed views and confirmations

---

## User Experience Considerations

### Information Hierarchy
1. **Primary Information:** Classification results, compliance actions (most important)
2. **Secondary Information:** Field matching details, download history (supporting context)
3. **Tertiary Information:** Source comparison, detailed metadata (expandable views)

### Progressive Disclosure
- Show summary information by default
- Allow expansion for detailed views
- Use tabs/accordions for complex information
- Provide tooltips for technical terms

### Real-time Updates
- Use SignalR for real-time updates where applicable
- Show loading states during data fetch
- Provide error states with retry options
- Visual indicators for new/updated data

### Accessibility
- Ensure proper ARIA labels for all interactive elements
- Support keyboard navigation throughout
- Provide color-blind friendly indicators (use icons + colors)
- Ensure sufficient contrast ratios (WCAG 2.1 AA compliance)
- Screen reader announcements for status changes

### Performance
- Virtual scrolling for large tables
- Lazy loading of detailed views
- Debounced filter inputs
- Cached filter results
- Progressive loading (load summary first, details on demand)

---

## Testing Considerations

### UX Testing
- User testing with compliance officers and analysts
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
- Load time for large datasets
- Filter/search responsiveness
- Real-time update performance
- Memory usage with many records

---

## Next Steps

1. **Review this document** with Product Owner and Development Team
2. **Prioritize UI components** based on user needs and story dependencies
3. **Create detailed wireframes** for each component (if needed)
4. **Implement UI components** following MudBlazor patterns and this specification
5. **Test UI components** with end users for usability
6. **Update story status** once UI components are complete

---

## Related Documentation

- [Front-End Specification](front-end-spec.md) - Overall UI/UX architecture
- [UI Requirements Analysis](ui-requirements-stories-1.1-1.4.md) - Comprehensive UI requirements
- [Story 1.1](../stories/1.1.browser-automation-document-download.md) - Browser automation story
- [Story 1.2](../stories/1.2.enhanced-metadata-extraction-classification.md) - Metadata extraction story
- [Story 1.3](../stories/1.3.field-matching-unified-metadata.md) - Field matching story

---

**Document Status:** ✅ **Complete**  
**Ready for Implementation** 🚀

