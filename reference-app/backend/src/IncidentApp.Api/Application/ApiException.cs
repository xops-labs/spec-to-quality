namespace IncidentApp.Api.Application;

public sealed record FieldError(string Field, string Code, string Message);

/// <summary>An error that maps to the spec's uniform error body (section 10.1).</summary>
public sealed class ApiException(int status, string code, string message,
    IReadOnlyList<FieldError>? errors = null, IDictionary<string, object?>? details = null) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
    public IReadOnlyList<FieldError>? Errors { get; } = errors;
    public IDictionary<string, object?>? Details { get; } = details;

    public static ApiException Unauthenticated(string message = "Authentication is required.") => new(401, "UNAUTHENTICATED", message);
    public static ApiException InvalidCredentials() => new(401, "INVALID_CREDENTIALS", "Invalid username or password.");
    public static ApiException Forbidden(string message = "You are not allowed to perform this action.") => new(403, "FORBIDDEN", message);
    public static ApiException NotFound(string message = "The requested resource was not found.") => new(404, "NOT_FOUND", message);
    public static ApiException Malformed(string message = "The request body is not valid JSON.") => new(400, "MALFORMED_REQUEST", message);

    public static ApiException Validation(IReadOnlyList<FieldError> errors) =>
        new(422, "VALIDATION_ERROR", "One or more fields are invalid.", errors);

    public static ApiException Validation(string field, string code, string message) =>
        Validation([new FieldError(field, code, message)]);

    public static ApiException BusinessRule(string rule, string message) =>
        new(422, "BUSINESS_RULE_VIOLATION", message, details: new Dictionary<string, object?> { ["rule"] = rule });

    public static ApiException Conflict(string code, string message, IDictionary<string, object?>? details = null) =>
        new(409, code, message, details: details);
}
