using System.Globalization;

namespace WorkOps.Application.WorkItems;

public static class WorkItemVersion
{
    public static string Encode(uint version) => version.ToString("X8", CultureInfo.InvariantCulture);

    public static bool TryDecode(string value, out uint version)
    {
        version = 0;
        return value.Length == 8 &&
               uint.TryParse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out version);
    }
}
