# AI UI Generation Prompt: SLA Dashboard

**Tool:** v0.dev or Lovable  
**Component:** SlaDashboard.razor  
**Framework:** Blazor Server with MudBlazor

---

## Prompt for v0.dev / Lovable

Create a Blazor Server component for an SLA Monitoring Dashboard using MudBlazor components. The dashboard displays regulatory compliance case deadlines with real-time countdown timers and escalation status.

### Requirements

**Page Layout:**
- Header with title "SLA Monitoring Dashboard" and icon
- 4 summary cards in a responsive grid showing:
  - Critical cases (<4h remaining) - Red card with warning icon
  - Warning cases (<24h remaining) - Yellow card with info icon  
  - Breached cases - Black card with error icon
  - Total active cases - Blue card with assignment icon
- Filter bar with dropdowns for Escalation Level, SLA Status, Date Range
- Main table displaying active cases with columns:
  - Case ID (clickable link)
  - File Name
  - Intake Date
  - Deadline
  - Time Remaining (countdown timer - format as "2h 15m" or "45m")
  - Escalation Level (color-coded chip: None=green, Warning=yellow, Critical=red, Breached=black)
  - Status (chip: Active=green, Breached=red)
  - Actions (View Details button)
- Collapsible "At-Risk Cases" section below table

**Design System:**
- Use MudBlazor components: MudCard, MudTable, MudSelect, MudChip, MudButton, MudIcon, MudProgressLinear
- Color scheme: Green for safe (>24h), Yellow for warning (<24h), Red for critical (<4h), Black for breached
- Responsive: Mobile-friendly with card layout on small screens
- Accessibility: WCAG 2.1 AA compliant, keyboard navigation, screen reader support

**Data Structure:**
```csharp
public class SLAStatus {
    public string FileId { get; set; }
    public DateTime IntakeDate { get; set; }
    public DateTime Deadline { get; set; }
    public TimeSpan RemainingTime { get; set; }
    public EscalationLevel EscalationLevel { get; set; }
    public bool IsBreached { get; set; }
}

public enum EscalationLevel {
    None, Warning, Critical, Breached
}
```

**Features:**
- Real-time countdown timers (update every 60 seconds)
- Color-coded rows based on escalation level
- Sortable table columns
- Filter by escalation level and status
- Loading states with MudProgressLinear
- Empty state message when no cases
- Error handling with user-friendly messages

**Component Structure:**
```razor
@page "/sla-dashboard"
@using MudBlazor
@inject SLATrackingService SLA trackingService
@inject ISnackbar Snackbar

<MudContainer MaxWidth="MaxWidth.ExtraLarge">
  <!-- Summary Cards -->
  <!-- Filter Bar -->
  <!-- Active Cases Table -->
  <!-- At-Risk Cases Section -->
</MudContainer>
```

**Visual Details:**
- Summary cards: Large numbers (h3 typography), icons above, labels below
- Table: Dense mode, striped rows, hover effects
- Countdown timer: Bold, color-coded text showing hours and minutes
- Escalation chips: Small size, matching color scheme
- Buttons: Icon buttons for actions, primary color for main actions

**Interactions:**
- Click Case ID → Navigate to case details
- Click Escalation card → Filter table by that level
- Apply filters → Refresh table data
- Real-time updates via SignalR (show refresh indicator)

**Code Style:**
- Follow Blazor Server patterns
- Use async/await for data loading
- Proper error handling with try-catch
- Loading states for all async operations
- Clean, readable code with comments

Generate the complete Razor component code with all features implemented.

