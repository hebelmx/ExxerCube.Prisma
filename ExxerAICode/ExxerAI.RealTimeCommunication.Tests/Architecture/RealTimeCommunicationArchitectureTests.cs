using NetArchTest.Rules;

namespace ExxerAI.RealTimeCommunication.Tests.Architecture
{
    /// <summary>
    /// Architecture tests to ensure RealTimeCommunication library follows clean architecture principles
    /// and maintains proper dependency relationships and design patterns.
    /// </summary>
    public class RealTimeCommunicationArchitectureTests
    {
        private readonly ILogger<RealTimeCommunicationArchitectureTests> _logger;
        private readonly Assembly _realTimeCommunicationAssembly;

        /// <summary>
        /// Initializes architecture tests with logging and assembly references.
        /// </summary>
        /// <param name="testOutputHelper">xUnit output helper for test diagnostics.</param>
        public RealTimeCommunicationArchitectureTests(ITestOutputHelper testOutputHelper)
        {
            _logger = XUnitLogger.CreateLogger<RealTimeCommunicationArchitectureTests>(testOutputHelper);
            _realTimeCommunicationAssembly = typeof(ExxerAI.RealTimeCommunication.Abstractions.IRealTimeCommunicationPort).Assembly;
        }

        /// <summary>
        /// Verifies that all port interfaces follow proper naming conventions.
        /// </summary>
        [Fact]
        public void All_Port_Interfaces_Should_Follow_Naming_Conventions()
        {
            _logger.LogInformation("Checking port interface naming conventions");
        
            var portInterfaces = Types.InAssembly(_realTimeCommunicationAssembly)
                .That()
                .AreInterfaces()
                .And()
                .ResideInNamespace("ExxerAI.RealTimeCommunication.Abstractions")
                .GetTypes()
                .ToList();

            var invalidNames = new List<string>();
        
            foreach (var portInterface in portInterfaces)
            {
                // Only interfaces that are actual ports should end with "Port"
                // Exclude utility abstractions like IConnection, IConnectionFactory, IConnectionManager, IRetryPolicy
                var isUtilityInterface = portInterface.Name is "IConnection" or "IConnectionFactory" or "IConnectionManager" or "IRetryPolicy";
            
                if (!isUtilityInterface && !portInterface.Name.EndsWith("Port"))
                {
                    invalidNames.Add(portInterface.Name);
                    _logger.LogError("❌ Port interface {InterfaceName} does not end with 'Port'", portInterface.Name);
                }
                else
                {
                    if (isUtilityInterface)
                    {
                        _logger.LogInformation("✅ Utility interface {InterfaceName} excluded from Port naming requirement", portInterface.Name);
                    }
                    else
                    {
                        _logger.LogInformation("✅ Port interface {InterfaceName} follows naming convention", portInterface.Name);
                    }
                }
            }

            invalidNames.ShouldBeEmpty($"Port interfaces must end with 'Port': {string.Join(", ", invalidNames)}");
        }

        /// <summary>
        /// Verifies that adapters depend only on abstractions, not concrete implementations.
        /// </summary>
        [Fact]
        public void Adapters_Should_Only_Depend_On_Abstractions()
        {
            _logger.LogInformation("Checking adapter dependency relationships");
        
            var adapterTypes = Types.InAssembly(_realTimeCommunicationAssembly)
                .That()
                .AreClasses()
                .And()
                .ResideInNamespaceStartingWith("ExxerAI.RealTimeCommunication.Adapters")
                .GetTypes()
                .ToList();

            var violations = new List<string>();
        
            foreach (var adapterType in adapterTypes)
            {
                var dependencies = adapterType.GetConstructors()
                    .SelectMany(c => c.GetParameters())
                    .Select(p => p.ParameterType)
                    .Where(t => t.Assembly == _realTimeCommunicationAssembly)
                    .ToList();

                foreach (var dependency in dependencies)
                {
                    if (!dependency.IsInterface && !dependency.IsAbstract && 
                        !dependency.Namespace?.Contains("Common") == true &&
                        !dependency.Namespace?.Contains("Models") == true)
                    {
                        violations.Add($"{adapterType.Name} depends on concrete type {dependency.Name}");
                        _logger.LogError("❌ {AdapterName} depends on concrete type {DependencyName}", 
                            adapterType.Name, dependency.Name);
                    }
                    else
                    {
                        _logger.LogInformation("✅ {AdapterName} properly depends on abstraction {DependencyName}", 
                            adapterType.Name, dependency.Name);
                    }
                }
            }

            violations.ShouldBeEmpty($"Adapters should only depend on abstractions: {string.Join(", ", violations)}");
        }

        /// <summary>
        /// Verifies that all public methods return Result or Result&lt;T&gt; for proper error handling.
        /// </summary>
        [Fact]
        public void Public_Methods_Should_Return_Result_Pattern()
        {
            _logger.LogInformation("Checking Result pattern usage in public methods");
        
            var portInterfaces = Types.InAssembly(_realTimeCommunicationAssembly)
                .That()
                .AreInterfaces()
                .And()
                .ResideInNamespace("ExxerAI.RealTimeCommunication.Abstractions")
                .GetTypes()
                .ToList();

            var violations = new List<string>();
        
            foreach (var portInterface in portInterfaces)
            {
                var publicMethods = portInterface.GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .Where(m => !m.IsSpecialName) // Exclude property getters/setters
                    .ToList();

                foreach (var method in publicMethods)
                {
                    var returnType = method.ReturnType;
                
                    // Check if return type is Task<Result> or Task<Result<T>>
                    if (returnType.IsGenericType && returnType.GetGenericTypeDefinition() == typeof(Task<>))
                    {
                        var taskReturnType = returnType.GetGenericArguments()[0];
                    
                        if (!IsResultType(taskReturnType))
                        {
                            violations.Add($"{portInterface.Name}.{method.Name} returns {returnType.Name} instead of Task<Result>");
                            _logger.LogError("❌ {InterfaceName}.{MethodName} returns {ReturnType} instead of Task<Result>", 
                                portInterface.Name, method.Name, returnType.Name);
                        }
                        else
                        {
                            _logger.LogInformation("✅ {InterfaceName}.{MethodName} returns proper Result pattern", 
                                portInterface.Name, method.Name);
                        }
                    }
                    else if (!IsResultType(returnType))
                    {
                        violations.Add($"{portInterface.Name}.{method.Name} returns {returnType.Name} instead of Result");
                        _logger.LogError("❌ {InterfaceName}.{MethodName} returns {ReturnType} instead of Result", 
                            portInterface.Name, method.Name, returnType.Name);
                    }
                }
            }

            violations.ShouldBeEmpty($"All public methods should return Result pattern: {string.Join(", ", violations)}");
        }

        /// <summary>
        /// Verifies that hub classes inherit from the correct base class.
        /// </summary>
        [Fact]
        public void Hub_Classes_Should_Inherit_From_BaseHub()
        {
            _logger.LogInformation("Checking hub inheritance hierarchy");
        
            var hubClasses = Types.InAssembly(_realTimeCommunicationAssembly)
                .That()
                .AreClasses()
                .And()
                .HaveNameEndingWith("Hub")
                .And()
                .DoNotHaveNameMatching(".*Base.*")
                .GetTypes()
                .ToList();

            var violations = new List<string>();
        
            foreach (var hubClass in hubClasses)
            {
                var baseType = hubClass.BaseType;
            
                // Check for BaseHub<T> generic pattern - the actual inheritance structure
                if (baseType?.IsGenericType != true || !baseType.Name.StartsWith("BaseHub"))
                {
                    violations.Add($"{hubClass.Name} does not inherit from BaseHub<T> (inherits from {baseType?.Name})");
                    _logger.LogError("❌ {HubName} does not inherit from BaseHub<T> (inherits from {BaseType})", 
                        hubClass.Name, baseType?.Name);
                }
                else
                {
                    _logger.LogInformation("✅ {HubName} correctly inherits from BaseHub<T>", hubClass.Name);
                }
            }

            violations.ShouldBeEmpty($"All hub classes should inherit from BaseHub<T>: {string.Join(", ", violations)}");
        }

        /// <summary>
        /// Verifies that classes follow proper namespace organization.
        /// </summary>
        [Fact]
        public void Classes_Should_Be_In_Correct_Namespaces()
        {
            _logger.LogInformation("Checking namespace organization");
        
            var allTypes = Types.InAssembly(_realTimeCommunicationAssembly)
                .GetTypes()
                .ToList();

            var violations = new List<string>();
        
            foreach (var type in allTypes)
            {
                var typeName = type.Name;
                var typeNamespace = type.Namespace ?? "";
            
                // Check specific namespace rules
                if (typeName.EndsWith("Port") && type.IsInterface)
                {
                    if (!typeNamespace.EndsWith(".Abstractions"))
                    {
                        violations.Add($"Port interface {typeName} should be in Abstractions namespace");
                        _logger.LogError("❌ Port interface {TypeName} is in {Namespace} instead of Abstractions", 
                            typeName, typeNamespace);
                    }
                }
                else if (typeName.EndsWith("Adapter"))
                {
                    if (!typeNamespace.Contains(".Adapters."))
                    {
                        violations.Add($"Adapter {typeName} should be in Adapters namespace");
                        _logger.LogError("❌ Adapter {TypeName} is in {Namespace} instead of Adapters", 
                            typeName, typeNamespace);
                    }
                }
                else if (typeName.EndsWith("Hub"))
                {
                    if (!typeNamespace.Contains(".Adapters.SignalR"))
                    {
                        violations.Add($"Hub {typeName} should be in Adapters.SignalR namespace");
                        _logger.LogError("❌ Hub {TypeName} is in {Namespace} instead of Adapters.SignalR", 
                            typeName, typeNamespace);
                    }
                }
            }

            violations.ShouldBeEmpty($"Classes should be in correct namespaces: {string.Join(", ", violations)}");
        }

        /// <summary>
        /// Verifies that the library has proper visibility controls for testing safety net.
        /// Implementation types are allowed to be public for testability.
        /// </summary>
        [Fact]
        public void Implementation_Types_Are_Accessible_For_Testing()
        {
            _logger.LogInformation("Verifying implementation types are accessible for testing safety net");
        
            var implementationTypes = Types.InAssembly(_realTimeCommunicationAssembly)
                .That()
                .AreClasses()
                .And()
                .ArePublic()
                .GetTypes()
                .Where(t => t.Namespace?.Contains(".Adapters") == true ||
                            t.Namespace?.Contains(".Hubs") == true)
                .ToList();

            var violations = new List<string>();
        
            // Ensure key implementation types are accessible for testing
            var expectedPublicTypes = new[] { "SignalRAdapter", "BaseHub", "SystemHub", "AgentHub", "TaskHub", "DocumentHub", "EconomicHub" };
            var actualPublicTypeNames = implementationTypes.Select(t => t.Name.Replace("`1", "")).ToList(); // Remove generic marker
        
            foreach (var expectedType in expectedPublicTypes)
            {
                if (!actualPublicTypeNames.Any(name => name.StartsWith(expectedType)))
                {
                    violations.Add($"{expectedType} should be public for testing but was not found");
                    _logger.LogError("❌ {TypeName} should be public for testing but was not found", expectedType);
                }
                else
                {
                    _logger.LogInformation("✅ {TypeName} is properly accessible for testing", expectedType);
                }
            }

            violations.ShouldBeEmpty($"Implementation types should be accessible for testing: {string.Join(", ", violations)}");
        }

        /// <summary>
        /// Helper method to check if a type is a Result type.
        /// </summary>
        private static bool IsResultType(Type type)
        {
            if (type.Name == "Result")
            {
                return true;
            }
        
            if (type.IsGenericType && type.Name.StartsWith("Result"))
            {
                return true;
            }
        
            return false;
        }
    }
}