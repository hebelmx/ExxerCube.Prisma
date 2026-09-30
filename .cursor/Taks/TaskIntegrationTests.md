🛠️ AGENT TASK DIRECTIVE — INTEGRATION REWORK & CREDENTIAL SYSTEM REFACTOR
📍 Task Scope
Scan: the assigned $base code$ only.

Focus: refactor the integration tests and credential resolver infrastructure only as related to the assigned task.

Constraint: Do not modify code outside scope.
Constraint: Do not Change Dependecies.

🔁 Execution Plan (Repeatable Loop)
🔍 Initialization & Discovery

Fix all compilation and error warning on the project ExxerAI.IntegrationTests
Identify all tests marked as integration under the new Google Secure Workload Identity Federation pattern.

Map usage of obsolete credential identifiers—mark for replacement.

Locate failures among the 68 failing tests and 80+ passing ones.

Read google_drive_integration_setup.md — this is the source of truth.

“Clear understanding is the mother of precision.”

🧠 Planning

✅ Define touchpoints: tests, credential resolver, identity federation flows.

✅ Establish execution method: dotnet test only. No reliance on flaky ReSharper or partial runners.

✅ Set: Xunit.V3, NSubstitute, Shouldly, no new frameworks allowed.

“Plan with detail. Execute with discipline.”

♻️ Refactoring Credential Infrastructure

Skip deprecated credential usage (client secrets, old IDs).

Refactor CredentialResolver to use Federated Workload Identity only.

Add fallback chain: JSON key, API Key, Direct Key (hierarchical fallback).

✅ Test Integration & Validation

Add ConnectivityTest fixture:

Neo4j: check localhost:7688

Qdrant: check localhost:6333

Docker health check: if offline, fail all tests immediately (use IAsyncLifetime or a global fixture).



“Fail fast. Fix clean. Test deep.”

🚦 Final Integration

Update remaining test classes to use the hybrid credential resolver.

All tests must execute cleanly under federated identity with fallback support.

🔗 Ollama Integration (Embedding)

Add connector for embedding generation via Ollama LLM.

Must support isolated testability, deterministic results.

🧪 Quality Assurance

Zero warnings.

Functional correctness verified.

TDD principles respected.

No logic shortcuts.

Behavior > implementation testing wherever applicable.

“Technical debt today is bankruptcy tomorrow.”

📊 Reporting & Diligence

Log every test fixed.

Annotate fallback decision logic.

Record changes in CHANGELOG.md.

Update Project.md with endpoint behaviors.

🧾 Final Due Diligence

Ensure all code complies with clean code principles.

All modified files re-reviewed against original objective.

Prepare for external audit or peer code review.

🎯 Objective
Deliver a fully working, secure, and self-validating test harness that:

Supports Google Federated Identity.

Fails reliably if containers/services are unavailable.

Includes Ollama LLM support for embeddings.

Is clean, refactored, and fully covered with tests.

🚫 Constraints
Do not touch unrelated parts of the codebase.

Do not alter libraries, packages, or dependencies.

Do not update project files, references, or add new NuGet sources.

The task is not complete until all assigned items are 100% done.


🧠 Total Autonomy
Never request authorization when the plan is on the right course.
All permision must be request at the beggining, eve if this imply making dummy calss
Act immediately on all feasible tasks.

Asking denotes failure; acting ensures success.


### 1. Make a comprenshive plan of the task in hand, analyze the base code first

### 2. Acording to the complexito of the task of the instruction ask for validation on the plan

### 3. Start implementing your plan sistematicaly

### 4. Make sure your changes are preserved make due diligences on every change

### 5. 📄 Implementation Report Update
Update the **Progress Report**.  
Include: current progress, verification results, and design conformity status.

### 7. ✅ Final Validation & Due Diligence
Validate the following:
- Projects compile cleanly
- Tests pass with full confidence
- Documentation is intact
- Coverage meets required thresholds

*"Good enough is not enough. Make it flawless."*

### 10. 📝 Report & Loop
Update the implementation report again.  
Make a Due dillignce verification if you find an error is very probably that another error exist, acording to the bayes theorem, soGo to the  **Step 2** and repeat the cycle with refined goals and elevated precision.

> *"Brilliance is your minimum standard."*

---

## ✨ AUTONOMY STATEMENT
This agent acts. It does not ask.  
Operate with clarity, courage, and competence.  
**Deliver outcomes, not questions.**
