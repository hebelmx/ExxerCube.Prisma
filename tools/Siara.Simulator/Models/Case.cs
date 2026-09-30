namespace Siara.Simulator.Models;

/// <summary>
/// Represents a single information requirement (a "case") in the simulator.
/// </summary>
public class Case
{
    /// <summary>
    /// The unique identifier for the case, derived from the document folio.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The current status of the case.
    /// </summary>
    public CaseStatus Status { get; set; } = CaseStatus.Arrived;

    /// <summary>
    /// The timestamp when the case "arrived" in the system.
    /// </summary>
    public DateTime ArrivalTimestamp { get; init; } = DateTime.Now;

    /// <summary>
    /// The relative path to the PDF document for this case.
    /// </summary>
    public string? PdfPath { get; set; }
    
    /// <summary>
    /// The relative path to the DOCX document for this case.
    /// </summary>
    public string? DocxPath { get; set; }

    /// <summary>
    /// The relative path to the XML document for this case.
    /// </summary>
    public string? XmlPath { get; set; }

    /// <summary>
    /// The relative path to the HTML document for this case.
    /// </summary>
    public string? HtmlPath { get; set; }
}

public enum CaseStatus
{
    Arrived,
    Downloaded,
    Answered,
    Closed,
    Sent,
    Archived,
    Unattended
}
