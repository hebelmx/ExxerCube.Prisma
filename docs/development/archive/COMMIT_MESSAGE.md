```
refactor(infrastructure): move metrics services to Infrastructure.Metrics project

Move ProcessingMetricsService and ProcessingContext from Application layer
to Infrastructure layer to comply with Hexagonal Architecture principles.

BREAKING CHANGE: None - all public APIs use interfaces

Changes:
- Create Infrastructure.Metrics project following Infrastructure.Classification pattern
- Move ProcessingMetricsService and ProcessingContext to Infrastructure.Metrics
- Update ProcessingContext to use IProcessingMetricsService interface
- Create AddMetricsServices DI extension method
- Update all references to use interfaces (MetricsController, UI components, HealthCheckService)
- Create Tests.Infrastructure.Metrics project with comprehensive unit tests
- Update solution file with new projects

Architectural Compliance:
- Domain interfaces now only implemented in Infrastructure layer ✅
- Application layer uses interfaces only ✅
- Architecture tests passing ✅

Files Created:
- Infrastructure.Metrics/ExxerCube.Prisma.Infrastructure.Metrics.csproj
- Infrastructure.Metrics/ProcessingMetricsService.cs
- Infrastructure.Metrics/ProcessingContext.cs
- Infrastructure.Metrics/DependencyInjection/ServiceCollectionExtensions.cs
- Tests.Infrastructure.Metrics/ExxerCube.Prisma.Tests.Infrastructure.Metrics.csproj
- Tests.Infrastructure.Metrics/ProcessingMetricsServiceTests.cs
- Tests.Infrastructure.Metrics/ProcessingContextTests.cs

Files Modified:
- Infrastructure/DependencyInjection/ServiceCollectionExtensions.cs
- UI/Controllers/MetricsController.cs
- UI/Components/Pages/Dashboard.razor
- UI/Components/Pages/OCRDemo.razor
- Application/Services/HealthCheckService.cs
- UI/ExxerCube.Prisma.Web.UI.csproj
- Infrastructure/ExxerCube.Prisma.Infrastructure.csproj
- ExxerCube.Prisma.sln

Files Deleted:
- Application/Services/ProcessingMetricsService.cs
- Application/Services/ProcessingContext.cs

Related: ADR-003
```

