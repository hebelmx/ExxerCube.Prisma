#!/usr/bin/env python3
"""
Prisma Code Quality Audit Batch Generator
==========================================
Generates organized audit templates for suspicious code patterns found by suspicious_code_detector.py

Adapted from ExxerAI tooling for Prisma's Hexagonal Architecture (Ports & Adapters pattern).

Features:
- Prioritizes HIGH severity issues first
- Groups by namespace for manageable batch sizes
- Creates comprehensive audit templates with context
- Generates action plans for each issue type
- ITTDD-aware analysis

Author: Code Quality Team
Version: 1.0 (Prisma Edition)
"""

import os
import json
from datetime import datetime
from collections import defaultdict
from pathlib import Path


class PrismaCodeAuditBatchGenerator:
    def __init__(self, input_json_path, output_base_dir):
        self.input_json_path = input_json_path
        self.output_base_dir = output_base_dir
        self.batch_counter = 0
        self.max_items_per_batch = 50  # Smaller batch size for better manageability
        self.max_total_items = 5000  # Limit total items to process (can be increased)

        # Priority mapping
        self.severity_priority = {"HIGH": 1, "MEDIUM": 2, "LOW": 3}

        print("📂 Loading suspicious patterns data...")
        # Load suspicious patterns data
        with open(input_json_path, "r", encoding="utf-8") as f:
            self.data = json.load(f)

        # Count total items for progress tracking
        total_patterns = sum(len(entries) if isinstance(entries, list) else 1
                           for entries in self.data.get("suspicious_patterns", {}).values())
        print(f"📊 Found {total_patterns} total suspicious patterns")

        if total_patterns > self.max_total_items:
            print(f"⚠️  Large dataset detected! Processing first {self.max_total_items} items.")
            print(f"   You can increase max_total_items if needed.")

    def safe_get(self, entry, key, default="UNKNOWN"):
        """Safely extract fields with fallback"""
        return entry.get(key, default)

    def get_severity_emoji(self, severity):
        """Get emoji for severity level"""
        return {"HIGH": "🚨", "MEDIUM": "⚠️", "LOW": "💡"}.get(severity, "❓")

    def get_category_emoji(self, category):
        """Get emoji for pattern category"""
        emojis = {
            "Incomplete Implementation": "🚧",
            "Production Mock/Stub": "🎭",
            "Hardcoded Values": "🔧",
            "Poor Exception Handling": "⚠️",
            "Test Quality Issues": "🧪",
            "Empty Implementation": "📝",
            "Other": "❓"
        }
        return emojis.get(category, "❓")

    def generate_audit_template(self, entry, batch_info):
        """Generate comprehensive audit template"""
        severity = self.safe_get(entry, 'severity')
        category = self.safe_get(entry, 'category')
        pattern = self.safe_get(entry, 'pattern')

        # Get specific guidance based on pattern type
        guidance = self.get_pattern_guidance(pattern, category, severity)

        template = f"""# {self.get_severity_emoji(severity)} Code Quality Audit: `{self.safe_get(entry, 'method')}`

## 📋 Issue Metadata

| Field | Value |
|-------|-------|
| **🎯 Severity** | {self.get_severity_emoji(severity)} {severity} |
| **📦 Project** | {self.safe_get(entry, 'project')} |
| **🏗️ Namespace** | {self.safe_get(entry, 'namespace')} |
| **🏷️ Class** | {self.safe_get(entry, 'class')} |
| **⚙️ Method** | {self.safe_get(entry, 'method')} |
| **📍 Line** | {self.safe_get(entry, 'line')} |
| **🔍 Pattern** | `{pattern}` |
| **📂 Category** | {self.get_category_emoji(category)} {category} |
| **📁 File** | `{self.safe_get(entry, 'file_name')}` |
| **🧪 Is Test** | {'✅ Yes' if self.safe_get(entry, 'is_test_file') else '❌ No'} |

---

## 🔍 Code Context

**File Path:** `{self.safe_get(entry, 'relative_path')}`

```csharp
// Line {self.safe_get(entry, 'line')}:
{self.safe_get(entry, 'line_preview')}
```

---

## 🎯 Pattern Analysis

{guidance['description']}

### 🚨 ITTDD Impact
{guidance['ittdd_impact']}

### 🔧 Recommended Actions
{guidance['actions']}

---

## ✅ Audit Checklist

### 🔍 Investigation Phase
- [ ] **Context Analysis**: Review surrounding code (±10 lines) for full context
- [ ] **Usage Analysis**: Check where this code is called from (production vs test)
- [ ] **Interface Compliance**: Verify if this implements any Domain interfaces correctly
- [ ] **Dependencies**: Identify what code depends on this method
- [ ] **Test Coverage**: Verify if adequate tests exist for this code

### 🎯 Severity-Specific Checks
{guidance['checklist']}

### 🛠️ Implementation Phase
- [ ] **Solution Design**: Plan the fix/improvement approach
- [ ] **Risk Assessment**: Evaluate impact of changes on existing functionality
- [ ] **Testing Strategy**: Plan regression tests for changes
- [ ] **Documentation**: Update relevant documentation if needed

### ✅ Verification Phase
- [ ] **Code Review**: Have changes reviewed by another developer
- [ ] **Testing**: Run all relevant tests (unit, integration, architecture)
- [ ] **Performance**: Verify no performance degradation
- [ ] **Security**: Ensure no security implications
- [ ] **Architecture Tests**: Verify hexagonal architecture constraints pass

---

## 🛠️ Fix Template

### Current Code:
```csharp
{self.safe_get(entry, 'line_preview')}
```

### Proposed Fix:
```csharp
// TODO: Implement proper solution based on audit findings
// Replace this comment with the actual fix
```

### Fix Reasoning:
> **Why this fix:** [Explain the reasoning behind the proposed solution]
>
> **Alternatives considered:** [List other approaches that were considered]
>
> **Risk mitigation:** [Explain how risks are addressed]

---

## 📊 Impact Assessment

| Aspect | Impact Level | Notes |
|--------|-------------|-------|
| **Production Risk** | {severity} | [Assessment notes] |
| **Performance** | TBD | [Performance impact analysis] |
| **Security** | TBD | [Security implications] |
| **Maintainability** | TBD | [Code maintainability impact] |
| **Testing Effort** | TBD | [Required testing effort] |

---

## 🏗️ Hexagonal Architecture Alignment

**Layer:** {self.get_hexagonal_layer(self.safe_get(entry, 'namespace'))}

**Architectural Considerations:**
- Does this code belong in its current layer (Domain/Application/Infrastructure)?
- Should this functionality be moved to a more appropriate layer?
- Does it follow Dependency Inversion Principle (depend on abstractions, not concretions)?
- For Infrastructure: Does it implement a Domain interface?
- For Domain: Is it free of infrastructure concerns?
- For Application: Does it orchestrate use cases without implementation details?

---

## ✍️ Audit Results

### 🎯 Final Decision
- [ ] **✅ Fix Implemented** - Issue resolved with proper implementation
- [ ] **📋 Fix Planned** - Solution designed, implementation scheduled
- [ ] **📝 Documented as Intentional** - Justified as correct for context
- [ ] **🗑️ Marked for Removal** - Code should be removed
- [ ] **🔄 Needs Further Analysis** - Requires additional investigation

### 📝 Auditor Notes
```
Date: {datetime.now().strftime('%Y-%m-%d')}
Auditor: ____________________

Summary: [Brief summary of findings and actions taken]

Technical Details: [Technical details about the fix or decision]

Follow-up Required: [Any follow-up tasks or monitoring needed]
```

### 🎯 Batch Information
- **Batch ID:** {batch_info['batch_id']}
- **Priority Group:** {batch_info['priority_group']}
- **Namespace Group:** {batch_info['namespace']}
- **Item:** {batch_info['item_number']} of {batch_info['total_items']}

---

*Generated by Prisma Code Quality Audit System - {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}*
"""
        return template

    def get_hexagonal_layer(self, namespace):
        """Get hexagonal architecture layer for namespace"""
        if not namespace:
            return "Unknown Layer"

        namespace_lower = namespace.lower()

        if "domain" in namespace_lower:
            if "interfaces" in namespace_lower:
                return "🔌 PORTS (Domain.Interfaces) - Business contracts and abstractions"
            elif "entities" in namespace_lower:
                return "💼 CORE (Domain.Entities) - Business entities and value objects"
            else:
                return "💼 CORE (Domain) - Business logic and rules"
        elif "application" in namespace_lower:
            return "🎯 APPLICATION - Use case orchestration and coordination"
        elif "infrastructure" in namespace_lower:
            if "database" in namespace_lower:
                return "🔌 ADAPTER (Infrastructure.Database) - Data persistence implementation"
            elif "extraction" in namespace_lower:
                return "🔌 ADAPTER (Infrastructure.Extraction) - Document extraction services"
            elif "classification" in namespace_lower:
                return "🔌 ADAPTER (Infrastructure.Classification) - Document classification services"
            elif "export" in namespace_lower:
                return "🔌 ADAPTER (Infrastructure.Export) - Export and signing services"
            elif "filestorage" in namespace_lower:
                return "🔌 ADAPTER (Infrastructure.FileStorage) - File storage abstraction"
            elif "browserautomation" in namespace_lower:
                return "🔌 ADAPTER (Infrastructure.BrowserAutomation) - Browser automation services"
            elif "filesystem" in namespace_lower:
                return "🔌 ADAPTER (Infrastructure.FileSystem) - File system operations"
            else:
                return "🔌 ADAPTER (Infrastructure) - External integrations"
        elif "test" in namespace_lower:
            return "🧪 TESTS - Test layer (outside hexagon)"
        else:
            return "❓ UNKNOWN LAYER - Please verify layer placement"

    def get_pattern_guidance(self, pattern, category, severity):
        """Get specific guidance based on pattern type"""

        # HIGH severity patterns
        if pattern == "not_implemented_exception":
            return {
                "description": "🚨 **CRITICAL**: Code throws NotImplementedException, indicating incomplete ITTDD cycle (interface tested but not implemented).",
                "ittdd_impact": "🔴 **INCOMPLETE ITTDD**: Interface defined and tested, but implementation missing. This violates the architecture test: All_Domain_Interfaces_Should_Have_At_Least_One_Implementation.",
                "actions": "1. Implement proper functionality immediately\n2. If not ready, return Result<T>.Failure() with clear message\n3. Add comprehensive tests\n4. Review all callers for error handling\n5. Run architecture tests to verify",
                "checklist": "- [ ] **URGENT**: Verify this is not called in production paths\n- [ ] **CRITICAL**: Implement proper functionality or remove\n- [ ] **MANDATORY**: Add error handling in all callers\n- [ ] **ARCHITECTURE**: Run HexagonalArchitectureTests to verify compliance"
            }
        elif pattern == "not_supported_exception":
            return {
                "description": "⚠️ **MAJOR**: Code throws NotSupportedException, which may indicate missing functionality or poor design.",
                "ittdd_impact": "🟡 **DESIGN ISSUE**: May indicate interface design mismatch or incomplete adapter implementation.",
                "actions": "1. Evaluate if functionality should be supported\n2. Return Result<T>.Failure() with clear message\n3. Consider redesign if many operations are unsupported",
                "checklist": "- [ ] **HIGH**: Determine if operation should be supported\n- [ ] **MEDIUM**: Replace with Result<T> pattern\n- [ ] **LOW**: Document limitations clearly"
            }

        # MEDIUM severity patterns
        elif pattern in ["hardcoded_localhost", "hardcoded_timeout", "hardcoded_credentials"]:
            return {
                "description": "⚠️ **CONFIGURATION ISSUE**: Hardcoded values prevent flexibility and could cause deployment issues.",
                "ittdd_impact": "🟡 **CONFIGURATION SMELL**: Adapters should use configuration, not hardcoded values.",
                "actions": "1. Move to configuration (appsettings.json, environment variables)\n2. Use IConfiguration or IOptions pattern\n3. Add validation for configuration values",
                "checklist": "- [ ] **MEDIUM**: Move to external configuration\n- [ ] **MEDIUM**: Add configuration validation\n- [ ] **LOW**: Update deployment documentation"
            }
        elif pattern == "generic_exception_catch":
            return {
                "description": "⚠️ **ERROR HANDLING**: Generic exception catching can hide important errors and make debugging difficult.",
                "ittdd_impact": "🟡 **RELIABILITY RISK**: May mask critical errors that should be handled specifically or allowed to propagate.",
                "actions": "1. Catch specific exception types\n2. Log exceptions properly\n3. Use Result<T> pattern for business logic errors",
                "checklist": "- [ ] **MEDIUM**: Replace with specific exception types\n- [ ] **MEDIUM**: Ensure proper logging\n- [ ] **LOW**: Consider Result<T> pattern"
            }
        elif pattern in ["placeholder_comment", "todo_comment"]:
            return {
                "description": "⚠️ **INCOMPLETE WORK**: Code contains placeholders or TODOs indicating unfinished ITTDD implementation.",
                "ittdd_impact": "🟡 **ITTDD DEBT**: Incomplete code may not function as expected or may have reduced reliability.",
                "actions": "1. Complete the implementation\n2. If not ready, create proper tracking tickets\n3. Add temporary Result<T>.Failure() if needed",
                "checklist": "- [ ] **MEDIUM**: Complete implementation or create tracking ticket\n- [ ] **LOW**: Remove placeholder comments\n- [ ] **LOW**: Add proper error handling if implementation delayed"
            }

        # LOW severity patterns
        elif pattern in ["magic_numbers", "magic_percentage"]:
            return {
                "description": "💡 **MAINTAINABILITY**: Magic numbers make code harder to understand and maintain.",
                "ittdd_impact": "🟢 **LOW RISK**: Generally safe but reduces code readability and maintainability.",
                "actions": "1. Extract to named constants\n2. Add documentation explaining values\n3. Consider if values should be configurable",
                "checklist": "- [ ] **LOW**: Extract to named constants\n- [ ] **LOW**: Add explanatory comments\n- [ ] **LOW**: Consider making configurable if appropriate"
            }
        elif pattern in ["static_return_value", "return_null", "return_empty_string"]:
            return {
                "description": "💡 **DESIGN QUESTION**: Static return values may indicate stub implementation from ITTDD.",
                "ittdd_impact": "🟢 **STUB INDICATOR**: Architecture test No_Stub_Implementations_Should_Exist may flag this.",
                "actions": "1. Verify if static value is intentional\n2. Consider if logic should be more dynamic\n3. Add documentation explaining why value is static",
                "checklist": "- [ ] **LOW**: Verify static value is intentional\n- [ ] **LOW**: Add documentation if appropriate\n- [ ] **LOW**: Consider if more dynamic logic needed"
            }
        elif pattern == "task_from_result":
            return {
                "description": "💡 **ASYNC PATTERN**: Using Task.FromResult may indicate synchronous code in async context.",
                "ittdd_impact": "🟢 **PERFORMANCE CONSIDERATION**: Usually safe but may indicate suboptimal async patterns.",
                "actions": "1. Verify if truly async operation is needed\n2. Consider using ValueTask for performance\n3. Document why synchronous result is returned",
                "checklist": "- [ ] **LOW**: Verify if async is necessary\n- [ ] **LOW**: Consider ValueTask for performance\n- [ ] **LOW**: Document reasoning"
            }

        # Default case
        else:
            return {
                "description": f"❓ **PATTERN DETECTED**: Code contains pattern '{pattern}' which may need review.",
                "ittdd_impact": "🟡 **REVIEW NEEDED**: Impact depends on specific context and usage.",
                "actions": "1. Review code context\n2. Determine if pattern is appropriate\n3. Apply best practices for this pattern type",
                "checklist": "- [ ] **MEDIUM**: Review pattern in context\n- [ ] **LOW**: Apply appropriate best practices"
            }

    def organize_by_namespace_and_priority(self):
        """Organize entries by namespace and priority for batch processing"""
        print("🔄 Organizing patterns by priority and namespace...")

        # Flatten the suspicious patterns data
        all_entries = []

        for key, entries in self.data.get("suspicious_patterns", {}).items():
            if isinstance(entries, list):
                for entry in entries:
                    entry['pattern_key'] = key
                    all_entries.append(entry)
            elif isinstance(entries, dict):
                entries['pattern_key'] = key
                all_entries.append(entries)

        print(f"📝 Processing {len(all_entries)} total entries...")

        # Sort by priority (HIGH first) then by namespace
        all_entries.sort(key=lambda x: (
            self.severity_priority.get(x.get('severity', 'LOW'), 3),
            x.get('namespace', ''),
            x.get('project', ''),
            x.get('class', ''),
            x.get('line', 0)
        ))

        # Limit to max_total_items for performance
        if len(all_entries) > self.max_total_items:
            print(f"🎯 Limiting to first {self.max_total_items} highest priority items...")
            all_entries = all_entries[:self.max_total_items]

        # Group by namespace for manageable batches
        namespace_groups = defaultdict(list)
        for entry in all_entries:
            namespace = entry.get('namespace', 'Unknown')
            namespace_groups[namespace].append(entry)

        print(f"📦 Organized into {len(namespace_groups)} namespace groups")
        return namespace_groups

    def generate_batch_summary(self, batch_entries, batch_info):
        """Generate batch summary document"""
        severity_counts = defaultdict(int)
        category_counts = defaultdict(int)

        for entry in batch_entries:
            severity_counts[entry.get('severity', 'UNKNOWN')] += 1
            category_counts[entry.get('category', 'UNKNOWN')] += 1

        summary = f"""# 📋 Prisma Code Quality Audit Batch Summary

## 🎯 Batch Information
- **Batch ID:** {batch_info['batch_id']}
- **Priority Group:** {batch_info['priority_group']}
- **Namespace:** {batch_info['namespace']}
- **Total Items:** {len(batch_entries)}
- **Generated:** {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}

## 📊 Issue Breakdown

### Severity Distribution
{chr(10).join([f"- {self.get_severity_emoji(sev)} **{sev}**: {count} issues" for sev, count in severity_counts.items()])}

### Category Distribution
{chr(10).join([f"- {self.get_category_emoji(cat)} **{cat}**: {count} issues" for cat, count in category_counts.items()])}

## 🎯 Audit Instructions

1. **📋 Review Each Issue**: Go through each audit template systematically
2. **🎯 Prioritize HIGH Severity**: Address 🚨 HIGH severity issues first
3. **🔍 Context Analysis**: Review surrounding code for full understanding
4. **✅ Complete Checklists**: Use the provided checklists to ensure thorough review
5. **📝 Document Decisions**: Fill in audit results for tracking
6. **🏗️ Run Architecture Tests**: Verify HexagonalArchitectureTests pass after fixes

## 🚨 Critical Items Requiring Immediate Attention

{chr(10).join([f"- Line {entry.get('line')}: {entry.get('pattern')} in `{entry.get('method')}`" for entry in batch_entries if entry.get('severity') == 'HIGH'])}

## 📁 Files in This Batch

{chr(10).join([f"- `{entry.get('relative_path')}` (Line {entry.get('line')})" for entry in batch_entries])}

---

*Generated by Prisma Code Quality Audit System*
"""
        return summary

    def generate_all_batches(self):
        """Generate all audit batches"""
        print("🎯 Starting Prisma Code Quality Audit Batch Generation...")

        # Create output directory
        timestamp = datetime.now().strftime('%Y%m%d_%H%M%S')
        output_dir = Path(self.output_base_dir) / f"audit_batches_{timestamp}"
        output_dir.mkdir(parents=True, exist_ok=True)

        # Organize entries
        namespace_groups = self.organize_by_namespace_and_priority()

        total_batches = 0
        total_items = 0

        # Process each namespace group
        for ns_idx, (namespace, entries) in enumerate(namespace_groups.items(), 1):
            print(f"📦 [{ns_idx}/{len(namespace_groups)}] Processing namespace: {namespace} ({len(entries)} items)")

            # Split large namespaces into multiple batches
            batch_number = 1
            for i in range(0, len(entries), self.max_items_per_batch):
                batch_entries = entries[i:i + self.max_items_per_batch]

                # Determine priority group based on highest severity in batch
                severities = [entry.get('severity', 'LOW') for entry in batch_entries]
                if 'HIGH' in severities:
                    priority_group = "P1_CRITICAL"
                elif 'MEDIUM' in severities:
                    priority_group = "P2_IMPORTANT"
                else:
                    priority_group = "P3_MAINTENANCE"

                # Create batch info
                batch_id = f"B{total_batches + 1:03d}_{namespace.replace('.', '_').replace(' ', '_')[:30]}_{batch_number}"
                batch_info = {
                    'batch_id': batch_id,
                    'priority_group': priority_group,
                    'namespace': namespace,
                    'batch_number': batch_number,
                    'total_items': len(batch_entries)
                }

                # Create batch directory
                batch_dir = output_dir / priority_group / batch_id
                batch_dir.mkdir(parents=True, exist_ok=True)

                print(f"  📝 Generating batch {batch_id} with {len(batch_entries)} items...")

                # Generate batch summary
                summary_content = self.generate_batch_summary(batch_entries, batch_info)
                (batch_dir / "BATCH_SUMMARY.md").write_text(summary_content, encoding='utf-8')

                # Generate individual audit templates
                for idx, entry in enumerate(batch_entries, 1):
                    if idx % 5 == 0:  # Progress indicator every 5 items
                        print(f"    ✏️  Generated {idx}/{len(batch_entries)} templates...")

                    batch_info['item_number'] = idx
                    template_content = self.generate_audit_template(entry, batch_info)

                    # Create safe filename
                    safe_class = str(entry.get('class', 'Unknown')).replace('<', '').replace('>', '')[:20]
                    safe_method = str(entry.get('method', 'Unknown')).replace('<', '').replace('>', '')[:20]
                    filename = f"audit_{idx:02d}_{safe_class}_{safe_method}_L{entry.get('line', 0)}.md"
                    filename = ''.join(c for c in filename if c.isalnum() or c in '._-')  # Keep only safe chars

                    (batch_dir / filename).write_text(template_content, encoding='utf-8')

                    # Save metadata
                    metadata_filename = filename.replace('.md', '_metadata.json')
                    (batch_dir / metadata_filename).write_text(json.dumps(entry, indent=2), encoding='utf-8')

                total_batches += 1
                total_items += len(batch_entries)
                batch_number += 1

                print(f"  ✅ Completed batch {batch_id} with {len(batch_entries)} items")

        # Generate master index
        self.generate_master_index(output_dir, total_batches, total_items)

        print(f"\n🎉 Batch generation complete!")
        print(f"📊 Summary:")
        print(f"   - Total batches: {total_batches}")
        print(f"   - Total items: {total_items}")
        print(f"   - Output directory: {output_dir}")

        return str(output_dir)

    def generate_master_index(self, output_dir, total_batches, total_items):
        """Generate master index of all batches"""
        index_content = f"""# 🎯 Prisma Code Quality Audit - Master Index

## 📊 Overview
- **Total Batches Generated:** {total_batches}
- **Total Issues to Audit:** {total_items}
- **Generated:** {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}
- **Generator Version:** Prisma Code Quality Audit System v1.0
- **Architecture:** Hexagonal Architecture (Ports & Adapters)

## 🎯 Priority Processing Order

### 🚨 P1_CRITICAL (Process First)
Contains HIGH severity issues that require immediate attention for ITTDD cycle completion.

### ⚠️ P2_IMPORTANT (Process Second)
Contains MEDIUM severity issues that should be addressed in upcoming sprints.

### 💡 P3_MAINTENANCE (Process Last)
Contains LOW severity issues for technical debt cleanup.

## 📁 Directory Structure

```
audit_batches_{datetime.now().strftime('%Y%m%d_%H%M%S')}/
├── P1_CRITICAL/
│   ├── B001_NamespaceA_1/
│   │   ├── BATCH_SUMMARY.md
│   │   ├── audit_01_ClassName_MethodName_L123.md
│   │   ├── audit_01_ClassName_MethodName_L123_metadata.json
│   │   └── ...
│   └── ...
├── P2_IMPORTANT/
│   └── ...
├── P3_MAINTENANCE/
│   └── ...
└── MASTER_INDEX.md (this file)
```

## 🏗️ Hexagonal Architecture Layers

Issues are organized by their hexagonal architecture layer:
- 🔌 **PORTS (Domain.Interfaces)**: Business contracts and abstractions
- 💼 **CORE (Domain)**: Business entities and rules
- 🎯 **APPLICATION**: Use case orchestration
- 🔌 **ADAPTERS (Infrastructure)**: External integrations and implementations

## 🛠️ Audit Workflow

1. **Start with P1_CRITICAL** - Address these immediately
2. **Review BATCH_SUMMARY.md** in each batch for overview
3. **Process each audit template** systematically
4. **Complete all checklists** for thoroughness
5. **Run architecture tests** after fixes: `dotnet test Tests.Architecture`
6. **Document decisions** in the audit results section
7. **Move to next priority level** when current level complete

## 📊 Quality Metrics

Track your progress:
- [ ] P1_CRITICAL batches completed: ___/___
- [ ] P2_IMPORTANT batches completed: ___/___
- [ ] P3_MAINTENANCE batches completed: ___/___

## 🎯 ITTDD Compliance

Remember: Each HIGH severity issue likely indicates an incomplete ITTDD cycle.
Prioritize fixes that ensure:
- ✅ All Domain interfaces have at least one Infrastructure implementation
- ✅ No stub implementations (NotImplementedException, empty methods)
- ✅ Proper error handling using Result<T> pattern
- ✅ Configuration flexibility for different environments
- ✅ Hexagonal architecture constraints maintained

## 🧪 Architecture Tests Integration

After fixing issues, always run:
```bash
dotnet test Tests.Architecture --filter "FullyQualifiedName~HexagonalArchitectureTests"
```

This will verify:
- All interfaces have implementations
- No stub implementations exist
- Layer dependencies are correct
- Hexagonal architecture constraints are satisfied

---

*Generated by Prisma Code Quality Audit System - ITTDD Excellence! 🏗️*
"""

        (output_dir / "MASTER_INDEX.md").write_text(index_content, encoding='utf-8')


def main():
    """Main execution function"""
    # Configuration - Prisma Specific Paths
    input_json_path = r"F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\scripts\suspicious_code_analysis.json"
    output_base_dir = r"F:\Dynamic\ExxerCubeBanamex\ExxerCube.Prisma\Prisma\scripts\audits"

    # Quick mode for testing - process only HIGH priority items first
    import sys
    quick_mode = len(sys.argv) > 1 and sys.argv[1] == "--quick"

    if quick_mode:
        print("🚀 QUICK MODE: Processing only HIGH severity issues")

    # Verify input file exists
    if not os.path.exists(input_json_path):
        print(f"❌ Error: Input file not found: {input_json_path}")
        print("📋 Please run suspicious_code_detector.py first to generate the analysis.")
        return

    # Generate batches
    generator = PrismaCodeAuditBatchGenerator(input_json_path, output_base_dir)

    # Adjust limits for quick mode
    if quick_mode:
        generator.max_total_items = 100  # Limit to 100 items for quick testing
        generator.max_items_per_batch = 10  # Smaller batches for quick review

    output_dir = generator.generate_all_batches()

    print(f"\n🎯 Next Steps:")
    print(f"1. Review the master index: {output_dir}\\MASTER_INDEX.md")
    print(f"2. Start with P1_CRITICAL batches for high-priority issues")
    print(f"3. Use the audit templates to systematically review each issue")
    print(f"4. Run architecture tests after fixes: dotnet test Tests.Architecture")

    if quick_mode:
        print(f"\n💡 Quick mode completed! Run without --quick to process all items.")


if __name__ == "__main__":
    main()
