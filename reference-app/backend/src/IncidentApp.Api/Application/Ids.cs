using System.Globalization;

namespace IncidentApp.Api.Application;

public static class Ids
{
    /// <summary>Parses a path id; non-integer or non-positive ids are reported as 404 (spec 9.1).</summary>
    public static long Parse(string raw) =>
        long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0 ? id : throw ApiException.NotFound();
}
