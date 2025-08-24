# Coding Standards for ExxerCube.Prisma

## General Principles
- **TreatWarningsAsErrors**: All code must compile with warnings treated as errors
- **Nullable Reference Types**: Enabled for better null safety
- **Railway Oriented Programming**: Use `Result<T>` pattern for error handling
- **Clean Architecture**: Maintain separation between layers
- **SOLID Principles**: Follow SOLID design principles

## Naming Conventions
- **PascalCase**: Classes, methods, properties, constants
- **camelCase**: Local variables, parameters
- **UPPER_CASE**: Constants, enum values
- **Descriptive Names**: Use descriptive names that explain intent
- **Avoid Abbreviations**: Use full words instead of abbreviations

## Code Style
- **Expression-bodied Members**: Use for simple methods without curly braces
- **Pattern Matching**: Use pattern matching where appropriate
- **Null-coalescing**: Use `??` operator for null handling
- **String Interpolation**: Use `$"..."` for string formatting
- **LINQ**: Use LINQ for data manipulation
- **Async/Await**: Use consistently for asynchronous operations

## XML Documentation
- **All Public APIs**: Must have XML documentation
- **Summary**: Required for all public classes, methods, and properties
- **Parameters**: Document all parameters with `<param name="...">`
- **Returns**: Document return values with `<returns>`
- **Exceptions**: Document exceptions with `<exception>`

## Error Handling
- **Result Pattern**: Use `Result<T>` for error handling
- **No Exceptions**: Avoid throwing exceptions for business logic
- **Validation**: Validate inputs early and return failure results
- **Logging**: Log errors with appropriate levels
- **Circuit Breaker**: Use circuit breaker pattern for external dependencies

## Testing Standards
- **Test Naming**: Use descriptive test names without "Real" prefix
- **Arrange-Act-Assert**: Follow AAA pattern
- **Test Isolation**: Each test should be independent
- **Mock Usage**: Use mocks sparingly, prefer real implementations
- **Coverage**: Aim for 90%+ test coverage
- **Test Logging**: Use `XUnitLogger.CreateLogger(output)` instead of `Console.WriteLine`

## Architecture Patterns
- **Hexagonal Architecture**: Maintain clean boundaries
- **Dependency Injection**: Use constructor injection
- **Interface Segregation**: Keep interfaces focused
- **Single Responsibility**: Each class has one reason to change
- **Open/Closed**: Open for extension, closed for modification

## Performance Guidelines
- **Async Operations**: Use async/await for I/O operations
- **Memory Management**: Dispose resources properly
- **Caching**: Use caching for expensive operations
- **Concurrency**: Use appropriate concurrency controls
- **Resource Pooling**: Pool expensive resources

## Security Guidelines
- **Input Validation**: Validate all inputs
- **Authentication**: Implement proper authentication
- **Authorization**: Check permissions before operations
- **Data Protection**: Protect sensitive data
- **HTTPS**: Use HTTPS in production

## Logging Standards
- **Structured Logging**: Use structured logging with correlation IDs
- **Log Levels**: Use appropriate log levels (Debug, Info, Warning, Error)
- **Sensitive Data**: Never log sensitive information
- **Performance**: Avoid expensive operations in logging
- **Context**: Include relevant context in log messages

## Configuration Management
- **Environment-specific**: Use environment-specific configuration
- **Secrets**: Store secrets securely
- **Validation**: Validate configuration at startup
- **Defaults**: Provide sensible defaults
- **Documentation**: Document configuration options

## Code Organization
- **Namespaces**: Use logical namespace organization
- **File Structure**: One class per file
- **Folder Structure**: Organize by feature or layer
- **Using Statements**: Group and order using statements
- **Regions**: Avoid regions, prefer smaller files

## Documentation Standards
- **README**: Maintain up-to-date README
- **API Documentation**: Document all public APIs
- **Architecture**: Document architecture decisions
- **Deployment**: Document deployment procedures
- **Troubleshooting**: Document common issues and solutions

## Review Guidelines
- **Code Review**: All code must be reviewed
- **Automated Checks**: Use automated code quality checks
- **Performance**: Consider performance implications
- **Security**: Review for security issues
- **Testing**: Ensure adequate test coverage