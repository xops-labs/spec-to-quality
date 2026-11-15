using System.Text.Json;
using System.Text.Json.Serialization;
using IncidentApp.Api.Application;
using Microsoft.EntityFrameworkCore;

namespace IncidentApp.Api.Api;

/// <summary>The uniform error body of spec 10.1.</summary>
public sealed record ApiError(
    string Code, string Message, int Status, string TraceId,
    IReadOnlyList<FieldError>? Errors = null,
    IDictionary<string, object?>? Details = null)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<FieldError>? Errors { get; init; } = Errors;

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IDictionary<string, object?>? Details { get; init; } = Details;
}

public static class ErrorWriter
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static Task WriteAsync(HttpContext ctx, ApiException ex) => WriteAsync(ctx, ex.Status, ex.Code, ex.Message, ex.Errors, ex.Details);

    public static Task WriteAsync(HttpContext ctx, int status, string code, string message,
        IReadOnlyList<FieldError>? errors = null, IDictionary<string, object?>? details = null)
    {
        if (code == "VALIDATION_ERROR") errors ??= [];
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        var body = new ApiError(code, message, status, ctx.TraceIdentifier, errors, details);
        return ctx.Response.WriteAsync(JsonSerializer.Serialize(body, Json));
    }

    public static (string Code, string Message)? ForBodylessStatus(int status) => status switch
    {
        400 => ("MALFORMED_REQUEST", "The request is malformed."),
        401 => ("UNAUTHENTICATED", "Authentication is required."),
        403 => ("FORBIDDEN", "You are not allowed to perform this action."),
        404 => ("NOT_FOUND", "The requested resource was not found."),
        405 => ("METHOD_NOT_ALLOWED", "The HTTP method is not allowed for this resource."),
        415 => ("MALFORMED_REQUEST", "Unsupported media type."),
        >= 400 => ("INTERNAL_ERROR", "The request could not be processed."),
        _ => null,
    };
}

/// <summary>Turns exceptions into error bodies; never leaks internals (NFR-04, section 10.1).</summary>
public sealed class ErrorMiddleware(RequestDelegate next, ILogger<ErrorMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (ApiException ex)
        {
            await ErrorWriter.WriteAsync(ctx, ex);
        }
        catch (DbUpdateConcurrencyException)
        {
            await ErrorWriter.WriteAsync(ctx, 409, "VERSION_CONFLICT", "The incident was modified by someone else.");
        }
        catch (BadHttpRequestException)
        {
            await ErrorWriter.WriteAsync(ctx, 400, "MALFORMED_REQUEST", "The request could not be read.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled exception for {Method} {Path}", ctx.Request.Method, ctx.Request.Path.Value);
            if (ctx.Response.HasStarted) throw;
            ctx.Response.Clear();
            await ErrorWriter.WriteAsync(ctx, 500, "INTERNAL_ERROR", "An unexpected error occurred.");
        }
    }
}
