using System.Collections.Concurrent;
using System.Reactive;
using System.Reactive.Subjects;
using System.Text.Json;
using System.Timers;
using Microsoft.Extensions.Options;
using Siara.Simulator.Configuration;
using Siara.Simulator.Models;
using Timer = System.Timers.Timer;

namespace Siara.Simulator.Services;

/// <summary>
/// Manages the simulation of SIARA case arrivals.
/// This service runs as a singleton for the lifetime of the application.
/// </summary>
public class CaseService : IDisposable
{
    private readonly ILogger<CaseService> _logger;
    private readonly SimulatorSettings _settings;
    private readonly string _documentSourcePath;
    private readonly string _persistenceFilePath;
    private readonly Timer _timer;
    private readonly ConcurrentBag<Case> _activeCases = new();
    private List<string> _availableCaseIds = new();
    private HashSet<string> _servedCaseIds = new();
    private bool _isStarted = false;

    // Observable subjects for reactive notifications
    private readonly Subject<Case> _caseArrivedSubject = new();
    private readonly Subject<Unit> _settingsChangedSubject = new();

    // Configurable simulation parameters
    private double _averageArrivalsPerMinute;

    /// <summary>
    /// Gets or sets the average number of case arrivals per minute.
    /// Valid range: 0.1 to 60 cases per minute.
    /// </summary>
    public double AverageArrivalsPerMinute
    {
        get => _averageArrivalsPerMinute;
        set
        {
            if (value is < 0.1 or > 60)
            {
                _logger.LogWarning("Attempted to set arrival rate to {Rate}, clamping to valid range [0.1, 60]", value);
                _averageArrivalsPerMinute = Math.Clamp(value, 0.1, 60);
            }
            else
            {
                _averageArrivalsPerMinute = value;
            }

            _logger.LogInformation("Arrival rate changed to {Rate} cases/minute", _averageArrivalsPerMinute);
            _settingsChangedSubject.OnNext(Unit.Default);
        }
    }

    /// <summary>
    /// Observable that emits when a new case arrives, providing the new Case object.
    /// </summary>
    public IObservable<Case> CaseArrived => _caseArrivedSubject;

    /// <summary>
    /// Observable that emits when simulation settings change.
    /// </summary>
    public IObservable<Unit> SettingsChanged => _settingsChangedSubject;

    public CaseService(
        ILogger<CaseService> logger,
        IHostEnvironment env,
        IOptions<SimulatorSettings> options)
    {
        _logger = logger;
        _settings = options.Value;

        // Determine paths - support both absolute and relative paths
        _documentSourcePath = Path.IsPathRooted(_settings.DocumentSourcePath)
            ? _settings.DocumentSourcePath
            : Path.Combine(env.ContentRootPath, _settings.DocumentSourcePath);

        _persistenceFilePath = Path.IsPathRooted(_settings.PersistenceFilePath)
            ? _settings.PersistenceFilePath
            : Path.Combine(env.ContentRootPath, _settings.PersistenceFilePath);

        // Initialize arrival rate from configuration
        _averageArrivalsPerMinute = _settings.AverageArrivalsPerMinute;

        _timer = new Timer();
        _timer.Elapsed += OnTimerElapsed;

        _logger.LogInformation("CaseService created with DocumentSourcePath: {DocumentPath}, PersistenceFile: {PersistenceFile}",
            _documentSourcePath, _persistenceFilePath);
        _logger.LogInformation("Initial arrival rate: {Rate} cases/minute", _averageArrivalsPerMinute);
    }

    /// <summary>
    /// Starts the case simulation. Should be called when Dashboard page loads.
    /// </summary>
    public void Start()
    {
        if (_isStarted)
        {
            _logger.LogInformation("CaseService already started. Ignoring duplicate Start() call.");
            return;
        }

        _isStarted = true;
        _logger.LogInformation("Starting Case Service...");
        LoadServedCases();
        DiscoverAvailableCases();

        // Start the simulation
        ScheduleNextCase();
    }

    /// <summary>
    /// Gets the list of currently active cases.
    /// </summary>
    public IEnumerable<Case> GetActiveCases() => _activeCases.OrderByDescending(c => c.ArrivalTimestamp);

    /// <summary>
    /// Resets the simulation: clears all active cases, resets served cases tracking, and restarts the simulation.
    /// </summary>
    public void Reset()
    {
        _logger.LogInformation("Resetting Case Service...");

        // Stop the timer
        _timer.Stop();

        // Clear active cases
        _activeCases.Clear();

        // Clear served case IDs
        _servedCaseIds.Clear();

        // Delete persistence file
        try
        {
            if (File.Exists(_persistenceFilePath))
            {
                File.Delete(_persistenceFilePath);
                _logger.LogInformation("Deleted persistence file: {PersistenceFile}", _persistenceFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete persistence file: {PersistenceFile}", _persistenceFilePath);
        }

        // Rediscover available cases
        DiscoverAvailableCases();

        // Restart the simulation
        if (_isStarted)
        {
            _logger.LogInformation("Restarting simulation after reset");
            ScheduleNextCase();
        }

        _logger.LogInformation("Case Service reset complete");
    }

    private void OnTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        try
        {
            // Stop the timer to prevent re-entrancy while processing
            _timer.Stop();

            _logger.LogInformation("Timer elapsed. Generating a new case.");

            string? newCaseId = GetNextAvailableCaseId();
            if (newCaseId == null)
            {
                _logger.LogWarning("No more available cases to serve. Stopping simulation.");
                return;
            }

            var caseDirectory = Path.Combine(_documentSourcePath, newCaseId);
            var files = Directory.GetFiles(caseDirectory);

            var newCase = new Case { Id = newCaseId };
            foreach (var file in files)
            {
                switch (Path.GetExtension(file).ToLowerInvariant())
                {
                    case ".pdf": newCase.PdfPath = GetRelativePath(file); break;
                    case ".docx": newCase.DocxPath = GetRelativePath(file); break;
                    case ".xml": newCase.XmlPath = GetRelativePath(file); break;
                    case ".html": newCase.HtmlPath = GetRelativePath(file); break;
                }
            }

            _activeCases.Add(newCase);
            _servedCaseIds.Add(newCaseId);

            // Persist the new state
            SaveServedCases();

            // Notify subscribers via Observable
            _logger.LogInformation("Publishing new case arrival to CaseArrived observable - Case ID: {CaseId}", newCaseId);
            _caseArrivedSubject.OnNext(newCase);
            _logger.LogInformation("CaseArrived notification published successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An error occurred while generating a new case.");
        }
        finally
        {
            // Schedule the next event
            ScheduleNextCase();
        }
    }

    private void ScheduleNextCase()
    {
        if(!_availableCaseIds.Any(id => !_servedCaseIds.Contains(id)))
        {
            _logger.LogWarning("All available cases have been served. Simulation finished.");
            return;
        }

        var delay = DistributionService.GetNextPoissonDelay(_averageArrivalsPerMinute);
        _timer.Interval = delay.TotalMilliseconds;
        _timer.Start();
        _logger.LogInformation("Next case scheduled in {Delay} seconds.", delay.TotalSeconds.ToString("F2"));
    }

    private void DiscoverAvailableCases()
    {
        try
        {
            if (!Directory.Exists(_documentSourcePath))
            {
                _logger.LogError("Document source directory not found: {Path}", _documentSourcePath);
                _availableCaseIds = new List<string>();
                return;
            }
            
            _availableCaseIds = Directory.GetDirectories(_documentSourcePath)
                                         .Select(Path.GetFileName)
                                         .Where(name => name != null)
                                         .ToList()!;
            _logger.LogInformation("Discovered {Count} available cases.", _availableCaseIds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover available cases.");
            _availableCaseIds = new List<string>();
        }
    }

    private string? GetNextAvailableCaseId()
    {
        var available = _availableCaseIds.Where(id => !_servedCaseIds.Contains(id)).ToList();
        if (!available.Any())
        {
            return null;
        }
        return available[Random.Shared.Next(available.Count)];
    }

    private void LoadServedCases()
    {
        try
        {
            // Check if reset is requested
            if (_settings.ResetCasesOnStartup)
            {
                _logger.LogInformation("ResetCasesOnStartup is enabled. Starting with empty served cases list.");
                _servedCaseIds = new HashSet<string>();

                // Optionally delete the persistence file
                if (File.Exists(_persistenceFilePath))
                {
                    File.Delete(_persistenceFilePath);
                    _logger.LogInformation("Deleted persistence file: {PersistenceFile}", _persistenceFilePath);
                }

                return;
            }

            if (File.Exists(_persistenceFilePath))
            {
                var json = File.ReadAllText(_persistenceFilePath);
                var servedIds = JsonSerializer.Deserialize<HashSet<string>>(json);
                _servedCaseIds = servedIds ?? new HashSet<string>();
                _logger.LogInformation("Loaded {Count} served case IDs from persistence.", _servedCaseIds.Count);
            }
            else
            {
                _servedCaseIds = new HashSet<string>();
                _logger.LogInformation("No persistence file found. Starting with empty served cases list.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load served cases from {PersistenceFile}. Starting fresh.", _persistenceFilePath);
            _servedCaseIds = new HashSet<string>();
        }
    }

    private void SaveServedCases()
    {
        try
        {
            var json = JsonSerializer.Serialize(_servedCaseIds, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_persistenceFilePath, json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save served cases to cases.json.");
        }
    }

    private string GetRelativePath(string fullPath)
    {
        // This creates a web-accessible path relative to the wwwroot
        var fileName = Path.GetFileName(fullPath);
        var caseId = Path.GetFileName(Path.GetDirectoryName(fullPath));
        return $"/document_store/{caseId}/{fileName}";
    }

    public void Dispose()
    {
        _timer.Elapsed -= OnTimerElapsed;
        _timer.Dispose();

        // Complete the subjects to signal no more values will be emitted
        _caseArrivedSubject.OnCompleted();
        _settingsChangedSubject.OnCompleted();
        _caseArrivedSubject.Dispose();
        _settingsChangedSubject.Dispose();
    }
}
