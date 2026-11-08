using System.Reflection;

namespace DiscordQuestDecoy.Services;

public static class AppInfo
{
    public const string Name = "Discord Quest Decoy";

    // TODO: replace YOUR-USERNAME with your GitHub username before publishing.
    // While it still says YOUR-USERNAME, the "GitHub page" button is hidden.
    public const string Repo = "aroxwex/discord-quest-decoy";

    public static bool RepoConfigured => !Repo.StartsWith("YOUR-", StringComparison.Ordinal);
    public static string RepoUrl => "https://github.com/" + Repo;

    public static string PlainVersion
    {
        get
        {
            var v = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "1.0.0";
            return v.Split('+')[0].Split('-')[0];
        }
    }

    public static string VersionLabel => "v" + PlainVersion;
}
