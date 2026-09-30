#!/usr/bin/env python3
"""
Enhanced Smart Dependency Analyzer v3 - Debug Edition
- Comprehensive logging and debugging to identify misses
- Detailed statistics on resolution success/failure rates
- Miss analysis with categorization
- Performance tracking and bottleneck identification
- Safety-compliant with industrial standards
"""

import os
import re
import json
import xml.etree.ElementTree as ET
from pathlib import Path
from typing import Set, List, Dict, Tuple, Optional
from collections import defaultdict, Counter
from datetime import datetime
import logging
import sys

class DebugLogger:
    """Centralized logging for debugging dependency resolution."""
    
    def __init__(self, base_path: str, verbose: bool = False):
        self.base_path = Path(base_path)
        self.verbose = verbose
        
        # Create logs directory
        log_dir = self.base_path / "logs"
        log_dir.mkdir(exist_ok=True)
        
        # Setup logging
        timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
        log_file = log_dir / f"dependency_analysis_debug_{timestamp}.log"
        
        logging.basicConfig(
            level=logging.DEBUG if verbose else logging.INFO,
            format='%(asctime)s - %(levelname)s - %(message)s',
            handlers=[
                logging.FileHandler(log_file, encoding='utf-8'),
                logging.StreamHandler(sys.stdout)
            ]
        )
        
        self.logger = logging.getLogger(__name__)
        self.logger.info(f"🐛 Debug logging initialized: {log_file}")
        
        # Statistics tracking
        self.stats = {
            'total_errors': 0,
            'resolved_types': 0,
            'missed_types': 0,
            'clr_matches': 0,
            'package_matches': 0,
            'dictionary_matches': 0,
            'projects_analyzed': 0,
            'files_scanned': 0,
            'resolution_sources': Counter(),
            'miss_categories': Counter(),
            'performance_metrics': {}
        }
        
        # Miss tracking
        self.missed_types = []
        self.resolution_attempts = []

class EnhancedSmartDependencyAnalyzerV3:
    """Enhanced analyzer with comprehensive debugging and miss analysis."""
    
    def __init__(self, base_path: str, verbose: bool = False):
        self.base_path = Path(base_path)
        self.src_path = self.base_path / "code" / "src"
        self.tests_path = self.base_path / "code" / "src" / "tests"
        
        # Initialize debug logger
        self.debug = DebugLogger(base_path, verbose)
        self.logger = self.debug.logger
        
        # Load type dictionary with detailed logging
        self.type_dictionary = self._load_type_dictionary()
        self.logger.info(f"📚 Type dictionary loaded: {len(self.type_dictionary.get('type_lookup', {}))} types")
        
        # Load Directory.Build.props injected namespaces
        self.injected_namespaces = self._load_directory_build_props()
        self.logger.info(f"🏗️ Directory.Build.props namespaces: {len(self.injected_namespaces)}")
        
        # Cache for existing GlobalUsings.cs content
        self.global_usings_cache = {}
        
        # Enhanced CLR types with debug tracking
        self.clr_type_namespaces = self._build_enhanced_clr_types()
        self.logger.info(f"🔧 CLR types registered: {len(self.clr_type_namespaces)}")
        
        # Enhanced package types
        self.package_type_namespaces = self._build_enhanced_package_types()
        self.logger.info(f"📦 Package types registered: {len(self.package_type_namespaces)}")
        
        # ExxerAI internal types
        self.exxerai_type_namespaces = self._build_exxerai_types()
        self.logger.info(f"🎯 ExxerAI types registered: {len(self.exxerai_type_namespaces)}")
        
        # Pattern matching rules
        self.pattern_rules = self._build_pattern_rules()
        self.logger.info(f"🔍 Pattern rules loaded: {len(self.pattern_rules)}")
    
    def _build_enhanced_clr_types(self) -> Dict[str, str]:
        """Build comprehensive CLR types dictionary with debug logging."""
        clr_types = {
            # Core System Types
            'Encoding': 'System.Text',
            'HttpClient': 'System.Net.Http',
            'JsonSerializer': 'System.Text.Json',
            'File': 'System.IO',
            'Path': 'System.IO',
            'Directory': 'System.IO',
            'Stream': 'System.IO',
            'MemoryStream': 'System.IO',
            'FileStream': 'System.IO',
            'StreamReader': 'System.IO',
            'StreamWriter': 'System.IO',
            'StringReader': 'System.IO',
            'StringWriter': 'System.IO',
            'BinaryReader': 'System.IO',
            'BinaryWriter': 'System.IO',
            'Task': 'System.Threading.Tasks',
            'TaskCompletionSource': 'System.Threading.Tasks',
            'CancellationToken': 'System.Threading',
            'CancellationTokenSource': 'System.Threading',
            'List': 'System.Collections.Generic',
            'Dictionary': 'System.Collections.Generic',
            'HashSet': 'System.Collections.Generic',
            'Queue': 'System.Collections.Generic',
            'Stack': 'System.Collections.Generic',
            'LinkedList': 'System.Collections.Generic',
            'SortedDictionary': 'System.Collections.Generic',
            'SortedList': 'System.Collections.Generic',
            'IEnumerable': 'System.Collections.Generic',
            'ICollection': 'System.Collections.Generic',
            'IList': 'System.Collections.Generic',
            'IDictionary': 'System.Collections.Generic',
            'ISet': 'System.Collections.Generic',
            'KeyValuePair': 'System.Collections.Generic',
            'Comparer': 'System.Collections.Generic',
            'EqualityComparer': 'System.Collections.Generic',
            'Guid': 'System',
            'DateTime': 'System',
            'DateTimeOffset': 'System',
            'TimeSpan': 'System',
            'TimeOnly': 'System',
            'DateOnly': 'System',
            'Math': 'System',
            'Convert': 'System',
            'String': 'System',
            'StringBuilder': 'System.Text',
            'Int32': 'System',
            'Int64': 'System',
            'Int16': 'System',
            'UInt32': 'System',
            'UInt64': 'System',
            'UInt16': 'System',
            'Byte': 'System',
            'SByte': 'System',
            'Double': 'System',
            'Single': 'System',
            'Decimal': 'System',
            'Boolean': 'System',
            'Char': 'System',
            'Object': 'System',
            'Exception': 'System',
            'ArgumentException': 'System',
            'ArgumentNullException': 'System',
            'ArgumentOutOfRangeException': 'System',
            'InvalidOperationException': 'System',
            'NotImplementedException': 'System',
            'NotSupportedException': 'System',
            'NullReferenceException': 'System',
            'IndexOutOfRangeException': 'System',
            'FormatException': 'System',
            'OverflowException': 'System',
            'DivideByZeroException': 'System',
            'Regex': 'System.Text.RegularExpressions',
            'Match': 'System.Text.RegularExpressions',
            'Group': 'System.Text.RegularExpressions',
            'Capture': 'System.Text.RegularExpressions',
            'RegexOptions': 'System.Text.RegularExpressions',
            'Console': 'System',
            'Environment': 'System',
            'Process': 'System.Diagnostics',
            'ProcessStartInfo': 'System.Diagnostics',
            'Stopwatch': 'System.Diagnostics',
            'Debug': 'System.Diagnostics',
            'Trace': 'System.Diagnostics',
            'EventLog': 'System.Diagnostics',
            'PerformanceCounter': 'System.Diagnostics',
            'Assembly': 'System.Reflection',
            'Type': 'System',
            'MethodInfo': 'System.Reflection',
            'PropertyInfo': 'System.Reflection',
            'FieldInfo': 'System.Reflection',
            'ConstructorInfo': 'System.Reflection',
            'ParameterInfo': 'System.Reflection',
            'MemberInfo': 'System.Reflection',
            'Attribute': 'System',
            'AttributeUsageAttribute': 'System',
            'ObsoleteAttribute': 'System',
            'SerializableAttribute': 'System',
            'IDisposable': 'System',
            'IComparable': 'System',
            'IEquatable': 'System',
            'IFormattable': 'System',
            'ICloneable': 'System',
            'IConvertible': 'System',
            'Action': 'System',
            'Func': 'System',
            'Predicate': 'System',
            'EventHandler': 'System',
            'EventArgs': 'System',
            'Random': 'System',
            'Uri': 'System',
            'UriBuilder': 'System',
            'Version': 'System',
            'Array': 'System',
            'Tuple': 'System',
            'ValueTuple': 'System',
            'Nullable': 'System',
            'Lazy': 'System',
            'WeakReference': 'System',
            'GC': 'System',
            'IntPtr': 'System',
            'UIntPtr': 'System',
            'Marshal': 'System.Runtime.InteropServices',
            'DllImportAttribute': 'System.Runtime.InteropServices',
            'StructLayoutAttribute': 'System.Runtime.InteropServices',
            'Thread': 'System.Threading',
            'ThreadPool': 'System.Threading',
            'ThreadStart': 'System.Threading',
            'ParameterizedThreadStart': 'System.Threading',
            'Monitor': 'System.Threading',
            'Mutex': 'System.Threading',
            'Semaphore': 'System.Threading',
            'SemaphoreSlim': 'System.Threading',
            'ManualResetEvent': 'System.Threading',
            'ManualResetEventSlim': 'System.Threading',
            'AutoResetEvent': 'System.Threading',
            'Timer': 'System.Threading',
            'Volatile': 'System.Threading',
            'Interlocked': 'System.Threading',
            'ReaderWriterLock': 'System.Threading',
            'ReaderWriterLockSlim': 'System.Threading',
        }
        
        self.logger.debug(f"🔧 CLR types built: {len(clr_types)} types")
        return clr_types
    
    def _build_enhanced_package_types(self) -> Dict[str, str]:
        """Build comprehensive package types dictionary."""
        package_types = {
            # Microsoft.Extensions.*
            'IServiceCollection': 'Microsoft.Extensions.DependencyInjection',
            'IServiceProvider': 'Microsoft.Extensions.DependencyInjection',
            'ServiceDescriptor': 'Microsoft.Extensions.DependencyInjection',
            'ServiceCollectionExtensions': 'Microsoft.Extensions.DependencyInjection',
            'IConfiguration': 'Microsoft.Extensions.Configuration',
            'IConfigurationRoot': 'Microsoft.Extensions.Configuration',
            'IConfigurationSection': 'Microsoft.Extensions.Configuration',
            'ConfigurationBuilder': 'Microsoft.Extensions.Configuration',
            'ILogger': 'Microsoft.Extensions.Logging',
            'ILoggerFactory': 'Microsoft.Extensions.Logging',
            'LogLevel': 'Microsoft.Extensions.Logging',
            'ILoggerProvider': 'Microsoft.Extensions.Logging',
            'IOptions': 'Microsoft.Extensions.Options',
            'IOptionsSnapshot': 'Microsoft.Extensions.Options',
            'IOptionsMonitor': 'Microsoft.Extensions.Options',
            'OptionsBuilder': 'Microsoft.Extensions.Options',
            'IHostBuilder': 'Microsoft.Extensions.Hosting',
            'IHost': 'Microsoft.Extensions.Hosting',
            'IHostedService': 'Microsoft.Extensions.Hosting',
            'BackgroundService': 'Microsoft.Extensions.Hosting',
            'FakeTimeProvider': 'Microsoft.Extensions.TimeProvider.Testing',
            'TimeProvider': 'System',
            
            # Newtonsoft.Json
            'JsonProperty': 'Newtonsoft.Json',
            'JsonPropertyAttribute': 'Newtonsoft.Json',
            'JsonIgnoreAttribute': 'Newtonsoft.Json',
            'JsonConvert': 'Newtonsoft.Json',
            'JsonSerializer': 'Newtonsoft.Json',
            'JsonReader': 'Newtonsoft.Json',
            'JsonWriter': 'Newtonsoft.Json',
            'JsonTextReader': 'Newtonsoft.Json',
            'JsonTextWriter': 'Newtonsoft.Json',
            'JObject': 'Newtonsoft.Json.Linq',
            'JArray': 'Newtonsoft.Json.Linq',
            'JToken': 'Newtonsoft.Json.Linq',
            'JValue': 'Newtonsoft.Json.Linq',
            'JProperty': 'Newtonsoft.Json.Linq',
            
            # System.Net.Http
            'HttpMethod': 'System.Net.Http',
            'HttpRequestMessage': 'System.Net.Http',
            'HttpResponseMessage': 'System.Net.Http',
            'HttpContent': 'System.Net.Http',
            'StringContent': 'System.Net.Http',
            'ByteArrayContent': 'System.Net.Http',
            'FormUrlEncodedContent': 'System.Net.Http',
            'MultipartFormDataContent': 'System.Net.Http',
            'HttpClientHandler': 'System.Net.Http',
            'DelegatingHandler': 'System.Net.Http',
            'HttpMessageHandler': 'System.Net.Http',
            'HttpRequestException': 'System.Net.Http',
            
            # Testing frameworks
            'TestClass': 'Microsoft.VisualStudio.TestTools.UnitTesting',
            'TestMethod': 'Microsoft.VisualStudio.TestTools.UnitTesting',
            'TestInitialize': 'Microsoft.VisualStudio.TestTools.UnitTesting',
            'TestCleanup': 'Microsoft.VisualStudio.TestTools.UnitTesting',
            'Assert': 'Microsoft.VisualStudio.TestTools.UnitTesting',
            'CollectionAssert': 'Microsoft.VisualStudio.TestTools.UnitTesting',
            'StringAssert': 'Microsoft.VisualStudio.TestTools.UnitTesting',
            
            # xUnit
            'Fact': 'Xunit',
            'Theory': 'Xunit',
            'InlineData': 'Xunit',
            'MemberData': 'Xunit',
            'ClassData': 'Xunit',
            'Trait': 'Xunit',
            'Skip': 'Xunit',
            'Collection': 'Xunit',
            'ITestOutputHelper': 'Xunit.Abstractions',
            'ICollectionFixture': 'Xunit',
            'IClassFixture': 'Xunit',
            
            # NSubstitute (mocking)
            'Substitute': 'NSubstitute',
            'Arg': 'NSubstitute',
            'Returns': 'NSubstitute',
            'Received': 'NSubstitute',
            'DidNotReceive': 'NSubstitute',
            
            # Shouldly (assertions)
            'Should': 'Shouldly',
            'ShouldBe': 'Shouldly',
            'ShouldNotBe': 'Shouldly',
            'ShouldBeNull': 'Shouldly',
            'ShouldNotBeNull': 'Shouldly',
            'ShouldBeTrue': 'Shouldly',
            'ShouldBeFalse': 'Shouldly',
            'ShouldContain': 'Shouldly',
            'ShouldNotContain': 'Shouldly',
            'ShouldBeEmpty': 'Shouldly',
            'ShouldNotBeEmpty': 'Shouldly',
            
            # Entity Framework Core
            'DbContext': 'Microsoft.EntityFrameworkCore',
            'DbSet': 'Microsoft.EntityFrameworkCore',
            'DbContextOptions': 'Microsoft.EntityFrameworkCore',
            'DbContextOptionsBuilder': 'Microsoft.EntityFrameworkCore',
            'Database': 'Microsoft.EntityFrameworkCore',
            'ModelBuilder': 'Microsoft.EntityFrameworkCore',
            'EntityEntry': 'Microsoft.EntityFrameworkCore',
            'ChangeTracker': 'Microsoft.EntityFrameworkCore',
            'EntityState': 'Microsoft.EntityFrameworkCore',
            'Migration': 'Microsoft.EntityFrameworkCore.Migrations',
            'MigrationBuilder': 'Microsoft.EntityFrameworkCore.Migrations',
        }
        
        self.logger.debug(f"📦 Package types built: {len(package_types)} types")
        return package_types
    
    def _build_exxerai_types(self) -> Dict[str, str]:
        """Build ExxerAI internal types dictionary."""
        exxerai_types = {
            # Core ExxerAI types (to be expanded based on analysis)
            'Result': 'ExxerAI.Core.Results',
            'IRepository': 'ExxerAI.Core.Interfaces',
            'IService': 'ExxerAI.Core.Interfaces',
            'BaseEntity': 'ExxerAI.Core.Entities',
            'IDomainEvent': 'ExxerAI.Core.Events',
            'ICommand': 'ExxerAI.Core.Commands',
            'IQuery': 'ExxerAI.Core.Queries',
            'IHandler': 'ExxerAI.Core.Handlers',
            'ValueObject': 'ExxerAI.Core.ValueObjects',
            'AggregateRoot': 'ExxerAI.Core.Entities',
            
            # Evocative Architecture Components
            'IAxisService': 'ExxerAI.Axis',
            'IDatastreamRepository': 'ExxerAI.Datastream',
            'ICortexService': 'ExxerAI.Cortex',
            'IGatekeeperAdapter': 'ExxerAI.Gatekeeper',
            'IVaultStorage': 'ExxerAI.Vault',
            'ISentinelService': 'ExxerAI.Sentinel',
            'IConduitMessaging': 'ExxerAI.Conduit',
            'INexusProcessor': 'ExxerAI.Nexus',
            'IChronosScheduler': 'ExxerAI.Chronos',
            'ISignalMonitor': 'ExxerAI.Signal',
            'IHelixGraph': 'ExxerAI.Helix',
            'INebulaExperiment': 'ExxerAI.Nebula',
            'IWisdomGovernance': 'ExxerAI.Wisdom',
        }
        
        self.logger.debug(f"🎯 ExxerAI types built: {len(exxerai_types)} types")
        return exxerai_types
    
    def _build_pattern_rules(self) -> List[Dict]:
        """Build pattern-based resolution rules."""
        rules = [
            {
                'pattern': r'I[A-Z]\w*Service$',
                'namespace': 'ExxerAI.Core.Interfaces',
                'description': 'Interface service pattern'
            },
            {
                'pattern': r'I[A-Z]\w*Repository$',
                'namespace': 'ExxerAI.Core.Interfaces',
                'description': 'Interface repository pattern'
            },
            {
                'pattern': r'\w+Tests?$',
                'namespace': 'Xunit',
                'description': 'Test class pattern'
            },
            {
                'pattern': r'\w+Exception$',
                'namespace': 'System',
                'description': 'Exception class pattern'
            },
            {
                'pattern': r'\w+Attribute$',
                'namespace': 'System',
                'description': 'Attribute class pattern'
            },
            {
                'pattern': r'Http\w+',
                'namespace': 'System.Net.Http',
                'description': 'HTTP-related types'
            },
            {
                'pattern': r'Json\w+',
                'namespace': 'System.Text.Json',
                'description': 'JSON-related types'
            },
        ]
        
        self.logger.debug(f"🔍 Pattern rules built: {len(rules)} rules")
        return rules
    
    def _load_type_dictionary(self) -> Dict:
        """Load the JSON type dictionary with detailed logging."""
        dict_path = self.base_path / "scripts" / "exxerai_types.json"
        
        if dict_path.exists():
            try:
                with open(dict_path, 'r', encoding='utf-8') as f:
                    data = json.load(f)
                    self.logger.info(f"📚 Type dictionary loaded from: {dict_path}")
                    self.logger.debug(f"📚 Dictionary structure: {list(data.keys())}")
                    return data
            except Exception as e:
                self.logger.error(f"❌ Error loading type dictionary: {e}")
                return {'type_lookup': {}}
        else:
            self.logger.warning(f"⚠️ Type dictionary not found at {dict_path}")
            self.logger.info("💡 Creating empty type dictionary structure")
            return {'type_lookup': {}}
    
    def _load_directory_build_props(self) -> Set[str]:
        """Load injected namespaces from Directory.Build.props with detailed logging."""
        injected = set()
        
        # Check both test and production Directory.Build.props
        props_files = [
            self.tests_path / "Directory.Build.props",
            self.src_path / "Directory.Build.props",
            self.base_path / "Directory.Build.props"
        ]
        
        for props_file in props_files:
            if props_file.exists():
                self.logger.debug(f"🏗️ Analyzing Directory.Build.props: {props_file}")
                try:
                    tree = ET.parse(props_file)
                    root = tree.getroot()
                    
                    # Look for GlobalUsings or Using elements
                    for using_elem in root.findall(".//Using"):
                        include = using_elem.get("Include")
                        if include:
                            injected.add(include)
                            self.logger.debug(f"🏗️ Found injected namespace: {include}")
                    
                    # Look for GlobalUsings elements
                    for global_elem in root.findall(".//GlobalUsing"):
                        include = global_elem.get("Include")
                        if include:
                            injected.add(include)
                            self.logger.debug(f"🏗️ Found global using: {include}")
                    
                except ET.ParseError as e:
                    self.logger.error(f"❌ Error parsing {props_file}: {e}")
                except Exception as e:
                    self.logger.error(f"❌ Unexpected error with {props_file}: {e}")
            else:
                self.logger.debug(f"🏗️ Directory.Build.props not found: {props_file}")
        
        self.logger.info(f"🏗️ Total injected namespaces found: {len(injected)}")
        for ns in sorted(injected):
            self.logger.debug(f"🏗️ Injected: {ns}")
        
        return injected
    
    def resolve_type_namespace(self, type_name: str, context: Dict = None) -> Tuple[Optional[str], str]:
        """
        Resolve a type to its namespace with detailed debugging.
        Returns (namespace, resolution_source)
        """
        if not type_name or not type_name.strip():
            self.logger.debug(f"🔍 Empty type name provided")
            return None, "empty_type"
        
        type_name = type_name.strip()
        self.logger.debug(f"🔍 Resolving type: '{type_name}'")
        
        # Track resolution attempt
        attempt = {
            'type': type_name,
            'timestamp': datetime.now(),
            'context': context or {},
            'resolution_path': []
        }
        
        # 1. Check CLR types first (highest priority)
        if type_name in self.clr_type_namespaces:
            namespace = self.clr_type_namespaces[type_name]
            self.logger.debug(f"✅ CLR match: {type_name} -> {namespace}")
            self.debug.stats['clr_matches'] += 1
            self.debug.stats['resolution_sources']['clr'] += 1
            attempt['resolution_path'].append(f"clr:{namespace}")
            attempt['resolved'] = True
            attempt['namespace'] = namespace
            self.resolution_attempts.append(attempt)
            return namespace, "clr"
        
        # 2. Check package types
        if type_name in self.package_type_namespaces:
            namespace = self.package_type_namespaces[type_name]
            self.logger.debug(f"✅ Package match: {type_name} -> {namespace}")
            self.debug.stats['package_matches'] += 1
            self.debug.stats['resolution_sources']['package'] += 1
            attempt['resolution_path'].append(f"package:{namespace}")
            attempt['resolved'] = True
            attempt['namespace'] = namespace
            self.resolution_attempts.append(attempt)
            return namespace, "package"
        
        # 3. Check ExxerAI internal types
        if type_name in self.exxerai_type_namespaces:
            namespace = self.exxerai_type_namespaces[type_name]
            self.logger.debug(f"✅ ExxerAI match: {type_name} -> {namespace}")
            self.debug.stats['resolution_sources']['exxerai'] += 1
            attempt['resolution_path'].append(f"exxerai:{namespace}")
            attempt['resolved'] = True
            attempt['namespace'] = namespace
            self.resolution_attempts.append(attempt)
            return namespace, "exxerai"
        
        # 4. Check type dictionary
        type_lookup = self.type_dictionary.get('type_lookup', {})
        if type_name in type_lookup:
            namespace = type_lookup[type_name]
            self.logger.debug(f"✅ Dictionary match: {type_name} -> {namespace}")
            self.debug.stats['dictionary_matches'] += 1
            self.debug.stats['resolution_sources']['dictionary'] += 1
            attempt['resolution_path'].append(f"dictionary:{namespace}")
            attempt['resolved'] = True
            attempt['namespace'] = namespace
            self.resolution_attempts.append(attempt)
            return namespace, "dictionary"
        
        # 5. Try pattern matching
        for rule in self.pattern_rules:
            if re.match(rule['pattern'], type_name):
                namespace = rule['namespace']
                self.logger.debug(f"✅ Pattern match: {type_name} -> {namespace} (rule: {rule['description']})")
                self.debug.stats['resolution_sources']['pattern'] += 1
                attempt['resolution_path'].append(f"pattern:{namespace}:{rule['description']}")
                attempt['resolved'] = True
                attempt['namespace'] = namespace
                self.resolution_attempts.append(attempt)
                return namespace, "pattern"
        
        # 6. Advanced heuristics
        namespace = self._try_heuristic_resolution(type_name, attempt)
        if namespace:
            self.logger.debug(f"✅ Heuristic match: {type_name} -> {namespace}")
            self.debug.stats['resolution_sources']['heuristic'] += 1
            attempt['resolved'] = True
            attempt['namespace'] = namespace
            self.resolution_attempts.append(attempt)
            return namespace, "heuristic"
        
        # 7. Resolution failed
        self.logger.warning(f"❌ MISS: Could not resolve type '{type_name}'")
        self.debug.stats['missed_types'] += 1
        
        # Categorize the miss
        miss_category = self._categorize_miss(type_name)
        self.debug.stats['miss_categories'][miss_category] += 1
        
        miss_info = {
            'type': type_name,
            'category': miss_category,
            'context': context or {},
            'timestamp': datetime.now(),
            'resolution_attempts': attempt['resolution_path']
        }
        self.missed_types.append(miss_info)
        
        attempt['resolved'] = False
        attempt['miss_category'] = miss_category
        self.resolution_attempts.append(attempt)
        
        return None, "miss"
    
    def _try_heuristic_resolution(self, type_name: str, attempt: Dict) -> Optional[str]:
        """Try various heuristics to resolve the type."""
        
        # Heuristic 1: Check for common suffixes
        if type_name.endswith('Extensions'):
            attempt['resolution_path'].append("heuristic:extensions")
            return 'Microsoft.Extensions.DependencyInjection'
        
        # Heuristic 2: Check for async patterns
        if type_name.endswith('Async') or 'Task' in type_name:
            attempt['resolution_path'].append("heuristic:async")
            return 'System.Threading.Tasks'
        
        # Heuristic 3: Check for collection patterns
        if any(pattern in type_name.lower() for pattern in ['collection', 'list', 'array', 'set']):
            attempt['resolution_path'].append("heuristic:collection")
            return 'System.Collections.Generic'
        
        # Heuristic 4: Check for web patterns
        if any(pattern in type_name.lower() for pattern in ['controller', 'action', 'route', 'mvc']):
            attempt['resolution_path'].append("heuristic:web")
            return 'Microsoft.AspNetCore.Mvc'
        
        # Heuristic 5: Check for data patterns
        if any(pattern in type_name.lower() for pattern in ['entity', 'model', 'dto']):
            attempt['resolution_path'].append("heuristic:data")
            return 'System.ComponentModel.DataAnnotations'
        
        # Heuristic 6: Check for validation patterns
        if any(pattern in type_name.lower() for pattern in ['valid', 'required', 'range']):
            attempt['resolution_path'].append("heuristic:validation")
            return 'System.ComponentModel.DataAnnotations'
        
        return None
    
    def _categorize_miss(self, type_name: str) -> str:
        """Categorize why a type resolution failed."""
        
        # Check for common patterns that might indicate the category
        if type_name.startswith('I') and type_name[1].isupper():
            return "interface"
        
        if type_name.endswith('Exception'):
            return "exception"
        
        if type_name.endswith('Attribute'):
            return "attribute"
        
        if type_name.endswith('Tests') or type_name.endswith('Test'):
            return "test_class"
        
        if type_name.endswith('Service'):
            return "service"
        
        if type_name.endswith('Repository'):
            return "repository"
        
        if type_name.endswith('Controller'):
            return "controller"
        
        if type_name.endswith('Model') or type_name.endswith('DTO'):
            return "data_model"
        
        if type_name.endswith('Extensions'):
            return "extensions"
        
        if any(pattern in type_name.lower() for pattern in ['async', 'task']):
            return "async_related"
        
        if type_name.lower().startswith('exxerai'):
            return "exxerai_internal"
        
        if any(char in type_name for char in ['<', '>', '[]', '?']):
            return "generic_or_complex"
        
        if type_name.isupper() or '_' in type_name:
            return "constant_or_enum"
        
        if len(type_name) <= 3:
            return "short_name"
        
        return "unknown"
    
    def analyze_cs0246_errors(self, errors_file: str) -> Dict:
        """
        Analyze CS0246 errors with comprehensive debugging.
        """
        self.logger.info(f"🔍 Starting CS0246 analysis from: {errors_file}")
        
        if not Path(errors_file).exists():
            self.logger.error(f"❌ Error file not found: {errors_file}")
            return {'error': f'Error file not found: {errors_file}'}
        
        # Read errors
        with open(errors_file, 'r', encoding='utf-8') as f:
            error_lines = f.readlines()
        
        self.logger.info(f"📄 Found {len(error_lines)} error lines to analyze")
        self.debug.stats['total_errors'] = len(error_lines)
        
        # Parse errors and extract types
        missing_types = set()
        error_details = []
        
        for i, line in enumerate(error_lines):
            line = line.strip()
            if not line:
                continue
            
            self.logger.debug(f"📄 Processing error line {i+1}: {line[:100]}...")
            
            # Extract type name from CS0246 error
            # Pattern: "error CS0246: The type or namespace name 'TypeName' could not be found"
            match = re.search(r"error CS0246.*?'([^']+)'.*?could not be found", line, re.IGNORECASE)
            if match:
                type_name = match.group(1)
                missing_types.add(type_name)
                
                # Extract file and line info if available
                file_match = re.search(r'^([^(]+)\((\d+),(\d+)\)', line)
                file_info = {}
                if file_match:
                    file_info = {
                        'file': file_match.group(1),
                        'line': int(file_match.group(2)),
                        'column': int(file_match.group(3))
                    }
                
                error_details.append({
                    'type': type_name,
                    'error_line': line,
                    'file_info': file_info,
                    'line_number': i + 1
                })
                
                self.logger.debug(f"🎯 Extracted type: '{type_name}' from line {i+1}")
            else:
                self.logger.debug(f"⚠️ Could not extract type from line {i+1}: {line}")
        
        self.logger.info(f"🎯 Extracted {len(missing_types)} unique missing types")
        
        # Resolve each type
        resolution_results = {}
        resolved_count = 0
        
        for type_name in sorted(missing_types):
            self.logger.debug(f"🔍 Attempting to resolve: {type_name}")
            namespace, source = self.resolve_type_namespace(type_name)
            
            if namespace:
                resolved_count += 1
                self.debug.stats['resolved_types'] += 1
                self.logger.info(f"✅ Resolved {type_name} -> {namespace} (source: {source})")
            else:
                self.logger.warning(f"❌ Failed to resolve: {type_name}")
            
            resolution_results[type_name] = {
                'namespace': namespace,
                'source': source,
                'resolved': namespace is not None
            }
        
        # Generate comprehensive analysis report
        analysis_report = {
            'metadata': {
                'generated_on': datetime.now().isoformat(),
                'analyzer_version': '3.0.0_debug',
                'errors_file': errors_file,
                'total_error_lines': len(error_lines),
                'unique_missing_types': len(missing_types),
                'resolved_types': resolved_count,
                'missed_types': len(missing_types) - resolved_count,
                'resolution_rate': (resolved_count / len(missing_types)) * 100 if missing_types else 0
            },
            'statistics': self.debug.stats,
            'missing_types': list(missing_types),
            'resolution_results': resolution_results,
            'error_details': error_details,
            'missed_types_analysis': self.missed_types,
            'resolution_attempts': self.resolution_attempts,
            'recommendations': self._generate_recommendations()
        }
        
        # Log summary
        self.logger.info(f"📊 ANALYSIS COMPLETE:")
        self.logger.info(f"📊 Total types: {len(missing_types)}")
        self.logger.info(f"📊 Resolved: {resolved_count} ({(resolved_count/len(missing_types)*100):.1f}%)")
        self.logger.info(f"📊 Missed: {len(missing_types) - resolved_count}")
        self.logger.info(f"📊 Resolution sources: {dict(self.debug.stats['resolution_sources'])}")
        self.logger.info(f"📊 Miss categories: {dict(self.debug.stats['miss_categories'])}")
        
        return analysis_report
    
    def _generate_recommendations(self) -> List[Dict]:
        """Generate recommendations for improving resolution rates."""
        recommendations = []
        
        # Analyze miss categories
        miss_cats = self.debug.stats['miss_categories']
        
        if miss_cats.get('exxerai_internal', 0) > 0:
            recommendations.append({
                'category': 'exxerai_internal',
                'priority': 'high',
                'count': miss_cats['exxerai_internal'],
                'recommendation': 'Scan ExxerAI source code to build comprehensive internal type dictionary',
                'action': 'Run code analysis to extract all internal types and their namespaces'
            })
        
        if miss_cats.get('interface', 0) > 0:
            recommendations.append({
                'category': 'interface',
                'priority': 'medium',
                'count': miss_cats['interface'],
                'recommendation': 'Add interface pattern rules and scanning',
                'action': 'Enhance pattern matching for interface types'
            })
        
        if miss_cats.get('generic_or_complex', 0) > 0:
            recommendations.append({
                'category': 'generic_or_complex',
                'priority': 'medium',
                'count': miss_cats['generic_or_complex'],
                'recommendation': 'Improve parsing of generic types and complex declarations',
                'action': 'Enhance regex patterns to handle generic type syntax'
            })
        
        if miss_cats.get('test_class', 0) > 0:
            recommendations.append({
                'category': 'test_class',
                'priority': 'low',
                'count': miss_cats['test_class'],
                'recommendation': 'These are likely test class names, not types to resolve',
                'action': 'Filter out test class declarations from CS0246 errors'
            })
        
        return recommendations

def main():
    import argparse
    
    parser = argparse.ArgumentParser(description='Enhanced Smart Dependency Analyzer v3 - Debug Edition')
    parser.add_argument('--base-path', default='F:/Dynamic/ExxerAi/ExxerAI',
                       help='Base path of the ExxerAI project')
    parser.add_argument('--errors', default='Errors/CS0246.txt',
                       help='Path to CS0246 errors file')
    parser.add_argument('--output', default='enhanced_dependency_analysis_debug.json',
                       help='Output file for analysis report')
    parser.add_argument('--verbose', '-v', action='store_true',
                       help='Enable verbose logging')
    
    args = parser.parse_args()
    
    # Create analyzer
    analyzer = EnhancedSmartDependencyAnalyzerV3(args.base_path, verbose=args.verbose)
    
    # Run analysis
    results = analyzer.analyze_cs0246_errors(args.errors)
    
    # Save results
    with open(args.output, 'w', encoding='utf-8') as f:
        json.dump(results, f, indent=2, ensure_ascii=False, default=str)
    
    print(f"\n📊 Analysis complete! Report saved to: {args.output}")
    print(f"📊 Check logs directory for detailed debug information")
    
    # Print key metrics
    if 'metadata' in results:
        meta = results['metadata']
        print(f"📊 Resolution rate: {meta['resolution_rate']:.1f}%")
        print(f"📊 Resolved: {meta['resolved_types']}/{meta['unique_missing_types']} types")

if __name__ == "__main__":
    main()