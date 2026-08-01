using System.Reflection;

namespace WorkOps.Application.Common;

public static class BuildVersion
{
    public static string Current { get; } =
        typeof(BuildVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion ?? "unknown";
}
