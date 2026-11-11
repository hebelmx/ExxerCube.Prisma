#!/usr/bin/env python3
"""
Test Class Recovery Map Generator
Creates a detailed map showing where each of the 21 high-priority test classes
should be recovered and why, based on namespace analysis and content type.
"""

import json
from pathlib import Path
from collections import defaultdict
from typing import Dict, List, Tuple
from datetime import datetime

class TestClassRecoveryMapper:
    """Maps test classes to their proper recovery destinations."""
    
    def __init__(self):
        with open('method_duplicate_analysis_fast_20251031_104145.json', 'r') as f:
            self.analysis = json.load(f)
        
        # Get high priority methods and group by class
        high_priority = [m for m in self.analysis['unique_backup_methods'] if m['is_high_priority']]
        self.high_priority_classes = self._group_by_class(high_priority)
        
        # Current test structure analysis
        self.current_structure = self._analyze_current_test_structure()
        
    def _group_by_class(self, methods: List[Dict]) -> Dict[str, Dict]:
        """Group methods by their test class."""
        classes = {}
        
        for method in methods:
            for instance in method['backup_instances']:
                class_name = instance['className']
                if class_name not in classes:
                    classes[class_name] = {
                        'class_name': class_name,
                        'namespace': instance.get('namespace', 'Unknown'),
                        'methods': [],
                        'backup_paths': set(),
                        'method_count': 0,
                        'patterns': set()
                    }
                
                classes[class_name]['methods'].append(method['backup_method'])
                classes[class_name]['backup_paths'].add(instance['fullPath'])
                classes[class_name]['method_count'] += 1
                classes[class_name]['patterns'].add(instance.get('testPattern', 'Other'))
        
        return classes
    
    def _analyze_current_test_structure(self) -> Dict[str, List[str]]:
        """Analyze current test directory structure."""
        test_path = Path("code/src/tests")
        structure = {}
        
        if test_path.exists():
            for item in test_path.iterdir():
                if item.is_dir():
                    structure[item.name] = [
                        subitem.name for subitem in item.iterdir() 
                        if subitem.is_dir()
                    ]
        
        return structure
    
    def _determine_destination(self, class_info: Dict) -> Tuple[str, str, str, int]:
        """
        Determine the best destination for a test class.
        Returns: (destination_path, reason, category, priority)
        """
        class_name = class_info['class_name']
        namespace = class_info['namespace']
        backup_paths = list(class_info['backup_paths'])
        method_count = class_info['method_count']
        
        # Extract the first backup path for analysis
        sample_path = backup_paths[0] if backup_paths else ""
        
        # Priority scoring (1-5, where 5 is highest)
        priority = 3  # Default
        
        # Determine category and destination based on analysis
        
        # 1. Integration Tests (Highest Priority)
        if ('integration' in class_name.lower() or 
            'integration' in namespace.lower() or
            'integration' in sample_path.lower()):
            
            if 'imageprocessing' in class_name.lower():
                return (
                    "code/src/tests/05CrossCutting/ExxerAI.Nexus.IntegrationTest",
                    "Image processing integration tests belong in Nexus (document processing)",
                    "🔗 Integration Test - Document Processing",
                    5
                )
            elif 'ocr' in class_name.lower():
                return (
                    "code/src/tests/05CrossCutting/ExxerAI.Nexus.IntegrationTest", 
                    "OCR processing is core document transformation in Nexus",
                    "🔗 Integration Test - OCR Processing",
                    5
                )
            elif 'health' in class_name.lower() or 'monitor' in class_name.lower():
                return (
                    "code/src/tests/07Infrastructure/ExxerAI.Signal.IntegrationTest",
                    "Health monitoring belongs in Signal (monitoring and system heartbeat)",
                    "🔗 Integration Test - Health Monitoring", 
                    5
                )
            elif 'agent' in class_name.lower() or 'swarm' in class_name.lower():
                return (
                    "code/src/tests/06Communication/ExxerAI.Conduit.IntegrationTest",
                    "Agent coordination belongs in Conduit (messaging and agent communication)",
                    "🔗 Integration Test - Agent Communication",
                    5
                )
            else:
                return (
                    "code/src/tests/09Standalone/ExxerAI.Integration.Test",
                    "General integration tests for cross-cutting concerns",
                    "🔗 Integration Test - General",
                    4
                )
        
        # 2. Document Processing Adapters
        elif any(adapter in class_name.lower() for adapter in ['npoi', 'openxml', 'pdfpig']):
            return (
                "code/src/tests/05CrossCutting/ExxerAI.Nexus.Test",
                "Document processing adapters are core Nexus functionality",
                "⚡ Nexus - Document Adapters",
                4
            )
        
        # 3. Authentication/External Services
        elif 'google' in class_name.lower() or 'authentication' in class_name.lower():
            return (
                "code/src/tests/04ExternalSystems/ExxerAI.Gatekeeper.Test",
                "External authentication adapters belong in Gatekeeper",
                "🚪 Gatekeeper - External Authentication",
                4
            )
        
        # 4. Configuration and Orchestration
        elif ('configuration' in class_name.lower() or 
              'orchestration' in class_name.lower() or
              'EIAOrchestration' in class_name):
            return (
                "code/src/tests/08Orchestration/ExxerAI.Chronos.Test",
                "Orchestration and configuration belong in Chronos",
                "⏰ Chronos - Orchestration",
                4
            )
        
        # 5. Repository and Data Access
        elif 'repository' in class_name.lower() or 'audit' in class_name.lower():
            return (
                "code/src/tests/02DataAccess/ExxerAI.Datastream.Test",
                "Repository and audit trails are data access concerns in Datastream", 
                "🌊 Datastream - Repository",
                4
            )
        
        # 6. AI/LLM Services
        elif ('ai' in class_name.lower() or 
              'microsoft' in class_name.lower() or
              'llm' in class_name.lower()):
            return (
                "code/src/tests/03Intelligence/ExxerAI.Cortex.Test",
                "AI and LLM adapters belong in Cortex (intelligent reasoning)",
                "🧠 Cortex - AI Services", 
                4
            )
        
        # 7. Validation and Business Rules
        elif 'validator' in class_name.lower() or 'validation' in class_name.lower():
            return (
                "code/src/tests/01BusinessLogic/ExxerAI.Wisdom.Test",
                "Validation logic belongs in Wisdom (governance and compliance)",
                "🦉 Wisdom - Validation",
                3
            )
        
        # 8. Infrastructure Services
        elif any(term in namespace.lower() for term in ['infrastructure', 'services']):
            return (
                "code/src/tests/07Infrastructure/ExxerAI.Axis.Test",
                "General infrastructure services belong in Axis",
                "🏗️ Axis - Infrastructure",
                3
            )
        
        # 9. Default fallback
        else:
            return (
                "code/src/tests/09Standalone/ExxerAI.Legacy.Test",
                "Unclear classification - place in standalone legacy folder for review",
                "📦 Legacy - Needs Review",
                2
            )
    
    def generate_recovery_map(self) -> str:
        """Generate comprehensive recovery map."""
        
        lines = [
            "# 🗺️ Test Class Recovery Map",
            f"*Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}*",
            "",
            "## 📋 Executive Summary",
            "",
            f"**21 high-priority test classes** identified for recovery with **{sum(cls['method_count'] for cls in self.high_priority_classes.values())} total test methods**.",
            "",
            "### Recovery Strategy:",
            "1. **Integration Tests First** (5 classes) - Maximum coverage impact",
            "2. **Document Processing** (3 classes) - Core business functionality", 
            "3. **External Systems** (4 classes) - Critical integrations",
            "4. **Infrastructure** (9 classes) - Supporting services",
            "",
            "---",
            ""
        ]
        
        # Group classes by destination for organized recovery
        recovery_groups = defaultdict(list)
        
        for class_name, class_info in self.high_priority_classes.items():
            destination, reason, category, priority = self._determine_destination(class_info)
            
            recovery_groups[destination].append({
                'class_info': class_info,
                'destination': destination,
                'reason': reason,
                'category': category,
                'priority': priority
            })
        
        # Sort destinations by priority
        sorted_destinations = sorted(
            recovery_groups.items(),
            key=lambda x: max(item['priority'] for item in x[1]),
            reverse=True
        )
        
        lines.extend([
            "## 🎯 Recovery Plan by Destination",
            ""
        ])
        
        total_classes = 0
        total_methods = 0
        
        for destination, classes in sorted_destinations:
            dest_classes = len(classes)
            dest_methods = sum(cls['class_info']['method_count'] for cls in classes)
            total_classes += dest_classes
            total_methods += dest_methods
            
            # Get the evocative component name
            component_name = self._extract_component_name(destination)
            
            lines.extend([
                f"### {component_name}",
                f"**Destination:** `{destination}`",
                f"**Classes:** {dest_classes} | **Methods:** {dest_methods}",
                ""
            ])
            
            # Sort classes within destination by priority
            sorted_classes = sorted(classes, key=lambda x: x['priority'], reverse=True)
            
            for item in sorted_classes:
                class_info = item['class_info']
                class_name = class_info['class_name']
                method_count = class_info['method_count']
                category = item['category']
                reason = item['reason']
                priority = item['priority']
                
                priority_icon = "🔥" if priority == 5 else "⭐" if priority == 4 else "📝"
                
                lines.extend([
                    f"#### {priority_icon} `{class_name}`",
                    f"- **Methods:** {method_count}",
                    f"- **Category:** {category}",
                    f"- **Namespace:** `{class_info['namespace']}`",
                    f"- **Why Here:** {reason}",
                    f"- **Priority:** {priority}/5",
                    ""
                ])
                
                # Show sample method names
                sample_methods = class_info['methods'][:3]
                if sample_methods:
                    lines.append("**Sample Methods:**")
                    for method in sample_methods:
                        lines.append(f"  - `{method}`")
                    if len(class_info['methods']) > 3:
                        lines.append(f"  - *...and {len(class_info['methods']) - 3} more*")
                    lines.append("")
            
            lines.extend(["---", ""])
        
        # Implementation commands
        lines.extend([
            "## 🚀 Implementation Commands",
            "",
            "### Step 1: Create Missing Test Project Directories",
            "```bash"
        ])
        
        # Generate directory creation commands
        unique_destinations = set(dest for dest, _ in sorted_destinations)
        for destination in sorted(unique_destinations):
            lines.append(f"mkdir -p \"{destination}\"")
        
        lines.extend([
            "```",
            "",
            "### Step 2: Recovery Commands by Priority",
            ""
        ])
        
        # Generate recovery commands for each destination
        for destination, classes in sorted_destinations:
            component_name = self._extract_component_name(destination)
            
            lines.extend([
                f"#### {component_name}",
                "```bash"
            ])
            
            for item in sorted(classes, key=lambda x: x['priority'], reverse=True):
                class_info = item['class_info']
                class_name = class_info['class_name']
                
                # Find the source file
                backup_path = list(class_info['backup_paths'])[0]
                
                lines.append(f"# Recover {class_name} ({class_info['method_count']} methods)")
                lines.append(f"cp \"{backup_path}\" \"{destination}/{class_name}.cs\"")
                lines.append("")
            
            lines.extend(["```", ""])
        
        # Summary stats
        lines.extend([
            "## 📊 Recovery Summary",
            "",
            f"| Destination Component | Classes | Methods | Priority |",
            f"|----------------------|---------|---------|----------|"
        ])
        
        for destination, classes in sorted_destinations:
            component_name = self._extract_component_name(destination)
            dest_classes = len(classes)
            dest_methods = sum(cls['class_info']['method_count'] for cls in classes)
            avg_priority = sum(cls['priority'] for cls in classes) / len(classes)
            
            lines.append(f"| {component_name} | {dest_classes} | {dest_methods} | {avg_priority:.1f}/5 |")
        
        lines.extend([
            f"| **TOTAL** | **{total_classes}** | **{total_methods}** | **-** |",
            "",
            "---",
            "",
            "## 🎯 Next Steps",
            "",
            "1. **Review and Approve** this recovery map",
            "2. **Execute Step 1** - Create missing directories", 
            "3. **Start with Priority 5** classes (Integration tests)",
            "4. **Verify and test** each recovered class",
            "5. **Update project references** and dependencies",
            "",
            "*Each test class should be reviewed for compilation and updated dependencies before integration.*"
        ])
        
        return '\n'.join(lines)
    
    def _extract_component_name(self, destination: str) -> str:
        """Extract evocative component name from destination path."""
        if 'Nexus' in destination:
            return "⚡ Nexus - Document Processing"
        elif 'Signal' in destination:
            return "📊 Signal - Monitoring"
        elif 'Conduit' in destination:
            return "📡 Conduit - Agent Communication"
        elif 'Gatekeeper' in destination:
            return "🚪 Gatekeeper - External Systems"
        elif 'Chronos' in destination:
            return "⏰ Chronos - Orchestration"
        elif 'Datastream' in destination:
            return "🌊 Datastream - Data Access"
        elif 'Cortex' in destination:
            return "🧠 Cortex - AI Intelligence"
        elif 'Wisdom' in destination:
            return "🦉 Wisdom - Governance"
        elif 'Axis' in destination:
            return "🏗️ Axis - Infrastructure"
        elif 'Integration' in destination:
            return "🔗 Integration Tests"
        else:
            return "📦 Legacy/Standalone"

def main():
    from datetime import datetime
    
    mapper = TestClassRecoveryMapper()
    recovery_map = mapper.generate_recovery_map()
    
    # Generate output filename
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    output_file = f"test_class_recovery_map_{timestamp}.md"
    
    with open(output_file, 'w', encoding='utf-8') as f:
        f.write(recovery_map)
    
    print(f"✅ Test class recovery map generated: {output_file}")
    print()
    print("🗺️ RECOVERY MAP SUMMARY:")
    print("=" * 50)
    
    # Print quick summary
    mapper_instance = TestClassRecoveryMapper()
    recovery_groups = defaultdict(list)
    
    for class_name, class_info in mapper_instance.high_priority_classes.items():
        destination, reason, category, priority = mapper_instance._determine_destination(class_info)
        recovery_groups[destination].append({
            'class_info': class_info,
            'priority': priority,
            'category': category
        })
    
    for destination, classes in sorted(recovery_groups.items(), 
                                     key=lambda x: max(item['priority'] for item in x[1]), 
                                     reverse=True):
        component = mapper_instance._extract_component_name(destination)
        count = len(classes)
        methods = sum(cls['class_info']['method_count'] for cls in classes)
        avg_priority = sum(cls['priority'] for cls in classes) / len(classes)
        
        print(f"{component}")
        print(f"  📍 {destination}")
        print(f"  📋 {count} classes, {methods} methods (Priority: {avg_priority:.1f}/5)")
        print()

if __name__ == "__main__":
    main()