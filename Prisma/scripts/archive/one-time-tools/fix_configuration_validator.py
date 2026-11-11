#!/usr/bin/env python3
"""
Fix CS1061 - Add IConfigurationValidator interface and methods
"""

import os
from datetime import datetime

def create_configuration_validator_interface():
    """Create the IConfigurationValidator interface with required methods"""
    
    interface_path = r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Core\ExxerAI.Domain\Interfaces\IConfigurationValidator.cs'
    
    interface_content = '''using ExxerAI.Application.DTOs;
using ExxerAI.Domain.Common;

namespace ExxerAI.Domain.Interfaces;

/// <summary>
/// Interface for configuration validation services.
/// Part of the romboid testing strategy base classes.
/// </summary>
public interface IConfigurationValidator
{
    /// <summary>
    /// Validates database configuration asynchronously.
    /// </summary>
    /// <param name="configuration">The database configuration to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating validation success or failure.</returns>
    Task<Result<bool>> ValidateDatabaseConfigurationAsync(DatabaseConfiguration configuration, CancellationToken cancellationToken);
    
    /// <summary>
    /// Validates API configuration asynchronously.
    /// </summary>
    /// <param name="configuration">The API configuration to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result indicating validation success or failure.</returns>
    Task<Result<bool>> ValidateApiConfigurationAsync(ApiConfiguration configuration, CancellationToken cancellationToken);
    
    /// <summary>
    /// Binds configuration asynchronously.
    /// </summary>
    /// <typeparam name="T">The type of configuration to bind.</typeparam>
    /// <param name="section">The configuration section name.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing the bound configuration.</returns>
    Task<Result<T>> BindConfigurationAsync<T>(string section, CancellationToken cancellationToken) where T : class, new();
}
'''
    
    # Create directory if it doesn't exist
    os.makedirs(os.path.dirname(interface_path), exist_ok=True)
    
    # Write interface
    with open(interface_path, 'w', encoding='utf-8') as f:
        f.write(interface_content)
    
    print(f"✓ Created IConfigurationValidator interface")
    return True

def create_api_configuration_dto():
    """Create the ApiConfiguration DTO"""
    
    dto_path = r'F:\Dynamic\ExxerAi\ExxerAI\code\src\Core\ExxerAI.Application\DTOs\ApiConfiguration.cs'
    
    dto_content = '''using System.ComponentModel.DataAnnotations;

namespace ExxerAI.Application.DTOs;

/// <summary>
/// Data transfer object for API configuration settings.
/// </summary>
public class ApiConfiguration
{
    /// <summary>
    /// Gets or sets the API base URL.
    /// </summary>
    [Required]
    public string BaseUrl { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the API key.
    /// </summary>
    [Required]
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
    
    /// <summary>
    /// Gets or sets the maximum retry attempts.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;
}
'''
    
    # Create directory if it doesn't exist
    os.makedirs(os.path.dirname(dto_path), exist_ok=True)
    
    # Write DTO
    with open(dto_path, 'w', encoding='utf-8') as f:
        f.write(dto_content)
    
    print(f"✓ Created ApiConfiguration DTO")
    return True

def fix_configuration_test_database_dto():
    """Fix the DatabaseConfiguration reference in test"""
    test_file = r'F:\Dynamic\ExxerAi\ExxerAI\code\src\tests\03UnitTests\ExxerAI.Configuration.Test\Services\ConfigurationServiceInterfaceTests.cs'
    
    if not os.path.exists(test_file):
        print(f"  ⚠ Test file not found: {test_file}")
        return False
    
    try:
        with open(test_file, 'r', encoding='utf-8') as f:
            content = f.read()
        
        # Replace the namespace reference
        content = content.replace(
            'ExxerAI.Configuration.Tests.DatabaseConfiguration',
            'ExxerAI.Application.DTOs.DatabaseConfiguration'
        )
        
        with open(test_file, 'w', encoding='utf-8') as f:
            f.write(content)
        
        print(f"✓ Fixed DatabaseConfiguration reference in test file")
        return True
    except Exception as e:
        print(f"  ✗ Error fixing test file: {e}")
        return False

def main():
    print(f"\n{'=' * 80}")
    print(f"FIXING CONFIGURATION VALIDATOR ISSUES")
    print(f"Started: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")
    
    fixed_count = 0
    
    # Create IConfigurationValidator interface
    if create_configuration_validator_interface():
        fixed_count += 1
    
    # Create ApiConfiguration DTO
    if create_api_configuration_dto():
        fixed_count += 1
    
    # Fix test file reference
    if fix_configuration_test_database_dto():
        fixed_count += 1
    
    print(f"\n{'=' * 80}")
    print(f"SUMMARY: Fixed {fixed_count} configuration issues")
    print(f"Completed: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    print(f"{'=' * 80}\n")

if __name__ == "__main__":
    main()