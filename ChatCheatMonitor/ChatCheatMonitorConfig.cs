namespace ChatCheatMonitor;

public class ChatCheatMonitorConfig
{
    public List<string> CheatPhrases { get; set; } = new()
    {
        "cheat",
        "cheater",
        "cheating",
        "cheats",
        "cheating?", 
        "aimbot",
        "aim bot",
        "soft aim",
        "wallhack",
        "wall hack",
        "walling",
        "waller",
        "spinbot",
        "spin bot",
        "hacks",
        "hackers",
        "hacker",
        "hacking",
        "modding",
        "modded",
        "exploiting",
        "exploits",
        "this guy is cheating",
        "he's cheating",
        "he is cheating"
    };

    public string ReminderMessage { get; set; } =
        "^1REMINDER:^3 If you believe a player is cheating, type ^1!rep <player> <reason>^3 to report it to the admins.";

    public int AlertCooldownSeconds { get; set; } = 45;

    public bool Debug { get; set; } = false;
}
