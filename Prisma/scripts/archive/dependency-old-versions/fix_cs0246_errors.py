#!/usr/bin/env python3
"""
CS0246 Error Fixer - Step 2: Add Missing Using Statements
Systematically fixes missing namespace and using directive errors
"""

import os
import re
from collections import defaultdict

def analyze_cs0246_errors():
    """Parse CS0246 errors from the error file"""
    error_file = r"F:\Dynamic\ExxerAi\ExxerAI\Errors\CS0246.txt"
    
    missing_types = set()
    
    try:
        with open(error_file, 'r', encoding='utf-8') as f:
            for line in f:
                # Extract type name from error message
                match = re.search(r"The type or namespace name '([^']+)' could not be found", line)
                if match:
                    missing_types.add(match.group(1))
    
    except Exception as e:
        print(f"Error reading CS0246 file: {e}")
        return set()
    
    return missing_types

def get_namespace_mappings():
    """Map missing types to their likely namespaces"""
    return {
        # System types
        'HttpStatusCode': 'System.Net',
        'WebApplicationFactory': 'Microsoft.AspNetCore.Mvc.Testing',
        
        # Vector Database
        'QdrantClient': 'Qdrant.Client',
        'QdrantConnectionConfig': 'ExxerAI.Infrastructure.VectorStore.Qdrant',
        'QdrantContainer': 'Testcontainers.Qdrant',
        'PostgreSQLVectorSearchService': 'ExxerAI.Infrastructure.VectorStore.PostgreSQL',
        'SQLServerVectorSearchService': 'ExxerAI.Infrastructure.VectorStore.SqlServer',
        'InMemoryVectorSearchService': 'ExxerAI.Infrastructure.VectorStore.InMemory',
        
        # Document Processing
        'WordDocumentExtractor': 'ExxerAI.Infrastructure.DocumentProcessing.Word',
        'ExcelDocumentExtractor': 'ExxerAI.Infrastructure.DocumentProcessing.Excel',
        'PolymorphicDocumentProcessor': 'ExxerAI.Infrastructure.DocumentProcessing',
        'TextExtractionEngine': 'ExxerAI.Infrastructure.DocumentProcessing.Text',
        'DocumentHashGenerator': 'ExxerAI.Infrastructure.DocumentProcessing',
        'DocumentType': 'ExxerAI.Domain.Documents',
        
        # Image/OCR Processing
        'OCRProcessingService': 'ExxerAI.Infrastructure.ImageProcessing.OCR',
        'ImageProcessingService': 'ExxerAI.Infrastructure.ImageProcessing',
        'ImageSharpAdapter': 'ExxerAI.Infrastructure.ImageProcessing.ImageSharp',
        'ImageProcessingOptions': 'ExxerAI.Infrastructure.ImageProcessing',
        
        # AI Providers
        'OllamaProvider': 'ExxerAI.Infrastructure.AI.Ollama',
        'OpenAIProvider': 'ExxerAI.Infrastructure.AI.OpenAI',
        
        # Graph Database
        'Neo4jContainer': 'Testcontainers.Neo4j',
        'Neo4jConnectionConfig': 'ExxerAI.Infrastructure.GraphStore.Neo4j',
        'IGraphClient': 'ExxerAI.Infrastructure.GraphStore',
        
        # Google Drive
        'GoogleDriveM2MService': 'ExxerAI.Infrastructure.GoogleDrive',
        'GoogleDriveWatchOptions': 'ExxerAI.Infrastructure.GoogleDrive',
        'DriveService': 'Google.Apis.Drive.v3',
        
        # Testing Infrastructure
        'PostgreSqlContainer': 'Testcontainers.PostgreSql',
        
        # Business Intelligence
        'DashboardService': 'ExxerAI.Application.BusinessIntelligence',
        'InfrastructureStatisticsService': 'ExxerAI.Application.Infrastructure',
        
        # CubeExplorer
        'CubeXDocumentParser': 'ExxerAI.Infrastructure.CubeExplorer',
        'ICubeXDocumentParser': 'ExxerAI.Infrastructure.CubeExplorer',
        'ArtifactLoader': 'ExxerAI.Infrastructure.CubeExplorer',
        'ArtifactSet': 'ExxerAI.Infrastructure.CubeExplorer',
        
        # Security & API
        'ISecurityService': 'ExxerAI.Infrastructure.Security',
        'ISATApiClient': 'ExxerAI.Infrastructure.External.SAT',
        'TimbradoApiClient': 'ExxerAI.Infrastructure.External.Timbrado',
        'LiveDataConnectorService': 'ExxerAI.Infrastructure.External',
        
        # Validation
        'ValidationRules': 'ExxerAI.Domain.Validation',
        'ValidatedStatement': 'ExxerAI.Domain.Validation',
        'RuleParserEngine': 'ExxerAI.Domain.Validation',
    }

def find_files_with_missing_types():
    """Find source files that likely contain the missing type references"""
    source_dirs = [
        r"F:\Dynamic\ExxerAi\ExxerAI\code\src\Core",
        r"F:\Dynamic\ExxerAi\ExxerAI\code\src\Infrastructure", 
        r"F:\Dynamic\ExxerAi\ExxerAI\code\src\tests"
    ]
    
    missing_types = analyze_cs0246_errors()
    namespace_map = get_namespace_mappings()
    
    files_to_fix = defaultdict(set)
    
    for source_dir in source_dirs:
        if not os.path.exists(source_dir):
            continue
            
        for root, dirs, files in os.walk(source_dir):
            for file in files:
                if file.endswith('.cs'):
                    file_path = os.path.join(root, file)
                    
                    try:
                        with open(file_path, 'r', encoding='utf-8') as f:
                            content = f.read()
                            
                        # Check which missing types are referenced in this file
                        for missing_type in missing_types:
                            if missing_type in content:
                                # Determine what namespace to add
                                namespace = namespace_map.get(missing_type, f"ExxerAI.Unknown.{missing_type}")
                                files_to_fix[file_path].add(namespace)
                                
                    except Exception as e:
                        print(f"Error reading {file_path}: {e}")
                        continue
    
    return files_to_fix

def add_using_statements(file_path, namespaces_to_add):
    """Add missing using statements to a C# file"""
    try:
        with open(file_path, 'r', encoding='utf-8') as f:
            lines = f.readlines()
        
        # Find existing using statements
        using_lines = []
        non_using_start = 0
        
        for i, line in enumerate(lines):
            stripped = line.strip()
            if stripped.startswith('using ') and not stripped.startswith('using ('):
                using_lines.append(stripped)
            elif stripped and not stripped.startswith('//'):
                non_using_start = i
                break
        
        # Get existing namespaces
        existing_namespaces = set()
        for using_line in using_lines:
            match = re.search(r'using\s+([^;]+);', using_line)
            if match:
                existing_namespaces.add(match.group(1).strip())
        
        # Add new namespaces that don't already exist
        new_namespaces = namespaces_to_add - existing_namespaces
        
        if new_namespaces:
            # Create new using statements
            new_usings = [f"using {ns};\n" for ns in sorted(new_namespaces)]
            
            # Find where to insert (after existing usings, before first non-using line)
            insert_point = non_using_start
            
            # Insert the new using statements
            for i, new_using in enumerate(new_usings):
                lines.insert(insert_point + i, new_using)
            
            # Write back to file
            with open(file_path, 'w', encoding='utf-8') as f:
                f.writelines(lines)
            
            print(f"✓ Added {len(new_namespaces)} using statements to {os.path.basename(file_path)}")
            for ns in sorted(new_namespaces):
                print(f"  + using {ns};")
            
            return True
        else:
            print(f"○ No new using statements needed for {os.path.basename(file_path)}")
            return False
            
    except Exception as e:
        print(f"✗ Error fixing {file_path}: {e}")
        return False

def main():
    print("🔧 CS0246 Error Fixer - Step 2: Adding Missing Using Statements")
    print("=" * 70)
    
    # Analyze errors
    missing_types = analyze_cs0246_errors()
    print(f"📊 Found {len(missing_types)} unique missing types")
    
    # Find files to fix
    print("\n🔍 Scanning source files for missing type references...")
    files_to_fix = find_files_with_missing_types()
    
    if not files_to_fix:
        print("✅ No files found that need using statement fixes")
        return
    
    print(f"\n📁 Found {len(files_to_fix)} files to fix")
    
    # Apply fixes
    print("\n🛠️ Applying using statement fixes...")
    fixed_count = 0
    
    for file_path, namespaces in files_to_fix.items():
        print(f"\n📝 Processing {os.path.basename(file_path)}:")
        if add_using_statements(file_path, namespaces):
            fixed_count += 1
    
    print(f"\n{'=' * 70}")
    print(f"✅ STEP 2 COMPLETE:")
    print(f"   • Files processed: {len(files_to_fix)}")
    print(f"   • Files modified: {fixed_count}")
    print(f"   • Missing types addressed: {len(missing_types)}")
    print(f"{'=' * 70}")

if __name__ == "__main__":
    main()