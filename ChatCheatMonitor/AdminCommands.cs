using Data.Models.Client;
using SharedLibraryCore;
using SharedLibraryCore.Commands;
using SharedLibraryCore.Configuration;
using SharedLibraryCore.Interfaces;

namespace ChatCheatMonitor;

public sealed class ChatCheatMonitorStatusCommand : Command
{
    private readonly CheatMonitorService _service;

    public ChatCheatMonitorStatusCommand(
        CommandConfiguration config,
        ITranslationLookup translationLookup,
        CheatMonitorService service) : base(config, translationLookup)
    {
        _service = service;
        Name = "ccmstatus";
        Alias = "ccms";
        Description = "shows ChatCheatMonitor status";
        Permission = EFClient.Permission.Moderator;
        RequiresTarget = false;
    }

    public override Task ExecuteAsync(GameEvent gameEvent) =>
        gameEvent.Origin.TellAsync([_service.GetStatusSummary()], gameEvent.Owner.Manager.CancellationToken);
}
public sealed class ChatCheatMonitorStatsCommand : Command
{
    private readonly ChatCheatMonitorStatistics _statistics;

    public ChatCheatMonitorStatsCommand(
        CommandConfiguration config,
        ITranslationLookup translationLookup,
        ChatCheatMonitorStatistics statistics) : base(config, translationLookup)
    {
        _statistics = statistics;
        Name = "ccmstats";
        Alias = "ccmst";
        Description = "shows or resets privacy-friendly ChatCheatMonitor counters";
        Permission = EFClient.Permission.Moderator;
        RequiresTarget = false;
    }

    public override async Task ExecuteAsync(GameEvent gameEvent)
    {
        if (gameEvent.Data?.Trim().Equals("reset", StringComparison.OrdinalIgnoreCase) == true)
        {
            if (gameEvent.Origin.Level < EFClient.Permission.SeniorAdmin)
            {
                await Reply(gameEvent, "SeniorAdmin permission is required to reset ChatCheatMonitor statistics.");
                return;
            }

            _statistics.Reset();
            await Reply(gameEvent, "ChatCheatMonitor statistics reset.");
            return;
        }

        var snapshot = _statistics.Snapshot();
        var reminders = snapshot.PrivateReminders + snapshot.PublicReminders;
        var suppressions = snapshot.PlayerCooldownSuppressions + snapshot.ServerCooldownSuppressions;
        var categorySummary = snapshot.Categories
            .GroupBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group => $"{group.Key}={group.Sum(item => item.Matches)}")
            .ToArray();

        await Reply(
            gameEvent,
            $"CCM since {snapshot.StartedAt:u}: inspected={snapshot.MessagesInspected}, matches={snapshot.Matches}, reminders={reminders}, suppressed={suppressions}, targets={snapshot.TargetsResolved}, staff alerts={snapshot.StaffAlerts}.",
            categorySummary.Length == 0
                ? "CCM categories: no detections yet."
                : $"CCM categories: {string.Join(", ", categorySummary)}");
    }

    private static Task Reply(GameEvent gameEvent, params string[] messages) =>
        gameEvent.Origin.TellAsync(messages, gameEvent.Owner.Manager.CancellationToken);
}

public sealed class ChatCheatMonitorTestCommand : Command
{
    private readonly CheatMonitorService _service;

    public ChatCheatMonitorTestCommand(
        CommandConfiguration config,
        ITranslationLookup translationLookup,
        CheatMonitorService service) : base(config, translationLookup)
    {
        _service = service;
        Name = "ccmtest";
        Alias = "ccmt";
        Description = "tests a chat message against ChatCheatMonitor rules";
        Permission = EFClient.Permission.Moderator;
        RequiresTarget = false;
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        if (string.IsNullOrWhiteSpace(gameEvent.Data))
        {
            return gameEvent.Origin.TellAsync(
                ["Usage: !ccmtest <message>"],
                gameEvent.Owner.Manager.CancellationToken);
        }

        var analysis = _service.AnalyzeMessage(
            gameEvent.Data,
            gameEvent.Owner.Id,
            gameEvent.Origin,
            gameEvent.Owner.ConnectedClients);

        var result = analysis.Matched
            ? $"CCM match: category={analysis.Category}, rule={analysis.Pattern}, target={analysis.TargetName ?? "unresolved"}. {analysis.Reason}"
            : $"CCM no match: {analysis.Reason}";

        return gameEvent.Origin.TellAsync([result], gameEvent.Owner.Manager.CancellationToken);
    }
}

public sealed class ChatCheatMonitorReloadCommand : Command
{
    private readonly CheatMonitorService _service;

    public ChatCheatMonitorReloadCommand(
        CommandConfiguration config,
        ITranslationLookup translationLookup,
        CheatMonitorService service) : base(config, translationLookup)
    {
        _service = service;
        Name = "ccmreload";
        Alias = "ccmr";
        Description = "rebuilds ChatCheatMonitor rules from the active configuration";
        Permission = EFClient.Permission.SeniorAdmin;
        RequiresTarget = false;
    }

    public override Task ExecuteAsync(GameEvent gameEvent)
    {
        _service.RefreshConfiguration();
        return gameEvent.Origin.TellAsync(
            [$"ChatCheatMonitor rules refreshed with {_service.ConfigurationIssues.Count} configuration issue(s)."],
            gameEvent.Owner.Manager.CancellationToken);
    }
}
