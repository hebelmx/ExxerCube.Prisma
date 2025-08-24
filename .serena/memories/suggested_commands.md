# Suggested Commands for ExxerCube.Prisma Development

## Build and Test Commands
```bash
# Navigate to project directory
cd Prisma/Code/Src/CSharp

# Restore dependencies
dotnet restore

# Build solution
dotnet build

# Run all tests
dotnet test

# Run tests with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run specific test project
dotnet test Tests/ExxerCube.Prisma.Tests.csproj

# Run tests with specific filter
dotnet test --filter "Category=Integration"

# Build in Release mode
dotnet build --configuration Release

# Clean solution
dotnet clean
```

## Development Commands
```bash
# Run the web application
dotnet run --project UI/ExxerCube.Prisma.Web.UI

# Run with specific environment
dotnet run --project UI/ExxerCube.Prisma.Web.UI --environment Development

# Watch for changes (development)
dotnet watch --project UI/ExxerCube.Prisma.Web.UI

# Add new package
dotnet add package PackageName

# Remove package
dotnet remove package PackageName

# Update packages
dotnet list package --outdated
dotnet add package PackageName --version NewVersion
```

## Testing Commands
```bash
# Run tests with verbose output
dotnet test --verbosity normal

# Run tests with specific logger
dotnet test --logger "console;verbosity=detailed"

# Run tests with parallel execution disabled
dotnet test --maxcpucount:1

# Run tests with specific framework
dotnet test --framework net10.0

# Generate test results in different formats
dotnet test --logger trx --results-directory TestResults
```

## Code Quality Commands
```bash
# Check for code style issues
dotnet format --verify-no-changes

# Format code
dotnet format

# Analyze code
dotnet build --verbosity normal

# Check for security vulnerabilities
dotnet list package --vulnerable
```

## Python Integration Commands
```bash
# Install Python dependencies
cd Python
pip install -r requirements.txt

# Run Python tests
python -m pytest ocr_modules/tests/

# Test Python CLI
python modular_ocr_cli.py --input test_document.png --outdir output --verbose

# Check Python module availability
python -c "import ocr_modules; print('Modules available')"
```

## Database Commands
```bash
# Add new migration
dotnet ef migrations add MigrationName --project UI/ExxerCube.Prisma.Web.UI

# Update database
dotnet ef database update --project UI/ExxerCube.Prisma.Web.UI

# Remove last migration
dotnet ef migrations remove --project UI/ExxerCube.Prisma.Web.UI

# Generate SQL script
dotnet ef migrations script --project UI/ExxerCube.Prisma.Web.UI
```

## Utility Commands
```bash
# List all projects
dotnet sln list

# Add project to solution
dotnet sln add path/to/project.csproj

# Remove project from solution
dotnet sln remove path/to/project.csproj

# Show project dependencies
dotnet list reference

# Show package references
dotnet list package

# Clean all build artifacts
dotnet clean --verbosity normal
```

## Git Commands
```bash
# Check status
git status

# Add all changes
git add .

# Commit changes
git commit -m "Description of changes"

# Push changes
git push

# Pull latest changes
git pull

# Create new branch
git checkout -b feature/description

# Switch to branch
git checkout branch-name

# Merge branch
git merge branch-name
```

## Windows-Specific Commands
```bash
# Open in Visual Studio
start ExxerCube.Prisma.sln

# Open in VS Code
code .

# Open PowerShell
pwsh

# Check .NET version
dotnet --version

# List installed .NET versions
dotnet --list-sdks
dotnet --list-runtimes
```

## Performance and Monitoring
```bash
# Profile application
dotnet trace collect --name ExxerCube.Prisma

# Monitor memory usage
dotnet counters monitor

# Check for memory leaks
dotnet-gcdump collect

# Analyze performance
dotnet trace analyze trace.nettrace
```