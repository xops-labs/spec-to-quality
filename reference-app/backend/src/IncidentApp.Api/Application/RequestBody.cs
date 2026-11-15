using System.Text.Json;
using System.Text.Json.Nodes;
using IncidentApp.Api.Domain;

namespace IncidentApp.Api.Application;

/// <summary>
/// The raw request body. Parsing is deferred so that malformed JSON is reported at the right step of the
/// normative order of checks (spec section 5.4), after authentication, role and scope checks.
/// </summary>
public sealed class RequestBody(string raw)
{
    public static readonly RequestBody Empty = new("");

    public static async Task<RequestBody> ReadAsync(HttpRequest request)
    {
        using var reader = new StreamReader(request.Body, System.Text.Encoding.UTF8);
        return new RequestBody(await reader.ReadToEndAsync());
    }

    /// <summary>Parses the body as a JSON object (empty body = empty object); throws 400 otherwise.</summary>
    public Fields Parse()
    {
        if (string.IsNullOrWhiteSpace(raw)) return new Fields(new JsonObject());
        try
        {
            return JsonNode.Parse(raw) is JsonObject obj ? new Fields(obj) : throw ApiException.Malformed("The request body must be a JSON object.");
        }
        catch (JsonException)
        {
            throw ApiException.Malformed();
        }
    }
}

/// <summary>Typed field access with error collection. Call <see cref="ThrowIfInvalid"/> once all fields were read.</summary>
public sealed class Fields(JsonObject obj)
{
    private readonly List<FieldError> _errors = [];

    public IReadOnlyList<FieldError> Errors => _errors;

    public bool Present(string name) => obj.TryGetPropertyValue(name, out var n) && n is not null;

    public void Add(string field, string code, string message) => _errors.Add(new FieldError(field, code, message));

    public void ThrowIfInvalid()
    {
        if (_errors.Count > 0) throw ApiException.Validation(_errors);
    }

    public string? String(string name, int min, int max, bool required = true)
    {
        if (!Present(name))
        {
            if (required) Add(name, "required", $"{name} is required.");
            return null;
        }
        if (obj[name] is not JsonValue v || !v.TryGetValue<string>(out var s))
        {
            Add(name, "invalid_format", $"{name} must be a string.");
            return null;
        }
        s = s.Trim();
        if (s.Length == 0 && min > 0)
        {
            if (required) Add(name, "required", $"{name} is required.");
            else Add(name, "too_short", $"{name} must be at least {min} characters.");
            return null;
        }
        if (s.Length < min) { Add(name, "too_short", $"{name} must be at least {min} characters."); return null; }
        if (s.Length > max) { Add(name, "too_long", $"{name} must be at most {max} characters."); return null; }
        return s;
    }

    /// <summary>A required string that is never trimmed (used for passwords).</summary>
    public string? Secret(string name)
    {
        if (!Present(name))
        {
            Add(name, "required", $"{name} is required.");
            return null;
        }
        if (obj[name] is not JsonValue v || !v.TryGetValue<string>(out var s))
        {
            Add(name, "invalid_format", $"{name} must be a string.");
            return null;
        }
        if (string.IsNullOrWhiteSpace(s))
        {
            Add(name, "required", $"{name} is required.");
            return null;
        }
        return s;
    }

    public long? Integer(string name, bool required = true, long? min = null, long? max = null)
    {
        if (!Present(name))
        {
            if (required) Add(name, "required", $"{name} is required.");
            return null;
        }
        if (obj[name] is not JsonValue v || !v.TryGetValue<JsonElement>(out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt64(out var n))
        {
            Add(name, "invalid_format", $"{name} must be an integer.");
            return null;
        }
        if ((min is not null && n < min) || (max is not null && n > max))
        {
            Add(name, "out_of_range", $"{name} is out of range.");
            return null;
        }
        return n;
    }

    public bool? Boolean(string name, bool required = true)
    {
        if (!Present(name))
        {
            if (required) Add(name, "required", $"{name} is required.");
            return null;
        }
        if (obj[name] is JsonValue v && v.TryGetValue<JsonElement>(out var el) && el.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return el.GetBoolean();
        }
        Add(name, "invalid_format", $"{name} must be a boolean.");
        return null;
    }

    public T? Enum<T>(string name, bool required = true) where T : struct, Enum
    {
        var s = String(name, 1, 100, required);
        if (s is null) return null;
        if (Wire.TryParse<T>(s, out var value)) return value;
        Add(name, "invalid_value", $"{name} has an unsupported value.");
        return null;
    }

    public DateOnly? Date(string name, bool required = true)
    {
        var s = String(name, 1, 20, required);
        if (s is null) return null;
        if (s.Length == 10 && DateOnly.TryParseExact(s, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var d)) return d;
        Add(name, "invalid_format", $"{name} must be a date in YYYY-MM-DD format.");
        return null;
    }
}

/// <summary>Query-string parameter parsing; errors use the parameter name as field (spec 9.1).</summary>
public sealed class QueryReader(IQueryCollection query)
{
    private readonly List<FieldError> _errors = [];

    public void ThrowIfInvalid()
    {
        if (_errors.Count > 0) throw ApiException.Validation(_errors);
    }

    public string? Raw(string name) => query.TryGetValue(name, out var v) && v.Count > 0 && v[0] is { Length: > 0 } s ? s : null;

    public long? PositiveInt(string name)
    {
        var s = Raw(name);
        if (s is null) return null;
        if (long.TryParse(s, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) && n > 0) return n;
        _errors.Add(new FieldError(name, "invalid_format", $"{name} must be a positive integer."));
        return null;
    }

    public bool? Bool(string name)
    {
        var s = Raw(name);
        if (s is null) return null;
        if (s == "true") return true;
        if (s == "false") return false;
        _errors.Add(new FieldError(name, "invalid_format", $"{name} must be true or false."));
        return null;
    }

    public List<T>? EnumList<T>(string name) where T : struct, Enum
    {
        var s = Raw(name);
        if (s is null) return null;
        var list = new List<T>();
        foreach (var part in s.Split(','))
        {
            if (Wire.TryParse<T>(part.Trim(), out var v)) list.Add(v);
            else _errors.Add(new FieldError(name, "invalid_value", $"{name} contains an unsupported value."));
        }
        return list;
    }

    public T? Enum<T>(string name) where T : struct, Enum
    {
        var s = Raw(name);
        if (s is null) return null;
        if (Wire.TryParse<T>(s, out var v)) return v;
        _errors.Add(new FieldError(name, "invalid_value", $"{name} has an unsupported value."));
        return null;
    }

    public string? OneOf(string name, params string[] allowed)
    {
        var s = Raw(name);
        if (s is null) return null;
        if (allowed.Contains(s)) return s;
        _errors.Add(new FieldError(name, "invalid_value", $"{name} must be one of: {string.Join(", ", allowed)}."));
        return null;
    }

    public (int Page, int PageSize) Paging()
    {
        var page = 1;
        var pageSize = 20;
        if (Raw("page") is { } p)
        {
            if (int.TryParse(p, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) && n >= 1) page = n;
            else _errors.Add(new FieldError("page", "out_of_range", "page must be an integer >= 1."));
        }
        if (Raw("pageSize") is { } ps)
        {
            if (int.TryParse(ps, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var n) && n is >= 1 and <= 100) pageSize = n;
            else _errors.Add(new FieldError("pageSize", "out_of_range", "pageSize must be an integer between 1 and 100."));
        }
        return (page, pageSize);
    }
}
