# IMPLEMENTATION PLAN: Events Infrastructure + Realistic Gaps
**Date**: 2025-11-29
**Approach**: Observable Pattern + SignalR for Real-Time Event System
**Estimated Effort**: 1-2 days (8-16 hours)

---

## OVERVIEW

**Goals**:
1. Fix realistic classification gaps (enum naming, confidence, precedence)
2. Add Observable event infrastructure throughout processing pipeline
3. Background workers subscribe and persist all events to database
4. SignalR broadcasts events to UI for real-time monitoring

**Architecture**:
```
Processing Pipeline → Publishes Events (IObservable<T>)
                                ↓
                    ┌───────────┴───────────┐
                    ↓                       ↓
        Background Worker              SignalR Hub
        (Subscribes & Persists)        (Broadcasts to UI)
                    ↓                       ↓
              Database                 Real-Time Dashboard
```

---

## PART 1: EVENT INFRASTRUCTURE (4-6 hours)

### Step 1: Define Domain Events (1 hour)

**File**: `Domain/Events/DomainEvent.cs`
```csharp
namespace ExxerCube.Prisma.Domain.Events;

/// <summary>
/// Base class for all domain events in the system.
/// Events are published via IObservable and consumed by background workers + SignalR.
/// </summary>
public abstract record DomainEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public string EventType { get; init; } = string.Empty;
    public Guid? CorrelationId { get; init; } // Links related events (e.g., all events for one document)
}

/// <summary>
/// Document downloaded from SIARA/email.
/// </summary>
public record DocumentDownloadedEvent : DomainEvent
{
    public Guid FileId { get; init; }
    public string FileName { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty; // SIARA, Email, Manual
    public long FileSizeBytes { get; init; }
    public FileFormat Format { get; init; }
    public string DownloadUrl { get; init; } = string.Empty;

    public DocumentDownloadedEvent()
    {
        EventType = nameof(DocumentDownloadedEvent);
    }
}

/// <summary>
/// Image quality analysis completed.
/// </summary>
public record QualityAnalysisCompletedEvent : DomainEvent
{
    public Guid FileId { get; init; }
    public ImageQualityLevel QualityLevel { get; init; }
    public decimal BlurScore { get; init; }
    public decimal NoiseScore { get; init; }
    public decimal ContrastScore { get; init; }
    public decimal SharpnessScore { get; init; }

    public QualityAnalysisCompletedEvent()
    {
        EventType = nameof(QualityAnalysisCompletedEvent);
    }
}

/// <summary>
/// OCR processing completed.
/// </summary>
public record OcrCompletedEvent : DomainEvent
{
    public Guid FileId { get; init; }
    public string OcrEngine { get; init; } = string.Empty; // Tesseract, GOT-OCR2
    public decimal Confidence { get; init; }
    public int ExtractedTextLength { get; init; }
    public TimeSpan ProcessingTime { get; init; }
    public bool FallbackTriggered { get; init; }

    public OcrCompletedEvent()
    {
        EventType = nameof(OcrCompletedEvent);
    }
}

/// <summary>
/// Classification completed with confidence and warnings.
/// </summary>
public record ClassificationCompletedEvent : DomainEvent
{
    public Guid FileId { get; init; }
    public RequirementType ClassifiedType { get; init; }
    public int Confidence { get; init; }
    public List<string> Warnings { get; init; } = new();
    public bool RequiresManualReview { get; init; }
    public DocumentRelationType RelationType { get; init; }

    public ClassificationCompletedEvent()
    {
        EventType = nameof(ClassificationCompletedEvent);
    }
}

/// <summary>
/// Conflict detected between XML and OCR data.
/// </summary>
public record ConflictDetectedEvent : DomainEvent
{
    public Guid FileId { get; init; }
    public string FieldName { get; init; } = string.Empty;
    public string XmlValue { get; init; } = string.Empty;
    public string OcrValue { get; init; } = string.Empty;
    public decimal SimilarityScore { get; init; }
    public string ConflictSeverity { get; init; } = string.Empty; // Low, Medium, High

    public ConflictDetectedEvent()
    {
        EventType = nameof(ConflictDetectedEvent);
    }
}

/// <summary>
/// Document flagged for manual review.
/// </summary>
public record DocumentFlaggedForReviewEvent : DomainEvent
{
    public Guid FileId { get; init; }
    public List<string> Reasons { get; init; } = new();
    public string Priority { get; init; } = string.Empty; // Low, Normal, High, Urgent

    public DocumentFlaggedForReviewEvent()
    {
        EventType = nameof(DocumentFlaggedForReviewEvent);
    }
}

/// <summary>
/// Document processing completed successfully.
/// </summary>
public record DocumentProcessingCompletedEvent : DomainEvent
{
    public Guid FileId { get; init; }
    public TimeSpan TotalProcessingTime { get; init; }
    public int AutoProcessed { get; init; } // 1 if auto-processed, 0 if flagged for review

    public DocumentProcessingCompletedEvent()
    {
        EventType = nameof(DocumentProcessingCompletedEvent);
    }
}

/// <summary>
/// Processing error occurred.
/// </summary>
public record ProcessingErrorEvent : DomainEvent
{
    public Guid? FileId { get; init; }
    public string ErrorMessage { get; init; } = string.Empty;
    public string StackTrace { get; init; } = string.Empty;
    public string Component { get; init; } = string.Empty; // OCR, Classification, Storage, etc.

    public ProcessingErrorEvent()
    {
        EventType = nameof(ProcessingErrorEvent);
    }
}
```

---

### Step 2: Event Publisher Service (1 hour)

**File**: `Application/Services/EventPublisher.cs`
```csharp
using System.Reactive.Subjects;
using System.Reactive.Linq;
using ExxerCube.Prisma.Domain.Events;

namespace ExxerCube.Prisma.Application.Services;

/// <summary>
/// Central event publisher using Reactive Extensions (Rx.NET).
/// All domain events flow through this service as IObservable streams.
/// </summary>
public interface IEventPublisher
{
    /// <summary>
    /// Publishes a domain event to all subscribers.
    /// </summary>
    void Publish<TEvent>(TEvent domainEvent) where TEvent : DomainEvent;

    /// <summary>
    /// Subscribe to all events of a specific type.
    /// </summary>
    IObservable<TEvent> GetEventStream<TEvent>() where TEvent : DomainEvent;

    /// <summary>
    /// Subscribe to all events (any type).
    /// </summary>
    IObservable<DomainEvent> GetAllEventsStream();
}

public class EventPublisher : IEventPublisher, IDisposable
{
    private readonly Subject<DomainEvent> _eventStream = new();
    private readonly ILogger<EventPublisher> _logger;

    public EventPublisher(ILogger<EventPublisher> logger)
    {
        _logger = logger;
    }

    public void Publish<TEvent>(TEvent domainEvent) where TEvent : DomainEvent
    {
        try
        {
            _logger.LogDebug(
                "Publishing event {EventType} with ID {EventId} (Correlation: {CorrelationId})",
                domainEvent.EventType,
                domainEvent.EventId,
                domainEvent.CorrelationId);

            _eventStream.OnNext(domainEvent);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error publishing event {EventType}", domainEvent.EventType);
            // Don't throw - event publishing should not break main processing flow
        }
    }

    public IObservable<TEvent> GetEventStream<TEvent>() where TEvent : DomainEvent
    {
        return _eventStream
            .OfType<TEvent>()
            .AsObservable();
    }

    public IObservable<DomainEvent> GetAllEventsStream()
    {
        return _eventStream.AsObservable();
    }

    public void Dispose()
    {
        _eventStream?.Dispose();
    }
}
```

**Package**: Add to `Application.csproj`:
```xml
<PackageReference Include="System.Reactive" Version="6.0.0" />
```

---

### Step 3: Background Event Persistence Worker (2 hours)

**File**: `Infrastructure.Workers/EventPersistenceWorker.cs`
```csharp
using System.Reactive.Linq;
using ExxerCube.Prisma.Application.Services;
using ExxerCube.Prisma.Domain.Events;
using ExxerCube.Prisma.Infrastructure.Database;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ExxerCube.Prisma.Infrastructure.Workers;

/// <summary>
/// Background worker that subscribes to all domain events and persists them to database.
/// Uses Reactive Extensions for event stream processing.
/// </summary>
public class EventPersistenceWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<EventPersistenceWorker> _logger;
    private IDisposable? _subscription;

    public EventPersistenceWorker(
        IServiceProvider serviceProvider,
        ILogger<EventPersistenceWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Event Persistence Worker starting...");

        using var scope = _serviceProvider.CreateScope();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        // Subscribe to all events
        _subscription = eventPublisher
            .GetAllEventsStream()
            .Buffer(TimeSpan.FromSeconds(1)) // Batch events every 1 second
            .Where(events => events.Any())
            .Subscribe(
                onNext: async events => await PersistEventsAsync(events, stoppingToken),
                onError: ex => _logger.LogError(ex, "Error in event stream"),
                onCompleted: () => _logger.LogInformation("Event stream completed"));

        _logger.LogInformation("Event Persistence Worker started and subscribed to event stream");

        return Task.CompletedTask;
    }

    private async Task PersistEventsAsync(IList<DomainEvent> events, CancellationToken ct)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<PrismaDbContext>();

            var auditRecords = events.Select(e => new AuditRecord
            {
                Id = Guid.NewGuid(),
                EventId = e.EventId,
                EventType = e.EventType,
                Timestamp = e.Timestamp,
                CorrelationId = e.CorrelationId,
                EventData = System.Text.Json.JsonSerializer.Serialize(e),
                CreatedDate = DateTime.UtcNow
            }).ToList();

            dbContext.AuditRecords.AddRange(auditRecords);
            await dbContext.SaveChangesAsync(ct);

            _logger.LogDebug("Persisted {Count} events to database", events.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error persisting {Count} events", events.Count);
            // Don't throw - allow worker to continue
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Event Persistence Worker stopping...");
        _subscription?.Dispose();
        await base.StopAsync(cancellationToken);
    }
}
```

**Add to Database Context**:
```csharp
public class PrismaDbContext : DbContext
{
    // ... existing DbSets ...

    public DbSet<AuditRecord> AuditRecords { get; set; }
}
```

**Update AuditRecord Entity** (if not already complete):
```csharp
public class AuditRecord
{
    public Guid Id { get; set; }
    public Guid EventId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public Guid? CorrelationId { get; set; }
    public string EventData { get; set; } = string.Empty; // JSON serialized event
    public DateTime CreatedDate { get; set; }
}
```

---

### Step 4: SignalR Hub for Real-Time UI Updates (1 hour)

**File**: `Web.UI/Hubs/ProcessingEventsHub.cs`
```csharp
using Microsoft.AspNetCore.SignalR;
using System.Reactive.Linq;
using ExxerCube.Prisma.Application.Services;
using ExxerCube.Prisma.Domain.Events;

namespace ExxerCube.Prisma.Web.UI.Hubs;

/// <summary>
/// SignalR hub that broadcasts processing events to connected UI clients in real-time.
/// </summary>
public class ProcessingEventsHub : Hub
{
    private readonly ILogger<ProcessingEventsHub> _logger;

    public ProcessingEventsHub(ILogger<ProcessingEventsHub> logger)
    {
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        _logger.LogInformation("Client connected: {ConnectionId}", Context.ConnectionId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        _logger.LogInformation("Client disconnected: {ConnectionId}", Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}

/// <summary>
/// Hosted service that subscribes to domain events and broadcasts them via SignalR.
/// </summary>
public class SignalREventBroadcaster : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHubContext<ProcessingEventsHub> _hubContext;
    private readonly ILogger<SignalREventBroadcaster> _logger;
    private IDisposable? _subscription;

    public SignalREventBroadcaster(
        IServiceProvider serviceProvider,
        IHubContext<ProcessingEventsHub> hubContext,
        ILogger<SignalREventBroadcaster> logger)
    {
        _serviceProvider = serviceProvider;
        _hubContext = hubContext;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SignalR Event Broadcaster starting...");

        using var scope = _serviceProvider.CreateScope();
        var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

        // Subscribe to all events and broadcast to UI
        _subscription = eventPublisher
            .GetAllEventsStream()
            .Subscribe(
                onNext: async e => await BroadcastEventAsync(e),
                onError: ex => _logger.LogError(ex, "Error in SignalR event stream"));

        _logger.LogInformation("SignalR Event Broadcaster started");
        return Task.CompletedTask;
    }

    private async Task BroadcastEventAsync(DomainEvent domainEvent)
    {
        try
        {
            // Broadcast to all connected clients
            await _hubContext.Clients.All.SendAsync(
                "ReceiveEvent",
                domainEvent.EventType,
                domainEvent);

            _logger.LogDebug("Broadcasted {EventType} to all clients", domainEvent.EventType);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error broadcasting event {EventType}", domainEvent.EventType);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("SignalR Event Broadcaster stopping...");
        _subscription?.Dispose();
        return Task.CompletedTask;
    }
}
```

**Register in `Program.cs`**:
```csharp
// Add SignalR
builder.Services.AddSignalR();

// Register event publisher as singleton
builder.Services.AddSingleton<IEventPublisher, EventPublisher>();

// Register background workers
builder.Services.AddHostedService<EventPersistenceWorker>();
builder.Services.AddHostedService<SignalREventBroadcaster>();

// Map SignalR hub
app.MapHub<ProcessingEventsHub>("/hubs/processing-events");
```

---

### Step 5: Integrate Events into Processing Pipeline (30 min - 1 hour per component)

**Example: OCR Service**
```csharp
public class TesseractOcrExecutor : IOcrExecutor
{
    private readonly IEventPublisher _eventPublisher;

    public async Task<Result<OcrResult>> ExtractTextAsync(
        Stream imageStream,
        Guid correlationId,
        CancellationToken ct)
    {
        var fileId = Guid.NewGuid();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // ... existing OCR logic ...

            // Publish event
            _eventPublisher.Publish(new OcrCompletedEvent
            {
                FileId = fileId,
                CorrelationId = correlationId,
                OcrEngine = "Tesseract",
                Confidence = result.Confidence,
                ExtractedTextLength = result.Text.Length,
                ProcessingTime = stopwatch.Elapsed,
                FallbackTriggered = false
            });

            return Result.Success(result);
        }
        catch (Exception ex)
        {
            _eventPublisher.Publish(new ProcessingErrorEvent
            {
                FileId = fileId,
                CorrelationId = correlationId,
                ErrorMessage = ex.Message,
                StackTrace = ex.StackTrace ?? string.Empty,
                Component = "Tesseract OCR"
            });

            throw;
        }
    }
}
```

**Example: Classification Service**
```csharp
public class LegalDirectiveClassifierService
{
    private readonly IEventPublisher _eventPublisher;

    public async Task<Result<ClassificationResult>> ClassifyAsync(
        string documentText,
        Guid fileId,
        Guid correlationId,
        CancellationToken ct)
    {
        var result = new ClassificationResult();

        // ... existing classification logic ...

        // Add warnings based on missing fields
        if (result.Type == RequirementType.TransferenciaElectronica && !ContainsCLABE(documentText))
        {
            result.Warnings.Add("Missing CLABE - Transfer requires 18-digit account");
            result.RequiresManualReview = true;
        }

        // Publish event
        _eventPublisher.Publish(new ClassificationCompletedEvent
        {
            FileId = fileId,
            CorrelationId = correlationId,
            ClassifiedType = result.Type,
            Confidence = result.Confidence,
            Warnings = result.Warnings,
            RequiresManualReview = result.RequiresManualReview,
            RelationType = result.RelationType
        });

        return Result.Success(result);
    }
}
```

---

## PART 2: CLASSIFICATION IMPROVEMENTS (2-4 hours)

### Step 1: Fix RequirementType Enum (30 min)

**File**: `Domain/Enum/RequirementType.cs`

**Change**:
```csharp
// OLD (Wrong):
public static readonly RequirementType Judicial = new(100, "Judicial", "Solicitud de Información");

// NEW (Correct):
public static readonly RequirementType InformationRequest = new(100, "InformationRequest", "Solicitud de Información");
```

**Update all references** (search and replace):
- `RequirementType.Judicial` → `RequirementType.InformationRequest`
- Files to check: All classification services, tests, database seed data

---

### Step 2: Add DocumentRelationType Enum (30 min)

**File**: `Domain/Enum/DocumentRelationType.cs`
```csharp
namespace ExxerCube.Prisma.Domain.Enum;

/// <summary>
/// Relationship type for follow-up documents per Article 5.
/// </summary>
public enum DocumentRelationType
{
    /// <summary>
    /// New requirement (standard case).
    /// </summary>
    NewRequirement = 0,

    /// <summary>
    /// Recordatorio - Reminder of previous request.
    /// Does NOT create new R29 record, updates FechaSolicitud to reminder date.
    /// </summary>
    Recordatorio = 1,

    /// <summary>
    /// Alcance - Scope expansion (adds accounts, extends date range, adds subjects).
    /// Creates NEW R29 record, references original NumeroOficio.
    /// </summary>
    Alcance = 2,

    /// <summary>
    /// Precisión - Clarification of ambiguous prior request.
    /// Updates EXISTING R29 record, keeps original NumeroOficio.
    /// </summary>
    Precision = 3
}
```

---

### Step 3: Enhanced Classification with Confidence & Warnings (1-2 hours)

**File**: `Domain/Models/ClassificationResult.cs`
```csharp
namespace ExxerCube.Prisma.Domain.Models;

public class ClassificationResult
{
    public RequirementType Type { get; set; } = RequirementType.Unknown;
    public int Confidence { get; set; } // 0-100
    public List<string> Warnings { get; set; } = new();
    public bool RequiresManualReview { get; set; }
    public DocumentRelationType RelationType { get; set; } = DocumentRelationType.NewRequirement;

    // Extracted details
    public string? ExtractedCLABE { get; set; }
    public string? PriorOrderReference { get; set; }
    public decimal? ExtractedAmount { get; set; }
    public string? AccountNumber { get; set; }
}
```

**Update**: `LegalDirectiveClassifierService.cs`
```csharp
public async Task<Result<ClassificationResult>> ClassifyWithConfidenceAsync(
    string documentText,
    CancellationToken ct = default)
{
    var result = new ClassificationResult();
    var upperText = documentText.ToUpperInvariant();

    // 1. Detect relation type first
    result.RelationType = DetectRelationType(upperText);

    // 2. Classify requirement type with precedence
    result.Type = ClassifyTypeWithPrecedence(upperText);
    result.Confidence = CalculateConfidence(upperText, result.Type);

    // 3. Validate and extract details based on type
    await ValidateAndExtractDetailsAsync(documentText, result, ct);

    // 4. Determine if manual review needed
    result.RequiresManualReview = DetermineManualReview(result);

    return Result.Success(result);
}

private DocumentRelationType DetectRelationType(string text)
{
    if (text.Contains("RECORDATORIO DEL OFICIO"))
        return DocumentRelationType.Recordatorio;

    if (text.Contains("ALCANCE AL OFICIO") || text.Contains("AMPLÍA"))
        return DocumentRelationType.Alcance;

    if (text.Contains("PRECISIÓN") || text.Contains("ACLARA") || text.Contains("CORRIGE"))
        return DocumentRelationType.Precision;

    return DocumentRelationType.NewRequirement;
}

private RequirementType ClassifyTypeWithPrecedence(string text)
{
    // PRIORITY 1: Desbloqueo takes precedence over Aseguramiento
    if (ContainsUnblockDirective(text))
        return RequirementType.Desbloqueo;

    // PRIORITY 2: Specific operations
    if (ContainsBlockDirective(text))
        return RequirementType.Aseguramiento;

    if (ContainsTransferDirective(text))
        return RequirementType.TransferenciaElectronica;

    if (ContainsCashiersCheckDirective(text))
        return RequirementType.SituacionFondos;

    // PRIORITY 3: Information request (default)
    if (ContainsInformationDirective(text))
        return RequirementType.InformationRequest;

    // PRIORITY 4: Unknown
    return RequirementType.Unknown;
}

private async Task ValidateAndExtractDetailsAsync(
    string text,
    ClassificationResult result,
    CancellationToken ct)
{
    var upperText = text.ToUpperInvariant();

    switch (result.Type)
    {
        case RequirementType.TransferenciaElectronica:
            result.ExtractedCLABE = ExtractCLABE(text);
            if (string.IsNullOrEmpty(result.ExtractedCLABE))
            {
                result.Warnings.Add("Missing CLABE - Electronic transfer requires 18-digit account");
            }
            break;

        case RequirementType.Desbloqueo:
            result.PriorOrderReference = ExtractPriorOrderReference(text);
            if (string.IsNullOrEmpty(result.PriorOrderReference))
            {
                result.Warnings.Add("Missing prior order reference - Unblocking requires original blocking order number");
            }
            break;

        case RequirementType.Aseguramiento:
            result.ExtractedAmount = ExtractAmount(text);
            // Amount is optional - if missing, freeze entire account
            break;
    }

    // Extract common fields
    result.AccountNumber = ExtractAccountNumber(text);
}

private bool DetermineManualReview(ClassificationResult result)
{
    // Manual review if:
    // 1. Any warnings present
    if (result.Warnings.Any())
        return true;

    // 2. Low confidence
    if (result.Confidence < 70)
        return true;

    // 3. Unknown type
    if (result.Type == RequirementType.Unknown)
        return true;

    // 4. Special relation types (need human verification)
    if (result.RelationType != DocumentRelationType.NewRequirement)
        return true;

    return false;
}

private static string? ExtractCLABE(string text)
{
    // CLABE is exactly 18 digits
    var clabePattern = new Regex(@"\b(\d{18})\b");
    var match = clabePattern.Match(text);
    return match.Success ? match.Groups[1].Value : null;
}

private static string? ExtractPriorOrderReference(string text)
{
    // Pattern: "oficio número ABC/123/2023" or "folio ABC/123/2023"
    var patterns = new[]
    {
        new Regex(@"oficio\s+n[uú]mero\s+([A-Z0-9\/\-]+)", RegexOptions.IgnoreCase),
        new Regex(@"folio\s+([A-Z0-9\/\-]+)", RegexOptions.IgnoreCase),
        new Regex(@"aseguramiento\s+([A-Z0-9\/\-]+)", RegexOptions.IgnoreCase)
    };

    foreach (var pattern in patterns)
    {
        var match = pattern.Match(text);
        if (match.Success)
            return match.Groups[1].Value;
    }

    return null;
}

private static decimal? ExtractAmount(string text)
{
    // Use existing amount extraction logic from current implementation
    // ... (already implemented in current code)
    return null; // Placeholder
}

private static string? ExtractAccountNumber(string text)
{
    // Use existing account extraction logic
    // ... (already implemented in current code)
    return null; // Placeholder
}
```

---

## PART 3: UI INTEGRATION (2-3 hours)

### Step 1: Real-Time Event Dashboard Component (2 hours)

**File**: `Web.UI/Components/Dashboard/RealTimeEventFeed.razor`
```razor
@using Microsoft.AspNetCore.SignalR.Client
@using ExxerCube.Prisma.Domain.Events
@implements IAsyncDisposable
@inject NavigationManager Navigation

<MudCard>
    <MudCardHeader>
        <CardHeaderContent>
            <MudText Typo="Typo.h6">Real-Time Processing Events</MudText>
        </CardHeaderContent>
        <CardHeaderActions>
            <MudChip Size="Size.Small" Color="@GetConnectionColor()">
                @GetConnectionStatus()
            </MudChip>
        </CardHeaderActions>
    </MudCardHeader>
    <MudCardContent>
        <MudTimeline TimelineOrientation="TimelineOrientation.Vertical" TimelinePosition="TimelinePosition.Start">
            @foreach (var evt in _recentEvents.Take(10))
            {
                <MudTimelineItem Color="GetEventColor(evt.EventType)" Size="Size.Small">
                    <ItemContent>
                        <MudText Typo="Typo.body2">
                            <strong>@evt.EventType</strong>
                        </MudText>
                        <MudText Typo="Typo.caption">
                            @evt.Timestamp.ToString("HH:mm:ss")
                        </MudText>
                        @if (evt is ClassificationCompletedEvent classEvent)
                        {
                            <MudText Typo="Typo.caption" Color="Color.Secondary">
                                Type: @classEvent.ClassifiedType.Name (@classEvent.Confidence%)
                                @if (classEvent.RequiresManualReview)
                                {
                                    <MudChip Size="Size.Small" Color="Color.Warning">Review</MudChip>
                                }
                            </MudText>
                        }
                    </ItemContent>
                </MudTimelineItem>
            }
        </MudTimeline>
    </MudCardContent>
</MudCard>

@code {
    private HubConnection? _hubConnection;
    private readonly List<DomainEvent> _recentEvents = new();
    private bool _isConnected;

    protected override async Task OnInitializedAsync()
    {
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(Navigation.ToAbsoluteUri("/hubs/processing-events"))
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<string, DomainEvent>("ReceiveEvent", (eventType, evt) =>
        {
            _recentEvents.Insert(0, evt);
            if (_recentEvents.Count > 50)
                _recentEvents.RemoveAt(50);

            InvokeAsync(StateHasChanged);
        });

        await _hubConnection.StartAsync();
        _isConnected = true;
    }

    private Color GetEventColor(string eventType) => eventType switch
    {
        nameof(DocumentDownloadedEvent) => Color.Primary,
        nameof(OcrCompletedEvent) => Color.Info,
        nameof(ClassificationCompletedEvent) => Color.Success,
        nameof(ConflictDetectedEvent) => Color.Warning,
        nameof(ProcessingErrorEvent) => Color.Error,
        _ => Color.Default
    };

    private Color GetConnectionColor() => _isConnected ? Color.Success : Color.Error;
    private string GetConnectionStatus() => _isConnected ? "Connected" : "Disconnected";

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection is not null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
```

---

## REGISTRATION & CONFIGURATION

### Program.cs (Complete Setup)
```csharp
// Event Infrastructure
builder.Services.AddSingleton<IEventPublisher, EventPublisher>();

// Background Workers
builder.Services.AddHostedService<EventPersistenceWorker>();
builder.Services.AddHostedService<SignalREventBroadcaster>();

// SignalR
builder.Services.AddSignalR();

// After app.Build()
app.MapHub<ProcessingEventsHub>("/hubs/processing-events");
```

---

## TESTING PLAN

### Unit Tests
```csharp
[Fact]
public async Task ClassifyWithConfidence_TransferWithoutCLABE_ReturnsWarning()
{
    // Arrange
    var classifier = new LegalDirectiveClassifierService(_eventPublisher, _logger);
    var text = "Solicito transferir $100,000 pesos a cuenta de FGR";

    // Act
    var result = await classifier.ClassifyWithConfidenceAsync(text);

    // Assert
    result.Should().BeSuccess();
    result.Value.Type.Should().Be(RequirementType.TransferenciaElectronica);
    result.Value.Warnings.Should().Contain(w => w.Contains("Missing CLABE"));
    result.Value.RequiresManualReview.Should().BeTrue();
}

[Fact]
public async Task DetectRelationType_Recordatorio_ReturnsRecordatorio()
{
    // Arrange
    var text = "Recordatorio del oficio número FGR/123/2023";

    // Act
    var relationType = classifier.DetectRelationType(text.ToUpperInvariant());

    // Assert
    relationType.Should().Be(DocumentRelationType.Recordatorio);
}
```

---

## MIGRATION

### Database Migration
```bash
cd Infrastructure.Database
dotnet ef migrations add AddEventDataToAuditRecords
dotnet ef database update
```

---

## SUMMARY

**Total Effort**: 1-2 days (8-16 hours)

**Part 1**: Event Infrastructure (4-6 hours)
- Domain events (1 hour)
- Event publisher (1 hour)
- Background worker (2 hours)
- SignalR hub (1 hour)
- Pipeline integration (30 min - 1 hour per component)

**Part 2**: Classification Improvements (2-4 hours)
- Fix enum naming (30 min)
- Add DocumentRelationType (30 min)
- Enhanced classification (1-2 hours)
- Testing (1 hour)

**Part 3**: UI Integration (2-3 hours)
- Real-time event feed component (2 hours)
- Dashboard integration (1 hour)

**Benefits**:
1. ✅ Full traceability - every event persisted to database
2. ✅ Real-time monitoring - UI updates via SignalR
3. ✅ Observable pattern - clean separation of concerns
4. ✅ Better classification - confidence scores, warnings, precedence rules
5. ✅ Semantic correctness - fixed enum naming

**This aligns perfectly with your "Defensive Intelligence" and "Full Traceability" goals!**
