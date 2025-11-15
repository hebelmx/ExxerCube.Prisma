# SLA Dashboard UI/UX Specification

**Component:** `SlaDashboard.razor`  
**Story:** 1.5 - SLA Tracking and Escalation Management  
**Status:** Design Specification  
**Created:** 2025-01-16  
**Author:** Sally (UX Expert)

---

## Overview

The SLA Dashboard provides compliance managers with real-time visibility into SLA deadlines, escalations, and at-risk cases. It enables proactive management of regulatory response deadlines with clear visual indicators and actionable insights.

## User Goals

**Primary User:** Compliance Manager  
**Key Goals:**
- Quickly identify cases approaching deadlines
- Monitor escalation status across all active cases
- Take immediate action on critical cases
- Track SLA compliance metrics

## Component Structure

### Page Layout

```
┌─────────────────────────────────────────────────────────┐
│  SLA Monitoring Dashboard                                │
│  [Icon] Title                                            │
├─────────────────────────────────────────────────────────┤
│  ESCALATION SUMMARY CARDS (4 cards in row)              │
│  ┌──────┐ ┌──────┐ ┌──────┐ ┌──────┐                 │
│  │Critical│ │Warning│ │Breached│ │Total│                │
│  │  <4h  │ │ <24h │ │  0    │ │Active│                │
│  └──────┘ └──────┘ └──────┘ └──────┘                 │
├─────────────────────────────────────────────────────────┤
│  FILTER BAR                                              │
│  [Escalation Level] [SLA Status] [Date Range] [Apply]  │
├─────────────────────────────────────────────────────────┤
│  ACTIVE CASES TABLE                                      │
│  [Sortable columns with countdown timers]               │
├─────────────────────────────────────────────────────────┤
│  AT-RISK CASES LIST (Collapsible)                       │
│  [Priority cases requiring immediate attention]        │
└─────────────────────────────────────────────────────────┘
```

## Detailed Component Specifications

### 1. Escalation Summary Cards

**Purpose:** Provide at-a-glance overview of escalation status

**Components:**
- 4 `MudCard` components in a `MudGrid` (xs=12, sm=6, md=3)
- Each card displays:
  - Icon (Material Icons)
  - Count (large number)
  - Label
  - Color coding

**Card Specifications:**

| Card | Icon | Color | Data Source |
|------|------|-------|-------------|
| Critical (<4h) | `Icons.Material.Filled.Warning` | `Color.Error` (Red) | `GetAtRiskCasesAsync()` filtered by `EscalationLevel.Critical` |
| Warning (<24h) | `Icons.Material.Filled.Info` | `Color.Warning` (Yellow) | Filtered by `EscalationLevel.Warning` |
| Breached | `Icons.Material.Filled.Error` | `Color.Dark` (Black) | `GetBreachedCasesAsync()` |
| Total Active | `Icons.Material.Filled.Assignment` | `Color.Info` (Blue) | `GetActiveCasesAsync()` |

**Visual Design:**
- Large number (Typo.h3) with color matching escalation level
- Icon above number
- Label below number
- Subtle elevation (Elevation="2")
- Hover effect for interactivity

### 2. Filter Bar

**Purpose:** Allow filtering of cases by escalation level and status

**Components:**
- `MudCard` with collapsible header
- `MudSelect` components for filters
- Apply/Clear buttons

**Filters:**
- **Escalation Level:** None, Warning, Critical, Breached (multi-select)
- **SLA Status:** Active, At-Risk, Breached
- **Date Range:** Intake Date range picker (optional)
- **File ID:** Text search (optional)

**Layout:**
- `MudGrid` with responsive columns
- Filters in `MudItem xs="12" sm="6" md="3"`
- Buttons below filters row

### 3. Active Cases Table

**Purpose:** Display all active cases with deadline countdown and escalation status

**Component:** `MudTable` with:
- Sortable columns
- Dense mode for information density
- Striped rows for readability
- Hover effects

**Columns:**

| Column | Data | Format | Special Features |
|--------|------|--------|------------------|
| Case ID | `FileId` | Text | Clickable link to case details |
| File Name | `FileMetadata.FileName` | Text | Truncate with tooltip if long |
| Intake Date | `IntakeDate` | `MM/dd/yyyy HH:mm` | Sortable |
| Deadline | `Deadline` | `MM/dd/yyyy HH:mm` | Sortable, color-coded |
| Time Remaining | `RemainingTime` | **Countdown timer** | Real-time update, color-coded |
| Escalation Level | `EscalationLevel` | Chip badge | Color-coded chip |
| Status | `IsBreached` | Chip | Green (Active) / Red (Breached) |
| Actions | - | Icon buttons | View Details, Escalate, Export |

**Countdown Timer Implementation:**
- Use JavaScript interop or SignalR for real-time updates
- Format: "2h 15m" or "45m" or "Breached"
- Color coding:
  - Green: >24h remaining
  - Yellow: <24h remaining
  - Red: <4h remaining
  - Black: Breached

**Row Color Coding:**
- Background tint based on escalation level (subtle)
- Border-left accent color matching escalation

### 4. At-Risk Cases List

**Purpose:** Highlight cases requiring immediate attention

**Component:** Collapsible `MudExpansionPanels` or separate `MudCard`

**Display:**
- List of cases with `EscalationLevel.Critical` or `EscalationLevel.Warning`
- Sorted by deadline (ascending)
- Each item shows:
  - File ID
  - Time remaining (large, prominent)
  - Escalation level badge
  - Quick action button (View, Escalate, Assign)

**Visual Design:**
- Red/yellow accent border
- Prominent countdown timer
- Action buttons for quick response

### 5. Real-time Updates

**Implementation:** SignalR Hub integration

**Update Frequency:**
- Countdown timers: Every 60 seconds
- Escalation status: On escalation level change
- New cases: On case creation

**User Experience:**
- Smooth transitions (no jarring updates)
- Toast notification for new critical escalations
- Visual indicator when data is refreshing

## Color Scheme

**Escalation Level Colors:**
- **None:** Green (`Color.Success`) - #4caf50
- **Warning:** Yellow (`Color.Warning`) - #ff9800
- **Critical:** Red (`Color.Error`) - #f44336
- **Breached:** Dark (`Color.Dark`) - #212121

**Status Colors:**
- **Active:** Green (`Color.Success`)
- **At-Risk:** Yellow (`Color.Warning`)
- **Breached:** Red (`Color.Error`)

## Accessibility Requirements

- **WCAG 2.1 AA Compliance:**
  - Keyboard navigation for all interactive elements
  - Screen reader labels for icons and status indicators
  - Color contrast ratios meet AA standards
  - Focus indicators visible
  - ARIA labels for countdown timers

- **Keyboard Shortcuts:**
  - `F` - Focus filter bar
  - `R` - Refresh data
  - `Escape` - Clear filters

## Responsive Design

**Breakpoints:**
- **Mobile (xs):** Single column cards, stacked filters, horizontal scroll for table
- **Tablet (sm):** 2-column cards, side-by-side filters
- **Desktop (md+):** 4-column cards, full table width

**Table Responsive Behavior:**
- Mobile: Card-based layout instead of table
- Tablet: Horizontal scroll with sticky first column
- Desktop: Full table with all columns visible

## Loading States

- **Initial Load:** `MudProgressLinear` indeterminate
- **Refresh:** Subtle spinner in header
- **Empty State:** Friendly message with icon
- **Error State:** Error message with retry button

## Interaction Patterns

**Click Behaviors:**
- Case ID → Navigate to case details page
- Escalation card → Filter table by that escalation level
- Action buttons → Open dialog or navigate

**Hover Behaviors:**
- Cards: Slight elevation increase
- Table rows: Background color change
- Buttons: Color intensity increase

## Data Requirements

**Service Methods Needed:**
- `GetActiveCasesAsync()` - All active SLA statuses
- `GetAtRiskCasesAsync()` - Cases within critical threshold
- `GetBreachedCasesAsync()` - Cases past deadline
- SignalR hub for real-time updates

**Data Refresh Strategy:**
- Initial load on page initialization
- Periodic refresh every 60 seconds
- SignalR push for immediate updates
- Manual refresh button available

## Error Handling

**Error States:**
- Service unavailable: Show error message with retry
- Partial data: Show available data with warning
- Network error: Show offline indicator

**User Feedback:**
- Success: Toast notification for actions
- Error: Toast notification with error details
- Loading: Progress indicators

## Performance Considerations

- **Lazy Loading:** Load table data on demand
- **Pagination:** Limit to 50 cases per page
- **Virtualization:** Consider for large datasets
- **Debouncing:** Debounce filter changes
- **Caching:** Cache summary card data

## Integration Points

**Dependencies:**
- `SLATrackingService` (Application layer)
- `ISLAEnforcer` (Domain interface)
- SignalR Hub (`ProcessingHub` or new `SLAHub`)
- Navigation to case detail pages

**Navigation:**
- Route: `/sla-dashboard`
- Link from main navigation menu
- Breadcrumb: Home > SLA Monitoring

---

## Implementation Notes

1. **SignalR Hub:** Create or extend existing hub for SLA updates
2. **Countdown Timer:** Use JavaScript interop for client-side timer updates
3. **State Management:** Consider using component state or service for shared data
4. **Testing:** Use Playwright for E2E testing of real-time updates

---

## Next Steps

1. Review this specification with stakeholders
2. Create wireframes/mockups (optional)
3. Generate AI prompt for v0/Lovable (see separate prompt file)
4. Implement component following MudBlazor patterns
5. Add SignalR integration
6. Test accessibility and responsive design
7. User acceptance testing

