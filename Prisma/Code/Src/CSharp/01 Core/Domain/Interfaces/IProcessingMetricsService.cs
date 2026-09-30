using ExxerCube.Prisma.Domain.Events;

namespace ExxerCube.Prisma.Domain.Interfaces;

/// <summary>
/// Defines a service contract for collecting, tracking, and analyzing performance metrics for document processing operations.
/// </summary>
/// <remarks>
/// <para>
/// This interface provides comprehensive metrics collection capabilities for OCR document processing workflows.
/// It enables tracking of individual document processing operations, aggregate statistics, throughput analysis,
/// and performance validation against defined requirements.
/// </para>
/// <para>
/// Implementations should be thread-safe and designed to handle concurrent processing operations efficiently.
/// The service tracks metrics such as processing time, OCR confidence scores, extracted field counts,
/// success rates, and throughput statistics.
/// </para>
/// <para>
/// Typical usage pattern:
/// <list type="number">
/// <item>Call <see cref="StartProcessingAsync"/> at the beginning of document processing to obtain a <see cref="IProcessingContext"/>.</item>
/// <item>Process the document using the returned context.</item>
/// <item>Call <see cref="CompleteProcessingAsync"/> on success or <see cref="RecordErrorAsync"/> on failure.</item>
/// <item>Dispose the <see cref="IProcessingContext"/> when finished.</item>
/// </list>
/// </para>
/// </remarks>
/// <seealso cref="IProcessingMetricsService"/>
/// <seealso cref="IProcessingContext"/>
/// <seealso cref="ProcessingStatistics"/>
/// <seealso cref="ProcessingMetrics"/>
public interface IProcessingMetricsService
{
    /// <summary>
    /// Gets the current aggregated processing statistics.
    /// </summary>
    /// <value>
    /// A <see cref="ProcessingStatistics"/> instance containing aggregated metrics including total documents processed,
    /// success/failure counts, average processing time, average confidence scores, and success rate.
    /// Statistics are updated periodically and after each processing completion.
    /// </value>
    /// <remarks>
    /// This property provides a snapshot of the current system performance metrics. The statistics are aggregated
    /// from all processed documents and updated automatically. For the most up-to-date statistics, consider using
    /// <see cref="GetCurrentStatisticsAsync"/> which ensures thread-safe access.
    /// </remarks>
    ProcessingStatistics CurrentStatistics { get; }

    /// <summary>
    /// Gets the maximum number of concurrent processing operations allowed.
    /// </summary>
    /// <value>
    /// An integer representing the maximum number of documents that can be processed simultaneously.
    /// This value is set during service initialization and cannot be changed after instantiation.
    /// </value>
    /// <remarks>
    /// When the number of active processing operations reaches this limit, new processing requests may be queued
    /// or delayed. Monitor <see cref="ActiveProcessingCount"/> to track current concurrency levels.
    /// </remarks>
    int MaxConcurrency { get; }

    /// <summary>
    /// Gets the current number of active processing operations.
    /// </summary>
    /// <value>
    /// An integer representing the number of documents currently being processed. This value is incremented
    /// when <see cref="StartProcessingAsync"/> is called and decremented when <see cref="CompleteProcessingAsync"/>
    /// or <see cref="RecordErrorAsync"/> is called.
    /// </value>
    /// <remarks>
    /// This property provides real-time visibility into the current processing load. When this value approaches
    /// <see cref="MaxConcurrency"/>, the system may experience queuing delays for new processing requests.
    /// </remarks>
    int ActiveProcessingCount { get; }

    /// <summary>
    /// Records the start of a document processing operation and returns a tracking context.
    /// </summary>
    /// <param name="documentId">The unique identifier for the document being processed. Must not be null or empty.</param>
    /// <param name="sourcePath">The file system path or source location of the document. Must not be null or empty.</param>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that represents the asynchronous operation. The task result contains
    /// a <see cref="IProcessingContext"/> instance that tracks the processing operation and must be disposed
    /// when processing completes or fails.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method initiates metrics tracking for a document processing operation. It increments the active
    /// processing count and starts timing the operation. The returned context should be used throughout the
    /// processing lifecycle and disposed properly to ensure accurate metrics collection.
    /// </para>
    /// <para>
    /// If the maximum concurrency limit is reached, a warning is logged but the operation still proceeds.
    /// The caller should monitor <see cref="ActiveProcessingCount"/> to understand system load.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="documentId"/> or <paramref name="sourcePath"/> is null or empty.</exception>
    /// <seealso cref="CompleteProcessingAsync"/>
    /// <seealso cref="RecordErrorAsync"/>
    /// <seealso cref="IProcessingContext"/>
    Task<IProcessingContext> StartProcessingAsync(string documentId, string sourcePath);

    /// <summary>
    /// Records the successful or unsuccessful completion of a document processing operation.
    /// </summary>
    /// <param name="context">The <see cref="IProcessingContext"/> obtained from <see cref="StartProcessingAsync"/>. Must not be null.</param>
    /// <param name="result">The processing result containing OCR results and extracted fields. Can be null if processing failed before completion.</param>
    /// <param name="isSuccess">A boolean value indicating whether the processing operation completed successfully. True for success, false for failure.</param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation. The task completes when metrics have been
    /// recorded and statistics have been updated.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method finalizes metrics collection for a processing operation. It calculates processing time,
    /// extracts confidence scores and field counts from the result, records the metrics, and updates aggregate
    /// statistics. The active processing count is decremented.
    /// </para>
    /// <para>
    /// When <paramref name="isSuccess"/> is false or <paramref name="result"/> is null, the operation is
    /// recorded as a failure. For explicit error recording with error messages, use <see cref="RecordErrorAsync"/> instead.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null.</exception>
    /// <seealso cref="StartProcessingAsync"/>
    /// <seealso cref="RecordErrorAsync"/>
    Task CompleteProcessingAsync(IProcessingContext context, ProcessingResult? result, bool isSuccess);

    /// <summary>
    /// Records a processing error for a document operation and marks it as failed.
    /// </summary>
    /// <param name="context">The <see cref="IProcessingContext"/> obtained from <see cref="StartProcessingAsync"/>. Must not be null.</param>
    /// <param name="error">A descriptive error message explaining what went wrong during processing. Must not be null or empty.</param>
    /// <returns>
    /// A <see cref="Task"/> that represents the asynchronous operation. The task completes when the error has been
    /// recorded, metrics have been updated, and the operation has been marked as failed.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method is used to record explicit error conditions during document processing. It automatically
    /// calls <see cref="CompleteProcessingAsync"/> with a failure status, logs the error, and creates an error
    /// event in the processing event queue.
    /// </para>
    /// <para>
    /// The error message is stored in the processing event and can be retrieved via <see cref="GetRecentEvents"/>.
    /// This method should be called whenever an exception or error condition occurs during processing.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="context"/> is null or <paramref name="error"/> is null or empty.</exception>
    /// <seealso cref="StartProcessingAsync"/>
    /// <seealso cref="CompleteProcessingAsync"/>
    Task RecordErrorAsync(IProcessingContext context, string error);

    /// <summary>
    /// Asynchronously retrieves the current aggregated processing statistics.
    /// </summary>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that represents the asynchronous operation. The task result contains
    /// a <see cref="ProcessingStatistics"/> instance with the most up-to-date aggregated metrics.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method provides thread-safe access to the current processing statistics. It ensures that the
    /// returned statistics are consistent and reflect the latest aggregated metrics from all processed documents.
    /// </para>
    /// <para>
    /// The statistics include total documents processed, success/failure counts, average processing time,
    /// average OCR confidence, average extracted field count, success rate, and last update timestamp.
    /// </para>
    /// </remarks>
    /// <seealso cref="CurrentStatistics"/>
    /// <seealso cref="ProcessingStatistics"/>
    Task<ProcessingStatistics> GetCurrentStatisticsAsync();

    /// <summary>
    /// Retrieves detailed processing metrics for a specific document by its identifier.
    /// </summary>
    /// <param name="documentId">The unique identifier of the document whose metrics are to be retrieved. Must not be null or empty.</param>
    /// <returns>
    /// A <see cref="ProcessingMetrics"/> instance containing detailed metrics for the specified document,
    /// or <c>null</c> if no metrics exist for the given document identifier.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method provides access to per-document metrics including processing time, OCR confidence score,
    /// number of extracted fields, success status, and completion timestamp.
    /// </para>
    /// <para>
    /// Metrics are only available for documents that have completed processing (successfully or unsuccessfully).
    /// Documents that are currently being processed or have not yet started will return <c>null</c>.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="documentId"/> is null or empty.</exception>
    /// <seealso cref="GetAllMetrics"/>
    /// <seealso cref="ProcessingMetrics"/>
    ProcessingMetrics? GetDocumentMetrics(string documentId);

    /// <summary>
    /// Retrieves processing metrics for all documents that have completed processing.
    /// </summary>
    /// <returns>
    /// A <see cref="List{T}"/> of <see cref="ProcessingMetrics"/> instances, one for each document that has
    /// completed processing. The list is a snapshot at the time of the call and may not include documents
    /// currently being processed.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method returns a complete collection of all document-level metrics that have been recorded.
    /// The returned list is a copy of the internal metrics collection, so modifications to the list will not
    /// affect the internal state.
    /// </para>
    /// <para>
    /// For large numbers of processed documents, consider using <see cref="GetRecentEvents"/> to retrieve
    /// a limited set of recent processing events instead.
    /// </para>
    /// </remarks>
    /// <seealso cref="GetDocumentMetrics"/>
    /// <seealso cref="GetRecentEvents"/>
    List<ProcessingMetrics> GetAllMetrics();

    /// <summary>
    /// Retrieves the most recent processing events from the event queue.
    /// </summary>
    /// <param name="count">The maximum number of recent events to retrieve. Defaults to 100 if not specified. Must be greater than zero.</param>
    /// <returns>
    /// A <see cref="List{T}"/> of <see cref="ProcessingEvent"/> instances representing the most recent processing
    /// events, ordered from oldest to newest. The list will contain at most <paramref name="count"/> events.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method provides access to recent processing activity through the event queue. Each event represents
    /// a completed processing operation (successful or failed) and includes timing information, success status,
    /// confidence scores, and error messages when applicable.
    /// </para>
    /// <para>
    /// Events are maintained in a queue with a limited capacity. Very old events may be automatically removed
    /// to manage memory usage. For comprehensive metrics, use <see cref="GetAllMetrics"/> instead.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is less than or equal to zero.</exception>
    /// <seealso cref="ProcessingEvent"/>
    /// <seealso cref="GetAllMetrics"/>
    List<ProcessingEvent> GetRecentEvents(int count = 100);

    /// <summary>
    /// Calculates throughput statistics for a specified time period based on recent processing events.
    /// </summary>
    /// <param name="timeSpan">The time period to analyze, measured backwards from the current time. Must be greater than <see cref="TimeSpan.Zero"/>.</param>
    /// <returns>
    /// A <see cref="ThroughputStatistics"/> instance containing calculated metrics for the specified period,
    /// including total documents processed, success/failure counts, average processing time, documents per hour,
    /// and success rate.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method analyzes processing events within the specified time window to calculate throughput metrics.
    /// It filters events by timestamp and computes aggregate statistics including processing rates and success rates.
    /// </para>
    /// <para>
    /// If no events exist within the specified time period, the method returns a <see cref="ThroughputStatistics"/>
    /// instance with zero values for all metrics.
    /// </para>
    /// <para>
    /// Common use cases include calculating hourly throughput (<c>TimeSpan.FromHours(1)</c>), daily throughput
    /// (<c>TimeSpan.FromDays(1)</c>), or short-term performance (<c>TimeSpan.FromMinutes(5)</c>).
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="timeSpan"/> is less than or equal to <see cref="TimeSpan.Zero"/>.</exception>
    /// <seealso cref="ThroughputStatistics"/>
    /// <seealso cref="ValidatePerformanceAsync"/>
    ThroughputStatistics CalculateThroughput(TimeSpan timeSpan);

    /// <summary>
    /// Validates system performance against defined requirements and returns detailed validation results.
    /// </summary>
    /// <returns>
    /// A <see cref="Task{TResult}"/> that represents the asynchronous operation. The task result contains
    /// a <see cref="Result{T}"/> wrapping a <see cref="PerformanceValidation"/> instance that indicates
    /// whether performance requirements are met and provides detailed validation results.
    /// </returns>
    /// <remarks>
    /// <para>
    /// This method performs comprehensive performance validation by checking multiple criteria:
    /// <list type="bullet">
    /// <item>Throughput requirements (minimum documents per hour)</item>
    /// <item>Processing time requirements (maximum average processing time per document)</item>
    /// <item>Concurrency requirements (minimum concurrent processing capacity)</item>
    /// <item>Success rate requirements (minimum success percentage)</item>
    /// </list>
    /// </para>
    /// <para>
    /// The validation results include detailed messages for any requirements that are not met, along with
    /// current statistics and throughput metrics for both 1-hour and 5-minute periods.
    /// </para>
    /// <para>
    /// This method always returns a successful <see cref="Result{T}"/>; validation failures are indicated
    /// by the <see cref="PerformanceValidation.IsMeetingRequirements"/> property being <c>false</c>.
    /// </para>
    /// </remarks>
    /// <seealso cref="PerformanceValidation"/>
    /// <seealso cref="CalculateThroughput"/>
    /// <seealso cref="GetCurrentStatisticsAsync"/>
    Task<Result<PerformanceValidation>> ValidatePerformanceAsync();
}