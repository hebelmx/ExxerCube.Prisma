# Browser Automation Demo - MVP Guide

## 📋 Overview

**Date Created:** 2025-11-25
**Status:** ✅ Complete and Ready for Demo
**Purpose:** Automated browser navigation and document download for CNBV RegTech MVP demonstration

## 🎯 What We Built

### 1. E2E Test Infrastructure (`Tests.Infrastructure.BrowserAutomation.E2E`)

**Test Files:**
- `NavigationDownloadTests.cs` - Automated browser tests with headed mode
- `RecordMode_Gutenberg_ManualExploration` - Manual exploration test for discovering download patterns

**Key Features:**
- ✅ Headed mode (visible browser) for stakeholder demos
- ✅ Strategic delays (3s navigation, 1.5s preparation, 1s downloads)
- ✅ Direct download URLs for Gutenberg classics
- ✅ Internet Archive navigation (read-only due to legal restrictions)
- ✅ Uses `IBrowserAutomationAgent` interface (hexagonal architecture)

**Test Books:**
- Adam Smith - "The Wealth of Nations" (1776) - Book ID: 3300
- John Stuart Mill - "Principles of Political Economy" (1848) - Book ID: 30107
- Niccolò Machiavelli - "The Prince" (1532) - Book ID: 1232

**How to Run Tests:**
```bash
cd Prisma/Code/Src/CSharp/Tests.Infrastructure.BrowserAutomation.E2E
dotnet test --filter "FullyQualifiedName!~RecordMode"
```

### 2. Web UI Page (`/browser-automation`)

**File:** `Components/Pages/BrowserAutomationDemo.razor`

**Features:**

#### Show Browser Toggle 🎯
- **MudSwitch** component to toggle headed/headless mode
- Default: **Headed Mode (ON)** - browser visible for demos
- Visual indicator chip (Green "Visible" / Gray "Headless")

#### Three Document Sources 🌐
1. **Project Gutenberg** - Economics/political economy classics
   - Working downloads via direct URLs
   - Format: Plain text (TXT)

2. **Internet Archive** - Financial markets search
   - Navigation demo only (downloads restricted by lawsuits)
   - Shows search results for "financial markets"

3. **SIARA Simulator** - CNBV regulatory documents
   - Connects to local SIARA Blazor app (port 5002)
   - Displays CNBV dummy documents

#### Advanced Configuration Panel 🔧
**Expandable section with editable settings:**
- SIARA URL (default: `https://localhost:5002`)
- Gutenberg URL (default: `https://www.gutenberg.org`)
- Internet Archive URL (default: `https://archive.org`)
- Browser timeout (default: 30000ms)
- Page timeout (default: 30000ms)

**All changes applied dynamically - no restart needed!**

#### Downloaded Files Explorer 📁
- **MudDataGrid** showing all downloaded files
- Columns: File Name, Format, Size, Source, Actions
- Real-time progress bar during downloads
- File size formatting (B, KB, MB)
- Source color coding

### 3. Architecture & DI

**Dependency Injection (Program.cs:175-178):**
```csharp
services.AddBrowserAutomationServices(options =>
{
    configuration.GetSection("BrowserAutomation").Bind(options);
});
```

**Configuration (appsettings.json:5-11):**
```json
{
  "BrowserAutomation": {
    "Headless": true,
    "BrowserLaunchTimeoutMs": 30000,
    "PageTimeoutMs": 30000,
    "DefaultWebsiteUrl": "https://www.gob.mx/uif",
    "FilePatterns": [ "*.pdf", "*.xml", "*.docx" ]
  }
}
```

**Navigation Registry:**
- Added to "Document Processing" section
- Searchable tags: browser, automation, download, playwright, gutenberg, archive
- Icon: `Icons.Material.Filled.Language`

## 🚀 How to Run MVP Demo

### Prerequisites
1. ✅ All tests passing (including GOT-OCR2)
2. ✅ Playwright browsers installed: `pwsh Prisma/Code/Src/CSharp/Infrastructure.BrowserAutomation/bin/Debug/net10.0/playwright.ps1 install`
3. ✅ SIARA Simulator running (optional, for SIARA source)

### Demo Steps

**1. Start Web UI:**
```bash
cd Prisma/Code/Src/CSharp/UI/ExxerCube.Prisma.Web.UI
dotnet run
```

**2. Navigate to Browser Automation:**
- Open: `https://localhost:7062/browser-automation`
- Or use left navigation menu: **Document Processing → Browser Automation**

**3. Configure for Demo:**
- ✅ **Toggle "Show Browser" ON** (browser will be visible)
- ✅ Select source: **Project Gutenberg** (best for downloads)
- ✅ Set file count: **3** (quick demo) or **9** (full demo)
- ✅ Optionally expand "Advanced Configuration" to show editable settings

**4. Start Download:**
- Click **"Start Download"** button
- **Visible browser window will open!**
- Watch navigation to Gutenberg book pages
- See downloads happen in real-time
- Progress bar shows status

**5. View Results:**
- Downloaded files appear in data grid below
- Shows file name, format, size, source
- Files are also saved to temp directory

## 📊 Demo Flow (What Stakeholders Will See)

### Gutenberg Source (Recommended)
```
1. Browser launches (VISIBLE window appears)
2. Navigates to: gutenberg.org/ebooks/3300 (Wealth of Nations)
3. Waits 3 seconds (visible on screen)
4. Downloads: pg3300.txt
5. Success notification
6. Repeats for books 30107, 1232
7. Shows 3 files in data grid
8. Browser closes
```

### Internet Archive Source (Navigation Demo)
```
1. Browser launches (VISIBLE)
2. Navigates to: archive.org/search.php?query=financial%20markets
3. Shows search results for 3 seconds
4. Info notification: "Downloads restricted"
5. Browser closes
```

### SIARA Simulator Source (Local Demo)
```
1. Browser launches (VISIBLE)
2. Navigates to: localhost:5002 (SIARA dashboard)
3. Shows CNBV regulatory documents
4. Stays open for 5 seconds
5. Success notification: "Connected to SIARA"
6. Browser closes
```

## 🔧 Configuration Options

### Runtime Editable (via UI)
- SIARA URL
- Gutenberg URL
- Archive URL
- Browser timeout
- Page timeout
- Headed/Headless mode

### Static (appsettings.json)
- Default headless mode
- File patterns
- Storage path

## 📝 Lessons Learned

### 1. Gutenberg Download Pattern Discovery
**Problem:** Initial tests tried to scrape download links from HTML.
**Solution:** Found direct URL pattern: `/cache/epub/{ID}/pg{ID}.txt`
**Method:** Used "Record Mode" test - manual exploration with browser paused.

### 2. Internet Archive Legal Restrictions
**Problem:** Archive.org has limited downloads due to publisher lawsuits.
**Solution:** Changed to navigation-only demo showing search results.
**Learning:** For MVP, navigation demo is sufficient to show capability.

### 3. Headed vs Headless Mode
**Problem:** Web page used appsettings.json config (Headless=true).
**Solution:** Added runtime toggle with custom `BrowserAutomationOptions`.
**Key:** Create new agent instance per request with custom config.

### 4. Variable Scope in Blazor
**Problem:** `customAgent` variable not accessible in `finally` block.
**Solution:** Declare `IBrowserAutomationAgent? customAgent = null;` before `try`.
**Learning:** Blazor `@code` blocks follow standard C# scoping rules.

### 5. Configuration Flexibility for Demos
**Problem:** Stakeholders may need different URLs/timeouts during demo.
**Solution:** Added expandable "Advanced Configuration" panel.
**Benefit:** No app restart needed to adjust demo parameters.

## 🎯 Testing Strategy

### E2E Tests (Infrastructure Layer)
- ✅ Test interface contract (`IBrowserAutomationAgent`)
- ✅ Test navigation to real websites
- ✅ Test file downloads with direct URLs
- ✅ Headed mode for visibility
- ✅ Strategic delays for demo pacing

### UI Tests (Future)
- 🔲 Test Blazor component rendering
- 🔲 Test toggle switches
- 🔲 Test configuration panel
- 🔲 Test progress bar updates
- 🔲 Mock `IBrowserAutomationAgent` for unit tests

## 📦 Files Created/Modified

### New Files
- `Tests.Infrastructure.BrowserAutomation.E2E/NavigationDownloadTests.cs`
- `Tests.Infrastructure.BrowserAutomation.E2E/GlobalUsings.cs`
- `Tests.Infrastructure.BrowserAutomation.E2E/*.csproj`
- `UI/Components/Pages/BrowserAutomationDemo.razor`

### Modified Files
- `UI/Components/Shared/Navigation/NavigationRegistry.cs` (added menu link)
- `UI/Program.cs` (DI already configured - no changes needed)
- `UI/appsettings.json` (configuration already present - no changes needed)

## 🚨 Troubleshooting

### Issue: "Playwright not found"
**Solution:**
```bash
cd Prisma/Code/Src/CSharp/Infrastructure.BrowserAutomation/bin/Debug/net10.0
pwsh playwright.ps1 install
```

### Issue: "SIARA connection failed"
**Solution:**
- Make sure SIARA Simulator is running: `cd Siara.Simulator && dotnet run`
- Or edit SIARA URL in Advanced Configuration panel

### Issue: Browser doesn't appear
**Solution:**
- Check "Show Browser" toggle is **ON**
- Verify headed mode indicator shows **"Visible" (green chip)**

### Issue: Downloads fail on Gutenberg
**Solution:**
- Check internet connection
- Verify URLs in Advanced Configuration
- Try increasing timeout values

## 🎓 Key Architecture Patterns

### Hexagonal Architecture (Ports & Adapters)
- **Port:** `IBrowserAutomationAgent` interface (Domain layer)
- **Adapter:** `PlaywrightBrowserAutomationAdapter` (Infrastructure layer)
- **Benefit:** Can swap Playwright for Selenium/Puppeteer without changing domain

### Dependency Injection
- Services registered in `Program.cs`
- Configuration via `IOptions<T>`
- Scoped lifetime for browser agents

### Result Pattern
- Uses `IndQuestResults.Result<T>`
- Success/failure handling without exceptions
- Error messages in `.Error` property

## 📈 Future Enhancements

### Short Term
- [ ] Add SIARA document download logic (once SIARA provides download API)
- [ ] Add file preview in modal dialog
- [ ] Add export downloaded files as ZIP
- [ ] Add download history persistence

### Long Term
- [ ] Add scheduling (download documents daily at 8am)
- [ ] Add retry logic for failed downloads
- [ ] Add download queue with priority
- [ ] Add WebDriver support (alternative to Playwright)

## 🎬 Demo Script for Stakeholders

**Opening (1 min):**
> "This demonstrates our automated document collection infrastructure. The system uses Playwright to navigate websites and download regulatory documents automatically."

**Configuration (30 sec):**
> "All settings are configurable in real-time. [Expand Advanced Configuration] You can change URLs, timeouts - no restart needed."

**Show Browser Toggle (30 sec):**
> "For this demo, I'll enable headed mode. [Toggle ON] Now you'll see the browser in action - this is the same browser automation that runs in the background for production."

**Run Gutenberg (2 min):**
> "[Select Gutenberg, Count=3, Click Start] Watch as it navigates to Project Gutenberg, finds classic economics books, and downloads them automatically. [Wait for completion] Here are the downloaded files - file name, size, format."

**Explain Architecture (1 min):**
> "This uses the IBrowserAutomationAgent interface - the same interface our SIARA integration will use for collecting CNBV regulatory documents. It's plug-and-play."

**Closing (30 sec):**
> "For production, we run this headless - no visible browser. It's fast, scalable, and the same code works for any document source."

**Total Time:** ~5 minutes

## ✅ Sign-Off Checklist

- [x] E2E tests pass
- [x] Web UI builds successfully
- [x] Headed mode works (browser visible)
- [x] Gutenberg downloads work
- [x] Archive navigation works
- [x] SIARA navigation ready (needs SIARA running)
- [x] Configuration panel works
- [x] Progress bar updates correctly
- [x] File explorer displays results
- [x] Navigation menu link works
- [x] Documentation complete

---

**Status:** 🚀 **READY FOR MVP DEMO**
**Last Updated:** 2025-11-25
**Next Steps:** Run demo for stakeholders, gather feedback, prioritize enhancements
