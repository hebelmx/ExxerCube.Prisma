# UI/UX Navigation Audit – 2025-11-20

## Scope & Method
- Reviewed the Blazor Server UI project at `Code/Src/CSharp/UI/ExxerCube.Prisma.Web.UI`, focusing on `NavMenu.razor`, `MainLayout.razor`, `Routes.razor`, and representative page components under `Components/Pages`.
- Compared navigation links against the route declarations discovered with `rg '@page "'` to ensure coverage and authorization alignment.
- Evaluated accessibility hooks (focus management, headings) and Drawer behavior for UX pitfalls.

## Findings & Remediation

### 1. Broken navigation URLs and unreachable destinations
- Evidence: `NavMenu.razor` hard-codes `Href="review-case-detail"`, `Href="audit-trail-viewer"`, and `Href="oc-demo"` (`Code/Src/CSharp/UI/ExxerCube.Prisma.Web.UI/Components/Layout/NavMenu.razor:11-64`). None of these routes exist: `ReviewCaseDetail` is bound to `/manual-review/{CaseId}` (`Components/Pages/ReviewCaseDetail.razor:1`), the only audit pages are `/audit-trail` and `/audit/viewer`, and the OCR demo runs under `/document-processing` (`Components/Pages/OCRDemo.razor:1`).
- Impact: Users click links that 404 with a blank screen (because there is no `<NotFound>` view yet). Case detail cannot be opened because it needs a `CaseId` and should be driven from the Manual Review table, not global nav.
- Remediation:
  1. Replace broken Hrefs with actual routes and drop the static “Case Detail” link in favor of contextual actions (already available inside `ManualReviewDashboard` rows).
  2. Move the audit viewer link to whichever route you want to keep (`/audit-trail` is the richer page) and ensure OCR demo points to `/document-processing`.
  3. Consider adding a `View last reviewed case` shortcut only if you can supply the last case id.
- Suggested change:

    ```razor
    <!-- Document Processing -->
    <MudNavLink Href="/document-processing" Match="NavLinkMatch.All" Icon="@Icons.Material.Filled.DocumentScanner">
        Upload &amp; Process
    </MudNavLink>
    <MudNavLink Href="/document-processing-dashboard" Match="NavLinkMatch.All" Icon="@Icons.Material.Filled.CloudDownload">
        Processing Dashboard
    </MudNavLink>

    <!-- Review & Compliance -->
    <AuthorizeView Roles="Reviewer,Admin">
        <MudNavLink Href="/manual-review" Match="NavLinkMatch.All" Icon="@Icons.Material.Filled.Assignment">
            Manual Review
        </MudNavLink>
    </AuthorizeView>

    <!-- Export & Audit -->
    <MudNavLink Href="/audit-trail" Match="NavLinkMatch.All" Icon="@Icons.Material.Filled.History">
        Audit Trail
    </MudNavLink>
    ```

### 2. Missing role-based gating for reviewer-only sections
- Evidence: Manual review screens are `[Authorize(Roles = "Reviewer,Admin")]` (`Components/Pages/ManualReviewDashboard.razor:8`, `Components/Pages/ReviewCaseDetail.razor:11`), yet the nav items are always rendered for any authenticated user (`NavMenu.razor:19-31`). Audit pages are `[Authorize]` but likewise exposed to anonymous visitors because the nav is outside an `AuthorizeView`.
- Impact: Non-reviewers click into restricted items, immediately see redirect-to-login, and assume the product is broken. This is also a low-grade information leak about available tooling.
- Remediation:
  1. Wrap each privileged group (“Manual Review”, “Case Detail shortcut” if retained, “Audit Trail Viewer”) inside `AuthorizeView` with the matching role or policy.
  2. For anonymous visitors, keep only the authentication links (already present) plus public examples.
- Suggested change for reviewer block:

    ```razor
    <AuthorizeView Roles="Reviewer,Admin">
        <MudText Typo="Typo.caption" Class="px-3 py-1 text-muted">Review &amp; Compliance</MudText>
        <MudNavLink Href="/manual-review" Match="NavLinkMatch.All" Icon="@Icons.Material.Filled.Assignment">
            Manual Review
        </MudNavLink>
    </AuthorizeView>

    <AuthorizeView Policy="CanViewAuditTrail">
        <MudText Typo="Typo.caption" Class="px-3 py-1 text-muted">Export &amp; Audit</MudText>
        <MudNavLink Href="/audit-trail" Match="NavLinkMatch.All" Icon="@Icons.Material.Filled.History">
            Audit Trail Viewer
        </MudNavLink>
    </AuthorizeView>
    ```

### 3. Prefix matching causes simultaneous active states
- Evidence: both “Upload & Process” (`Href="document-processing"`) and “Processing Dashboard” (`Href="document-processing-dashboard"`) use `Match="NavLinkMatch.Prefix"` (`NavMenu.razor:11-16`). Navigating to `/document-processing-dashboard` sets both links to active because the URL begins with `document-processing`.
- Impact: Drawer highlights two menu items at once, creating confusion about the current location and making any screenshot-based documentation inaccurate.
- Remediation:
  1. Use `Match="NavLinkMatch.All"` for leaf routes so they only highlight on exact match.
  2. Alternatively rename the dashboard route to avoid using the same prefix.
- Suggested change:

    ```razor
    <MudNavLink Href="/document-processing" Match="NavLinkMatch.All" Icon="@Icons.Material.Filled.DocumentScanner">
        Upload &amp; Process
    </MudNavLink>
    <MudNavLink Href="/processing-dashboard" Match="NavLinkMatch.All" Icon="@Icons.Material.Filled.CloudDownload">
        Processing Dashboard
    </MudNavLink>
    ```

    _(If keeping the existing slug, still switch both links to `NavLinkMatch.All` so they do not overlap.)_

### 4. Router fallback and focus management are incomplete
- Evidence:
  - `Routes.razor` contains a `<Found>` block but no `<NotFound>` (`Code/Src/CSharp/UI/ExxerCube.Prisma.Web.UI/Components/Routes.razor:2-11`). Broken links therefore render an empty page.
  - The router tries to focus the first `<h1>` via `<FocusOnNavigate ... Selector="h1" />` (`Routes.razor:9`), yet only `Error.razor` renders an actual `<h1>` (`rg '<h1' Components/Pages` produced a single match at `Components/Pages/Error.razor:6`). All feature pages use `<MudText Typo="Typo.h4">` without setting `Tag="h1"`, so focus never moves and screen-reader users remain stuck at the menu toggle.
- Impact: Invalid URLs (e.g., the broken nav items above) fail silently, hurting troubleshooting. Focus is not reset after navigation, which is a WCAG 2.4.3 violation and makes keyboard navigation painful.
- Remediation:
  1. Add a `<NotFound>` block that renders a friendly error (`Error` or new `NotFound` component) with a link back to `/`.
  2. Update each top-level heading to render as `<h1>` (`MudText` supports `Tag="Tag.h1"`), ensuring the focus helper can find it.
- Suggested change for router:

    ```razor
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
            <LayoutView Layout="typeof(Layout.MainLayout)">
                <Error />
            </LayoutView>
        </NotFound>
    </Router>
    ```

    Example page heading fix (`Components/Pages/Home.razor`):

    ```razor
    <MudText Tag="Tag.h1" Typo="Typo.h3" GutterBottom="true" Class="mb-4">
        <MudIcon Icon="@Icons.Material.Filled.AutoAwesome" Class="mr-2" />
        ExxerCube Prisma OCR Processing System
    </MudText>
    ```

## Playwright Regression Coverage
- **`Code/Src/CSharp/Tests.UI/Navigation/NavigationSmokeTests.cs`**
  1. `DrawerLinkNavigatesToDocumentProcessing` – launches the Blazor site (base URL comes from `PRISMA_UI_BASEURL` or defaults to `https://localhost:5001`), ensures the drawer toggle is accessible, clicks “Upload & Process”, and asserts the URL ends with `/document-processing` while the link holds `aria-current="page"`.
  2. `UnknownRouteShowsHelpfulNotFoundPanel` – navigates to a bogus route, asserts the hero heading “Let’s get you back on track” is visible, exercises the search box to surface the “Open Audit Trail” CTA, and verifies “Go home” returns to `/`.
- Future enhancements: add role-aware fixtures (Reviewer vs Analyst) so unauthorized blocks stay hidden in production but surface during development overrides, and extend the suite to cover administrative links (`/admin/*`) and the SLA dashboard.

## Implementation Status – 2025-11-20
- Navigation rebuilt on top of a shared `NavigationRegistry` (`Components/Shared/Navigation/NavigationRegistry.cs`) and a dynamic menu (`Components/Layout/NavMenu.razor`) so every route knows its icon, description, role, and dev override. Dev/Debug builds now surface a “Development Quick Access” drawer that exposes every page regardless of authorization, satisfying the request for full discoverability while keeping runtime policies intact.
- Introduced a rich `NotFound` experience (`Components/Pages/NotFound.razor`) wired through the router (`Components/Routes.razor`) that offers a search bar, quick links, and fuzzy suggestions to recover from typos or dead links.
- Normalized hero headings across high-traffic pages (Home, DocumentProcessing, ManualReview, Audit, Export, SLA, etc.) by rendering the lead `MudText` as `<h1>` tags so the router’s focus trap has a valid anchor on every screen.
- Added first-pass Playwright coverage in `Code/Src/CSharp/Tests.UI/Navigation/NavigationSmokeTests.cs` (C#) that verifies drawer navigation, URL activation, and the new NotFound helper; the tests look up the target base URL through the `PRISMA_UI_BASEURL` environment variable.
- Created a navigation contract: a curated registry (`NavigationRegistry`) is now enforced by an automated test (`Tests.UI/Navigation/NavigationRegistryTests.cs`) that reflects over every `[Route]` component, skipping those decorated with the new `[HideFromNavigation]` attribute, and fails the build if pages or links fall out of sync.
