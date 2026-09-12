using System.Globalization;

namespace WorkOps.Application.Tenancy;

public static class MembershipVersion
{
    public static string Encode(uint version) => version.ToString("X8", CultureInfo.InvariantCulture);

    public static bool TryDecode(string? value, out uint version)
    {
        version = 0;
        return value is { Length: 8 } &&
               uint.TryParse(value, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out version);
    }
}
