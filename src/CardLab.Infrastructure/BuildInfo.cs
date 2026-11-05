using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace CardLab.Infrastructure;

/// <summary>The build commit reported by health and stamped on log records (DEC-12, BUG-001).</summary>
public static class BuildInfo
{
    /// <summary>The commit for this process: <c>CardLab:Commit</c>, else the source revision of the entry assembly, else "unknown".</summary>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>The commit to report.</returns>
    public static string Commit(IConfiguration configuration) =>
        Resolve(
            configuration["CardLab:Commit"],
            InformationalVersion);

    /// <summary>The entry assembly's informational version, e.g. <c>1.0.0+&lt;sha&gt;</c>, or null.</summary>
    public static string? InformationalVersion =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

    /// <summary>Commit: the configured value; else the part after the last '+' of the informational version; else "unknown".</summary>
    /// <param name="configured">The configured commit, possibly empty.</param>
    /// <param name="informationalVersion">The entry assembly's informational version, e.g. <c>1.0.0+&lt;sha&gt;</c>.</param>
    /// <returns>The commit to report.</returns>
    public static string Resolve(string? configured, string? informationalVersion)
    {
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return "unknown";
        }

        var plus = informationalVersion.LastIndexOf('+');
        return plus >= 0 && plus < informationalVersion.Length - 1 ? informationalVersion[(plus + 1)..] : informationalVersion;
    }
}
