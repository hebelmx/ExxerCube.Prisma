# ExxerCube.Prisma Frontend Architecture Document

**Version:** 1.0  
**Date:** 2025-01-12  
**Status:** Draft  
**Project:** ExxerCube.Prisma - Regulatory Compliance Automation System

---

## Table of Contents

1. [Template and Framework Selection](#template-and-framework-selection)
2. [Frontend Tech Stack](#frontend-tech-stack)
3. [Project Structure](#project-structure)
4. [Component Standards](#component-standards)
5. [State Management](#state-management)
6. [API Integration](#api-integration)
7. [Routing](#routing)
8. [Styling Guidelines](#styling-guidelines)
9. [Testing Requirements](#testing-requirements)
10. [Environment Configuration](#environment-configuration)
11. [Frontend Developer Standards](#frontend-developer-standards)

---

## Template and Framework Selection

### Change Log

| Date | Version | Description | Author |
|------|---------|-------------|--------|
| 2025-01-12 | 1.0 | Initial frontend architecture document | Architect |

### Framework and Template Analysis

**Existing Project Status:** Brownfield Enhancement

The ExxerCube.Prisma project already has a **Blazor Server** application in place at `Prisma/Code/Src/CSharp/UI/ExxerCube.Prisma.Web.UI/`. This is not a new UI project, but rather an enhancement of an existing Blazor Server application.

**Current Setup:**
- **Framework:** Blazor Server (ASP.NET Core) with Interactive Server Render Mode
- **UI Component Library:** MudBlazor (already integrated via `AddMudServices()`)
- **Real-time Communication:** SignalR (ProcessingHub already configured)
- **Authentication:** ASP.NET Core Identity (already implemented)
- **Existing Components:** Home, Dashboard, OCRDemo, Weather, Counter, and full Account management pages

**Project Structure:**
```
ExxerCube.Prisma.Web.UI/
├── Components/
│   ├── Pages/          # Page components (Home, Dashboard, OCRDemo, etc.)
│   ├── Layout/         # Layout components (MainLayout, NavMenu)
│   ├── Account/        # Identity/authentication components
│   └── App.razor       # Root component
├── Controllers/        # API controllers
├── Data/              # ApplicationDbContext, Identity setup
├── Hubs/              # SignalR hubs (ProcessingHub)
└── wwwroot/           # Static assets
```

**Key Findings from Codebase Analysis:**
1. **MudBlazor Integration:** Already configured in `Program.cs` with `builder.Services.AddMudServices()`
2. **SignalR Setup:** ProcessingHub exists for real-time updates
3. **Identity System:** Full ASP.NET Core Identity implementation with account management pages
4. **Existing Patterns:** Components follow standard Blazor Server patterns with Razor components

**Enhancement Requirements:**
Based on the PRD and Front-end Specification, the following new UI components need to be added:
- Manual Review Dashboard and Case Detail screens
- SLA Monitoring Dashboard
- Compliance Validation screen
- Export Management interface
- Enhanced Document Processing Dashboard
- Audit Trail Viewer

**No Starter Template Required:**
Since this is a brownfield enhancement of an existing Blazor Server application, no new starter template is needed. The existing project structure and MudBlazor integration will be extended with new components following established patterns.

**Constraints from Existing Setup:**
- Must maintain compatibility with existing Blazor Server architecture
- Must follow existing MudBlazor component patterns
- Must integrate with existing SignalR hub infrastructure
- Must respect existing authentication and authorization patterns
- Must follow existing project structure conventions

**Decision:**
Proceed with enhancing the existing Blazor Server application using MudBlazor components. No new framework or template selection is required. New components will be added to the existing `Components/Pages/` directory structure, following the patterns established in existing components.

---

## Frontend Tech Stack

**Note:** This section MUST remain synchronized with the main architecture document's Technology Stack Table. The technologies listed here are extracted from the existing project configuration and requirements.

### Technology Stack Table

| Category | Technology | Version | Purpose | Rationale |
|----------|------------|---------|---------|-----------|
| **Framework** | Blazor Server (ASP.NET Core) | 9.0.8 | Server-side rendering with real-time updates | C#-based, eliminates need for separate JavaScript framework, SignalR integration built-in, aligns with backend C# stack |
| **UI Library** | MudBlazor | 8.11 | Component library for modern UI | Material Design-based, comprehensive component set, well-maintained, already integrated in project |
| **State Management** | Blazor Component State + SignalR | N/A | Component-level state and real-time updates | Built into Blazor Server, SignalR for real-time SLA alerts and processing status, no additional state management library needed |
| **Routing** | Blazor Router (ASP.NET Core) | 9.0.8 | Client-side routing | Built into Blazor Server, supports route parameters, authorization, and lazy loading |
| **Build Tool** | .NET SDK / MSBuild | 9.0 | Compilation and bundling | Native .NET tooling, handles Razor compilation, asset bundling, and optimization |
| **Styling** | MudBlazor Theme + CSS | 8.11 | Component styling and custom CSS | MudBlazor provides Material Design theme system, custom CSS for project-specific styling, CSS variables for theming |
| **Testing** | bUnit + Playwright | Latest | Component and E2E testing | bUnit for Blazor component unit tests, Playwright for end-to-end browser testing (already in project dependencies) |
| **Component Library** | MudBlazor | 8.11 | Pre-built UI components | Tables, forms, dialogs, navigation, data display components - all from MudBlazor |
| **Form Handling** | Blazor EditForm + MudBlazor Form Components | 9.0.8 / 8.11 | Form validation and submission | Native Blazor EditForm with DataAnnotations validation, MudBlazor form components (MudTextField, MudSelect, etc.) |
| **Animation** | MudBlazor Transitions | 8.11 | UI transitions and animations | Built into MudBlazor components, Material Design motion principles |
| **Dev Tools** | .NET Hot Reload + Browser DevTools | N/A | Development experience | .NET Hot Reload for live code updates, browser DevTools for debugging, Blazor DevTools extension |

**Key Technology Decisions:**

1. **Blazor Server vs Blazor WebAssembly:** Server-side rendering chosen for real-time SignalR updates, reduced client-side complexity, and better integration with existing C# backend.

2. **MudBlazor vs Other UI Libraries:** Already integrated, provides comprehensive Material Design components, reduces custom CSS needs, active community support.

3. **No External State Management:** Blazor Server's component state management and SignalR are sufficient for the application's needs. No Redux/Flux pattern required.

4. **Built-in Routing:** Blazor's router provides all necessary routing capabilities without additional libraries.

5. **Testing Strategy:** bUnit for fast component tests, Playwright for realistic E2E scenarios covering critical compliance workflows.

---

## Project Structure

The project structure follows Blazor Server conventions and organizes components by feature domain. This structure is designed for AI tools to easily locate and understand where each type of file belongs.

### Root Project Structure

```
ExxerCube.Prisma.Web.UI/
├── Components/                    # All Blazor components
│   ├── _Imports.razor            # Global using directives
│   ├── App.razor                 # Root application component
│   ├── Routes.razor              # Router configuration
│   │
│   ├── Layout/                   # Layout components
│   │   ├── MainLayout.razor     # Main application layout
│   │   └── NavMenu.razor        # Navigation menu
│   │
│   ├── Pages/                    # Page components (routes)
│   │   ├── Home.razor           # Homepage (/)
│   │   ├── Dashboard.razor      # Main dashboard (/dashboard)
│   │   ├── Error.razor          # Error page
│   │   │
│   │   ├── Documents/           # Document processing pages
│   │   │   ├── ProcessingQueue.razor      # /documents/processing-queue
│   │   │   ├── FileUpload.razor           # /documents/upload
│   │   │   └── BrowserAutomationResults.razor  # /documents/automation-results
│   │   │
│   │   ├── Review/               # Manual review pages
│   │   │   ├── ReviewQueue.razor         # /review/queue
│   │   │   ├── CaseDetail.razor         # /review/case/{id}
│   │   │   └── FieldAnnotations.razor   # Field annotation component
│   │   │
│   │   ├── SLA/                  # SLA monitoring pages
│   │   │   ├── SLADashboard.razor        # /sla/dashboard
│   │   │   ├── ActiveCases.razor         # /sla/active-cases
│   │   │   └── EscalationManagement.razor  # /sla/escalations
│   │   │
│   │   ├── Compliance/           # Compliance validation pages
│   │   │   └── Validation.razor          # /compliance/validation/{caseId}
│   │   │
│   │   ├── Export/               # Export management pages
│   │   │   ├── ExportManagement.razor    # /export/management
│   │   │   └── ExportHistory.razor       # /export/history
│   │   │
│   │   └── Audit/                # Audit trail pages
│   │       ├── AuditTrailViewer.razor     # /audit/viewer
│   │       └── ReportGenerator.razor     # /audit/reports
│   │
│   ├── Shared/                   # Shared/reusable components
│   │   ├── StatusBadge.razor              # Status indicator badge
│   │   ├── ConfidenceIndicator.razor     # Confidence score display
│   │   ├── SourceChip.razor               # Source type chip/badge
│   │   ├── FieldAnnotationDisplay.razor  # Field annotation UI
│   │   ├── SLACountdownTimer.razor        # SLA countdown component
│   │   ├── ProcessingStatusIndicator.razor  # Processing status display
│   │   └── UnifiedMetadataRecord.razor   # Unified metadata display
│   │
│   ├── Account/                  # Identity/authentication components
│   │   ├── Pages/                # Account management pages
│   │   │   ├── Login.razor
│   │   │   ├── Register.razor
│   │   │   ├── ForgotPassword.razor
│   │   │   └── Manage/          # Account management sub-pages
│   │   └── Shared/              # Shared account components
│   │       ├── ExternalLoginPicker.razor
│   │       └── StatusMessage.razor
│   │
│   └── Widgets/                  # Dashboard/widget components
│       ├── SLAAlertsWidget.razor          # SLA alerts dashboard widget
│       ├── ProcessingStatsWidget.razor    # Processing statistics widget
│       ├── ReviewQueueWidget.razor       # Review queue summary widget
│       └── RecentActivityWidget.razor    # Recent activity feed widget
│
├── Controllers/                  # API controllers (REST endpoints)
│   ├── DocumentProcessingController.cs
│   ├── ReviewController.cs
│   ├── SLAController.cs
│   ├── ExportController.cs
│   ├── AuditController.cs
│   └── HealthCheckController.cs
│
├── Services/                     # Client-side services (if needed)
│   ├── SignalRService.cs        # SignalR connection management
│   ├── ApiClientService.cs      # HTTP client wrapper
│   └── NotificationService.cs   # Toast notification service
│
├── Models/                       # View models and DTOs
│   ├── ProcessingResponse.cs
│   ├── ReviewCaseViewModel.cs
│   ├── SLAStatusViewModel.cs
│   └── ExportRequestViewModel.cs
│
├── Data/                         # Data access and Identity
│   ├── ApplicationDbContext.cs
│   ├── ApplicationUser.cs
│   └── Migrations/              # EF Core migrations
│
├── Hubs/                         # SignalR hubs
│   ├── ProcessingHub.cs         # Processing status updates
│   └── SLAHub.cs                # SLA alerts and updates
│
├── wwwroot/                      # Static assets
│   ├── css/                     # Custom CSS files
│   │   └── site.css            # Site-specific styles
│   ├── js/                      # JavaScript files
│   │   ├── download.js         # File download utilities
│   │   └── signalr-helpers.js  # SignalR helper functions
│   ├── images/                  # Image assets
│   └── favicon.ico
│
├── Program.cs                    # Application entry point
├── appsettings.json             # Configuration
├── appsettings.Development.json
└── ExxerCube.Prisma.Web.UI.csproj
```

### Component Organization Principles

1. **Pages Directory:** Contains all routable page components. Each page corresponds to a route (e.g., `/review/queue` → `Review/ReviewQueue.razor`).

2. **Feature-Based Organization:** Pages are organized by feature domain (Documents, Review, SLA, Compliance, Export, Audit) to improve maintainability and discoverability.

3. **Shared Components:** Reusable UI components that are used across multiple pages are placed in `Components/Shared/`. These are domain-agnostic components.

4. **Widgets:** Dashboard-specific components that can be composed into dashboard pages are placed in `Components/Widgets/`.

5. **Account Components:** Identity-related components remain in `Components/Account/` following ASP.NET Core Identity conventions.

6. **Naming Conventions:**
   - Page components: PascalCase matching route name (e.g., `ReviewQueue.razor`)
   - Shared components: Descriptive PascalCase (e.g., `StatusBadge.razor`)
   - Code-behind files: `ComponentName.razor.cs` (if using code-behind pattern)

### File Location Rules for AI Tools

**When creating a new page component:**
- Place in `Components/Pages/{Feature}/` directory
- Use `@page "/feature/route-name"` directive
- Follow existing naming patterns

**When creating a reusable component:**
- If used across multiple features → `Components/Shared/`
- If dashboard-specific → `Components/Widgets/`
- If feature-specific → `Components/Pages/{Feature}/` (as sub-component)

**When creating an API endpoint:**
- Add controller in `Controllers/` directory
- Follow RESTful naming: `{Resource}Controller.cs`
- Use attribute routing: `[Route("api/[controller]")]`

**When creating a SignalR hub:**
- Place in `Hubs/` directory
- Name: `{Feature}Hub.cs`
- Register in `Program.cs` with `app.MapHub<FeatureHub>("/featureHub")`

**When creating a view model/DTO:**
- Place in `Models/` directory
- Name: `{Purpose}ViewModel.cs` or `{Purpose}Dto.cs`
- Use for data transfer between components and API

### Integration with Backend Layers

The UI project references:
- `ExxerCube.Prisma.Application` - For application services and use cases
- `ExxerCube.Prisma.Infrastructure` - For infrastructure adapters (when needed)
- `ExxerCube.Prisma.Domain` - For domain entities (read-only, for display)

**Important:** The UI layer should NOT directly reference Infrastructure implementations. All access should go through Application layer services or API controllers.

---

## Component Standards

This section defines the exact patterns for component creation based on Blazor Server and MudBlazor conventions. All new components must follow these standards to ensure consistency and maintainability.

### Component Template

The following template represents a complete, production-ready Blazor Server component following project conventions:

```razor
@page "/feature/component-name"
@using ExxerCube.Prisma.Application.Services
@using ExxerCube.Prisma.Domain.Entities
@using Microsoft.AspNetCore.Components.Forms
@using MudBlazor
@using Microsoft.AspNetCore.SignalR.Client
@using Microsoft.AspNetCore.Authorization
@attribute [Authorize]  // If authentication required

@inject IApplicationService ApplicationService
@inject ISnackbar Snackbar
@inject NavigationManager Navigation
@inject IJSRuntime JSRuntime

<PageTitle>Component Title</PageTitle>

<MudContainer MaxWidth="MaxWidth.Large" Class="mt-4">
    <MudPaper Class="pa-6" Elevation="3">
        <MudText Typo="Typo.h4" Class="mb-4">
            <MudIcon Icon="@Icons.Material.Filled.IconName" Class="mr-2" />
            Component Title
        </MudText>
        
        <MudText Typo="Typo.body2" Class="mb-3">
            Component description or instructions.
        </MudText>

        @if (isLoading)
        {
            <MudProgressLinear Indeterminate="true" Color="Color.Primary" Class="mb-4" />
        }

        @if (!string.IsNullOrEmpty(errorMessage))
        {
            <MudAlert Severity="Severity.Error" Class="mb-4">
                @errorMessage
            </MudAlert>
        }

        <!-- Main Content Section -->
        <MudCard Class="mb-4">
            <MudCardHeader>
                <MudText Typo="Typo.h6">
                    Section Title
                </MudText>
            </MudCardHeader>
            <MudCardContent>
                <!-- Component content here -->
            </MudCardContent>
        </MudCard>

        <!-- Action Buttons -->
        <MudStack Row="true" Justify="Justify.FlexEnd" Spacing="2" Class="mt-4">
            <MudButton Variant="Variant.Text" Color="Color.Default" OnClick="HandleCancel">
                Cancel
            </MudButton>
            <MudButton Variant="Variant.Filled" Color="Color.Primary" OnClick="HandleSubmit" Disabled="isLoading">
                Submit
            </MudButton>
        </MudStack>
    </MudPaper>
</MudContainer>

@code {
    // Private fields - use camelCase
    private bool isLoading = false;
    private string? errorMessage;
    private HubConnection? hubConnection;

    // Public properties for component parameters - use PascalCase
    [Parameter]
    public string? Id { get; set; }

    [Parameter]
    public EventCallback<string> OnItemSelected { get; set; }

    // Lifecycle methods
    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
        await InitializeSignalRAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // First render initialization (e.g., JS interop)
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
        {
            await hubConnection.DisposeAsync();
        }
    }

    // Private methods - use camelCase, async methods end with Async
    private async Task LoadDataAsync()
    {
        try
        {
            isLoading = true;
            errorMessage = null;
            StateHasChanged();

            // Load data logic here
            await Task.Delay(100); // Placeholder
        }
        catch (Exception ex)
        {
            errorMessage = $"Error loading data: {ex.Message}";
            Snackbar.Add(errorMessage, Severity.Error);
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task InitializeSignalRAsync()
    {
        try
        {
            hubConnection = new HubConnectionBuilder()
                .WithUrl(Navigation.ToAbsoluteUri("/hubName"))
                .Build();

            hubConnection.On<string>("EventName", HandleEvent);

            await hubConnection.StartAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to connect to real-time updates: {ex.Message}", Severity.Warning);
        }
    }

    // Event handlers - use Handle prefix or On prefix
    private async Task HandleSubmit()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            // Submit logic here

            Snackbar.Add("Operation completed successfully", Severity.Success);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error: {ex.Message}", Severity.Error);
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private void HandleCancel()
    {
        Navigation.NavigateTo("/previous-page");
    }

    private void HandleEvent(string data)
    {
        // Handle SignalR event
        StateHasChanged();
    }
}
```

### Component Template Guidelines

**Required Elements:**
1. **@page directive** - Must be first line (for page components)
2. **@using directives** - Grouped by namespace (Blazor → MudBlazor → Application → Domain → Project)
3. **@inject directives** - Dependency injection, alphabetical order
4. **PageTitle component** - For SEO and browser tab title
5. **MudContainer** - Wraps all content with consistent max-width
6. **MudPaper** - Main content container with elevation
7. **Error handling** - Try-catch blocks with user-friendly error messages
8. **Loading states** - MudProgressLinear for async operations
9. **Dispose pattern** - Implement IAsyncDisposable for SignalR connections

**Optional but Recommended:**
- **@attribute [Authorize]** - For protected pages
- **SignalR integration** - For real-time updates
- **MudAlert** - For error display
- **MudStack** - For button groups and layouts

### Naming Conventions

#### Component Files
- **Page components:** `FeatureName.razor` (e.g., `ReviewQueue.razor`, `CaseDetail.razor`)
- **Shared components:** `ComponentPurpose.razor` (e.g., `StatusBadge.razor`, `ConfidenceIndicator.razor`)
- **Widget components:** `WidgetName.razor` (e.g., `SLAAlertsWidget.razor`)
- **Code-behind files:** `ComponentName.razor.cs` (if using partial class pattern)

#### Component Classes (if using code-behind)
- **Class name:** Matches file name (e.g., `ReviewQueue` for `ReviewQueue.razor`)
- **Namespace:** `ExxerCube.Prisma.Web.UI.Components.Pages.{Feature}` or `ExxerCube.Prisma.Web.UI.Components.Shared`

#### Parameters
- **Public properties:** PascalCase (e.g., `CaseId`, `OnItemSelected`)
- **Parameter attribute:** `[Parameter]` for component parameters
- **Cascading parameters:** `[CascadingParameter]` for cascading values

#### Fields and Variables
- **Private fields:** camelCase (e.g., `isLoading`, `errorMessage`, `hubConnection`)
- **Local variables:** camelCase
- **Constants:** PascalCase (e.g., `MaxFileSize`, `DefaultPageSize`)

#### Methods
- **Public methods:** PascalCase (e.g., `LoadData`, `HandleSubmit`)
- **Private methods:** camelCase (e.g., `loadDataAsync`, `handleSubmit`)
- **Async methods:** End with `Async` suffix (e.g., `LoadDataAsync`, `InitializeSignalRAsync`)
- **Event handlers:** Prefix with `Handle` or `On` (e.g., `HandleSubmit`, `OnFileSelected`)

#### MudBlazor Component Usage
- **Consistent spacing:** Use MudBlazor spacing classes (`mb-4`, `mt-2`, `pa-6`)
- **Icons:** Use `Icons.Material.Filled.{IconName}` pattern
- **Colors:** Use MudBlazor Color enum (`Color.Primary`, `Color.Success`, `Color.Error`)
- **Variants:** Use MudBlazor Variant enum (`Variant.Filled`, `Variant.Outlined`, `Variant.Text`)

#### Route Patterns
- **Feature routes:** `/feature/action` (e.g., `/review/queue`, `/sla/dashboard`)
- **Detail routes:** `/feature/item/{id}` (e.g., `/review/case/{id}`, `/export/history/{id}`)
- **Nested routes:** `/feature/subfeature/action` (e.g., `/audit/reports/generate`)

### Component Structure Best Practices

1. **Single Responsibility:** Each component should have one clear purpose
2. **Composition over Complexity:** Break large components into smaller, reusable pieces
3. **Parameter Validation:** Validate parameters in `OnParametersSet` or `OnParametersSetAsync`
4. **Error Boundaries:** Use try-catch blocks around async operations
5. **State Management:** Use component state for local UI state, services for shared state
6. **SignalR Cleanup:** Always dispose SignalR connections in `DisposeAsync`
7. **Loading States:** Show loading indicators for all async operations
8. **User Feedback:** Use Snackbar for success/error messages
9. **Accessibility:** Include ARIA labels and semantic HTML
10. **Performance:** Use `StateHasChanged()` only when necessary, avoid unnecessary re-renders

### Component Types and Patterns

#### Page Component Pattern
- Located in `Components/Pages/{Feature}/`
- Has `@page` directive
- Typically uses `MudContainer` and `MudPaper`
- Handles routing and navigation
- May contain multiple child components

#### Shared Component Pattern
- Located in `Components/Shared/`
- No `@page` directive
- Reusable across multiple features
- Accepts parameters for customization
- Stateless when possible

#### Widget Component Pattern
- Located in `Components/Widgets/`
- Designed for dashboard composition
- May have its own data loading logic
- Can be embedded in multiple pages
- Typically displays summary or aggregated data

#### Form Component Pattern
- Uses `EditForm` with `EditContext`
- MudBlazor form components (`MudTextField`, `MudSelect`, etc.)
- Validation using DataAnnotations or FluentValidation
- Submit handler with loading state
- Error display using `ValidationSummary` or custom error messages

---

## State Management

Blazor Server uses a different state management approach compared to traditional JavaScript frameworks. State is managed through component state, injected services, and SignalR for real-time updates. This section defines the state management patterns for the application.

### Store Structure

Blazor Server doesn't use a traditional "store" pattern like Redux or Vuex. Instead, state is managed through:

```
ExxerCube.Prisma.Web.UI/
├── Services/                     # State management services
│   ├── SignalRService.cs        # SignalR connection management (if needed)
│   ├── NotificationService.cs   # Toast notification state (if needed)
│   └── [Feature]StateService.cs  # Feature-specific state services (if needed)
│
├── Hubs/                         # SignalR hubs for real-time state updates
│   ├── ProcessingHub.cs         # Processing status updates
│   └── SLAHub.cs                # SLA alerts and updates
│
└── Components/                   # Component-local state
    └── [Component].razor        # Private fields for component state
```

**State Management Layers:**

1. **Component State** - Local to each component (private fields)
2. **Service State** - Shared state via injected services (Singleton/Scoped)
3. **SignalR State** - Real-time updates via SignalR hubs
4. **Cascading Parameters** - Shared state passed down component tree

### State Management Template

The following template shows how to implement state management in a Blazor Server component:

```csharp
@page "/feature/component"
@using ExxerCube.Prisma.Application.Services
@using Microsoft.AspNetCore.SignalR.Client
@inject IApplicationService ApplicationService
@inject ISnackbar Snackbar
@inject NavigationManager Navigation
@implements IAsyncDisposable

<PageTitle>Component</PageTitle>

<!-- Component markup -->

@code {
    // ============================================
    // Component State (Local)
    // ============================================
    private bool isLoading = false;
    private string? errorMessage;
    private List<ItemViewModel> items = new();
    private ItemViewModel? selectedItem;

    // ============================================
    // SignalR State (Real-time Updates)
    // ============================================
    private HubConnection? hubConnection;

    // ============================================
    // Lifecycle Methods
    // ============================================
    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
        await InitializeSignalRAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (hubConnection is not null)
        {
            await hubConnection.DisposeAsync();
        }
    }

    // ============================================
    // Data Loading (Service-based State)
    // ============================================
    private async Task LoadDataAsync()
    {
        try
        {
            isLoading = true;
            errorMessage = null;
            StateHasChanged();

            // Load from service (shared state)
            items = await ApplicationService.GetItemsAsync();
        }
        catch (Exception ex)
        {
            errorMessage = $"Error loading data: {ex.Message}";
            Snackbar.Add(errorMessage, Severity.Error);
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    // ============================================
    // SignalR Initialization (Real-time State)
    // ============================================
    private async Task InitializeSignalRAsync()
    {
        try
        {
            hubConnection = new HubConnectionBuilder()
                .WithUrl(Navigation.ToAbsoluteUri("/featureHub"))
                .Build();

            // Subscribe to real-time updates
            hubConnection.On<List<ItemViewModel>>("ItemsUpdated", OnItemsUpdated);
            hubConnection.On<ItemViewModel>("ItemAdded", OnItemAdded);
            hubConnection.On<string>("ItemDeleted", OnItemDeleted);

            await hubConnection.StartAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Failed to connect to real-time updates: {ex.Message}", Severity.Warning);
        }
    }

    // ============================================
    // SignalR Event Handlers (State Updates)
    // ============================================
    private void OnItemsUpdated(List<ItemViewModel> updatedItems)
    {
        items = updatedItems;
        StateHasChanged();
    }

    private void OnItemAdded(ItemViewModel newItem)
    {
        items.Add(newItem);
        StateHasChanged();
    }

    private void OnItemDeleted(string itemId)
    {
        items.RemoveAll(i => i.Id == itemId);
        StateHasChanged();
    }

    // ============================================
    // State Modification Methods
    // ============================================
    private async Task AddItemAsync(ItemViewModel item)
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            // Update service state
            await ApplicationService.AddItemAsync(item);

            // SignalR will notify all clients, including this one
            // So we don't need to manually update local state
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error adding item: {ex.Message}", Severity.Error);
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private void SelectItem(ItemViewModel item)
    {
        // Update local component state
        selectedItem = item;
        StateHasChanged();
    }
}
```

### State Management Patterns

#### 1. Component Local State

**Use for:** UI state that doesn't need to be shared (loading flags, form inputs, selected items)

```csharp
@code {
    // Local UI state
    private bool isExpanded = false;
    private string searchText = "";
    private int selectedIndex = 0;
    
    // Local data cache (fetched from service)
    private List<ItemViewModel>? cachedItems;
}
```

**Guidelines:**
- Use for component-specific UI state
- State is lost when component is disposed
- Use `StateHasChanged()` to trigger re-render after state changes

#### 2. Service-Based State (Singleton/Scoped)

**Use for:** Shared state across multiple components or users

```csharp
// Service registered as Singleton in Program.cs
public class ApplicationStateService
{
    private readonly ConcurrentDictionary<string, object> _state = new();
    
    public T? GetState<T>(string key) where T : class
    {
        return _state.TryGetValue(key, out var value) ? value as T : null;
    }
    
    public void SetState<T>(string key, T value) where T : class
    {
        _state.AddOrUpdate(key, value, (k, v) => value);
    }
}
```

**Registration in Program.cs:**
```csharp
builder.Services.AddSingleton<ApplicationStateService>();
```

**Guidelines:**
- **Singleton:** Shared across all users (e.g., metrics, configuration)
- **Scoped:** Per user session (e.g., user preferences, current session data)
- Use thread-safe collections (`ConcurrentDictionary`, `ConcurrentQueue`) for Singleton services

#### 3. SignalR Real-Time State

**Use for:** Real-time updates that need to be pushed to clients

```csharp
// SignalR Hub
public class FeatureHub : Hub
{
    public async Task NotifyStateChanged(string stateKey, object newState)
    {
        await Clients.All.SendAsync("StateChanged", stateKey, newState);
    }
}

// Component SignalR Connection
private HubConnection? hubConnection;

private async Task InitializeSignalRAsync()
{
    hubConnection = new HubConnectionBuilder()
        .WithUrl(Navigation.ToAbsoluteUri("/featureHub"))
        .Build();

    hubConnection.On<string, object>("StateChanged", (key, state) =>
    {
        // Update local state based on SignalR message
        UpdateLocalState(key, state);
        StateHasChanged();
    });

    await hubConnection.StartAsync();
}
```

**Guidelines:**
- Use for real-time updates (SLA alerts, processing status, notifications)
- Always dispose SignalR connections in `DisposeAsync`
- Handle connection failures gracefully

#### 4. Cascading Parameters

**Use for:** State that needs to be shared down a component tree

```csharp
// Parent Component
<CascadingValue Value="@currentUser" Name="CurrentUser">
    <ChildComponent />
</CascadingValue>

@code {
    private UserViewModel? currentUser;
}

// Child Component
@code {
    [CascadingParameter(Name = "CurrentUser")]
    private UserViewModel? CurrentUser { get; set; }
}
```

**Guidelines:**
- Use for shared context (current user, theme, feature flags)
- Avoid deep cascading (prefer services for complex state)
- Use `Name` parameter to avoid conflicts

### State Management Best Practices

1. **Choose the Right Pattern:**
   - Component state → Local UI state
   - Service state → Shared application state
   - SignalR → Real-time updates
   - Cascading parameters → Context shared down tree

2. **Thread Safety:**
   - Use thread-safe collections for Singleton services
   - Use `SemaphoreSlim` or `lock` for critical sections
   - SignalR callbacks run on UI thread (safe for StateHasChanged)

3. **State Updates:**
   - Always call `StateHasChanged()` after modifying state (unless using SignalR which triggers automatically)
   - Batch state updates when possible
   - Avoid unnecessary re-renders

4. **Memory Management:**
   - Dispose SignalR connections properly
   - Clear large collections when not needed
   - Use `IDisposable` or `IAsyncDisposable` for services with resources

5. **Error Handling:**
   - Handle state update failures gracefully
   - Show user-friendly error messages
   - Log state management errors for debugging

### Common State Management Scenarios

#### Scenario 1: Dashboard Metrics (Singleton Service)
```csharp
// Service: ProcessingMetricsService (Singleton)
// Used by: Dashboard component
// Pattern: Service-based state with SignalR updates
```

#### Scenario 2: User Session Data (Scoped Service)
```csharp
// Service: UserSessionService (Scoped)
// Used by: Multiple components in user session
// Pattern: Service-based state, per-user
```

#### Scenario 3: Real-time SLA Alerts (SignalR)
```csharp
// Hub: SLAHub
// Used by: SLA Dashboard component
// Pattern: SignalR real-time updates
```

#### Scenario 4: Form Input State (Component State)
```csharp
// Component: ReviewCaseDetail
// Used by: Single component instance
// Pattern: Component local state
```

#### Scenario 5: Current User Context (Cascading Parameter)
```csharp
// CascadingValue in MainLayout
// Used by: All child components
// Pattern: Cascading parameter
```

---

## API Integration

Blazor Server components can integrate with backend APIs in two ways: **direct service injection** (preferred) or **HTTP client calls** to API controllers. This section defines the patterns for both approaches.

### Service Template

**Pattern 1: Direct Service Injection (Recommended for Blazor Server)**

Since Blazor Server runs on the server, components can directly inject and call Application layer services without HTTP overhead:

```csharp
@page "/feature/component"
@using ExxerCube.Prisma.Application.Services
@inject IApplicationService ApplicationService
@inject ISnackbar Snackbar

<PageTitle>Component</PageTitle>

<!-- Component markup -->

@code {
    private List<ItemViewModel> items = new();
    private bool isLoading = false;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            // Direct service call - no HTTP overhead
            items = await ApplicationService.GetItemsAsync();
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error loading data: {ex.Message}", Severity.Error);
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task SaveItemAsync(ItemViewModel item)
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            // Direct service call
            var result = await ApplicationService.SaveItemAsync(item);
            
            if (result.IsSuccess)
            {
                Snackbar.Add("Item saved successfully", Severity.Success);
                await LoadDataAsync(); // Refresh data
            }
            else
            {
                Snackbar.Add($"Error: {result.Error}", Severity.Error);
            }
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error saving item: {ex.Message}", Severity.Error);
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }
}
```

**Pattern 2: HTTP Client Calls (For API Controllers)**

When calling API controllers (useful for future separation or external APIs):

```csharp
@page "/feature/component"
@using System.Net.Http.Json
@inject IHttpClientFactory HttpClientFactory
@inject ISnackbar Snackbar

<PageTitle>Component</PageTitle>

<!-- Component markup -->

@code {
    private List<ItemViewModel> items = new();
    private bool isLoading = false;

    protected override async Task OnInitializedAsync()
    {
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            var httpClient = HttpClientFactory.CreateClient("api");
            var response = await httpClient.GetFromJsonAsync<List<ItemViewModel>>("api/items");

            if (response != null)
            {
                items = response;
            }
        }
        catch (HttpRequestException ex)
        {
            Snackbar.Add($"Network error: {ex.Message}", Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error loading data: {ex.Message}", Severity.Error);
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }

    private async Task SaveItemAsync(ItemViewModel item)
    {
        try
        {
            isLoading = true;
            StateHasChanged();

            var httpClient = HttpClientFactory.CreateClient("api");
            var response = await httpClient.PostAsJsonAsync("api/items", item);

            if (response.IsSuccessStatusCode)
            {
                Snackbar.Add("Item saved successfully", Severity.Success);
                await LoadDataAsync(); // Refresh data
            }
            else
            {
                var errorMessage = await response.Content.ReadAsStringAsync();
                Snackbar.Add($"Error: {errorMessage}", Severity.Error);
            }
        }
        catch (HttpRequestException ex)
        {
            Snackbar.Add($"Network error: {ex.Message}", Severity.Error);
        }
        catch (Exception ex)
        {
            Snackbar.Add($"Error saving item: {ex.Message}", Severity.Error);
        }
        finally
        {
            isLoading = false;
            StateHasChanged();
        }
    }
}
```

### API Client Configuration

HTTP clients are configured in `Program.cs`:

```csharp
// Program.cs
builder.Services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7062/");
    client.Timeout = TimeSpan.FromMinutes(5);
    
    // Add default headers if needed
    client.DefaultRequestHeaders.Add("Accept", "application/json");
});
```

**Configuration Options:**
- **BaseAddress:** API base URL (from configuration or default)
- **Timeout:** Request timeout (default: 5 minutes for long-running operations)
- **Default Headers:** Common headers (Accept, Authorization, etc.)

**For Authentication:**
```csharp
builder.Services.AddHttpClient("api", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7062/");
    client.Timeout = TimeSpan.FromMinutes(5);
})
.AddHttpMessageHandler<AuthTokenHandler>(); // Custom handler for auth tokens
```

### API Controller Pattern

API controllers follow RESTful conventions and integrate with Application services:

```csharp
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using ExxerCube.Prisma.Application.Services;
using ExxerCube.Prisma.Web.UI.Models;
using ExxerCube.Prisma.Web.UI.Hubs;

namespace ExxerCube.Prisma.Web.UI.Controllers;

/// <summary>
/// API controller for feature operations.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class FeatureController : ControllerBase
{
    private readonly IApplicationService _applicationService;
    private readonly ILogger<FeatureController> _logger;
    private readonly FeatureHub _featureHub;

    public FeatureController(
        IApplicationService applicationService,
        ILogger<FeatureController> logger,
        FeatureHub featureHub)
    {
        _applicationService = applicationService;
        _logger = logger;
        _featureHub = featureHub;
    }

    /// <summary>
    /// Gets all items.
    /// </summary>
    /// <returns>The list of items.</returns>
    [HttpGet]
    public async Task<IActionResult> GetItems()
    {
        try
        {
            var items = await _applicationService.GetItemsAsync();
            return Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get items");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Gets an item by ID.
    /// </summary>
    /// <param name="id">The item identifier.</param>
    /// <returns>The item.</returns>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetItem(string id)
    {
        try
        {
            var item = await _applicationService.GetItemByIdAsync(id);
            
            if (item == null)
            {
                return NotFound($"Item with ID {id} not found");
            }

            return Ok(item);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get item: {ItemId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Creates a new item.
    /// </summary>
    /// <param name="request">The item creation request.</param>
    /// <returns>The created item.</returns>
    [HttpPost]
    public async Task<IActionResult> CreateItem([FromBody] CreateItemRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _applicationService.CreateItemAsync(request);

            if (result.IsSuccess)
            {
                // Notify all clients via SignalR
                await _featureHub.NotifyItemCreated(result.Value!);
                
                return CreatedAtAction(nameof(GetItem), new { id = result.Value!.Id }, result.Value);
            }
            else
            {
                return BadRequest(result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create item");
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Updates an existing item.
    /// </summary>
    /// <param name="id">The item identifier.</param>
    /// <param name="request">The item update request.</param>
    /// <returns>The updated item.</returns>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateItem(string id, [FromBody] UpdateItemRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var result = await _applicationService.UpdateItemAsync(id, request);

            if (result.IsSuccess)
            {
                // Notify all clients via SignalR
                await _featureHub.NotifyItemUpdated(result.Value!);
                
                return Ok(result.Value);
            }
            else
            {
                return BadRequest(result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update item: {ItemId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    /// <summary>
    /// Deletes an item.
    /// </summary>
    /// <param name="id">The item identifier.</param>
    /// <returns>No content.</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteItem(string id)
    {
        try
        {
            var result = await _applicationService.DeleteItemAsync(id);

            if (result.IsSuccess)
            {
                // Notify all clients via SignalR
                await _featureHub.NotifyItemDeleted(id);
                
                return NoContent();
            }
            else
            {
                return BadRequest(result.Error);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete item: {ItemId}", id);
            return StatusCode(500, "Internal server error");
        }
    }
}
```

### API Integration Best Practices

1. **Choose the Right Pattern:**
   - **Direct Service Injection:** Use for Blazor Server components (same process, no HTTP overhead)
   - **HTTP Client:** Use for API controllers, external APIs, or future separation

2. **Error Handling:**
   - Always wrap API calls in try-catch blocks
   - Use `Result<T>` pattern from Application layer when available
   - Provide user-friendly error messages via Snackbar
   - Log errors for debugging

3. **Loading States:**
   - Show loading indicators during API calls
   - Disable buttons/forms while processing
   - Use `StateHasChanged()` after state updates

4. **SignalR Integration:**
   - Use SignalR hubs to notify clients of state changes
   - Broadcast updates after successful API operations
   - Handle SignalR connection failures gracefully

5. **Request/Response Models:**
   - Use DTOs/ViewModels for API requests and responses
   - Don't expose domain entities directly
   - Validate requests using DataAnnotations or FluentValidation

6. **Authentication:**
   - Use `[Authorize]` attribute on controllers/actions
   - Pass authentication tokens in HTTP client headers
   - Handle 401/403 responses appropriately

### Common API Integration Scenarios

#### Scenario 1: Loading List Data (Direct Service)
```csharp
@inject IReviewService ReviewService

private async Task LoadReviewCasesAsync()
{
    var cases = await ReviewService.GetReviewCasesAsync();
    // Use cases directly
}
```

#### Scenario 2: File Upload (API Controller)
```csharp
@inject IHttpClientFactory HttpClientFactory

private async Task UploadFileAsync(IBrowserFile file)
{
    var httpClient = HttpClientFactory.CreateClient("api");
    var content = new MultipartFormDataContent();
    content.Add(new StreamContent(file.OpenReadStream()), "file", file.Name);
    
    var response = await httpClient.PostAsync("api/documents/upload", content);
    // Handle response
}
```

#### Scenario 3: Real-time Updates (SignalR + API)
```csharp
// API Controller
[HttpPost("approve/{caseId}")]
public async Task<IActionResult> ApproveCase(string caseId)
{
    var result = await _reviewService.ApproveCaseAsync(caseId);
    if (result.IsSuccess)
    {
        await _reviewHub.NotifyCaseApproved(caseId);
    }
    return Ok(result);
}

// Component SignalR Handler
hubConnection.On<string>("CaseApproved", (caseId) =>
{
    // Update UI
    StateHasChanged();
});
```

#### Scenario 4: Paginated Data (Direct Service)
```csharp
@inject IReviewService ReviewService

private async Task LoadPageAsync(int pageNumber, int pageSize)
{
    var result = await ReviewService.GetReviewCasesPagedAsync(pageNumber, pageSize);
    // Use result.Items and result.TotalCount
}
```

#### Scenario 5: Batch Operations (API Controller)
```csharp
[HttpPost("batch-approve")]
public async Task<IActionResult> BatchApprove([FromBody] string[] caseIds)
{
    var result = await _reviewService.BatchApproveAsync(caseIds);
    if (result.IsSuccess)
    {
        await _reviewHub.NotifyBatchApproved(caseIds);
    }
    return Ok(result);
}
```

---

## Routing

Blazor Server uses a built-in router that supports route parameters, authorization, lazy loading, and navigation. This section defines the routing structure and patterns for the application.

### Route Configuration

The router is configured in `Components/Routes.razor`:

```razor
@using ExxerCube.Prisma.Web.UI.Components.Account.Shared
<Router AppAssembly="typeof(Program).Assembly">
    <Found Context="routeData">
        <AuthorizeRouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)">
            <NotAuthorized>
                <RedirectToLogin />
            </NotAuthorized>
        </AuthorizeRouteView>
        <FocusOnNavigate RouteData="routeData" Selector="h1" />
    </Found>
    <NotFound>
        <PageTitle>Not found</PageTitle>
        <LayoutView Layout="typeof(Layout.MainLayout)">
            <MudContainer>
                <MudText Typo="Typo.h4">Sorry, there's nothing at this address.</MudText>
            </MudContainer>
        </LayoutView>
    </NotFound>
</Router>
```

**Key Components:**
- **Router:** Scans assembly for `@page` directives
- **AuthorizeRouteView:** Handles authorization for routes
- **DefaultLayout:** `MainLayout` wraps all pages
- **FocusOnNavigate:** Accessibility feature (focuses h1 on navigation)
- **RedirectToLogin:** Redirects unauthorized users to login

### Route Patterns

#### Basic Routes

```razor
@page "/dashboard"
@page "/review/queue"
@page "/sla/dashboard"
```

#### Route Parameters

```razor
@page "/review/case/{CaseId}"
@page "/export/history/{ExportId:int}"
@page "/audit/viewer/{FileId?}"  // Optional parameter
```

#### Multiple Routes

```razor
@page "/"
@page "/home"
```

#### Route Constraints

```razor
@page "/review/case/{CaseId:guid}"      // GUID constraint
@page "/export/history/{Year:int}"       // Integer constraint
@page "/documents/{FileName:required}"   // Required parameter
```

### Route Structure

Based on the front-end specification, the following route structure is defined:

```
/                           → Home/Dashboard
/dashboard                  → Main Dashboard
/documents/processing-queue → Document Processing Queue
/documents/upload           → File Upload
/documents/automation-results → Browser Automation Results
/review/queue               → Review Queue
/review/case/{id}           → Case Detail
/sla/dashboard              → SLA Monitoring Dashboard
/sla/active-cases           → Active Cases
/sla/escalations            → Escalation Management
/compliance/validation/{caseId} → Compliance Validation
/export/management          → Export Management
/export/history             → Export History
/audit/viewer               → Audit Trail Viewer
/audit/reports               → Report Generator
/Account/*                   → Identity/Account pages (ASP.NET Core Identity)
```

### Protected Routes

Routes that require authentication use the `[Authorize]` attribute:

```razor
@page "/review/queue"
@using Microsoft.AspNetCore.Authorization
@attribute [Authorize]

<PageTitle>Review Queue</PageTitle>
<!-- Component content -->
```

**Role-Based Authorization:**
```razor
@page "/settings"
@attribute [Authorize(Roles = "Administrator")]

<PageTitle>Settings</PageTitle>
<!-- Component content -->
```

**Policy-Based Authorization:**
```razor
@page "/export/management"
@attribute [Authorize(Policy = "CanExport")]

<PageTitle>Export Management</PageTitle>
<!-- Component content -->
```

### Navigation Patterns

#### Programmatic Navigation

```csharp
@inject NavigationManager Navigation

private void NavigateToCase(string caseId)
{
    Navigation.NavigateTo($"/review/case/{caseId}");
}

private async Task NavigateAfterSave()
{
    // Perform save operation
    await SaveAsync();
    
    // Navigate after successful save
    Navigation.NavigateTo("/review/queue");
}
```

#### Navigation with Query Parameters

```csharp
private void NavigateWithQuery(string filter, int page)
{
    Navigation.NavigateTo($"/review/queue?filter={filter}&page={page}");
}

// Reading query parameters
@code {
    [SupplyParameterFromQuery]
    public string? Filter { get; set; }
    
    [SupplyParameterFromQuery]
    public int Page { get; set; } = 1;
}
```

#### Navigation Guards

```csharp
@inject NavigationManager Navigation
@inject IJSRuntime JSRuntime

private async Task<bool> CanNavigateAway()
{
    if (hasUnsavedChanges)
    {
        var confirmed = await JSRuntime.InvokeAsync<bool>("confirm", 
            "You have unsaved changes. Are you sure you want to leave?");
        return confirmed;
    }
    return true;
}

private async Task HandleNavigation(NavigationContext context)
{
    if (!await CanNavigateAway())
    {
        context.PreventNavigation();
    }
}
```

### Lazy Loading

For large components, use lazy loading to improve initial load time:

```razor
@page "/reports"
@using Microsoft.AspNetCore.Components.Web.Virtualization

<PageTitle>Reports</PageTitle>

@if (reports == null)
{
    <MudProgressLinear Indeterminate="true" />
}
else
{
    <!-- Render reports -->
}
```

**Assembly-Level Lazy Loading:**
```csharp
// In Program.cs
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddAdditionalAssemblies(typeof(ReportsModule).Assembly); // Lazy loaded assembly
```

### Route Templates

#### Template 1: List Page

```razor
@page "/feature/items"
@attribute [Authorize]

<PageTitle>Items</PageTitle>

<!-- List content -->
```

#### Template 2: Detail Page

```razor
@page "/feature/item/{ItemId}"
@attribute [Authorize]

<PageTitle>Item Details</PageTitle>

@code {
    [Parameter]
    public string ItemId { get; set; } = string.Empty;
    
    protected override async Task OnParametersSetAsync()
    {
        await LoadItemAsync(ItemId);
    }
}
```

#### Template 3: Create/Edit Page

```razor
@page "/feature/item/create"
@page "/feature/item/edit/{ItemId}"
@attribute [Authorize]

<PageTitle>@(ItemId == null ? "Create Item" : "Edit Item")</PageTitle>

@code {
    [Parameter]
    public string? ItemId { get; set; }
    
    private bool IsEditMode => !string.IsNullOrEmpty(ItemId);
}
```

### Navigation Menu Integration

The navigation menu (`NavMenu.razor`) uses `MudNavLink` components:

```razor
<MudNavMenu>
    <MudNavLink Href="/dashboard" Match="NavLinkMatch.All">
        <MudIcon Icon="@Icons.Material.Filled.Dashboard" />
        Dashboard
    </MudNavLink>
    
    <MudNavLink Href="/review/queue">
        <MudIcon Icon="@Icons.Material.Filled.RateReview" />
        Review Queue
    </MudNavLink>
    
    <MudNavLink Href="/sla/dashboard">
        <MudIcon Icon="@Icons.Material.Filled.Schedule" />
        SLA Monitoring
    </MudNavLink>
</MudNavMenu>
```

**Active Route Highlighting:**
- `Match="NavLinkMatch.All"` - Exact match (for root/home)
- `Match="NavLinkMatch.Prefix"` - Prefix match (default, for nested routes)

### Routing Best Practices

1. **Route Naming:**
   - Use lowercase with hyphens: `/review/queue` not `/Review/Queue`
   - Use descriptive, RESTful routes
   - Group related routes by feature prefix

2. **Route Parameters:**
   - Use meaningful parameter names: `{CaseId}` not `{id}`
   - Add constraints when possible: `{CaseId:guid}`, `{Page:int}`
   - Validate parameters in `OnParametersSetAsync`

3. **Authorization:**
   - Use `[Authorize]` attribute for protected routes
   - Specify roles or policies when needed
   - Handle unauthorized access gracefully

4. **Navigation:**
   - Use `NavigationManager.NavigateTo()` for programmatic navigation
   - Use `MudNavLink` for menu navigation
   - Consider navigation guards for unsaved changes

5. **Performance:**
   - Use lazy loading for large, infrequently accessed pages
   - Prefer direct service calls over API calls when possible
   - Cache route data when appropriate

6. **Accessibility:**
   - Use `FocusOnNavigate` for keyboard navigation
   - Ensure route changes are announced to screen readers
   - Provide clear navigation breadcrumbs

### Common Routing Scenarios

#### Scenario 1: Feature List with Detail Navigation
```razor
// List Page
@page "/review/queue"

<MudTable Items="@reviewCases">
    <RowTemplate>
        <MudTd><MudLink Href="@($"/review/case/{context.CaseId}")">@context.CaseId</MudLink></MudTd>
    </RowTemplate>
</MudTable>

// Detail Page
@page "/review/case/{CaseId}"

@code {
    [Parameter]
    public string CaseId { get; set; } = string.Empty;
}
```

#### Scenario 2: Query Parameter Filtering
```razor
@page "/review/queue"
@code {
    [SupplyParameterFromQuery]
    public string? Status { get; set; }
    
    [SupplyParameterFromQuery]
    public int Page { get; set; } = 1;
    
    protected override async Task OnParametersSetAsync()
    {
        await LoadFilteredCasesAsync(Status, Page);
    }
}
```

#### Scenario 3: Conditional Routes
```razor
@page "/export/{Format?}"

@code {
    [Parameter]
    public string? Format { get; set; }
    
    private string ExportFormat => Format ?? "xml";
}
```

#### Scenario 4: Nested Routes with Layout
```razor
// Parent layout route
@page "/settings"
@layout SettingsLayout

// Child routes inherit SettingsLayout
@page "/settings/general"
@page "/settings/notifications"
```

---

## Styling Guidelines

The application uses MudBlazor's Material Design theme system with CSS custom properties (CSS variables) for customization. This section defines the styling approach, theme configuration, and custom CSS patterns.

### Styling Approach

**Primary Styling Method:** MudBlazor components with Material Design theme
- MudBlazor provides comprehensive component styling out of the box
- Material Design principles ensure consistency
- Theme customization via MudThemeProvider

**Custom CSS:** Used for project-specific styling and overrides
- Custom CSS files in `wwwroot/css/`
- CSS custom properties (CSS variables) for theme values
- Minimal custom CSS - prefer MudBlazor classes when possible

**Styling Hierarchy:**
1. MudBlazor default theme (Material Design)
2. MudThemeProvider customization (theme overrides)
3. CSS custom properties (global theme variables)
4. Component-specific CSS (when needed)

### Global Theme Variables

CSS custom properties provide a centralized theme system that works across all frameworks. Create `wwwroot/css/theme.css`:

```css
:root {
  /* Primary Colors */
  --color-primary: #1976D2;
  --color-secondary: #424242;
  --color-success: #4CAF50;
  --color-warning: #FF9800;
  --color-error: #F44336;
  --color-info: #2196F3;

  /* SLA Status Colors */
  --color-sla-normal: #4CAF50;      /* >24h remaining */
  --color-sla-warning: #FF9800;     /* <24h remaining */
  --color-sla-critical: #F44336;    /* <4h remaining */
  --color-sla-breached: #212121;    /* Deadline passed */

  /* Confidence Score Colors */
  --color-confidence-high: #4CAF50;    /* ≥90% */
  --color-confidence-medium: #FF9800;   /* 70-89% */
  --color-confidence-low: #F44336;      /* <70% */

  /* Source Badge Colors */
  --color-source-xml: #2196F3;
  --color-source-docx: #4CAF50;
  --color-source-pdf: #F44336;
  --color-source-ocr: #9C27B0;

  /* Neutral Colors */
  --color-background: #FAFAFA;
  --color-surface: #FFFFFF;
  --color-border: #E0E0E0;
  --color-text-primary: #212121;
  --color-text-secondary: #757575;
  --color-disabled: #BDBDBD;

  /* Typography */
  --font-family-primary: 'Roboto', sans-serif;
  --font-family-monospace: 'Roboto Mono', monospace;
  
  --font-size-h1: 2.5rem;      /* 40px */
  --font-size-h2: 2rem;        /* 32px */
  --font-size-h3: 1.75rem;     /* 28px */
  --font-size-h4: 1.5rem;      /* 24px */
  --font-size-h5: 1.25rem;     /* 20px */
  --font-size-h6: 1rem;        /* 16px */
  --font-size-body: 1rem;       /* 16px */
  --font-size-body-small: 0.875rem;  /* 14px */
  --font-size-caption: 0.75rem; /* 12px */

  --font-weight-light: 300;
  --font-weight-regular: 400;
  --font-weight-medium: 500;
  --font-weight-bold: 700;

  --line-height-tight: 1.2;
  --line-height-normal: 1.5;
  --line-height-relaxed: 1.75;

  /* Spacing (8px base unit) */
  --spacing-xs: 4px;    /* 0.5 units */
  --spacing-sm: 8px;    /* 1 unit */
  --spacing-md: 16px;   /* 2 units */
  --spacing-lg: 24px;   /* 3 units */
  --spacing-xl: 32px;   /* 4 units */
  --spacing-xxl: 48px;  /* 6 units */

  /* Component Spacing */
  --spacing-card-padding: 16px;
  --spacing-form-field-vertical: 16px;
  --spacing-form-field-horizontal: 8px;
  --spacing-button-padding-vertical: 12px;
  --spacing-button-padding-horizontal: 24px;
  --spacing-table-cell-vertical: 12px;
  --spacing-table-cell-horizontal: 16px;
  --spacing-modal-padding: 24px;

  /* Shadows & Elevation */
  --shadow-elevation-0: none;
  --shadow-elevation-1: 0 2px 4px rgba(0, 0, 0, 0.1);
  --shadow-elevation-2: 0 4px 8px rgba(0, 0, 0, 0.12);
  --shadow-elevation-3: 0 8px 16px rgba(0, 0, 0, 0.15);
  --shadow-elevation-4: 0 12px 24px rgba(0, 0, 0, 0.18);

  /* Border Radius */
  --border-radius-small: 4px;
  --border-radius-medium: 8px;
  --border-radius-large: 12px;

  /* Transitions */
  --transition-fast: 150ms ease-in-out;
  --transition-normal: 250ms ease-in-out;
  --transition-slow: 350ms ease-in-out;
}

/* Dark Mode Support (Future Enhancement) */
@media (prefers-color-scheme: dark) {
  :root {
    --color-background: #121212;
    --color-surface: #1E1E1E;
    --color-text-primary: #FFFFFF;
    --color-text-secondary: #B0B0B0;
    --color-border: #424242;
  }
}
```

**Usage in Components:**
```razor
<div style="background-color: var(--color-background); padding: var(--spacing-md);">
    <h1 style="color: var(--color-text-primary); font-size: var(--font-size-h1);">
        Title
    </h1>
</div>
```

### MudBlazor Theme Configuration

Customize MudBlazor theme in `MainLayout.razor` or a separate theme file:

```razor
<MudThemeProvider Theme="@customTheme" />

@code {
    private MudTheme customTheme = new()
    {
        Palette = new PaletteLight()
        {
            Primary = "#1976D2",
            Secondary = "#424242",
            Success = "#4CAF50",
            Warning = "#FF9800",
            Error = "#F44336",
            Info = "#2196F3",
            Background = "#FAFAFA",
            Surface = "#FFFFFF",
            AppbarBackground = "#1976D2",
            DrawerBackground = "#FFFFFF",
            DrawerText = "#212121"
        },
        Typography = new Typography()
        {
            Default = new Default()
            {
                FontFamily = new[] { "Roboto", "sans-serif" },
                FontSize = "1rem",
                FontWeight = 400,
                LineHeight = 1.5
            },
            H1 = new H1()
            {
                FontSize = "2.5rem",
                FontWeight = 500,
                LineHeight = 1.2
            },
            H2 = new H2()
            {
                FontSize = "2rem",
                FontWeight = 500,
                LineHeight = 1.2
            }
            // ... other typography settings
        },
        Shadows = new Shadow()
        {
            Elevation = new string[]
            {
                "none",
                "0 2px 4px rgba(0,0,0,0.1)",
                "0 4px 8px rgba(0,0,0,0.12)",
                "0 8px 16px rgba(0,0,0,0.15)",
                "0 12px 24px rgba(0,0,0,0.18)"
            }
        },
        LayoutProperties = new LayoutProperties()
        {
            DefaultBorderRadius = "4px",
            AppbarHeight = "64px",
            DrawerWidthLeft = "280px",
            DrawerWidthRight = "320px"
        }
    };
}
```

### MudBlazor Spacing Classes

MudBlazor provides utility classes for spacing:

**Margin:**
- `m-{size}` - All sides (e.g., `m-4` = 16px margin)
- `mt-{size}` - Top margin
- `mb-{size}` - Bottom margin
- `ml-{size}` - Left margin
- `mr-{size}` - Right margin
- `mx-{size}` - Horizontal (left + right)
- `my-{size}` - Vertical (top + bottom)

**Padding:**
- `p-{size}` - All sides (e.g., `p-6` = 24px padding)
- `pt-{size}` - Top padding
- `pb-{size}` - Bottom padding
- `pl-{size}` - Left padding
- `pr-{size}` - Right padding
- `px-{size}` - Horizontal padding
- `py-{size}` - Vertical padding

**Spacing Scale:**
- `0` = 0px
- `1` = 4px
- `2` = 8px
- `3` = 12px
- `4` = 16px
- `5` = 20px
- `6` = 24px
- `8` = 32px
- `12` = 48px

**Example Usage:**
```razor
<MudContainer Class="mt-4 mb-6">  <!-- 16px top, 24px bottom -->
    <MudPaper Class="pa-6">        <!-- 24px padding all sides -->
        <!-- Content -->
    </MudPaper>
</MudContainer>
```

### Custom CSS Patterns

When MudBlazor classes aren't sufficient, use custom CSS:

**File Structure:**
```
wwwroot/
├── css/
│   ├── site.css           # Global custom styles
│   ├── components.css     # Component-specific styles
│   └── utilities.css     # Utility classes
```

**Custom CSS Example (`wwwroot/css/site.css`):**
```css
/* Status Badge Custom Styles */
.status-badge-sla-normal {
    background-color: var(--color-sla-normal);
    color: white;
    padding: var(--spacing-xs) var(--spacing-sm);
    border-radius: var(--border-radius-small);
    font-size: var(--font-size-caption);
    font-weight: var(--font-weight-medium);
}

.status-badge-sla-warning {
    background-color: var(--color-sla-warning);
    color: white;
    padding: var(--spacing-xs) var(--spacing-sm);
    border-radius: var(--border-radius-small);
    font-size: var(--font-size-caption);
    font-weight: var(--font-weight-medium);
}

.status-badge-sla-critical {
    background-color: var(--color-sla-critical);
    color: white;
    padding: var(--spacing-xs) var(--spacing-sm);
    border-radius: var(--border-radius-small);
    font-size: var(--font-size-caption);
    font-weight: var(--font-weight-bold);
}

/* Confidence Indicator */
.confidence-indicator {
    display: flex;
    align-items: center;
    gap: var(--spacing-sm);
}

.confidence-indicator.high {
    color: var(--color-confidence-high);
}

.confidence-indicator.medium {
    color: var(--color-confidence-medium);
}

.confidence-indicator.low {
    color: var(--color-confidence-low);
}

/* Source Chip Styles */
.source-chip {
    display: inline-flex;
    align-items: center;
    padding: 6px 12px;
    border-radius: var(--border-radius-medium);
    font-size: var(--font-size-caption);
    font-weight: var(--font-weight-medium);
}

.source-chip-xml {
    background-color: var(--color-source-xml);
    color: white;
}

.source-chip-docx {
    background-color: var(--color-source-docx);
    color: white;
}

.source-chip-pdf {
    background-color: var(--color-source-pdf);
    color: white;
}

.source-chip-ocr {
    background-color: var(--color-source-ocr);
    color: white;
}

/* Field Annotation Display */
.field-annotation {
    display: flex;
    flex-direction: column;
    gap: var(--spacing-xs);
}

.field-annotation-conflict {
    border-left: 3px solid var(--color-warning);
    padding-left: var(--spacing-sm);
}

.field-annotation-low-confidence {
    border-left: 3px solid var(--color-error);
    padding-left: var(--spacing-sm);
}

/* Breadcrumb Styles */
.breadcrumb {
    display: flex;
    align-items: center;
    gap: var(--spacing-sm);
    margin-bottom: var(--spacing-md);
    font-size: var(--font-size-body-small);
    color: var(--color-text-secondary);
}

.breadcrumb-separator {
    color: var(--color-text-secondary);
}

.breadcrumb-current {
    color: var(--color-text-primary);
    font-weight: var(--font-weight-medium);
}
```

**Include Custom CSS in `App.razor`:**
```razor
<head>
    <!-- ... existing head content ... -->
    <link href="css/theme.css" rel="stylesheet" />
    <link href="css/site.css" rel="stylesheet" />
</head>
```

### Styling Best Practices

1. **Prefer MudBlazor Classes:**
   - Use MudBlazor spacing classes (`mb-4`, `pa-6`) over custom CSS
   - Use MudBlazor color enums (`Color.Primary`, `Color.Success`) over hex codes
   - Use MudBlazor typography (`Typo.h4`, `Typo.body1`) over custom font sizes

2. **CSS Custom Properties:**
   - Use CSS variables for theme values (colors, spacing, typography)
   - Centralize theme values in `theme.css`
   - Reference variables in custom CSS: `var(--color-primary)`

3. **Component-Specific Styles:**
   - Use scoped CSS when possible (Blazor CSS isolation)
   - Avoid inline styles except for dynamic values
   - Group related styles in component-specific CSS files

4. **Responsive Design:**
   - Use MudBlazor's responsive classes (`xs`, `sm`, `md`, `lg`, `xl`)
   - Use CSS media queries for complex responsive behavior
   - Test on multiple screen sizes

5. **Accessibility:**
   - Maintain WCAG 2.1 AA color contrast ratios
   - Use semantic HTML with MudBlazor components
   - Ensure focus indicators are visible

6. **Performance:**
   - Minimize custom CSS (prefer MudBlazor)
   - Use CSS custom properties for runtime theme changes
   - Avoid deep CSS selectors

### MudBlazor Component Styling Examples

#### Status Badge
```razor
<MudChip Color="@GetSlaColor(slaStatus)" Size="Size.Small">
    @slaStatus
</MudChip>

@code {
    private Color GetSlaColor(SlaStatus status) => status switch
    {
        SlaStatus.Normal => Color.Success,
        SlaStatus.Warning => Color.Warning,
        SlaStatus.Critical => Color.Error,
        SlaStatus.Breached => Color.Dark,
        _ => Color.Default
    };
}
```

#### Confidence Indicator
```razor
<MudProgressLinear 
    Value="@confidenceScore" 
    Color="@GetConfidenceColor(confidenceScore)"
    Class="mb-2" />
<MudText Typo="Typo.caption">@confidenceScore%</MudText>

@code {
    private Color GetConfidenceColor(int score) => score switch
    {
        >= 90 => Color.Success,
        >= 70 => Color.Warning,
        _ => Color.Error
    };
}
```

#### Source Chip
```razor
<MudChip 
    Color="@GetSourceColor(source)" 
    Size="Size.Small"
    Variant="Variant.Filled">
    @source
</MudChip>

@code {
    private Color GetSourceColor(string source) => source.ToUpperInvariant() switch
    {
        "XML" => Color.Info,
        "DOCX" => Color.Success,
        "PDF" => Color.Error,
        "OCR" => Color.Secondary,
        _ => Color.Default
    };
}
```
