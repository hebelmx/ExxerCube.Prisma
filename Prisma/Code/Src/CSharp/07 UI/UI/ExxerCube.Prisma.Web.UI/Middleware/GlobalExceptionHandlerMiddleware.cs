using System;
using System.Diagnostics;
using System.Net;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;

namespace ExxerCube.Prisma.Web.UI.Middleware;

/// <summary>
/// Global exception handler middleware that catches all unhandled exceptions during request processing.
/// Provides rich contextual logging with request details, user information, and structured error data.
/// </summary>
public class GlobalExceptionHandlerMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="GlobalExceptionHandlerMiddleware"/> class.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="environment">The hosting environment.</param>
    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
    }

    /// <summary>
    /// Invokes the middleware to handle the HTTP request.
    /// </summary>
    /// <param name="httpContext">The HTTP context.</param>
    public async Task InvokeAsync(HttpContext httpContext)
    {
        try
        {
            await _next(httpContext);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(httpContext, ex);
        }
    }

    /// <summary>
    /// Handles the exception by logging rich contextual information and returning an appropriate response.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <param name="exception">The exception that occurred.</param>
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // Extract contextual information
        var requestPath = context.Request.Path;
        var requestMethod = context.Request.Method;
        var queryString = context.Request.QueryString.ToString();
        var userAgent = context.Request.Headers.UserAgent.ToString();
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
        var userName = context.User?.Identity?.Name ?? "Anonymous";
        var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

        // Log exception with rich structured data
        _logger.LogError(exception,
            "Unhandled exception occurred during request processing. " +
            "Method: {RequestMethod}, Path: {RequestPath}, QueryString: {QueryString}, " +
            "User: {UserName}, IP: {IpAddress}, TraceId: {TraceId}, UserAgent: {UserAgent}",
            requestMethod,
            requestPath,
            queryString,
            userName,
            ipAddress,
            traceId,
            userAgent);

        // Determine status code based on exception type
        var statusCode = exception switch
        {
            UnauthorizedAccessException => HttpStatusCode.Unauthorized,
            ArgumentException => HttpStatusCode.BadRequest,
            KeyNotFoundException => HttpStatusCode.NotFound,
            InvalidOperationException => HttpStatusCode.Conflict,
            NotImplementedException => HttpStatusCode.NotImplemented,
            _ => HttpStatusCode.InternalServerError
        };

        // Set response status code
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        // Create error response
        var errorResponse = new
        {
            StatusCode = (int)statusCode,
            Message = _environment.IsDevelopment()
                ? exception.Message
                : "An error occurred while processing your request.",
            TraceId = traceId,
            Details = _environment.IsDevelopment() ? exception.ToString() : null
        };

        // Write JSON response
        var jsonResponse = JsonSerializer.Serialize(errorResponse, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = _environment.IsDevelopment()
        });

        await context.Response.WriteAsync(jsonResponse);
    }
}