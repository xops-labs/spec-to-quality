using IncidentApp.Api.Application;
using IncidentApp.Api.Domain;

namespace IncidentApp.Tests.Domain;

public class WireAndFieldsTests
{
    [Theory]
    [InlineData(TimerStatus.AtRisk, "AT_RISK")]
    [InlineData(TimerStatus.Running, "RUNNING")]
    [InlineData(IncidentStatus.Acknowledged, "ACKNOWLEDGED")]
    [InlineData(Severity.Sev1, "SEV1")]
    [InlineData(AuditAction.IncidentCreated, "INCIDENT_CREATED")]
    [InlineData(AuditAction.ActionItemUpdated, "ACTION_ITEM_UPDATED")]
    [InlineData(NotificationType.EscalationL1, "ESCALATION_L1")]
    [InlineData(Role.Commander, "COMMANDER")]
    public void Enums_serialise_to_upper_snake_case(object value, string expected)
    {
        var text = value switch
        {
            TimerStatus v => Wire.ToWire(v), IncidentStatus v => Wire.ToWire(v), Severity v => Wire.ToWire(v),
            AuditAction v => Wire.ToWire(v), NotificationType v => Wire.ToWire(v), Role v => Wire.ToWire(v),
            _ => throw new InvalidOperationException(),
        };
        Assert.Equal(expected, text);
    }

    [Fact]
    public void Parsing_is_case_sensitive()
    {
        Assert.True(Wire.TryParse<Severity>("SEV3", out var v));
        Assert.Equal(Severity.Sev3, v);
        Assert.False(Wire.TryParse<Severity>("sev3", out _));
        Assert.False(Wire.TryParse<Severity>(null, out _));
    }

    private static Fields Parse(string json) => new RequestBody(json).Parse();

    [Fact]
    public void Strings_are_trimmed_and_length_checked_after_trim()
    {
        var f = Parse("""{"a":"  hello  ","b":"  x  "}""");
        Assert.Equal("hello", f.String("a", 5, 5));
        Assert.Null(f.String("b", 2, 10));
        Assert.Equal("too_short", Assert.Single(f.Errors).Code);
    }

    [Fact]
    public void Missing_null_blank_and_wrong_typed_required_strings_are_field_errors()
    {
        var f = Parse("""{"n":null,"blank":"   ","num":5}""");
        f.String("n", 1, 5);
        f.String("blank", 1, 5);
        f.String("missing", 1, 5);
        f.String("num", 1, 5);
        Assert.Equal(["required", "required", "required", "invalid_format"], f.Errors.Select(e => e.Code));
    }

    [Fact]
    public void Too_long_is_reported()
    {
        var f = Parse("""{"a":"abcdef"}""");
        f.String("a", 1, 5);
        Assert.Equal("too_long", Assert.Single(f.Errors).Code);
    }

    [Fact]
    public void Integers_reject_fractions_strings_and_out_of_range()
    {
        var f = Parse("""{"a":1.5,"b":"7","c":9,"d":3}""");
        Assert.Null(f.Integer("a"));
        Assert.Null(f.Integer("b"));
        Assert.Null(f.Integer("c", min: 1, max: 3));
        Assert.Equal(3, f.Integer("d", min: 1, max: 3));
        Assert.Equal(["invalid_format", "invalid_format", "out_of_range"], f.Errors.Select(e => e.Code));
    }

    [Fact]
    public void Enums_are_case_sensitive()
    {
        var f = Parse("""{"s":"SEV2","t":"sev2","u":"SEV9"}""");
        Assert.Equal(Severity.Sev2, f.Enum<Severity>("s"));
        Assert.Null(f.Enum<Severity>("t"));
        Assert.Null(f.Enum<Severity>("u"));
        Assert.Equal(2, f.Errors.Count);
    }

    [Fact]
    public void Dates_must_be_strict_iso_dates()
    {
        var f = Parse("""{"ok":"2026-03-01","bad":"2026-3-1","bad2":"01/03/2026"}""");
        Assert.Equal(new DateOnly(2026, 3, 1), f.Date("ok"));
        Assert.Null(f.Date("bad"));
        Assert.Null(f.Date("bad2"));
    }

    [Fact]
    public void Malformed_json_and_non_objects_raise_400()
    {
        Assert.Equal(400, Assert.Throws<ApiException>(() => new RequestBody("{oops").Parse()).Status);
        Assert.Equal(400, Assert.Throws<ApiException>(() => new RequestBody("[1,2]").Parse()).Status);
    }

    [Fact]
    public void Empty_body_is_an_empty_object() => Assert.False(Parse("").Present("anything"));

    [Fact]
    public void Id_parsing_treats_garbage_as_not_found()
    {
        Assert.Equal(7, Ids.Parse("7"));
        foreach (var bad in new[] { "0", "-1", "abc", "1.5", "" })
        {
            Assert.Equal(404, Assert.Throws<ApiException>(() => Ids.Parse(bad)).Status);
        }
    }

    [Fact]
    public void Page_of_computes_total_pages()
    {
        Assert.Equal(0, PagedResult<int>.Of([], 1, 10, 0).TotalPages);
        Assert.Equal(3, PagedResult<int>.Of([1], 1, 10, 21).TotalPages);
    }
}
