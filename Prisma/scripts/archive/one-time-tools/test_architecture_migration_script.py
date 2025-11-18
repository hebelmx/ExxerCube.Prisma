#!/usr/bin/env python3
"""
TEST ARCHITECTURE MIGRATION SCRIPT
===================================

Fixes Clean Architecture violations by relocating test classes to correct layers
based on their actual dependencies and abstraction levels.

VIOLATIONS ADDRESSED:
- ExxerAI.Domain.Cortex.Test → Split across Domain/Application/Infrastructure
- ExxerAI.Domain.Nexus.Test → Move to Infrastructure

SAFETY PROTOCOLS:
- Git status verification
- Backup creation
- Dry-run mode (default)
- User confirmation required
"""

import os
import shutil
import json
import subprocess
from pathlib import Path
from typing import Dict, List, Any
from datetime import datetime

class TestArchitectureMigrator:
    """Migrates test projects to fix Clean Architecture violations"""
    
    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.backup_dir = self.base_path / "backups" / f"test_migration_{datetime.now().strftime('%Y%m%d_%H%M%S')}"
        self.migration_spec = self._get_migration_specification()
        
    def _get_migration_specification(self) -> Dict[str, Any]:
        """Complete migration specification for architectural fixes"""
        return {
            "cortex_splits": {
                "domain": {
                    "target_project": "code/src/tests/00Domain/ExxerAI.Domain.Cortex.Test",
                    "target_namespace": "ExxerAI.Domain.Cortex.UnitTests",
                    "files": ["AI/TokenUsageTests.cs", "GlobalUsings.cs"],
                    "dependencies": ["ExxerAI.Domain"]
                },
                "application": {
                    "target_project": "code/src/tests/01Application/ExxerAI.Application.Cortex.Test",
                    "target_namespace": "ExxerAI.Application.Cortex.UnitTests", 
                    "files": [
                        "LLMFeatures/LanguageModelTests.cs",
                        "LLMFeatures/LLMIntegrationTests.cs",
                        "CortexTestBase.cs"
                    ],
                    "dependencies": ["ExxerAI.Domain", "ExxerAI.Application"]
                },
                "infrastructure": {
                    "target_project": "code/src/tests/02Infrastructure/ExxerAI.Infrastructure.Cortex.Test",
                    "target_namespace": "ExxerAI.Infrastructure.Cortex.UnitTests",
                    "files": [
                        "AI/LLMRequestTests.cs", 
                        "AI/LLMResponseTests.cs",
                        "AI/TextAnalysisRequestTests.cs",
                        "LLMFeatures/ModelCapabilitiesTests.cs"
                    ],
                    "dependencies": ["ExxerAI.Domain", "ExxerAI.Application", "ExxerAI.Cortex"]
                }
            },
            "nexus_move": {
                "source_project": "code/src/tests/00Domain/ExxerAI.Domain.Nexus.Test",
                "target_project": "code/src/tests/02Infrastructure/ExxerAI.Infrastructure.Nexus.Test",
                "namespace_transform": {
                    "from": "ExxerAI.Domain.Nexus.UnitTests",
                    "to": "ExxerAI.Infrastructure.Nexus.UnitTests"
                },
                "preserve_structure": True,
                "dependencies": ["ExxerAI.Domain", "ExxerAI.Nexus.Library"]
            }
        }
    
    def verify_git_status(self) -> bool:
        """Verify git repository is clean before migration"""
        try:
            result = subprocess.run(['git', 'status', '--porcelain'], 
                                  capture_output=True, text=True, cwd=self.base_path)
            if result.stdout.strip():
                print("❌ Git working directory is not clean:")
                print(result.stdout)
                print("Please commit or stash changes before migration.")
                return False
            print("✅ Git working directory is clean")
            return True
        except Exception as e:
            print(f"❌ Git status check failed: {e}")
            return False
    
    def create_backup(self) -> bool:
        """Create backup of test directories before migration"""
        try:
            self.backup_dir.mkdir(parents=True, exist_ok=True)
            
            # Backup Cortex and Nexus test projects
            source_cortex = self.base_path / "code/src/tests/00Domain/ExxerAI.Domain.Cortex.Test"
            source_nexus = self.base_path / "code/src/tests/00Domain/ExxerAI.Domain.Nexus.Test"
            
            if source_cortex.exists():
                shutil.copytree(source_cortex, self.backup_dir / "ExxerAI.Domain.Cortex.Test")
                print(f"✅ Backed up Cortex tests to {self.backup_dir}")
            
            if source_nexus.exists():
                shutil.copytree(source_nexus, self.backup_dir / "ExxerAI.Domain.Nexus.Test") 
                print(f"✅ Backed up Nexus tests to {self.backup_dir}")
                
            return True
        except Exception as e:
            print(f"❌ Backup creation failed: {e}")
            return False
    
    def create_project_file(self, project_path: Path, project_name: str, 
                           dependencies: List[str], layer: str) -> bool:
        """Create new test project file with correct dependencies"""
        
        project_content = f'''<Project Sdk="Microsoft.NET.Sdk">
\t<!-- ============================================================================ -->
\t<!-- {project_name.upper()} - {layer.upper()} LAYER TESTS -->
\t<!-- Evocative Architecture: {layer} tests for Cortex AI brain validating -->
\t<!-- layer-appropriate concerns and architectural boundaries -->
\t<!-- ============================================================================ -->

\t<!-- ============================================================================ -->
\t<!-- ASSEMBLY METADATA -->
\t<!-- ============================================================================ -->
\t<PropertyGroup>
\t\t<Version>2025.10.30.001</Version>
\t\t<AssemblyVersion>2025.10.30.001</AssemblyVersion>
\t\t<FileVersion>2025.10.30.001</FileVersion>
\t\t<Company>Exxerpro Solutions SA de CV</Company>
\t\t<Authors>Abel Briones</Authors>
\t\t<Product>ExxerAI Intelligence System</Product>
\t\t<AssemblyTitle>{project_name} - {layer} Layer Tests</AssemblyTitle>
\t\t<Description>{layer} tests for Cortex AI brain validating layer-appropriate concerns</Description>
\t\t<Copyright>© 2025 Exxerpro Solutions SA de CV</Copyright>
\t\t<RepositoryUrl>https://github.com/Exxerpro/ExxerAI.git</RepositoryUrl>
\t\t<RepositoryType>git</RepositoryType>
\t</PropertyGroup>

\t<!-- ============================================================================ -->
\t<!-- CORE PROPERTIES -->
\t<!-- ============================================================================ -->
\t<PropertyGroup>
\t\t<TargetFramework>net10.0</TargetFramework>
\t\t<LangVersion>latest</LangVersion>
\t\t<Nullable>enable</Nullable>
\t\t<ImplicitUsings>enable</ImplicitUsings>
\t\t<TreatWarningsAsErrors>true</TreatWarningsAsErrors>
\t\t<GenerateDocumentationFile>true</GenerateDocumentationFile>
\t\t<IsPackable>false</IsPackable>
\t\t<IsTestProject>true</IsTestProject>
\t\t<OutputType>Exe</OutputType>
\t\t<GenerateTestingPlatformEntryPoint>false</GenerateTestingPlatformEntryPoint>
\t\t<UseMicrosoftTestingPlatformRunner>true</UseMicrosoftTestingPlatformRunner>
\t\t<TestingPlatformDotnetTestSupport>true</TestingPlatformDotnetTestSupport>
\t\t<TestingPlatformServer>true</TestingPlatformServer>
\t</PropertyGroup>

\t<!-- ============================================================================ -->
\t<!-- PACKAGE REFERENCES - XUNIT V3 UNIVERSAL CONFIGURATION -->
\t<!-- ============================================================================ -->
\t<ItemGroup Label="XUnit v3 Universal Configuration">
\t\t<PackageReference Include="xunit" />
\t\t<PackageReference Include="xunit.runner.visualstudio" />
\t\t<PackageReference Include="Microsoft.NET.Test.Sdk" />
\t\t<PackageReference Include="Microsoft.Testing.Platform.MSBuild" />
\t\t<PackageReference Include="Microsoft.Testing.Extensions.TrxReport" />
\t</ItemGroup>

\t<!-- ============================================================================ -->
\t<!-- PACKAGE REFERENCES - TESTING FRAMEWORK -->
\t<!-- ============================================================================ -->
\t<ItemGroup Label="Testing Framework">
\t\t<PackageReference Include="Shouldly" />
\t\t<PackageReference Include="NSubstitute" />
\t\t<PackageReference Include="Meziantou.Extensions.Logging.Xunit" />
\t</ItemGroup>

\t<!-- ============================================================================ -->
\t<!-- PROJECT REFERENCES - LAYER DEPENDENCIES -->
\t<!-- ============================================================================ -->'''
        
        # Add project references based on layer
        if layer == "Domain":
            project_content += '''
\t<ItemGroup Label="Core Layer">
\t\t<ProjectReference Include="..\\..\\..\\..\\src\\Core\\ExxerAI.Domain\\ExxerAI.Domain.csproj" />
\t</ItemGroup>'''
        elif layer == "Application":
            project_content += '''
\t<ItemGroup Label="Core Layer">
\t\t<ProjectReference Include="..\\..\\..\\..\\src\\Core\\ExxerAI.Domain\\ExxerAI.Domain.csproj" />
\t\t<ProjectReference Include="..\\..\\..\\..\\src\\Core\\ExxerAI.Application\\ExxerAI.Application.csproj" />
\t</ItemGroup>'''
        elif layer == "Infrastructure":
            project_content += '''
\t<ItemGroup Label="Core Layer">
\t\t<ProjectReference Include="..\\..\\..\\..\\src\\Core\\ExxerAI.Domain\\ExxerAI.Domain.csproj" />
\t\t<ProjectReference Include="..\\..\\..\\..\\src\\Core\\ExxerAI.Application\\ExxerAI.Application.csproj" />
\t</ItemGroup>

\t<!-- ============================================================================ -->
\t<!-- PROJECT REFERENCES - INFRASTRUCTURE LAYER -->
\t<!-- ============================================================================ -->
\t<ItemGroup Label="Infrastructure Layer">
\t\t<ProjectReference Include="..\\..\\..\\..\\src\\Infrastructure\\ExxerAI.Cortex\\ExxerAI.Cortex.csproj" />
\t</ItemGroup>'''
        
        project_content += '''
</Project>
'''
        
        try:
            project_file = project_path / f"{project_name}.csproj"
            project_file.write_text(project_content, encoding='utf-8')
            print(f"✅ Created project file: {project_file}")
            return True
        except Exception as e:
            print(f"❌ Failed to create project file: {e}")
            return False
    
    def migrate_cortex_tests(self, dry_run: bool = True) -> bool:
        """Split Cortex tests across appropriate layers"""
        
        source_project = self.base_path / "code/src/tests/00Domain/ExxerAI.Domain.Cortex.Test"
        if not source_project.exists():
            print(f"❌ Source project not found: {source_project}")
            return False
        
        print("🧠 Migrating Cortex tests across layers...")
        
        for layer, config in self.migration_spec["cortex_splits"].items():
            target_project = self.base_path / config["target_project"]
            
            if dry_run:
                print(f"📋 DRY RUN - Would migrate {layer} tests:")
                print(f"   Target: {target_project}")
                print(f"   Files: {config['files']}")
                print(f"   Namespace: {config['target_namespace']}")
                continue
            
            # Create target project directory
            target_project.mkdir(parents=True, exist_ok=True)
            
            # Create project file
            project_name = target_project.name
            if not self.create_project_file(target_project, project_name, 
                                          config["dependencies"], layer.title()):
                return False
            
            # Migrate files
            for file_path in config["files"]:
                source_file = source_project / file_path
                target_file = target_project / file_path
                
                if source_file.exists():
                    # Create target directory if needed
                    target_file.parent.mkdir(parents=True, exist_ok=True)
                    
                    # Copy file and update namespace
                    content = source_file.read_text(encoding='utf-8')
                    updated_content = content.replace(
                        "ExxerAI.Domain.Cortex.UnitTests",
                        config["target_namespace"]
                    )
                    target_file.write_text(updated_content, encoding='utf-8')
                    print(f"✅ Migrated: {file_path} → {layer}")
        
        return True
    
    def migrate_nexus_tests(self, dry_run: bool = True) -> bool:
        """Move Nexus tests to Infrastructure layer"""
        
        source_project = self.base_path / self.migration_spec["nexus_move"]["source_project"]
        target_project = self.base_path / self.migration_spec["nexus_move"]["target_project"]
        
        if not source_project.exists():
            print(f"❌ Source project not found: {source_project}")
            return False
        
        print("⚡ Migrating Nexus tests to Infrastructure...")
        
        if dry_run:
            print(f"📋 DRY RUN - Would move entire project:")
            print(f"   From: {source_project}")
            print(f"   To: {target_project}")
            print(f"   Namespace: {self.migration_spec['nexus_move']['namespace_transform']['from']} → {self.migration_spec['nexus_move']['namespace_transform']['to']}")
            return True
        
        # Create target directory
        target_project.parent.mkdir(parents=True, exist_ok=True)
        
        # Copy entire project
        shutil.copytree(source_project, target_project, dirs_exist_ok=True)
        
        # Update all namespaces in CS files
        namespace_from = self.migration_spec["nexus_move"]["namespace_transform"]["from"]
        namespace_to = self.migration_spec["nexus_move"]["namespace_transform"]["to"]
        
        for cs_file in target_project.glob("**/*.cs"):
            if cs_file.name.endswith(".cs") and not cs_file.name.endswith(".g.cs"):
                content = cs_file.read_text(encoding='utf-8')
                updated_content = content.replace(namespace_from, namespace_to)
                cs_file.write_text(updated_content, encoding='utf-8')
        
        # Update project file with Infrastructure dependencies
        project_file = target_project / f"{target_project.name}.csproj"
        if project_file.exists():
            content = project_file.read_text(encoding='utf-8')
            # Update project references to include Infrastructure layer
            updated_content = content.replace(
                'Include="..\\..\\..\\..\\src\\Infrastructure\\ExxerAI.Nexus.Library\\ExxerAI.Nexus.Library.csproj"',
                'Include="..\\..\\..\\..\\src\\Infrastructure\\ExxerAI.Nexus.Library\\ExxerAI.Nexus.Library.csproj"'
            )
            project_file.write_text(updated_content, encoding='utf-8')
        
        print(f"✅ Moved Nexus tests to Infrastructure layer")
        return True
    
    def cleanup_original_files(self, dry_run: bool = True) -> bool:
        """Remove original test files after successful migration"""
        
        if dry_run:
            print("📋 DRY RUN - Would cleanup original files:")
            print("   - Remove migrated files from Domain.Cortex.Test")
            print("   - Remove entire Domain.Nexus.Test project")
            return True
        
        # Remove migrated Cortex files (keep TokenUsageTests in Domain)
        source_cortex = self.base_path / "code/src/tests/00Domain/ExxerAI.Domain.Cortex.Test"
        files_to_remove = [
            "LLMFeatures/LanguageModelTests.cs",
            "LLMFeatures/LLMIntegrationTests.cs", 
            "LLMFeatures/ModelCapabilitiesTests.cs",
            "AI/LLMRequestTests.cs",
            "AI/LLMResponseTests.cs",
            "AI/TextAnalysisRequestTests.cs",
            "CortexTestBase.cs"
        ]
        
        for file_path in files_to_remove:
            file_to_remove = source_cortex / file_path
            if file_to_remove.exists():
                file_to_remove.unlink()
                print(f"✅ Removed: {file_path}")
        
        # Remove empty directories
        for folder in ["LLMFeatures"]:
            folder_path = source_cortex / folder
            if folder_path.exists() and not list(folder_path.iterdir()):
                folder_path.rmdir()
                print(f"✅ Removed empty folder: {folder}")
        
        # Remove entire Nexus project
        source_nexus = self.base_path / "code/src/tests/00Domain/ExxerAI.Domain.Nexus.Test"
        if source_nexus.exists():
            shutil.rmtree(source_nexus)
            print(f"✅ Removed original Nexus project")
        
        return True
    
    def run_migration(self, dry_run: bool = True, user_confirmation: bool = True) -> bool:
        """Execute complete test architecture migration"""
        
        print("🏗️ TEST ARCHITECTURE MIGRATION - CLEAN ARCHITECTURE FIX")
        print("=" * 60)
        
        if not dry_run and user_confirmation:
            response = input("⚠️  This will modify your codebase. Continue? (yes/no): ")
            if response.lower() != 'yes':
                print("❌ Migration cancelled by user")
                return False
        
        # Safety checks
        if not dry_run:
            if not self.verify_git_status():
                return False
            if not self.create_backup():
                return False
        
        # Execute migrations
        success = True
        success &= self.migrate_cortex_tests(dry_run)
        success &= self.migrate_nexus_tests(dry_run)
        
        if success and not dry_run:
            success &= self.cleanup_original_files(dry_run)
        
        if success:
            print("✅ Migration completed successfully!")
            if not dry_run:
                print(f"📂 Backup available at: {self.backup_dir}")
                print("🔍 Please verify builds and tests after migration")
        else:
            print("❌ Migration failed!")
        
        return success

def main():
    """Main execution with CLI interface"""
    import argparse
    
    parser = argparse.ArgumentParser(description="Fix Clean Architecture violations in test projects")
    parser.add_argument("--base-path", required=True, help="Base path to ExxerAI project")
    parser.add_argument("--apply", action="store_true", help="Apply changes (default: dry-run)")
    parser.add_argument("--no-confirm", action="store_true", help="Skip user confirmation")
    
    args = parser.parse_args()
    
    migrator = TestArchitectureMigrator(args.base_path)
    
    dry_run = not args.apply
    user_confirmation = not args.no_confirm
    
    success = migrator.run_migration(dry_run, user_confirmation)
    
    return 0 if success else 1

if __name__ == "__main__":
    exit(main())