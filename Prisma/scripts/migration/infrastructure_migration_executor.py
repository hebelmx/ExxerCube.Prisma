#!/usr/bin/env python3
"""
ExxerAI Infrastructure Split Migration Executor
Dependency-safe migration of 325 files from Infrastructure to 7 specialized projects
"""

import os
import shutil
import subprocess
import json
import datetime
from pathlib import Path
from typing import Dict, List, Tuple, Optional
import logging

class InfrastructureMigrationExecutor:
    """Executes the infrastructure split migration with dependency safety"""
    
    def __init__(self, base_path: str):
        self.base_path = Path(base_path)
        self.source_path = self.base_path / "code/src/Infrastructure/ExxerAI.Infrastructure"
        self.target_base = self.base_path / "code/src/Infrastructure"
        
        # Migration configuration
        self.projects = {
            "ExxerAI.Data": {
                "namespace": "ExxerAI.Data",
                "dependencies": ["ExxerAI.Domain", "ExxerAI.Application"],
                "nuget_packages": [
                    "Microsoft.EntityFrameworkCore",
                    "Microsoft.EntityFrameworkCore.Design", 
                    "Npgsql.EntityFrameworkCore.PostgreSQL",
                    "Pgvector.EntityFrameworkCore"
                ]
            },
            "ExxerAI.AI": {
                "namespace": "ExxerAI.AI",
                "dependencies": ["ExxerAI.Domain", "ExxerAI.Application", "ExxerAI.Storage"],
                "nuget_packages": [
                    "Microsoft.Extensions.AI",
                    "Azure.AI.OpenAI",
                    "Microsoft.Extensions.AI.OpenAI"
                ]
            },
            "ExxerAI.External": {
                "namespace": "ExxerAI.External", 
                "dependencies": ["ExxerAI.Domain", "ExxerAI.Application"],
                "nuget_packages": []
            },
            "ExxerAI.Security": {
                "namespace": "ExxerAI.Security",
                "dependencies": ["ExxerAI.Domain", "ExxerAI.Application", "ExxerAI.External"],
                "nuget_packages": [
                    "Google.Apis.Auth",
                    "Google.Apis.Drive.v3",
                    "Azure.Identity"
                ]
            },
            "ExxerAI.Storage": {
                "namespace": "ExxerAI.Storage",
                "dependencies": ["ExxerAI.Domain", "ExxerAI.Application", "ExxerAI.Security"],
                "nuget_packages": [
                    "Qdrant.Client",
                    "Neo4jClient"
                ]
            },
            "ExxerAI.Messaging": {
                "namespace": "ExxerAI.Messaging",
                "dependencies": ["ExxerAI.Domain", "ExxerAI.Application", "ExxerAI.AI", "ExxerAI.Storage"],
                "nuget_packages": [
                    "A2A",
                    "A2A.AspNetCore",
                    "Microsoft.Agents.AI.A2A"
                ]
            },
            "ExxerAI.Processing": {
                "namespace": "ExxerAI.Processing",
                "dependencies": ["ExxerAI.Domain", "ExxerAI.Application", "ExxerAI.AI", "ExxerAI.Storage"],
                "nuget_packages": [
                    "PdfPig",
                    "DocumentFormat.OpenXml",
                    "NPOI",
                    "OpenCvSharp4",
                    "Tesseract"
                ]
            }
        }
        
        # File classification mapping
        self.file_mapping = {
            "ExxerAI.Data": [
                "Data/", "Database/", "Repositories/", "Migrations/"
            ],
            "ExxerAI.AI": [
                "Adapters/AI/", "LLM/", "Embedding/", "Embeddings/"
            ],
            "ExxerAI.External": [
                "External/"
            ],
            "ExxerAI.Security": [
                "Adapters/GoogleDrive/"
            ],
            "ExxerAI.Storage": [
                "VectorStore/", "GraphStore/", "Storage/"
            ],
            "ExxerAI.Messaging": [
                "AgentCommunication/"
            ],
            "ExxerAI.Processing": [
                "DocumentProcessing/", "TechStack/", "Adapters/DocumentProcessing/", 
                "Adapters/ImageProcessing/", "Services/ImageProcessing/"
            ]
        }
        
        # Migration phases with dependency order
        self.migration_phases = [
            {
                "name": "Phase 1: Foundation Setup",
                "batches": [
                    {"name": "Project Creation", "projects": list(self.projects.keys()), "parallel": True},
                    {"name": "Shared Interfaces", "projects": ["Infrastructure"], "parallel": False}
                ]
            },
            {
                "name": "Phase 2: Low-Dependency Projects", 
                "batches": [
                    {"name": "External APIs", "projects": ["ExxerAI.External"], "parallel": False},
                    {"name": "Security Foundation", "projects": ["ExxerAI.Security"], "parallel": False}
                ]
            },
            {
                "name": "Phase 3: Storage Layer",
                "batches": [
                    {"name": "Storage Services", "projects": ["ExxerAI.Storage"], "parallel": False}
                ]
            },
            {
                "name": "Phase 4: Data Layer",
                "batches": [
                    {"name": "Data Services", "projects": ["ExxerAI.Data"], "parallel": False}
                ]
            },
            {
                "name": "Phase 5: AI Services", 
                "batches": [
                    {"name": "AI Services", "projects": ["ExxerAI.AI"], "parallel": False}
                ]
            },
            {
                "name": "Phase 6: Document Processing",
                "batches": [
                    {"name": "Processing Services", "projects": ["ExxerAI.Processing"], "parallel": False}
                ]
            },
            {
                "name": "Phase 7: Messaging",
                "batches": [
                    {"name": "Messaging Services", "projects": ["ExxerAI.Messaging"], "parallel": False}
                ]
            }
        ]
        
        # Setup logging
        self.setup_logging()
    
    def setup_logging(self):
        """Setup logging configuration"""
        log_dir = self.base_path / "docs/migration/logs"
        log_dir.mkdir(parents=True, exist_ok=True)
        
        timestamp = datetime.datetime.now().strftime("%Y%m%d_%H%M%S")
        log_file = log_dir / f"infrastructure_migration_{timestamp}.log"
        
        logging.basicConfig(
            level=logging.INFO,
            format='%(asctime)s - %(levelname)s - %(message)s',
            handlers=[
                logging.FileHandler(log_file),
                logging.StreamHandler()
            ]
        )
        self.logger = logging.getLogger(__name__)
    
    def create_project_structure(self, project_name: str) -> bool:
        """Create the basic project structure for a specialized project"""
        try:
            project_path = self.target_base / project_name
            project_path.mkdir(parents=True, exist_ok=True)
            
            # Create project file
            self.create_project_file(project_name, project_path)
            
            # Create basic directory structure
            dirs_to_create = [
                "Interfaces", "Services", "Extensions", "Configuration"
            ]
            
            for dir_name in dirs_to_create:
                (project_path / dir_name).mkdir(exist_ok=True)
            
            self.logger.info(f"Created project structure for {project_name}")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to create project structure for {project_name}: {e}")
            return False
    
    def create_project_file(self, project_name: str, project_path: Path):
        """Create the .csproj file for a specialized project"""
        config = self.projects[project_name]
        
        project_content = f'''<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
    <EnableDynamicPgo>true</EnableDynamicPgo>
    <TieredCompilation>true</TieredCompilation>
  </PropertyGroup>

  <ItemGroup Label="Core Framework">
    <PackageReference Include="Microsoft.Extensions.Logging" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" />
    <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" />
    <PackageReference Include="IndQuestResults" />
  </ItemGroup>
'''

        # Add specific NuGet packages
        if config["nuget_packages"]:
            project_content += f'''
  <ItemGroup Label="{project_name} Specific">
'''
            for package in config["nuget_packages"]:
                project_content += f'    <PackageReference Include="{package}" />\n'
            project_content += '  </ItemGroup>\n'

        # Add project references
        project_content += '''
  <ItemGroup>
'''
        for dep in config["dependencies"]:
            if dep.startswith("ExxerAI."):
                if dep in ["ExxerAI.Domain", "ExxerAI.Application"]:
                    rel_path = f"../../Core/{dep}/{dep}.csproj"
                else:
                    rel_path = f"../{dep}/{dep}.csproj"
                project_content += f'    <ProjectReference Include="{rel_path}" />\n'
        
        project_content += '''  </ItemGroup>

</Project>'''

        with open(project_path / f"{project_name}.csproj", 'w', encoding='utf-8') as f:
            f.write(project_content)
    
    def classify_files(self) -> Dict[str, List[str]]:
        """Classify all files in Infrastructure by target project"""
        classified_files = {project: [] for project in self.projects.keys()}
        classified_files["Infrastructure"] = []  # For files staying in Infrastructure
        
        if not self.source_path.exists():
            self.logger.error(f"Source path does not exist: {self.source_path}")
            return classified_files
        
        for root, dirs, files in os.walk(self.source_path):
            for file in files:
                if not file.endswith('.cs'):
                    continue
                    
                file_path = Path(root) / file
                relative_path = file_path.relative_to(self.source_path)
                
                # Classify based on directory structure
                classified = False
                for project, patterns in self.file_mapping.items():
                    for pattern in patterns:
                        if str(relative_path).startswith(pattern):
                            classified_files[project].append(str(relative_path))
                            classified = True
                            break
                    if classified:
                        break
                
                # If not classified, keep in Infrastructure
                if not classified:
                    # Check for shared infrastructure files
                    if any(keyword in str(relative_path).lower() for keyword in 
                           ['extension', 'interface', 'configuration', 'dependencyinjection']):
                        classified_files["Infrastructure"].append(str(relative_path))
                    else:
                        # Default classification based on content analysis would go here
                        classified_files["Infrastructure"].append(str(relative_path))
        
        return classified_files
    
    def move_files(self, project_name: str, files: List[str]) -> bool:
        """Move files to the target project"""
        try:
            target_path = self.target_base / project_name
            moved_count = 0
            
            for file_path in files:
                source_file = self.source_path / file_path
                target_file = target_path / file_path
                
                if not source_file.exists():
                    self.logger.warning(f"Source file does not exist: {source_file}")
                    continue
                
                # Create target directory if needed
                target_file.parent.mkdir(parents=True, exist_ok=True)
                
                # Move file
                shutil.move(str(source_file), str(target_file))
                moved_count += 1
                
                # Update namespace in file
                self.update_namespace(target_file, project_name)
            
            self.logger.info(f"Moved {moved_count} files to {project_name}")
            return True
            
        except Exception as e:
            self.logger.error(f"Failed to move files to {project_name}: {e}")
            return False
    
    def update_namespace(self, file_path: Path, project_name: str):
        """Update namespace in a C# file"""
        try:
            with open(file_path, 'r', encoding='utf-8') as f:
                content = f.read()
            
            # Update namespace
            target_namespace = self.projects[project_name]["namespace"]
            
            # Replace Infrastructure namespace
            content = content.replace(
                "namespace ExxerAI.Infrastructure",
                f"namespace {target_namespace}"
            )
            
            # Update using statements
            content = content.replace(
                "using ExxerAI.Infrastructure",
                f"using {target_namespace}"
            )
            
            with open(file_path, 'w', encoding='utf-8') as f:
                f.write(content)
                
        except Exception as e:
            self.logger.warning(f"Failed to update namespace in {file_path}: {e}")
    
    def validate_build(self) -> bool:
        """Validate that the solution still builds"""
        try:
            result = subprocess.run(
                ["dotnet", "build", "--no-restore", "--verbosity", "minimal"],
                cwd=self.base_path,
                capture_output=True,
                text=True,
                timeout=300
            )
            
            if result.returncode == 0:
                self.logger.info("Build validation PASSED")
                return True
            else:
                self.logger.error(f"Build validation FAILED: {result.stderr}")
                return False
                
        except subprocess.TimeoutExpired:
            self.logger.error("Build validation TIMEOUT")
            return False
        except Exception as e:
            self.logger.error(f"Build validation ERROR: {e}")
            return False
    
    def execute_migration(self, dry_run: bool = False) -> bool:
        """Execute the complete migration"""
        self.logger.info("Starting Infrastructure Split Migration")
        self.logger.info(f"Dry run mode: {dry_run}")
        
        # Step 1: Classify all files
        self.logger.info("Step 1: Classifying files...")
        classified_files = self.classify_files()
        
        # Log classification results
        total_files = sum(len(files) for files in classified_files.values())
        self.logger.info(f"Total files classified: {total_files}")
        for project, files in classified_files.items():
            self.logger.info(f"  {project}: {len(files)} files")
        
        if dry_run:
            self.logger.info("DRY RUN - No actual changes made")
            return True
        
        # Step 2: Create project structures
        self.logger.info("Step 2: Creating project structures...")
        for project_name in self.projects.keys():
            if not self.create_project_structure(project_name):
                return False
        
        # Step 3: Execute migration in phases
        for phase in self.migration_phases:
            self.logger.info(f"Executing {phase['name']}")
            
            for batch in phase['batches']:
                self.logger.info(f"  Batch: {batch['name']}")
                
                if batch['name'] == 'Project Creation':
                    continue  # Already done in Step 2
                
                if batch['name'] == 'Shared Interfaces':
                    # Keep shared files in Infrastructure
                    continue
                
                # Move files for projects in this batch
                for project_name in batch['projects']:
                    if project_name in classified_files:
                        files = classified_files[project_name]
                        if files:
                            self.logger.info(f"    Moving {len(files)} files to {project_name}")
                            if not self.move_files(project_name, files):
                                return False
                
                # Validate build after each batch
                self.logger.info("    Validating build...")
                if not self.validate_build():
                    self.logger.error(f"Build failed after {batch['name']}")
                    return False
        
        self.logger.info("Infrastructure Split Migration COMPLETED successfully")
        return True
    
    def generate_report(self) -> Dict:
        """Generate migration report"""
        classified_files = self.classify_files()
        
        report = {
            "timestamp": datetime.datetime.now().isoformat(),
            "total_files": sum(len(files) for files in classified_files.values()),
            "projects": {}
        }
        
        for project, files in classified_files.items():
            report["projects"][project] = {
                "file_count": len(files),
                "files": files,
                "percentage": round(len(files) / report["total_files"] * 100, 1) if report["total_files"] > 0 else 0
            }
        
        return report


def main():
    """Main execution function"""
    import argparse
    
    parser = argparse.ArgumentParser(description="ExxerAI Infrastructure Split Migration")
    parser.add_argument("--base-path", required=True, help="Base path to ExxerAI project")
    parser.add_argument("--dry-run", action="store_true", help="Perform dry run without making changes")
    parser.add_argument("--report-only", action="store_true", help="Generate report only")
    
    args = parser.parse_args()
    
    executor = InfrastructureMigrationExecutor(args.base_path)
    
    if args.report_only:
        report = executor.generate_report()
        report_file = Path(args.base_path) / "docs/migration/infrastructure_classification_report.json"
        report_file.parent.mkdir(parents=True, exist_ok=True)
        
        with open(report_file, 'w', encoding='utf-8') as f:
            json.dump(report, f, indent=2)
        
        print(f"Report generated: {report_file}")
        print(f"Total files: {report['total_files']}")
        for project, data in report['projects'].items():
            print(f"  {project}: {data['file_count']} files ({data['percentage']}%)")
    else:
        success = executor.execute_migration(dry_run=args.dry_run)
        exit(0 if success else 1)


if __name__ == "__main__":
    main()