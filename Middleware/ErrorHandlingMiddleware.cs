using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using System.Security;
using ParadeGuard.Api.Models;

namespace ParadeGuard.Api.Middleware
{
    public class ErrorHandlingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ErrorHandlingMiddleware> _logger;
        private readonly IWebHostEnvironment _environment;

        public ErrorHandlingMiddleware(RequestDelegate next, ILogger<ErrorHandlingMiddleware> logger, IWebHostEnvironment environment)
        {
            _next = next;
            _logger = logger;
            _environment = environment;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unhandled exception occurred. RequestId: {RequestId}, Path: {Path}",
                    context.TraceIdentifier, context.Request.Path);
                await HandleExceptionAsync(context, ex);
            }
        }

        private async Task HandleExceptionAsync(HttpContext context, Exception exception)
        {
            context.Response.ContentType = "application/json";

            var error = new ApiError
            {
                RequestId = context.TraceIdentifier
            };

            // Determine status code and message based on exception type
            var (statusCode, message, shouldLogStackTrace) = exception switch
            {
                ArgumentException or ArgumentNullException =>
                    ((int)HttpStatusCode.BadRequest, exception.Message, false),

                InvalidOperationException =>
                    ((int)HttpStatusCode.ServiceUnavailable, exception.Message, false),

                HttpRequestException =>
                    ((int)HttpStatusCode.ServiceUnavailable, "External service unavailable", true),

                TaskCanceledException when exception.InnerException is TimeoutException =>
                    ((int)HttpStatusCode.RequestTimeout, "Request timeout", true),

                SecurityException =>
                    ((int)HttpStatusCode.Forbidden, "Access denied", true),

                UnauthorizedAccessException =>
                    ((int)HttpStatusCode.Unauthorized, "Unauthorized access", true),

                JsonException =>
                    ((int)HttpStatusCode.BadRequest, "Invalid JSON format", false),

                _ => ((int)HttpStatusCode.InternalServerError, "An error occurred while processing your request", true)
            };

            context.Response.StatusCode = statusCode;
            error.Message = message;

            // Log stack trace for server errors and specific exceptions
            if (shouldLogStackTrace || statusCode >= 500)
            {
                _logger.LogError(exception, "Exception details for RequestId: {RequestId}", context.TraceIdentifier);
            }

            // Include additional details in development
            if (_environment.IsDevelopment())
            {
                error.Details = exception.ToString();
            }

            var jsonResponse = JsonSerializer.Serialize(error, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = _environment.IsDevelopment()
            });

            await context.Response.WriteAsync(jsonResponse);
        }
    }
}