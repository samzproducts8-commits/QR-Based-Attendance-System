using Attendance.Application.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Attendance.Api.Middleware;

/// <summary>
/// Global exception handler: converts every unhandled exception into a
/// consistent RFC 7807 <see cref="ProblemDetails"/> JSON response and logs it.
/// Stack traces are never included in the response body.
/// </summary>
/// <remarks>
/// Satisfies Requirement 7.5 and the design's error-handling table:
/// <list type="bullet">
///   <item>PhotoValidationException / ValidationException → 400</item>
///   <item>NotFoundException / KeyNotFoundException → 404</item>
///   <item>DuplicateAttendanceException / DuplicateEmailException / TokenAlreadyUsedException / SlotInUseException → 409</item>
///   <item>TokenExpiredException → 410</item>
///   <item>OutsideScheduleException → 422</item>
///   <item>anything else → 500</item>
/// </list>
/// </remarks>
public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            (int status, string title) = MapException(ex);

            if (status >= 500)
                _logger.LogError(ex, "Unhandled exception on {Method} {Path}",
                    context.Request.Method, context.Request.Path);
            else
                _logger.LogWarning("Request fault {Status} on {Method} {Path}: {Message}",
                    status, context.Request.Method, context.Request.Path, ex.Message);

            if (context.Response.HasStarted)
                throw;

            context.Response.Clear();
            context.Response.StatusCode = status;
            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = status,
                Title  = title,
                // Client-safe message only; 500s never leak internals.
                Detail = status >= 500 ? "An unexpected error occurred." : ex.Message,
                Instance = context.Request.Path
            };

            await context.Response.WriteAsJsonAsync(problem);
        }
    }

    private static (int Status, string Title) MapException(Exception ex) => ex switch
    {
        PhotoValidationException      => (StatusCodes.Status400BadRequest, "Invalid photo"),
        ValidationException           => (StatusCodes.Status400BadRequest, "Validation failed"),
        NotFoundException             => (StatusCodes.Status404NotFound, "Resource not found"),
        KeyNotFoundException          => (StatusCodes.Status404NotFound, "Resource not found"),
        DuplicateAttendanceException  => (StatusCodes.Status409Conflict, "Duplicate attendance"),
        DuplicateEmailException       => (StatusCodes.Status409Conflict, "Duplicate email"),
        TokenAlreadyUsedException     => (StatusCodes.Status409Conflict, "QR code already used"),
        SlotInUseException            => (StatusCodes.Status409Conflict, "Slot in use"),
        TokenExpiredException         => (StatusCodes.Status410Gone, "QR code expired"),
        OutsideScheduleException      => (StatusCodes.Status422UnprocessableEntity, "Outside attendance window"),
        BusinessRuleException         => (StatusCodes.Status422UnprocessableEntity, "Business rule violation"),
        _                             => (StatusCodes.Status500InternalServerError, "Server error")
    };
}
