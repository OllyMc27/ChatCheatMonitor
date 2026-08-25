using System.Globalization;
using System.Net;
using System.Text;
using Data.Models.Client;
using SharedLibraryCore.Helpers;
using SharedLibraryCore.Interfaces;

namespace ChatCheatMonitor;

public sealed class ChatCheatMonitorWebfront : IDisposable
{
    public const string InteractionKey = "Webfront::Nav::Admin::ChatCheatMonitor";

    private readonly IInteractionRegistration _interactions;
    private readonly IConfigurationHandlerV2<ChatCheatMonitorConfig> _configurationHandler;
    private readonly ChatCheatMonitorConfig _config;
    private readonly CheatMonitorService _service;
    private readonly ChatCheatMonitorStatistics _statistics;
    private bool _disposed;

    public ChatCheatMonitorWebfront(
        IInteractionRegistration interactions,
        IConfigurationHandlerV2<ChatCheatMonitorConfig> configurationHandler,
        ChatCheatMonitorConfig config,
        CheatMonitorService service,
        ChatCheatMonitorStatistics statistics)
    {
        _interactions = interactions;
        _configurationHandler = configurationHandler;
        _config = config;
        _service = service;
        _statistics = statistics;
        _configurationHandler.Updated += OnConfigurationUpdated;
    }

    public void Register()
    {
        _interactions.UnregisterInteraction(InteractionKey);
        if (!_config.EnableWebfrontDashboard)
            return;

        _interactions.RegisterInteraction(InteractionKey, (_, _, _) =>
        {
            var interaction = new InteractionData
            {
                Enabled = true,
                Name = "Chat Cheat Monitor",
                Description = "Chat Cheat Monitor dashboard",
                DisplayMeta = "ph-shield-check",
                InteractionId = InteractionKey,
                MinimumPermission = _config.WebfrontMinimumPermission,
                InteractionType = InteractionType.TemplateContent,
                Source = "ChatCheatMonitor",
                PermissionEntity = "Interaction",
                PermissionAccess = "Read",
                Action = (_, _, _, _, _) => Task.FromResult(RenderDashboard())
            };

            return Task.FromResult<IInteractionData>(interaction);
        });
    }

    private string RenderDashboard() =>
        RenderDashboard(_config, _statistics.Snapshot(), _service.ConfigurationIssues);

    internal static string RenderDashboard(
        ChatCheatMonitorConfig config,
        StatisticsSnapshot stats,
        IReadOnlyList<ConfigurationIssue> issues)
    {
        var builder = new StringBuilder();

        builder.Append("""
            <div class="grid grid-cols-1 sm:grid-cols-2 xl:grid-cols-4 gap-4 mb-6">
            """);
        AppendMetric(builder, "Matches", stats.Matches, "ph-magnifying-glass", "text-primary");
        AppendMetric(builder, "Reminders", stats.PrivateReminders + stats.PublicReminders, "ph-chat-circle-text",
            "text-emerald-400");
        AppendMetric(builder, "Suppressed",
            stats.PlayerCooldownSuppressions + stats.ServerCooldownSuppressions, "ph-timer", "text-amber-400");
        AppendMetric(builder, "Targets resolved", stats.TargetsResolved, "ph-user-focus", "text-violet-400");
        builder.Append("</div>");

        builder.Append("""
            <div class="grid grid-cols-1 xl:grid-cols-3 gap-6 mb-6">
              <section class="xl:col-span-2 rounded-xl border border-line bg-surface-alt/30 overflow-hidden">
                <div class="px-5 py-4 border-b border-line flex items-center justify-between gap-3">
                  <div>
                    <h3 class="font-semibold text-foreground">Detection activity</h3>
                    <p class="text-sm text-muted">In-memory, privacy-friendly counters. Raw chat is never stored.</p>
                  </div>
                  <a class="text-sm text-primary hover:underline" href="/Interaction/Render/Webfront::Nav::Admin::ChatCheatMonitor">Refresh</a>
                </div>
                <div class="overflow-x-auto">
                  <table class="w-full text-left">
                    <thead class="text-xs uppercase text-muted border-b border-line">
                      <tr><th class="px-5 py-3">Server</th><th class="px-5 py-3">Category</th><th class="px-5 py-3">Matches</th><th class="px-5 py-3">Private</th><th class="px-5 py-3">Public</th><th class="px-5 py-3">Suppressed</th></tr>
                    </thead>
                    <tbody>
            """);

        if (stats.Categories.Count == 0)
        {
            builder.Append("""
                <tr><td colspan="6" class="px-5 py-8 text-center text-muted">No detections have been recorded since startup.</td></tr>
                """);
        }
        else
        {
            foreach (var category in stats.Categories)
            {
                builder.Append("""<tr class="border-b border-line/60">""")
                    .Append(ServerCell(category.ServerName, category.Game, category.ServerId))
                    .Append(Cell(category.Category))
                    .Append(Cell(category.Matches))
                    .Append(Cell(category.PrivateReminders))
                    .Append(Cell(category.PublicReminders))
                    .Append(Cell(category.Suppressions))
                    .Append("</tr>");
            }
        }

        builder.Append("""
                    </tbody>
                  </table>
                </div>
              </section>
              <section class="rounded-xl border border-line bg-surface-alt/30 p-5">
                <h3 class="font-semibold text-foreground mb-4">Configuration health</h3>
            """);

        if (issues.Count == 0)
        {
            builder.Append("""
                <div class="rounded-lg border border-emerald-500/30 bg-emerald-500/10 p-4 text-emerald-300">
                  <div class="flex items-center gap-2 font-medium"><i class="ph ph-check-circle"></i> Configuration valid</div>
                </div>
                """);
        }
        else
        {
            foreach (var issue in issues)
            {
                var error = issue.Severity == ConfigurationIssueSeverity.Error;
                builder.Append("""<div class="rounded-lg border p-3 mb-3 """)
                    .Append(error
                        ? "border-red-500/30 bg-red-500/10 text-red-300"
                        : "border-amber-500/30 bg-amber-500/10 text-amber-300")
                    .Append("\"><div class=\"flex gap-2\"><i class=\"ph ")
                    .Append(error ? "ph-warning-circle" : "ph-warning")
                    .Append("\"></i><span>")
                    .Append(Encode(issue.Message))
                    .Append("</span></div></div>");
            }
        }

        builder.Append("""<dl class="mt-5 space-y-2 text-sm">""")
            .Append(Definition("Enabled", config.Enabled ? "Yes" : "No"))
            .Append(Definition("Response mode", config.ResponseMode.ToString()))
            .Append(Definition("Player cooldown", $"{config.PlayerCooldownSeconds}s"))
            .Append(Definition("Server cooldown", $"{config.ServerCooldownSeconds}s"))
            .Append(Definition("Categories", config.Categories.Count(category => category.Enabled)))
            .Append(Definition("Staff alerts", config.NotifyStaff ? "Enabled" : "Disabled"))
            .Append(Definition("Started", stats.StartedAt.ToString("u", CultureInfo.InvariantCulture)))
            .Append("</dl>")
            .Append("""<div class="mt-5"><a href="/configuration" class="inline-flex items-center gap-2 text-primary hover:underline"><i class="ph ph-gear"></i> Open configuration</a></div>""")
            .Append("</section></div>");

        builder.Append("""
            <section class="rounded-xl border border-line bg-surface-alt/30 overflow-hidden">
              <div class="px-5 py-4 border-b border-line">
                <h3 class="font-semibold text-foreground">Recent detection outcomes</h3>
                <p class="text-sm text-muted">Only time, server, category and outcome are retained.</p>
              </div>
              <div class="overflow-x-auto">
                <table class="w-full text-left">
                  <thead class="text-xs uppercase text-muted border-b border-line">
                    <tr><th class="px-5 py-3">Time (UTC)</th><th class="px-5 py-3">Server</th><th class="px-5 py-3">Category</th><th class="px-5 py-3">Target</th><th class="px-5 py-3">Outcome</th></tr>
                  </thead><tbody>
            """);

        if (stats.RecentEvents.Count == 0)
        {
            builder.Append("""
                <tr><td colspan="5" class="px-5 py-8 text-center text-muted">No recent events.</td></tr>
                """);
        }
        else
        {
            foreach (var item in stats.RecentEvents)
            {
                builder.Append("""<tr class="border-b border-line/60">""")
                    .Append(Cell(item.When.ToString("u", CultureInfo.InvariantCulture)))
                    .Append(ServerCell(item.ServerName, item.Game, item.ServerId))
                    .Append(Cell(item.Category))
                    .Append(Cell(item.TargetResolved ? "Resolved" : "Unresolved"))
                    .Append(Cell(item.Outcome))
                    .Append("</tr>");
            }
        }

        builder.Append("</tbody></table></div></section>");
        return builder.ToString();
    }

    private static void AppendMetric(
        StringBuilder builder,
        string label,
        long value,
        string icon,
        string colorClass)
    {
        builder.Append("""<div class="rounded-xl border border-line bg-surface-alt/30 p-5">""")
            .Append("""<div class="flex items-center justify-between"><span class="text-sm text-muted">""")
            .Append(Encode(label))
            .Append("""</span><i class="ph """)
            .Append(Encode(icon))
            .Append(' ')
            .Append(Encode(colorClass))
            .Append(""" text-xl"></i></div><div class="mt-2 text-3xl font-bold text-foreground">""")
            .Append(value.ToString("N0", CultureInfo.InvariantCulture))
            .Append("</div></div>");
    }

    private static string Cell(object value, string? extraClasses = null) =>
        $"""<td class="px-5 py-3 text-sm {Encode(extraClasses ?? string.Empty)}">{Encode(value)}</td>""";

    private static string ServerCell(string name, string game, string endpoint) =>
        $"""
        <td class="px-5 py-3 text-sm">
          <div class="flex items-center gap-2">
            <span class="text-foreground font-medium">{Encode(name)}</span>
            <span class="rounded-md border border-primary/30 bg-primary/10 px-1.5 py-0.5 text-xs font-semibold text-primary">{Encode(game)}</span>
          </div>
          <div class="mt-1 font-mono text-xs text-muted">{Encode(endpoint)}</div>
        </td>
        """;

    private static string Definition(string label, object value) =>
        $"""<div class="flex justify-between gap-4"><dt class="text-muted">{Encode(label)}</dt><dd class="text-foreground text-right">{Encode(value)}</dd></div>""";

    private static string Encode(object? value) => WebUtility.HtmlEncode(value?.ToString() ?? string.Empty);

    private void OnConfigurationUpdated(ChatCheatMonitorConfig _) => Register();

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _configurationHandler.Updated -= OnConfigurationUpdated;
        _interactions.UnregisterInteraction(InteractionKey);
    }
}
