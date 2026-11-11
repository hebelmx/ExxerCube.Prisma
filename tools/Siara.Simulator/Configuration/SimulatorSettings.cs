namespace Siara.Simulator.Configuration;

/// <summary>
/// Configuration settings for the SIARA case simulator.
/// </summary>
public class SimulatorSettings
{
    /// <summary>
    /// Path to the directory containing case documents.
    /// Can be absolute or relative to the application root.
    /// Default: "../bulk_generated_documents_all_formats"
    /// </summary>
    public string DocumentSourcePath { get; set; } = "../bulk_generated_documents_all_formats";

    /// <summary>
    /// Path to the JSON file that persists served case IDs.
    /// Can be absolute or relative to the application root.
    /// Default: "cases.json"
    /// </summary>
    public string PersistenceFilePath { get; set; } = "cases.json";

    /// <summary>
    /// Average number of case arrivals per minute.
    /// Valid range: 0.1 to 60 cases per minute.
    /// Default: 6.0
    /// </summary>
    public double AverageArrivalsPerMinute { get; set; } = 6.0;

    /// <summary>
    /// Whether to reset served cases on application restart.
    /// If true, all cases become available again on startup.
    /// Default: false
    /// </summary>
    public bool ResetCasesOnStartup { get; set; } = false;
}
