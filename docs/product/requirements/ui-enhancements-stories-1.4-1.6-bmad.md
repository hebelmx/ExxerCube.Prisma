# UI/UX Enhancements: Stories 1.4-1.6 (BMAD Agent)

**Component:** Identity Resolution Display, SLA Dashboard, Manual Review Interface Enhancements  
**Stories:** 1.4 - Identity Resolution, 1.5 - SLA Tracking, 1.6 - Manual Review  
**Status:** UX Enhancement Recommendations  
**Created:** 2025-01-15  
**Author:** BMAD Agent (Browser-based Markdown Documentation)

---

## Executive Summary

Stories 1.4, 1.5, and 1.6 deliver robust backend functionality for identity resolution, legal classification, SLA tracking, and manual review workflows. However, **critical UI components are missing or need enhancement** to expose these capabilities to end users. This document provides comprehensive UI/UX enhancement specifications to transform backend functionality into intuitive, user-friendly interfaces.

**Current State:** 
- Story 1.4: Backend complete ✅ | UI Status: ⚠️ **Missing**
- Story 1.5: Backend complete ✅ | UI Status: ⚠️ **Missing** (AC5 not implemented)
- Story 1.6: Backend complete ✅ | UI Status: ⚠️ **Partial** (needs enhancements)

---

## Story 1.4: Identity Resolution and Legal Directive Classification - UI Enhancements

### Current State Analysis

**Backend Status:** ✅ Complete (QA Approved)  
**UI Status:** ⚠️ **Missing** - No UI to display identity resolution and compliance actions

**Gaps Identified:**
- No display of resolved person identities
- No visualization of RFC variant matching
- No display of legal directive classification
- No compliance action mapping visualization
- No identity deduplication status display

### Required UI Components

#### 1. Identity Resolution Display Component

**Location:** `Components/Shared/IdentityResolutionView.razor` or integrate into `ReviewCaseDetail.razor`  
**Purpose:** Show resolved person identities with RFC variants and deduplication status

**Features:**

**A. Person Identity Cards**
- **Person Details:**
  - Full name (Paterno, Materno, Nombre)
  - RFC (with variant matching visualization)
  - PersonaTipo (Fisica/Moral) badge
  - Caracter (role/character) badge

- **RFC Variant Matching Visualization:**
  - Show original RFC vs matched RFC
  - Highlight differences/similarities
  - Show matching confidence score
  - List all detected RFC variants

- **Deduplication Status:**
  - Show if person was deduplicated across documents
  - Related cases link (other cases involving same person)
  - Deduplication confidence indicator

**B. Related Cases Panel**
- List of other cases involving the same person
- Quick navigation to related cases
- Person involvement count across all cases

**Implementation Code:**

```razor
@using ExxerCube.Prisma.Domain.Entities
@using ExxerCube.Prisma.Application.Services

<MudCard Class="mb-4">
    <MudCardHeader>
        <MudText Typo="Typo.h6">
            <MudIcon Icon="@Icons.Material.Filled.Person" Class="mr-2" />
            Resolved Person Identities
        </MudText>
    </MudCardHeader>
    <MudCardContent>
        @foreach (var person in ResolvedPersons)
        {
            <MudExpansionPanels Class="mb-3">
                <MudExpansionPanel Text="@person.FullName">
                    <MudGrid>
                        <!-- Person Details -->
                        <MudItem xs="12" sm="6" md="4">
                            <MudText Typo="Typo.subtitle2">RFC</MudText>
                            <MudText Typo="Typo.body1" Class="font-weight-bold">@person.Rfc</MudText>
                            @if (person.RfcVariants.Any())
                            {
                                <MudText Typo="Typo.caption" Color="Color.Secondary">
                                    @person.RfcVariants.Count variant(s) detected
                                </MudText>
                            }
                        </MudItem>
                        <MudItem xs="12" sm="6" md="4">
                            <MudText Typo="Typo.subtitle2">Type</MudText>
                            <MudChip Size="Size.Small" Color="@GetPersonTypeColor(person.PersonaTipo)">
                                @person.PersonaTipo
                            </MudChip>
                        </MudItem>
                        <MudItem xs="12" sm="6" md="4">
                            <MudText Typo="Typo.subtitle2">Role</MudText>
                            <MudChip Size="Size.Small" Color="Color.Info">
                                @person.Caracter
                            </MudChip>
                        </MudItem>
                        
                        <!-- RFC Variants -->
                        @if (person.RfcVariants.Any())
                        {
                            <MudItem xs="12">
                                <MudDivider Class="my-2" />
                                <MudText Typo="Typo.subtitle2" Class="mb-2">RFC Variants Detected</MudText>
                                <MudChipSet>
                                    @foreach (var variant in person.RfcVariants)
                                    {
                                        <MudChip Size="Size.Small" 
                                                Color="@(variant == person.Rfc ? Color.Success : Color.Info)"
                                                Icon="@(variant == person.Rfc ? Icons.Material.Filled.CheckCircle : Icons.Material.Filled.Info)">
                                            @variant
                                        </MudChip>
                                    }
                                </MudChipSet>
                                <MudText Typo="Typo.caption" Color="Color.Secondary" Class="mt-1">
                                    Matched with @GetMatchingConfidence(person) confidence
                                </MudText>
                            </MudItem>
                        }
                        
                        <!-- Deduplication Status -->
                        @if (person.IsDeduplicated)
                        {
                            <MudItem xs="12">
                                <MudDivider Class="my-2" />
                                <MudAlert Severity="Severity.Info" Variant="Variant.Filled">
                                    <MudIcon Icon="@Icons.Material.Filled.Merge" Class="mr-2" />
                                    This person was deduplicated across @person.RelatedCasesCount document(s)
                                </MudAlert>
                            </MudItem>
                        }
                        
                        <!-- Related Cases -->
                        @if (person.RelatedCases.Any())
                        {
                            <MudItem xs="12">
                                <MudDivider Class="my-2" />
                                <MudText Typo="Typo.subtitle2" Class="mb-2">Related Cases</MudText>
                                <MudList Dense="true">
                                    @foreach (var relatedCase in person.RelatedCases)
                                    {
                                        <MudListItem>
                                            <MudLink Href="@($"/review-case/{relatedCase.CaseId}")" 
                                                   Color="Color.Primary">
                                                @relatedCase.CaseId - @relatedCase.FileName
                                            </MudLink>
                                        </MudListItem>
                                    }
                                </MudList>
                            </MudItem>
                        }
                    </MudGrid>
                </MudExpansionPanel>
            </MudExpansionPanels>
        }
    </MudCardContent>
</MudCard>

@code {
    [Parameter] public List<Persona> ResolvedPersons { get; set; } = new();
    
    private Color GetPersonTypeColor(string personaTipo) => personaTipo switch
    {
        "Fisica" => Color.Primary,
        "Moral" => Color.Secondary,
        _ => Color.Default
    };
    
    private int GetMatchingConfidence(Persona person)
    {
        // Calculate matching confidence based on RFC variants
        // Implementation depends on your matching algorithm
        return 95; // Placeholder
    }
}
```

#### 2. Legal Directive Classification Display Component

**Location:** `Components/Shared/LegalDirectiveClassificationView.razor`  
**Purpose:** Show legal directive classification and compliance actions

**Features:**

**A. Legal Instrument Detection Card**
- **Detected Legal Instruments:**
  - List of detected legal instruments (e.g., "Acuerdo 105/2021")
  - Detection confidence score
  - Source text excerpt where detected
  - Link to full document text

**B. Compliance Actions List**
- **Action Cards:**
  - Action type badges (Block, Unblock, Document, Transfer, Information, Ignore)
  - Action details: Account number, product type, amount (if applicable)
  - Origin references: Expediente, Oficio, Requerimiento
  - Confidence scores per action
  - Visual indicators: Color-coded by action type
  - Action status (Pending, In Progress, Completed)

**Implementation Code:**

```razor
@using ExxerCube.Prisma.Domain.Entities
@using ExxerCube.Prisma.Application.Services

<!-- Legal Instrument Detection -->
<MudCard Class="mb-4">
    <MudCardHeader>
        <MudText Typo="Typo.h6">
            <MudIcon Icon="@Icons.Material.Filled.Gavel" Class="mr-2" />
            Legal Directive Classification
        </MudText>
    </MudCardHeader>
    <MudCardContent>
        @if (LegalInstruments.Any())
        {
            <MudList>
                @foreach (var instrument in LegalInstruments)
                {
                    <MudListItem>
                        <MudGrid>
                            <MudItem xs="12" sm="8">
                                <MudText Typo="Typo.body1" Class="font-weight-bold">
                                    @instrument.InstrumentName
                                </MudText>
                                <MudText Typo="Typo.caption" Color="Color.Secondary">
                                    Detected in: @instrument.SourceText
                                </MudText>
                            </MudItem>
                            <MudItem xs="12" sm="4">
                                <MudProgressCircular Value="@instrument.Confidence" 
                                                   Size="Size.Small"
                                                   Color="@GetConfidenceColor(instrument.Confidence)" />
                                <MudText Typo="Typo.caption">@instrument.Confidence% confidence</MudText>
                            </MudItem>
                        </MudGrid>
                    </MudListItem>
                }
            </MudList>
        }
        else
        {
            <MudAlert Severity="Severity.Info">No legal instruments detected</MudAlert>
        }
    </MudCardContent>
</MudCard>

<!-- Compliance Actions -->
<MudCard>
    <MudCardHeader>
        <MudText Typo="Typo.h6">
            <MudIcon Icon="@Icons.Material.Filled.Checklist" Class="mr-2" />
            Compliance Actions
        </MudText>
    </MudCardHeader>
    <MudCardContent>
        @foreach (var action in ComplianceActions)
        {
            <MudCard Class="mb-3" Elevation="2">
                <MudCardContent>
                    <MudGrid>
                        <MudItem xs="12">
                            <div class="d-flex align-center">
                                <MudChip Size="Size.Large" 
                                        Color="@GetActionColor(action.ActionType)"
                                        Icon="@GetActionIcon(action.ActionType)">
                                    @action.ActionType
                                </MudChip>
                                <MudProgressCircular Value="@action.Confidence" 
                                                    Size="Size.Small"
                                                    Class="ml-2" />
                                <MudText Typo="Typo.caption" Class="ml-2">
                                    @action.Confidence% confidence
                                </MudText>
                                <MudSpacer />
                                <MudChip Size="Size.Small" 
                                        Color="@GetStatusColor(action.Status)">
                                    @action.Status
                                </MudChip>
                            </div>
                        </MudItem>
                        
                        @if (!string.IsNullOrEmpty(action.AccountNumber))
                        {
                            <MudItem xs="12" sm="6" md="4">
                                <MudText Typo="Typo.subtitle2">Account Number</MudText>
                                <MudText Typo="Typo.body1">@action.AccountNumber</MudText>
                            </MudItem>
                        }
                        
                        @if (!string.IsNullOrEmpty(action.ProductType))
                        {
                            <MudItem xs="12" sm="6" md="4">
                                <MudText Typo="Typo.subtitle2">Product Type</MudText>
                                <MudText Typo="Typo.body1">@action.ProductType</MudText>
                            </MudItem>
                        }
                        
                        @if (action.Amount.HasValue)
                        {
                            <MudItem xs="12" sm="6" md="4">
                                <MudText Typo="Typo.subtitle2">Amount</MudText>
                                <MudText Typo="Typo.body1">@action.Amount.Value.ToString("C")</MudText>
                            </MudItem>
                        }
                        
                        <MudItem xs="12">
                            <MudDivider Class="my-2" />
                            <MudText Typo="Typo.caption" Color="Color.Secondary">
                                Origin: @action.ExpedienteOrigen / @action.OficioOrigen
                                @if (!string.IsNullOrEmpty(action.RequerimientoOrigen))
                                {
                                    <span> / @action.RequerimientoOrigen</span>
                                }
                            </MudText>
                        </MudItem>
                    </MudGrid>
                </MudCardContent>
            </MudCard>
        }
        
        @if (!ComplianceActions.Any())
        {
            <MudAlert Severity="Severity.Info">No compliance actions identified</MudAlert>
        }
    </MudCardContent>
</MudCard>

@code {
    [Parameter] public List<LegalInstrument> LegalInstruments { get; set; } = new();
    [Parameter] public List<ComplianceAction> ComplianceActions { get; set; } = new();
    
    private Color GetActionColor(ComplianceActionType actionType) => actionType switch
    {
        ComplianceActionType.Block => Color.Error,
        ComplianceActionType.Unblock => Color.Success,
        ComplianceActionType.Document => Color.Info,
        ComplianceActionType.Transfer => Color.Warning,
        ComplianceActionType.Information => Color.Primary,
        ComplianceActionType.Ignore => Color.Default,
        _ => Color.Default
    };
    
    private string GetActionIcon(ComplianceActionType actionType) => actionType switch
    {
        ComplianceActionType.Block => Icons.Material.Filled.Block,
        ComplianceActionType.Unblock => Icons.Material.Filled.LockOpen,
        ComplianceActionType.Document => Icons.Material.Filled.Description,
        ComplianceActionType.Transfer => Icons.Material.Filled.SwapHoriz,
        ComplianceActionType.Information => Icons.Material.Filled.Info,
        ComplianceActionType.Ignore => Icons.Material.Filled.SkipNext,
        _ => Icons.Material.Filled.Help
    };
    
    private Color GetConfidenceColor(int confidence) => confidence switch
    {
        >= 90 => Color.Success,
        >= 70 => Color.Warning,
        _ => Color.Error
    };
    
    private Color GetStatusColor(string status) => status switch
    {
        "Completed" => Color.Success,
        "In Progress" => Color.Info,
        "Pending" => Color.Warning,
        _ => Color.Default
    };
}
```

---

## Story 1.5: SLA Tracking and Escalation Management - UI Enhancements

### Current State Analysis

**Backend Status:** ✅ Complete (QA Approved)  
**UI Status:** ⚠️ **Missing** - AC5 (SLA Dashboard UI) NOT implemented

**Gaps Identified:**
- No SLA dashboard showing active cases
- No deadline countdown visualization
- No risk indicators (color-coded)
- No escalation summary cards
- No real-time updates via SignalR

### Required UI Components

#### 1. SLA Dashboard Component

**Location:** `Components/Pages/SlaDashboard.razor`  
**Purpose:** Central hub for monitoring SLA deadlines and escalations

**Features:**

**A. Escalation Summary Cards**
- **Metrics:**
  - Critical cases (<4h remaining) - Red badge
  - Warning cases (<24h remaining) - Yellow badge
  - Breached cases - Black badge
  - Total active cases - Blue badge

- **Visual Design:**
  - Large number with trend indicator (↑/↓)
  - Clickable cards that filter the table
  - Mini chart showing trend over time

**B. Active Cases Table**
- **Columns:**
  - Case ID / File Name
  - Intake Date
  - Deadline (formatted date/time)
  - Time Remaining (live countdown timer)
  - Escalation Level (badge with color coding)
  - Status (In Progress, Pending, Completed)
  - Actions (View Details, Expedite, Request Extension)

- **Sorting:**
  - By deadline (soonest first)
  - By escalation level (Critical first)
  - By time remaining

- **Filtering:**
  - Escalation level (Critical, Warning, Breached, None)
  - Status filter
  - Date range (intake date or deadline)

**C. SLA Timeline Visualization**
- **Visual Timeline:**
  - Intake Date → Current Time → Deadline
  - Color segments: Green (safe), Yellow (warning), Red (critical)
  - Markers for escalation points
  - Tooltip showing exact times

**D. At-Risk Cases List**
- Filtered view of cases approaching deadline
- Quick actions: Assign to Analyst, Expedite, Request Extension, Export Immediately

**E. Real-time Updates**
- Countdown timers update every minute via SignalR
- Color coding updates dynamically
- Toast notifications for escalations

**Implementation Code:**

```razor
@page "/sla-dashboard"
@using ExxerCube.Prisma.Domain.Entities
@using ExxerCube.Prisma.Application.Services
@inject ISLAEnforcer SLAEnforcer
@inject ILogger<SlaDashboard> Logger
@implements IAsyncDisposable

<PageTitle>SLA Tracking Dashboard</PageTitle>

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">
        <MudIcon Icon="@Icons.Material.Filled.Schedule" Class="mr-2" />
        SLA Tracking Dashboard
    </MudText>

    <!-- Escalation Summary Cards -->
    <MudGrid Class="mb-4">
        <MudItem xs="12" sm="6" md="3">
            <MudCard OnClick="@(() => FilterByEscalation(EscalationLevel.Critical))" 
                    Class="cursor-pointer"
                    Elevation="@(selectedEscalation == EscalationLevel.Critical ? 8 : 2)">
                <MudCardContent>
                    <MudText Typo="Typo.h4" Color="Color.Error">@criticalCount</MudText>
                    <MudText Typo="Typo.body2">Critical (<4h)</MudText>
                    <MudText Typo="Typo.caption" Color="Color.Secondary">
                        @GetTrendText(criticalTrend)
                    </MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>
        <MudItem xs="12" sm="6" md="3">
            <MudCard OnClick="@(() => FilterByEscalation(EscalationLevel.Warning))" 
                    Class="cursor-pointer"
                    Elevation="@(selectedEscalation == EscalationLevel.Warning ? 8 : 2)">
                <MudCardContent>
                    <MudText Typo="Typo.h4" Color="Color.Warning">@warningCount</MudText>
                    <MudText Typo="Typo.body2">Warning (<24h)</MudText>
                    <MudText Typo="Typo.caption" Color="Color.Secondary">
                        @GetTrendText(warningTrend)
                    </MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>
        <MudItem xs="12" sm="6" md="3">
            <MudCard OnClick="@(() => FilterByEscalation(EscalationLevel.Breached))" 
                    Class="cursor-pointer"
                    Elevation="@(selectedEscalation == EscalationLevel.Breached ? 8 : 2)">
                <MudCardContent>
                    <MudText Typo="Typo.h4" Color="Color.Dark">@breachedCount</MudText>
                    <MudText Typo="Typo.body2">Breached</MudText>
                    <MudText Typo="Typo.caption" Color="Color.Secondary">
                        Requires immediate action
                    </MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>
        <MudItem xs="12" sm="6" md="3">
            <MudCard Class="cursor-pointer">
                <MudCardContent>
                    <MudText Typo="Typo.h4" Color="Color.Info">@totalActive</MudText>
                    <MudText Typo="Typo.body2">Total Active</MudText>
                    <MudText Typo="Typo.caption" Color="Color.Secondary">
                        All tracked cases
                    </MudText>
                </MudCardContent>
            </MudCard>
        </MudItem>
    </MudGrid>

    <!-- Active Cases Table -->
    <MudCard>
        <MudCardHeader>
            <MudText Typo="Typo.h6">Active Cases</MudText>
            <MudSpacer />
            <MudTextField @bind-Value="searchText" 
                         Placeholder="Search cases..." 
                         Adornment="Adornment.Start"
                         AdornmentIcon="@Icons.Material.Filled.Search"
                         Variant="Variant.Outlined"
                         Dense="true"
                         Immediate="true" />
        </MudCardHeader>
        <MudCardContent>
            <MudTable Items="@FilteredSLAStatuses" 
                     Dense="true" 
                     Hover="true" 
                     Striped="true"
                     Filter="new Func<SLAStatus, bool>(FilterCases)"
                     SortMode="SortMode.Multiple">
                <HeaderContent>
                    <MudTh>
                        <MudTableSortLabel SortBy="@new Func<SLAStatus, object>(x => x.FileId)">
                            Case ID / File Name
                        </MudTableSortLabel>
                    </MudTh>
                    <MudTh>
                        <MudTableSortLabel SortBy="@new Func<SLAStatus, object>(x => x.IntakeDate)">
                            Intake Date
                        </MudTableSortLabel>
                    </MudTh>
                    <MudTh>
                        <MudTableSortLabel SortBy="@new Func<SLAStatus, object>(x => x.Deadline)">
                            Deadline
                        </MudTableSortLabel>
                    </MudTh>
                    <MudTh>
                        <MudTableSortLabel SortBy="@new Func<SLAStatus, object>(x => x.RemainingTime)">
                            Time Remaining
                        </MudTableSortLabel>
                    </MudTh>
                    <MudTh>Escalation Level</MudTh>
                    <MudTh>Status</MudTh>
                    <MudTh>Actions</MudTh>
                </HeaderContent>
                <RowTemplate>
                    <MudTd DataLabel="Case ID">
                        <MudText Typo="Typo.body2" Class="font-weight-medium">
                            @context.FileId
                        </MudText>
                    </MudTd>
                    <MudTd DataLabel="Intake Date">
                        <MudText Typo="Typo.body2">@context.IntakeDate.ToString("g")</MudText>
                    </MudTd>
                    <MudTd DataLabel="Deadline">
                        <MudText Typo="Typo.body2" Class="font-weight-bold">
                            @context.Deadline.ToString("g")
                        </MudText>
                    </MudTd>
                    <MudTd DataLabel="Time Remaining">
                        <div class="d-flex align-center">
                            <MudText Typo="Typo.body2" 
                                    Color="@GetRemainingTimeColor(context.RemainingTime)"
                                    Class="font-weight-bold">
                                @FormatRemainingTime(context.RemainingTime)
                            </MudText>
                            @if (context.RemainingTime.TotalHours < 24)
                            {
                                <MudProgressCircular Size="Size.Small" 
                                                   Indeterminate="true"
                                                   Class="ml-2" />
                            }
                        </div>
                    </MudTd>
                    <MudTd DataLabel="Escalation Level">
                        <MudChip Size="Size.Small" 
                                Color="@GetEscalationColor(context.EscalationLevel)"
                                Icon="@GetEscalationIcon(context.EscalationLevel)">
                            @context.EscalationLevel
                        </MudChip>
                    </MudTd>
                    <MudTd DataLabel="Status">
                        <MudChip Size="Size.Small" Color="@GetStatusColor(context.Status)">
                            @context.Status
                        </MudChip>
                    </MudTd>
                    <MudTd DataLabel="Actions">
                        <MudIconButton Icon="@Icons.Material.Filled.Visibility" 
                                      Size="Size.Small"
                                      OnClick="@(() => ViewCaseDetails(context))" />
                        <MudIconButton Icon="@Icons.Material.Filled.FastForward" 
                                      Size="Size.Small"
                                      OnClick="@(() => ExpediteCase(context))" />
                        <MudIconButton Icon="@Icons.Material.Filled.Extension" 
                                      Size="Size.Small"
                                      OnClick="@(() => RequestExtension(context))" />
                    </MudTd>
                </RowTemplate>
                <PagerContent>
                    <MudTablePager />
                </PagerContent>
            </MudTable>
        </MudCardContent>
    </MudCard>

    <!-- SLA Timeline Visualization (for selected case) -->
    @if (selectedCase != null)
    {
        <MudCard Class="mt-4">
            <MudCardHeader>
                <MudText Typo="Typo.h6">SLA Timeline: @selectedCase.FileId</MudText>
            </MudCardHeader>
            <MudCardContent>
                <SlaTimelineView SLAStatus="@selectedCase" />
            </MudCardContent>
        </MudCard>
    }
</MudContainer>

@code {
    private List<SLAStatus> slaStatuses = new();
    private List<SLAStatus> filteredSLAStatuses = new();
    private string searchText = string.Empty;
    private EscalationLevel? selectedEscalation;
    private SLAStatus? selectedCase;
    
    private int criticalCount => slaStatuses.Count(s => s.EscalationLevel == EscalationLevel.Critical);
    private int warningCount => slaStatuses.Count(s => s.EscalationLevel == EscalationLevel.Warning);
    private int breachedCount => slaStatuses.Count(s => s.EscalationLevel == EscalationLevel.Breached);
    private int totalActive => slaStatuses.Count;
    
    private string criticalTrend = "→";
    private string warningTrend = "→";

    protected override async Task OnInitializedAsync()
    {
        await LoadSLAStatuses();
        await StartSignalRConnection();
    }

    private async Task LoadSLAStatuses()
    {
        // Load SLA statuses from SLAEnforcer
        // Implementation depends on your service interface
    }

    private async Task StartSignalRConnection()
    {
        // Connect to SignalR hub for real-time updates
        // Update countdown timers every minute
    }

    private bool FilterCases(SLAStatus status)
    {
        if (!string.IsNullOrEmpty(searchText) && 
            !status.FileId.Contains(searchText, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        
        if (selectedEscalation.HasValue && status.EscalationLevel != selectedEscalation.Value)
            return false;
            
        return true;
    }

    private void FilterByEscalation(EscalationLevel level)
    {
        selectedEscalation = selectedEscalation == level ? null : level;
        filteredSLAStatuses = slaStatuses.Where(FilterCases).ToList();
        StateHasChanged();
    }

    private void ViewCaseDetails(SLAStatus status)
    {
        selectedCase = status;
        StateHasChanged();
    }

    private void ExpediteCase(SLAStatus status)
    {
        // Navigate to expedite workflow
    }

    private void RequestExtension(SLAStatus status)
    {
        // Open extension request dialog
    }

    private string FormatRemainingTime(TimeSpan remaining)
    {
        if (remaining.TotalDays >= 1)
            return $"{(int)remaining.TotalDays}d {remaining.Hours}h";
        if (remaining.TotalHours >= 1)
            return $"{(int)remaining.TotalHours}h {remaining.Minutes}m";
        if (remaining.TotalMinutes >= 1)
            return $"{(int)remaining.TotalMinutes}m";
        return $"{remaining.TotalSeconds}s";
    }

    private Color GetRemainingTimeColor(TimeSpan remaining)
    {
        if (remaining.TotalHours < 4) return Color.Error;
        if (remaining.TotalHours < 24) return Color.Warning;
        return Color.Success;
    }

    private Color GetEscalationColor(EscalationLevel level) => level switch
    {
        EscalationLevel.Critical => Color.Error,
        EscalationLevel.Warning => Color.Warning,
        EscalationLevel.Breached => Color.Dark,
        _ => Color.Default
    };

    private string GetEscalationIcon(EscalationLevel level) => level switch
    {
        EscalationLevel.Critical => Icons.Material.Filled.Warning,
        EscalationLevel.Warning => Icons.Material.Filled.Info,
        EscalationLevel.Breached => Icons.Material.Filled.Error,
        _ => Icons.Material.Filled.CheckCircle
    };

    private Color GetStatusColor(string status) => status switch
    {
        "Completed" => Color.Success,
        "In Progress" => Color.Info,
        "Pending" => Color.Warning,
        _ => Color.Default
    };

    private string GetTrendText(string trend) => trend switch
    {
        "↑" => "Increasing",
        "↓" => "Decreasing",
        _ => "Stable"
    };

    public async ValueTask DisposeAsync()
    {
        // Dispose SignalR connection
    }
}
```

#### 2. SLA Timeline Visualization Component

**Location:** `Components/Shared/SlaTimelineView.razor`  
**Purpose:** Visual timeline showing intake → current → deadline with escalation markers

**Implementation Code:**

```razor
@using ExxerCube.Prisma.Domain.Entities

<MudGrid>
    <MudItem xs="12">
        <MudText Typo="Typo.subtitle2" Class="mb-2">Timeline</MudText>
        <div class="position-relative" style="height: 60px;">
            <!-- Timeline Bar -->
            <MudProgressLinear Value="@GetProgressPercentage()" 
                             Color="@GetTimelineColor()"
                             Variant="Variant.Filled"
                             Class="mt-3" />
            
            <!-- Markers -->
            <div class="position-absolute" style="left: 0; top: 0;">
                <MudIcon Icon="@Icons.Material.Filled.Circle" 
                        Size="Size.Small" 
                        Color="Color.Success" />
                <MudText Typo="Typo.caption">Intake</MudText>
            </div>
            
            <div class="position-absolute" style="left: @GetCurrentPosition()%; top: 0;">
                <MudIcon Icon="@Icons.Material.Filled.Circle" 
                        Size="Size.Small" 
                        Color="Color.Info" />
                <MudText Typo="Typo.caption">Now</MudText>
            </div>
            
            <div class="position-absolute" style="right: 0; top: 0;">
                <MudIcon Icon="@Icons.Material.Filled.Circle" 
                        Size="Size.Small" 
                        Color="@GetDeadlineColor()" />
                <MudText Typo="Typo.caption">Deadline</MudText>
            </div>
        </div>
        
        <!-- Time Labels -->
        <MudGrid Class="mt-2">
            <MudItem xs="4">
                <MudText Typo="Typo.caption">@SLAStatus.IntakeDate.ToString("g")</MudText>
            </MudItem>
            <MudItem xs="4" Class="text-center">
                <MudText Typo="Typo.caption">@DateTime.Now.ToString("g")</MudText>
            </MudItem>
            <MudItem xs="4" Class="text-right">
                <MudText Typo="Typo.caption">@SLAStatus.Deadline.ToString("g")</MudText>
            </MudItem>
        </MudGrid>
    </MudItem>
</MudGrid>

@code {
    [Parameter] public SLAStatus SLAStatus { get; set; } = default!;
    
    private double GetProgressPercentage()
    {
        var total = SLAStatus.Deadline - SLAStatus.IntakeDate;
        var elapsed = DateTime.Now - SLAStatus.IntakeDate;
        return Math.Min(100, (elapsed.TotalMilliseconds / total.TotalMilliseconds) * 100);
    }
    
    private double GetCurrentPosition()
    {
        return GetProgressPercentage();
    }
    
    private Color GetTimelineColor()
    {
        if (SLAStatus.IsBreached) return Color.Error;
        if (SLAStatus.IsAtRisk) return Color.Warning;
        return Color.Success;
    }
    
    private Color GetDeadlineColor()
    {
        if (SLAStatus.IsBreached) return Color.Error;
        if (SLAStatus.IsAtRisk) return Color.Warning;
        return Color.Success;
    }
}
```

---

## Story 1.6: Manual Review Interface - UI Enhancements

### Current State Analysis

**Backend Status:** ✅ Complete  
**UI Status:** ⚠️ **Partial** - Basic components exist, needs enhancements

**Gaps Identified:**
- No SLA priority indicators in dashboard
- No bulk actions support
- Limited filtering capabilities
- No SLA timeline visualization in case detail
- Basic field annotations (needs enhancement)
- No source comparison panel
- Limited real-time updates

### Required UI Enhancements

#### 1. Manual Review Dashboard Enhancements

**Location:** Enhance existing `ManualReviewDashboard.razor`  
**Purpose:** Add SLA integration, bulk actions, and enhanced filtering

**Enhancements:**

**A. SLA Priority Indicators**
- Add SLA status column to review queue table
- Priority badges: Urgent (red, <4h), High (yellow, <24h), Normal (green)
- Sort by priority (Urgent first)

**B. Bulk Actions**
- Checkbox column for selection
- Bulk action toolbar: Assign to Reviewer, Bulk Approve, Export Selected
- Selection counter display

**C. Enhanced Filtering**
- SLA Priority filter (Urgent, High, Normal)
- Assigned Reviewer dropdown
- Created Date Range picker
- Review Reason multi-select
- Filter presets: "My Cases", "Urgent Reviews", "Low Confidence", "Unassigned"

**D. Quick Stats Enhancement**
- Make stat cards clickable to filter table
- Visual feedback on hover

**Implementation Code:**

```razor
<!-- Add to existing ManualReviewDashboard.razor -->

<!-- SLA Priority Column -->
<MudTh>
    <MudTableSortLabel SortBy="@new Func<ReviewCase, object>(x => x.SLAStatus?.RemainingTime)">
        SLA Priority
    </MudTableSortLabel>
</MudTh>

<!-- In RowTemplate -->
<MudTd DataLabel="SLA Priority">
    @if (context.SLAStatus != null)
    {
        <MudChip Size="Size.Small" 
                Color="@GetSLAPriorityColor(context.SLAStatus.RemainingTime)"
                Icon="@GetSLAPriorityIcon(context.SLAStatus.RemainingTime)">
            @GetSLAPriorityLabel(context.SLAStatus.RemainingTime)
        </MudChip>
    }
    else
    {
        <MudText Typo="Typo.caption" Color="Color.Secondary">N/A</MudText>
    }
</MudTd>

<!-- Bulk Actions Bar -->
@if (selectedCases.Any())
{
    <MudCard Class="mb-2" Elevation="4">
        <MudCardContent>
            <div class="d-flex align-center">
                <MudText Typo="Typo.body1" Class="mr-3">
                    @selectedCases.Count case(s) selected
                </MudText>
                <MudButton Variant="Variant.Filled" 
                          Color="Color.Primary"
                          StartIcon="@Icons.Material.Filled.PersonAdd"
                          OnClick="AssignSelectedCases">
                    Assign to Reviewer
                </MudButton>
                <MudButton Variant="Variant.Filled" 
                          Color="Color.Success"
                          StartIcon="@Icons.Material.Filled.Check"
                          OnClick="BulkApproveSelected">
                    Bulk Approve
                </MudButton>
                <MudButton Variant="Variant.Outlined" 
                          Color="Color.Info"
                          StartIcon="@Icons.Material.Filled.FileDownload"
                          OnClick="ExportSelected">
                    Export Selected
                </MudButton>
                <MudSpacer />
                <MudIconButton Icon="@Icons.Material.Filled.Close" 
                             OnClick="@(() => selectedCases.Clear())" />
            </div>
        </MudCardContent>
    </MudCard>
}

<!-- Checkbox Column -->
<MudTh>
    <MudCheckBox @bind-Checked="@selectAll" 
                Indeterminate="@(selectedCases.Count > 0 && selectedCases.Count < reviewCases.Count)"
                OnCheckedChanged="@SelectAllCases" />
</MudTh>

<!-- In RowTemplate -->
<MudTd>
    <MudCheckBox @bind-Checked="@context.IsSelected" 
                OnCheckedChanged="@(() => ToggleCaseSelection(context))" />
</MudTd>

@code {
    private HashSet<string> selectedCases = new();
    private bool selectAll = false;
    
    private Color GetSLAPriorityColor(TimeSpan? remainingTime)
    {
        if (!remainingTime.HasValue) return Color.Default;
        if (remainingTime.Value.TotalHours < 4) return Color.Error;
        if (remainingTime.Value.TotalHours < 24) return Color.Warning;
        return Color.Success;
    }
    
    private string GetSLAPriorityIcon(TimeSpan? remainingTime)
    {
        if (!remainingTime.HasValue) return Icons.Material.Filled.Help;
        if (remainingTime.Value.TotalHours < 4) return Icons.Material.Filled.Warning;
        if (remainingTime.Value.TotalHours < 24) return Icons.Material.Filled.Info;
        return Icons.Material.Filled.CheckCircle;
    }
    
    private string GetSLAPriorityLabel(TimeSpan? remainingTime)
    {
        if (!remainingTime.HasValue) return "N/A";
        if (remainingTime.Value.TotalHours < 4) return "Urgent";
        if (remainingTime.Value.TotalHours < 24) return "High";
        return "Normal";
    }
    
    private void SelectAllCases(bool? isChecked)
    {
        if (isChecked == true)
        {
            selectedCases = new HashSet<string>(reviewCases.Select(c => c.CaseId));
        }
        else
        {
            selectedCases.Clear();
        }
        StateHasChanged();
    }
    
    private void ToggleCaseSelection(ReviewCase reviewCase)
    {
        if (selectedCases.Contains(reviewCase.CaseId))
            selectedCases.Remove(reviewCase.CaseId);
        else
            selectedCases.Add(reviewCase.CaseId);
        StateHasChanged();
    }
    
    private async Task AssignSelectedCases()
    {
        // Open assign dialog for selected cases
    }
    
    private async Task BulkApproveSelected()
    {
        // Bulk approve workflow
    }
    
    private async Task ExportSelected()
    {
        // Export selected cases
    }
}
```

#### 2. Review Case Detail Enhancements

**Location:** Enhance existing `ReviewCaseDetail.razor`  
**Purpose:** Add SLA timeline, enhanced field annotations, and source comparison

**Enhancements:**

**A. SLA Timeline Visualization**
- Visual timeline showing intake → current → deadline
- Color segments: Green (safe), Yellow (warning), Red (critical)
- Escalation markers
- Tooltip with exact times

**B. Enhanced Field Annotations**
- Source badges (XML, DOCX, PDF, OCR)
- Confidence indicator (progress bar 0-100%)
- Agreement level (all agree / conflicts)
- Origin trace (click to see source)

**C. Source Comparison Panel**
- Collapsible side panel
- Side-by-side comparison table
- Highlight differences
- Show confidence scores per source

**Implementation Code:**

```razor
<!-- Add to existing ReviewCaseDetail.razor -->

<!-- SLA Timeline -->
<MudCard Class="mb-4">
    <MudCardHeader>
        <MudText Typo="Typo.h6">SLA Timeline</MudText>
    </MudCardHeader>
    <MudCardContent>
        @if (reviewCase.SLAStatus != null)
        {
            <SlaTimelineView SLAStatus="@reviewCase.SLAStatus" />
        }
        else
        {
            <MudAlert Severity="Severity.Info">No SLA tracking available for this case</MudAlert>
        }
    </MudCardContent>
</MudCard>

<!-- Enhanced Field Annotations -->
<MudTextField Label="@field.Label" 
             Value="@field.Value"
             Variant="Variant.Outlined"
             @bind-Value="@field.Value">
    <Adornment Start>
        @foreach (var source in field.Sources)
        {
            <MudChip Size="Size.Small" 
                    Color="@GetSourceColor(source)"
                    Class="mr-1">
                @source
            </MudChip>
        }
    </Adornment>
    <Adornment End>
        <MudProgressCircular Value="@field.Confidence" 
                           Size="Size.Small"
                           Color="@GetConfidenceColor(field.Confidence)" />
        <MudText Typo="Typo.caption" Class="ml-1">
            @field.Confidence%
        </MudText>
    </Adornment>
    @if (field.HasConflict)
    {
        <HelperText>
            <MudIcon Icon="@Icons.Material.Filled.Warning" Size="Size.Small" />
            Conflicting values detected across sources
        </HelperText>
    }
</MudTextField>

<!-- Source Comparison Panel -->
<MudExpansionPanels Class="mt-4">
    <MudExpansionPanel Text="Source Comparison" Icon="@Icons.Material.Filled.CompareArrows">
        <MudTable Items="@GetConflictedFields()" Dense="true" Hover="true">
            <HeaderContent>
                <MudTh>Field Name</MudTh>
                <MudTh>XML</MudTh>
                <MudTh>DOCX</MudTh>
                <MudTh>PDF</MudTh>
                <MudTh>OCR</MudTh>
                <MudTh>Unified</MudTh>
            </HeaderContent>
            <RowTemplate>
                <MudTd>@context.FieldName</MudTd>
                <MudTd>
                    <MudText Typo="Typo.body2" 
                            Color="@(context.XmlValue == context.UnifiedValue ? Color.Success : Color.Default)">
                        @(context.XmlValue ?? "-")
                    </MudText>
                </MudTd>
                <MudTd>
                    <MudText Typo="Typo.body2" 
                            Color="@(context.DocxValue == context.UnifiedValue ? Color.Success : Color.Default)">
                        @(context.DocxValue ?? "-")
                    </MudText>
                </MudTd>
                <MudTd>
                    <MudText Typo="Typo.body2" 
                            Color="@(context.PdfValue == context.UnifiedValue ? Color.Success : Color.Default)">
                        @(context.PdfValue ?? "-")
                    </MudText>
                </MudTd>
                <MudTd>
                    <MudText Typo="Typo.body2" 
                            Color="@(context.OcrValue == context.UnifiedValue ? Color.Success : Color.Default)">
                        @(context.OcrValue ?? "-")
                    </MudText>
                </MudTd>
                <MudTd>
                    <MudText Typo="Typo.body2" Class="font-weight-bold">
                        @context.UnifiedValue
                    </MudText>
                </MudTd>
            </RowTemplate>
        </MudTable>
    </MudExpansionPanel>
</MudExpansionPanels>

@code {
    private Color GetSourceColor(string source) => source switch
    {
        "XML" => Color.Info,
        "DOCX" => Color.Primary,
        "PDF" => Color.Error,
        "OCR" => Color.Warning,
        _ => Color.Default
    };
    
    private Color GetConfidenceColor(int confidence) => confidence switch
    {
        >= 90 => Color.Success,
        >= 70 => Color.Warning,
        _ => Color.Error
    };
    
    private List<FieldComparison> GetConflictedFields()
    {
        // Extract conflicted fields from unified metadata record
        return new();
    }
    
    private class FieldComparison
    {
        public string FieldName { get; set; } = string.Empty;
        public string? XmlValue { get; set; }
        public string? DocxValue { get; set; }
        public string? PdfValue { get; set; }
        public string? OcrValue { get; set; }
        public string UnifiedValue { get; set; } = string.Empty;
    }
}
```

---

## Consolidated Implementation Plan

### Priority 1: Critical UI Components (Required for Story Completion)

1. **SLA Dashboard** (Story 1.5 - AC5)
   - Escalation summary cards
   - Active cases table with countdown timers
   - SLA timeline visualization
   - Real-time updates via SignalR

2. **Identity Resolution Display** (Story 1.4)
   - Person identity cards with RFC variants
   - Legal directive classification display
   - Compliance actions list

3. **Manual Review Dashboard Enhancements** (Story 1.6)
   - SLA priority indicators
   - Bulk actions
   - Enhanced filtering

### Priority 2: Supporting UI Components (Enhancement)

1. **Review Case Detail Enhancements** (Story 1.6)
   - SLA timeline visualization
   - Enhanced field annotations
   - Source comparison panel

2. **Integration Components**
   - Integrate all components into Review Case Detail view
   - Add navigation between related components

### Implementation Order

**Phase 1:** Story 1.5 UI Components (Critical - AC5)
- SLA Dashboard
- SLA Timeline Visualization
- Real-time SignalR integration

**Phase 2:** Story 1.4 UI Components
- Identity Resolution Display
- Legal Directive Classification Display
- Compliance Actions Display

**Phase 3:** Story 1.6 UI Enhancements
- Manual Review Dashboard enhancements
- Review Case Detail enhancements

**Phase 4:** Integration
- Integrate all components into unified case detail view
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
- **Progress Indicators:** Use MudProgressLinear and MudProgressCircular for confidence/timeline visualization
- **Dialogs:** Use MudDialog for detailed views and confirmations
- **Real-time Updates:** Use SignalR for live countdown timers and status updates

---

## User Experience Considerations

### Information Hierarchy
1. **Primary Information:** SLA deadlines, compliance actions, review priorities (most important)
2. **Secondary Information:** Identity resolution details, field matching (supporting context)
3. **Tertiary Information:** Source comparison, RFC variants (expandable views)

### Progressive Disclosure
- Show summary information by default
- Allow expansion for detailed views
- Use tabs/accordions for complex information
- Provide tooltips for technical terms

### Real-time Updates
- Use SignalR for real-time countdown timers (update every minute)
- Show loading states during data fetch
- Provide error states with retry options
- Visual indicators for new/updated data
- Toast notifications for escalations

### Accessibility
- Ensure proper ARIA labels for all interactive elements
- Support keyboard navigation throughout
- Provide color-blind friendly indicators (use icons + colors)
- Ensure sufficient contrast ratios (WCAG 2.1 AA compliance)
- Screen reader announcements for status changes
- Skip links for main content

### Performance
- Virtual scrolling for large tables
- Lazy loading of detailed views
- Debounced filter inputs
- Cached filter results
- Progressive loading (load summary first, details on demand)
- Efficient SignalR updates (batch updates, debounce)

---

## Testing Considerations

### UX Testing
- User testing with compliance officers, managers, and analysts
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
- Real-time update performance (SignalR)
- Memory usage with many records
- Countdown timer performance

### Integration Testing
- Verify SLA dashboard integrates with SLA tracking service
- Verify identity resolution display integrates with resolution service
- Verify manual review enhancements integrate with review service
- Verify SignalR real-time updates work correctly

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
- [Manual Review UX Improvements](ui-improvements-manual-review.md) - Story 1.6 enhancements
- [Story 1.4](../stories/1.4.identity-resolution-legal-classification.md) - Identity resolution story
- [Story 1.5](../stories/1.5.sla-tracking-escalation.md) - SLA tracking story
- [Story 1.6](../stories/1.6.manual-review-interface.md) - Manual review story

---

**Document Status:** ✅ **Complete**  
**Ready for Implementation** 🚀

