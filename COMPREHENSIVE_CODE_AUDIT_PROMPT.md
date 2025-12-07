# Comprehensive Code Audit Prompt (Agent Instruction)

## Mission Statement

Conduct a **thorough, evidence-based code audit** of the ExxerCube.Prisma C# codebase using the testing code, implementation code, and design documentes, commit logs, documentation reports, adr, and ony othr kind of documntation on th Code
 as your baseline.
Your task is to **find, document, any deviation founded on the code, also, to document any decision make to hencance the aplication, find any missing gap and any oportuny to quickle add any missing o quick feature in orde to have a better application**

This is your **ONLY MESSAGE** to the user. Make it comprehensive, actionable, and complete.

---


## Input Document

**Primary Source**: 

**Supporting Sources** (Use these to validate requirements):
- `docs/AAA Initiative Design/DATA_MODEL.md`
- `docs/AAA Initiative Design/SYSTEM_FLOW_DIAGRAM.md`
- `docs/AAA Initiative Design/Requirements.md`
- `docs/AAA Initiative Design/Laws/ClassificationRules.md`
- `docs/AAA Initiative Design/Laws/MandatoryFields_CNBV.md`


---

<--  Very importat, you must inspect all the cs documentes on the project and all the related md documents, as long as all fixtures and related documents alist of references is Expected
whit all the documentes you persolanlly readed the fist list is just the starting poing, you must read all the documents on the repo and create a pland and a check list to ensure al documentes where
readed do to ensure these audit was comprensiove -->
---

## Report Structure (Follow This Exactly)

Your final report MUST follow this structure:

```markdown
# Code Audit Report - ExxerCube.Prisma
**Audit ID**: Audit05122005.md
**Audit Date**: [Current Date]
**Auditor**: [Audhitor Name]
**Status**: COMPREHENSIVE REVIEW

---

## Executive Summary

[70-10 paragraphs summarizing:
- Overall code health (excellent/good/needs work/critical)
- Total findings count by priority
- Key blockers to MVP/production
- Funding recommendation (SECURE/AT RISK/NOT READY)
- High-level next steps

---

## Audit Methodology

**Evidence Sources**:
- Live code analysis (actual file inspection)
- Requirement traceability (docs/AAA Initiative Design/*)
- Test coverage verification (Tests.* projects)
- DI container validation (Program.cs, ServiceCollectionExtensions.cs)
- Architecture compliance (Clean Architecture rules)

**Classification Criteria**:
- **CRITICAL**: MVP blocker, prevents core functionality, funding risk
- **URGENT**: Key feature missing, severely limits value proposition
- **IMPORTANT**: Feature gap, impacts completeness but not core flow
- **WOW FACTOR**: Enhancement that significantly boosts fundability/value

**Definitions**:
- **COMPLETE**: All requirements from source-of-truth documents are implemented, tested (GREEN), and integrated in DI container
- **GAP**: Requirement exists in design docs but implementation is missing, stubbed, or untested
- **STUB**: Method/class exists but returns placeholder (empty, throws NotImplementedException, returns null)
- **EVIDENCE**: Actual code snippet (file:line) or explicit absence (empty directory, missing registration)

---

## Findings (Organized by Priority)

### CRITICAL Findings (MVP Blockers)

[For each CRITICAL finding, use this template:]

---

#### Finding C-[N]: [Concise Title]

**Status**: [NEW / CONFIRMED / UPDATED / RESOLVED]

**Requirement Sources** (Cite exact sections):
- `[Document]` (Section X.Y): "[Exact quote or paraphrase]"
- `[Document]` (Feature FN): "[Exact quote or paraphrase]"
- _(At least 2 sources required for CRITICAL findings)_

**Gap Definition**:
[Explain in 2-3 sentences what "gap" means for this finding]
- **What exists**: [Current state]
- **What's required**: [Target state per design docs]
- **Why it's a gap**: [Specific discrepancy]

**Code Evidence** (Show, don't tell):

**File**: `[Full path]:[Line numbers]`
```csharp
[Actual code snippet - 5-15 lines showing the gap]
```

**Evidence Type**: [STUB / MISSING / INCOMPLETE / EMPTY DIRECTORY / NOT REGISTERED]

**Proof of Gap**:
- [ ] Requirement exists in: `[document:section]`
- [ ] Implementation is: [missing/stubbed/incomplete]
- [ ] Tests are: [missing/RED/don't cover requirement]
- [ ] DI registration: [missing/present but incomplete]

**Impact Analysis**:

**Business Impact**:
- [How this blocks user value]
- [Revenue/compliance risk]
- [Funding risk explanation]

**Technical Impact**:
- [What features are blocked]
- [Cascade effects on other components]
- [Test coverage impact]

**Consumer Impact** (Who/what breaks):
- **Consumers**: [List classes/services that depend on this]
- **Breaking Changes**: [YES/NO - if YES, detail what breaks]
- **Data Changes**: [Any database schema changes needed]
- **Behavior Changes**: [Any API contract changes]

**Root Cause Analysis**:
[Explain the underlying reason for the gap - not just symptoms]

**E2E Solution** (Attack root cause AND symptoms):

**Step 1: Test-First (ITDD/TDD)**
```
Location: [Test project path]
Test File: [Filename.cs]
Test Method: [TestMethodName]

Test Code Outline:
[Pseudo-code or actual test structure showing what to assert]

Expected Outcome: RED (test fails because implementation missing)
```

**Step 2: Implement (Make Tests GREEN)**
```
Location: [Implementation project path]
File: [Filename.cs]
Implementation Outline:
[Key code structure, interfaces to implement, dependencies to inject]

Expected Outcome: GREEN (all tests pass)
```

**Step 3: Integrate (DI Registration)**
```
Location: Program.cs or ServiceCollectionExtensions.cs
Registration Code:
services.AddScoped<IInterface, Implementation>();

Validation: [How to verify DI works - integration test or manual check]
```

**Step 4: Verify E2E**
```
System Test Location: [Tests.System.* project]
E2E Test: [Test that exercises full flow including this component]

Success Criteria:
- [ ] Unit tests GREEN
- [ ] Integration tests GREEN
- [ ] E2E test GREEN
- [ ] No breaking changes to consumers (or migration plan documented)
```

**Definition of COMPLETE for This Finding**:
This finding is considered COMPLETE when:
1. [ ] All code from Step 2 implemented
2. [ ] All tests from Step 1 are GREEN
3. [ ] DI registration from Step 3 is verified
4. [ ] E2E test from Step 4 passes
5. [ ] Documentation updated (if needed)
6. [ ] No consumers broken (or migration completed)

**Estimated Effort**: [X hours/days]
**Fundability Impact**: [HIGH/MEDIUM/LOW - explain]

---

[Repeat above template for each CRITICAL finding]

---

### URGENT Findings (Key MVP Features)

[Use same template structure as CRITICAL, but adjust Impact Analysis to "URGENT" level]

---

### IMPORTANT Findings (Completeness)

[Use same template structure, adjust Impact Analysis to "IMPORTANT" level]

---

### WOW FACTOR Findings (Fundability Boosters)

[Use modified template - focus on value proposition and competitive advantage]

#### Finding W-[N]: [Concise Title]

**Opportunity**: [What impressive feature could be added]
**Value Proposition**: [Why this secures funding]
**Code Foundation**: [What's already in place to build this]
**Implementation Outline**: [High-level steps - not as detailed as CRITICAL]
**Effort vs. Impact**: [ROI analysis]
**Fundability Boost**: [Explain competitive advantage]

---

## Cross-Cutting Concerns

### Architecture Compliance

**Clean Architecture Rules Verification**:
- [ ] All interfaces in Domain/Interfaces? [YES/NO - cite violations]
- [ ] Infrastructure projects independent? [YES/NO - cite dependencies]
- [ ] Application layer orchestrates use cases? [YES/NO - cite gaps]
- [ ] DI properly configured? [YES/NO - cite missing registrations]

**Evidence**: [Show Program.cs structure, project references, etc.]

---

### Test Coverage Analysis

**Test Suite Status**:
```
Domain Tests:        [X/Y GREEN] - [Percentage]%
Infrastructure Tests: [X/Y GREEN] - [Percentage]%
Application Tests:    [X/Y GREEN] - [Percentage]%
System Tests:         [X/Y GREEN] - [Percentage]%
TOTAL:               [X/Y GREEN] - [Percentage]%
```

**Coverage Gaps** (Requirements without tests):
1. [Requirement] - No tests found in [Project]
2. [Requirement] - Tests exist but are RED/skipped

**Evidence**: Show test project structure and key missing test files

---

### Technical Debt Inventory

**Identified Stubs** (Methods that need implementation):
```
File: [path:line]
Method: [signature]
Current: throw new NotImplementedException();
Required: [Based on which requirement]
```

**Identified TODOs/FIXMEs**:
```
File: [path:line]
Comment: [TODO text]
Context: [Why this matters]
Priority: [CRITICAL/URGENT/IMPORTANT]
```

**Incomplete Interfaces** (Defined but not implemented):
```
Interface: [IInterfaceName]
Location: [path:line]
Implementations Found: [NONE / STUB / INCOMPLETE]
Required By: [Which design doc]
```

---

## Requirement Traceability Matrix

[For each requirement in design docs, trace to code]

| Req ID | Source Doc | Description | Implementation Status | Test Status | Evidence |
|--------|------------|-------------|----------------------|-------------|----------|
| F1 | Requirements.md | [Description] | [COMPLETE/PARTIAL/MISSING] | [GREEN/RED/MISSING] | [File:line] |
| F2 | Requirements.md | [Description] | [COMPLETE/PARTIAL/MISSING] | [GREEN/RED/MISSING] | [File:line] |
| ... | ... | ... | ... | ... | ... |

**Summary**:
- Requirements COMPLETE: [X/Y] ([Percentage]%)
- Requirements PARTIAL: [X/Y] ([Percentage]%)
- Requirements MISSING: [X/Y] ([Percentage]%)

---

## Refactoring Impact Analysis

[For any proposed refactorings, analyze impact]

### Refactoring: [Name]

**Current State**: [Code as-is]
**Proposed State**: [Code as-should-be]

**Breaking Changes**: [YES/NO]

**If YES, detail**:

**Consumer Impact Table**:
| Consumer Class/Service | Breaking Change | Migration Required | Code Example |
|------------------------|-----------------|-------------------|--------------|
| [Class1] | [What breaks] | [What to change] | [Before/After snippet] |

**Data Impact**: [Any database changes needed - migrations, data loss risks, etc.]

**Behavior Impact**: [Any observable behavior changes - API contracts, response formats, error handling]

**Migration Path**:
1. [Step-by-step consumer migration plan]
2. [Backward compatibility strategy if applicable]
3. [Rollback plan if refactoring fails]

---

## Actionable Roadmap

### Phase 1: CRITICAL Blockers (MVP Prerequisites)
**Goal**: Unblock core functionality
**Findings**: [C-1, C-2, ...]
**Estimated Effort**: [X days/weeks]
**Success Criteria**: [List measurable outcomes]
**Fundability Gate**: MUST COMPLETE for MVP funding

---

### Phase 2: URGENT Features (MVP Completeness)
**Goal**: Deliver key value propositions
**Findings**: [U-1, U-2, ...]
**Estimated Effort**: [X days/weeks]
**Success Criteria**: [List measurable outcomes]
**Fundability Gate**: SHOULD COMPLETE for full funding

---

### Phase 3: IMPORTANT Gaps (Production Readiness)
**Goal**: Complete the solution
**Findings**: [I-1, I-2, ...]
**Estimated Effort**: [X days/weeks]
**Success Criteria**: [List measurable outcomes]
**Fundability Gate**: NICE TO HAVE for initial funding, REQUIRED for production

---

### Phase 4: WOW FACTORS (Fundability Boosters)
**Goal**: Secure continued funding
**Findings**: [W-1, W-2, ...]
**Estimated Effort**: [X days/weeks]
**Success Criteria**: [List measurable outcomes]
**Fundability Gate**: DIFFERENTIATOR for competitive advantage

---

## Funding Recommendation

**Current Fundability Score**: [0-10] / 10

**Scoring Breakdown**:
- Core Functionality: [0-3] (Based on CRITICAL findings)
- Feature Completeness: [0-3] (Based on URGENT findings)
- Production Readiness: [0-2] (Based on IMPORTANT findings)
- Competitive Edge: [0-2] (Based on WOW FACTORS)

**Recommendation**: [SECURE FUNDING / AT RISK / NOT READY]

**Rationale**:
[2-3 paragraphs explaining the score and recommendation]

**To Secure Funding**:
1. [Minimum viable completion criteria]
2. [Key demonstrations needed]
3. [Risk mitigation required]

---

## Appendix A: Code Snippets

[Include longer code excerpts here if needed to support findings]

---

## Appendix B: Design Document Excerpts

[Include relevant quotes from AAA Initiative Design docs that validate requirements]

Example:
```
From: docs/AAA Initiative Design/DATA_MODEL.md, Section 2.1

"The Unified Requirement must contain the following 42 fields:
1. CnbvExpedienteId (string, required)
2. OficioNumero (string, required)
..."
```

---

## Document Metadata

**Total Findings**: [N]
- CRITICAL: [N]
- URGENT: [N]
- IMPORTANT: [N]
- WOW FACTOR: [N]

**Code Files Analyzed**: [N]
**Test Files Analyzed**: [N]
**Design Documents Referenced**: [N]
**Total Evidence Citations**: [N]

**Report Completeness**:
This report is COMPLETE when all sections above are filled with:
✅ Actual code evidence (not references to "see file X")
✅ Exact line numbers for every code citation
✅ Requirement citations with document:section format
✅ E2E solutions for every finding
✅ Impact analysis for every finding
✅ Definition of "complete" for every finding
✅ Actionable roadmap with effort estimates

**Agent Notes**:
[Any limitations, assumptions, or areas needing human review]

---

END OF REPORT
```

---

## Critical Instructions for Agent

### 1. Evidence Requirements

**For EVERY finding, you MUST provide**:
- ✅ **Actual code snippets** (5-15 lines with line numbers)
- ✅ **File paths** (absolute paths with line ranges)
- ✅ **Requirement citations** (docs/AAA Initiative Design/[doc]:[section])
- ✅ **Proof of gap** (show what exists vs. what's required)

**DO NOT**:
- ❌ Say "see file X for details" without showing the code
- ❌ Summarize code without quoting it
- ❌ Reference requirements without quoting them
- ❌ Claim a gap exists without proving it with code

---

### 2. Solution Requirements

**For EVERY finding, you MUST provide**:
- ✅ **Test-first approach** (show test structure BEFORE implementation)
- ✅ **Implementation outline** (key interfaces, classes, methods)
- ✅ **DI registration** (where to register in Program.cs)
- ✅ **E2E verification** (how to prove it works end-to-end)
- ✅ **Definition of COMPLETE** (checklist of done criteria)

**DO NOT**:
- ❌ Provide full implementation code (this is audit, not implementation)
- ❌ Provide vague solutions ("implement the service")
- ❌ Skip test-first approach (ITDD/TDD is mandatory)
- ❌ Ignore consumer impact (breaking changes must be documented)

---

### 3. Classification Rules

**CRITICAL** (Use sparingly - only true MVP blockers):
- Prevents core functionality from working
- Blocks other features (cascade effect)
- Makes system unusable for primary purpose
- Creates funding risk

**URGENT** (Key features missing):
- Limits value proposition significantly
- Reduces competitive advantage
- Impacts user experience majorly
- Missing from design but not blocking

**IMPORTANT** (Completeness gaps):
- Reduces overall completeness
- Impacts secondary features
- Creates technical debt
- Not blocking but needed for production

**WOW FACTOR** (Fundability boosters):
- Impressive demonstration value
- Competitive differentiator
- Secures continued funding
- Exceeds baseline expectations

---

### 4. Definitions You MUST Provide

**When you say "COMPLETE"**:
- Define EXACTLY what "complete" means for that finding
- Provide checklist of completion criteria
- Include test requirements (how many tests, what they verify)
- Include integration requirements (DI registration, E2E test)

**When you say "GAP"**:
- Define what exists NOW (with code evidence)
- Define what's REQUIRED (with requirement citation)
- Explain WHY the delta is a gap (impact)
- Explain what MUST CEASE for it to no longer be a gap

**When you say "STUB"**:
- Show the actual stub code
- Explain what the method SHOULD do (based on requirements)
- Estimate effort to implement (hours/days)

---

### 5. Requirement Traceability

**For EVERY finding**:
1. Cite at least ONE source-of-truth document
2. Quote the exact requirement (copy/paste from doc)
3. Show the code that violates/lacks that requirement
4. Trace the gap back to root cause (not just symptom)

**Source Priority**:
1. `Laws/*.md` (regulatory requirements - highest priority)
2. `Requirements.md` (business requirements)
3. `DATA_MODEL.md` (data structure requirements)
4. `SYSTEM_FLOW_DIAGRAM.md` (architectural requirements)
5. `ClassificationRules.md` (business logic requirements)

---

### 6. Consumer Impact Analysis

**For ANY proposed change/refactoring**:

**You MUST answer**:
- Who consumes this? (list classes/services)
- Does this break them? (YES/NO)
- What behavior changes? (API contracts, responses, errors)
- What data changes? (schema, migrations, data loss risk)
- What must consumers do? (code changes, config changes, data migration)

**Provide migration code examples**:
```csharp
// BEFORE (current consumer code):
var result = oldService.DoSomething();

// AFTER (migrated consumer code):
var result = await newService.DoSomethingAsync(cancellationToken);
// ^^ Show exactly what changes
```

---

### 7. Actionable = Executable

**Every solution MUST be actionable**:
- Developer can copy test outline and start coding
- Developer knows EXACTLY which file to create/edit
- Developer knows EXACTLY what interfaces to implement
- Developer knows EXACTLY where to register in DI
- Developer knows EXACTLY how to verify it works

**Test**:
Ask yourself: "Could a junior developer follow these steps without asking questions?"
If NO → Add more detail

---

### 8. Report Completeness Checklist

Before submitting your report, verify:

- [ ] Every finding has code evidence (actual snippets, not references)
- [ ] Every finding cites at least 1 requirement source
- [ ] Every finding defines "gap" and "complete"
- [ ] Every finding has E2E solution (test → implement → integrate → verify)
- [ ] Every finding analyzes consumer impact
- [ ] Every refactoring documents breaking changes
- [ ] Requirement traceability matrix is complete
- [ ] Test coverage is analyzed with numbers
- [ ] Technical debt is inventoried (stubs, TODOs)
- [ ] Actionable roadmap is provided with estimates
- [ ] Funding recommendation is justified with scoring

---

## Output Format

**Single, comprehensive markdown report following the structure above.**

**Length**: As long as needed to be thorough (expect 50-150 pages for comprehensive audit)

**Tone**: Professional, evidence-based, actionable

**Audience**: Technical lead, architect, funding committee

**Goal**: Enable team to complete application with concrete, traceable steps

---

## Final Reminders

1. **This is your ONLY message** - make it count
2. **Evidence over opinion** - show code, don't summarize
3. **Cite requirements** - every finding needs source-of-truth backing
4. **Define completeness** - "complete" needs specific criteria
5. **Be actionable** - developers should know exactly what to do
6. **Analyze impact** - breaking changes must be documented
7. **Test-first** - every solution starts with tests
8. **E2E thinking** - attack root cause AND symptoms

**This report will determine funding. Make it comprehensive, accurate, and actionable.**

---

<--  Very importat, you must inspect all the cs documentes on the project and all the related md documents, as long as all fixtures and related documents alist of references is Expected
whit all the documentes you persolanlly readed the fist list is just the starting poing, you must read all the documents on the repo and create a pland and a check list to ensure al documentes where
readed do to ensure these audit was comprensiove -->

---

END OF INSTRUCTION
