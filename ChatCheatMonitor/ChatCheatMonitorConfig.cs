using Data.Models.Client;
using System.Text.Json.Serialization;

namespace ChatCheatMonitor;

public enum ChatResponseMode
{
    Disabled,
    Private,
    Public,
    Both
}

public enum PhraseMatchMode
{
    WholeWord,
    Substring
}

public sealed class DetectionCategoryConfig
{
    public string Name { get; set; } = "Cheating";
    public bool Enabled { get; set; } = true;
    public PhraseMatchMode MatchMode { get; set; } = PhraseMatchMode.WholeWord;
    public List<string> Phrases { get; set; } = [];
    public List<string> RegexPatterns { get; set; } = [];
    public Dictionary<string, string> ReminderMessages { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ServerOverrideConfig
{
    public bool? Enabled { get; set; }
    public ChatResponseMode? ResponseMode { get; set; }
    public string? ReportCommand { get; set; }
    public int? PlayerCooldownSeconds { get; set; }
    public int? ServerCooldownSeconds { get; set; }
    public string? Language { get; set; }
    public List<string> ExcludedPhrases { get; set; } = [];
}

public sealed class ChatCheatMonitorConfig
{
    public bool Enabled { get; set; } = true;
    public ChatResponseMode ResponseMode { get; set; } = ChatResponseMode.Private;
    public string ReportCommand { get; set; } = "!rep";
    public int PlayerCooldownSeconds { get; set; } = 45;
    public int ServerCooldownSeconds { get; set; } = 20;
    public bool IgnoreReportCommands { get; set; } = true;
    public bool IgnoreTeamMessages { get; set; }
    public bool EnableLeetNormalization { get; set; } = true;
    public bool EnableTargetAssistance { get; set; } = true;
    public int MinimumTargetNameLength { get; set; } = 3;
    public int MaxMessageLength { get; set; } = 140;
    public string DefaultLanguage { get; set; } = "en";
    public bool Debug { get; set; }

    public Dictionary<string, string> ReminderMessages { get; set; } = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "^1REMINDER:^3 If you believe {target} is {category}, type ^1{reportCommand} {target} <reason>^3 to report it to the admins."
    };

    public List<string> ExcludedPhrases { get; set; } = [];

    public List<string> CommunityReportPhrases { get; set; } =
    [
        "soft aimbot", "aimbotting", "aim lock", "aimlock", "magnetic aim", "wall hacks",
        "wallhacks", "wallhacking", "walls", "wh", "hallhack", "hallhacking", "esp", "chams",
        "red boxes", "redboxes", "tracking through walls", "tracking thru walls", "radar hack",
        "radar cheat"
    ];

    public List<string> CommunityReportExclusions { get; set; } = ["anti cheat", "anticheat"];

    public List<DetectionCategoryConfig> Categories { get; set; } =
    [
        new()
        {
            Name = "cheating",
            Phrases =
            [
                "cheat", "cheater", "cheating", "cheats", "aimbot", "aim bot", "soft aim",
                "wallhack", "wall hack", "walling", "waller", "spinbot", "spin bot", "hacks",
                "hackers", "hacker", "hacking", "modding", "modded", "this guy is cheating",
                "he's cheating", "he is cheating"
            ]
        },
        new()
        {
            Name = "exploiting",
            Phrases = ["exploit", "exploits", "exploiting", "abusing an exploit", "bug abuse"]
        },
        new()
        {
            Name = "glitching",
            Phrases = ["glitch", "glitching", "under the map", "under map", "out of map", "outside the map"]
        }
    ];

    // Keys are IW4MAdmin server IDs (normally ip:port). A "*" entry is also supported.
    public Dictionary<string, ServerOverrideConfig> ServerOverrides { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public bool NotifyStaff { get; set; }
    public EFClient.Permission StaffMinimumPermission { get; set; } = EFClient.Permission.Moderator;
    public int StaffAlertThreshold { get; set; } = 3;
    public int StaffAlertWindowSeconds { get; set; } = 120;
    public string StaffAlertMessage { get; set; } =
        "^1[CCM]^3 Repeated {category} accusations detected for {target} ({count} in {window}s).";

    public bool EnableWebfrontDashboard { get; set; } = true;
    public EFClient.Permission WebfrontMinimumPermission { get; set; } = EFClient.Permission.Moderator;
    public int RecentEventLimit { get; set; } = 50;

    [JsonPropertyName("CheatPhrases")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? LegacyCheatPhrases { get; set; }

    [JsonPropertyName("ReminderMessage")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyReminderMessage { get; set; }

    [JsonPropertyName("AlertCooldownSeconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? LegacyAlertCooldownSeconds { get; set; }

    public bool ApplyLegacySettings()
    {
        var migrated = false;
        if (LegacyCheatPhrases is { Count: > 0 })
        {
            var cheatingCategory = Categories.FirstOrDefault(category =>
                category.Name.Equals("cheating", StringComparison.OrdinalIgnoreCase));
            if (cheatingCategory is not null)
                cheatingCategory.Phrases = LegacyCheatPhrases;
            migrated = true;
        }

        if (!string.IsNullOrWhiteSpace(LegacyReminderMessage))
        {
            ReminderMessages[DefaultLanguage] = LegacyReminderMessage;
            migrated = true;
        }

        if (LegacyAlertCooldownSeconds.HasValue)
        {
            PlayerCooldownSeconds = LegacyAlertCooldownSeconds.Value;
            migrated = true;
        }

        LegacyCheatPhrases = null;
        LegacyReminderMessage = null;
        LegacyAlertCooldownSeconds = null;
        return migrated;
    }
}
