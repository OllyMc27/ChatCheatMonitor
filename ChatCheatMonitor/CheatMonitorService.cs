using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Database.Models;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Interfaces;

namespace ChatCheatMonitor;

public sealed record MessageAnalysis(
    bool Matched,
    string? Category,
    string? Pattern,
    string? TargetName,
    string Reason);

public sealed class CheatMonitorService : IDisposable
{
    private readonly ChatCheatMonitorConfig _config;
    private readonly IConfigurationHandlerV2<ChatCheatMonitorConfig> _configurationHandler;
    private readonly DetectionEngine _detectionEngine;
    private readonly ChatCheatMonitorStatistics _statistics;
    private readonly ILogger<CheatMonitorService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ConcurrentDictionary<PlayerCooldownKey, long> _playerCooldowns = new();
    private readonly ConcurrentDictionary<string, long> _serverCooldowns = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, StaffAlertWindow> _staffAlertWindows =
        new(StringComparer.OrdinalIgnoreCase);
    private int _messagesSincePrune;
    private bool _disposed;

    public CheatMonitorService(
        ChatCheatMonitorConfig config,
        IConfigurationHandlerV2<ChatCheatMonitorConfig> configurationHandler,
        DetectionEngine detectionEngine,
        ChatCheatMonitorStatistics statistics,
        ILogger<CheatMonitorService> logger,
        TimeProvider timeProvider)
    {
        _config = config;
        _configurationHandler = configurationHandler;
        _detectionEngine = detectionEngine;
        _statistics = statistics;
        _logger = logger;
        _timeProvider = timeProvider;
        _configurationHandler.Updated += OnConfigurationUpdated;
        RefreshConfiguration();
    }

    public IReadOnlyList<ConfigurationIssue> ConfigurationIssues => _detectionEngine.Issues;

    public void RefreshConfiguration()
    {
        if (_config.ApplyLegacySettings())
        {
            _logger.LogWarning(
                "[ChatCheatMonitor] Applied legacy v1 configuration keys in memory. Update the JSON file to the v2 format when convenient.");
        }

        _detectionEngine.Reload(_config);

        foreach (var issue in _detectionEngine.Issues)
        {
            if (issue.Severity == ConfigurationIssueSeverity.Error)
                _logger.LogError("[ChatCheatMonitor] Configuration: {Message}", issue.Message);
            else
                _logger.LogWarning("[ChatCheatMonitor] Configuration: {Message}", issue.Message);
        }

        _logger.LogInformation(
            "[ChatCheatMonitor] Detection rules refreshed. Enabled={Enabled}, Categories={CategoryCount}, Issues={IssueCount}",
            _config.Enabled,
            _config.Categories.Count(category => category.Enabled),
            _detectionEngine.Issues.Count);
    }

    public async Task HandleClientMessageAsync(ClientMessageEvent chatEvent, CancellationToken token)
    {
        var client = chatEvent.Origin;
        var server = client?.CurrentServer;
        var message = chatEvent.Message ?? string.Empty;

        if (client is null || server is null || string.IsNullOrWhiteSpace(message))
            return;

        _statistics.RecordInspected();
        var settings = GetEffectiveSettings(server.Id);

        if (!settings.Enabled || settings.ResponseMode == ChatResponseMode.Disabled ||
            (_config.IgnoreTeamMessages && chatEvent.IsTeamMessage))
        {
            _statistics.RecordIgnored();
            return;
        }

        if (_config.IgnoreReportCommands && IsReportCommand(message, settings.ReportCommand))
        {
            _statistics.RecordIgnored();
            return;
        }

        var match = _detectionEngine.Detect(message, settings.ExcludedPhrases);
        if (match is null)
            return;

        var target = _config.EnableTargetAssistance
            ? FindMentionedTarget(message, client, server.ConnectedClients)
            : null;
        var targetResolved = target is not null;
        var serverDisplay = new ServerDisplayInfo(
            server.Id,
            string.IsNullOrWhiteSpace(server.Hostname) ? server.Id : server.Hostname.StripColors(),
            server.GameCode.ToString());

        _statistics.RecordMatch(serverDisplay, match.Category, targetResolved);
        PruneCooldownsIfNeeded();

        if (!TryAcquireCooldown(
                _playerCooldowns,
                new PlayerCooldownKey(server.Id, client.ClientId),
                settings.PlayerCooldown))
        {
            _statistics.RecordPlayerCooldownSuppression(serverDisplay, match.Category, targetResolved);
            await MaybeNotifyStaffAsync(server.ConnectedClients, server.Id, match.Category, target, token);
            return;
        }

        var reminder = FormatReminder(match.Category, client.CleanedName, target?.CleanedName, settings);
        var messages = SplitMessage(reminder, Math.Clamp(_config.MaxMessageLength, 40, 1000));

        if (settings.ResponseMode is ChatResponseMode.Private or ChatResponseMode.Both)
        {
            await client.TellAsync(messages, token);
            _statistics.RecordPrivateReminder(serverDisplay, match.Category, targetResolved);
        }

        if (settings.ResponseMode is ChatResponseMode.Public or ChatResponseMode.Both)
        {
            if (TryAcquireCooldown(_serverCooldowns, server.Id, settings.ServerCooldown))
            {
                await server.BroadcastAsync(messages, token: token);
                _statistics.RecordPublicReminder(serverDisplay, match.Category, targetResolved);
            }
            else
            {
                _statistics.RecordServerCooldownSuppression(serverDisplay, match.Category, targetResolved);
            }
        }

        if (_config.Debug)
        {
            _logger.LogDebug(
                "[ChatCheatMonitor] Matched {Category}/{Pattern} for {Player} on {Server}; target={Target}; mode={Mode}",
                match.Category,
                match.Pattern,
                client.Name,
                server.Id,
                target?.CleanedName ?? "unresolved",
                settings.ResponseMode);
        }

        await MaybeNotifyStaffAsync(server.ConnectedClients, server.Id, match.Category, target, token);
    }

    public MessageAnalysis AnalyzeMessage(
        string message,
        string serverId,
        EFClient? origin,
        IReadOnlyList<EFClient>? connectedClients)
    {
        var settings = GetEffectiveSettings(serverId);
        if (!settings.Enabled)
            return new MessageAnalysis(false, null, null, null, "Plugin is disabled for this server.");
        if (_config.IgnoreReportCommands && IsReportCommand(message, settings.ReportCommand))
            return new MessageAnalysis(false, null, null, null, "Message is already a report command.");

        var match = _detectionEngine.Detect(message, settings.ExcludedPhrases);
        if (match is null)
            return new MessageAnalysis(false, null, null, null, "No detection rule matched.");

        var target = origin is not null && connectedClients is not null && _config.EnableTargetAssistance
            ? FindMentionedTarget(message, origin, connectedClients)
            : null;

        return new MessageAnalysis(
            true,
            match.Category,
            match.Pattern,
            target?.CleanedName,
            target is null ? "Matched; no unique online target was identified." : "Matched and resolved an online target.");
    }

    public void RemoveClientCooldowns(int clientId)
    {
        foreach (var key in _playerCooldowns.Keys.Where(key => key.ClientId == clientId))
            _playerCooldowns.TryRemove(key, out _);
    }

    public string GetStatusSummary()
    {
        var snapshot = _statistics.Snapshot();
        return
            $"ChatCheatMonitor {(_config.Enabled ? "enabled" : "disabled")} | mode={_config.ResponseMode} | " +
            $"matches={snapshot.Matches} | reminders={snapshot.PrivateReminders + snapshot.PublicReminders} | " +
            $"suppressed={snapshot.PlayerCooldownSuppressions + snapshot.ServerCooldownSuppressions} | " +
            $"config issues={ConfigurationIssues.Count}";
    }

    private void OnConfigurationUpdated(ChatCheatMonitorConfig _) => RefreshConfiguration();

    private EffectiveSettings GetEffectiveSettings(string serverId)
    {
        var serverOverride = FindServerOverride(serverId);
        var exclusions = _config.ExcludedPhrases
            .Concat(serverOverride?.ExcludedPhrases ?? [])
            .ToArray();

        return new EffectiveSettings(
            serverOverride?.Enabled ?? _config.Enabled,
            serverOverride?.ResponseMode ?? _config.ResponseMode,
            string.IsNullOrWhiteSpace(serverOverride?.ReportCommand)
                ? _config.ReportCommand
                : serverOverride!.ReportCommand!,
            TimeSpan.FromSeconds(Math.Max(0,
                serverOverride?.PlayerCooldownSeconds ?? _config.PlayerCooldownSeconds)),
            TimeSpan.FromSeconds(Math.Max(0,
                serverOverride?.ServerCooldownSeconds ?? _config.ServerCooldownSeconds)),
            string.IsNullOrWhiteSpace(serverOverride?.Language)
                ? _config.DefaultLanguage
                : serverOverride!.Language!,
            exclusions);
    }

    private ServerOverrideConfig? FindServerOverride(string serverId)
    {
        var exact = _config.ServerOverrides.FirstOrDefault(pair =>
            pair.Key.Equals(serverId, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrEmpty(exact.Key))
            return exact.Value;

        var wildcard = _config.ServerOverrides.FirstOrDefault(pair => pair.Key == "*");
        return string.IsNullOrEmpty(wildcard.Key) ? null : wildcard.Value;
    }

    private string FormatReminder(
        string categoryName,
        string senderName,
        string? targetName,
        EffectiveSettings settings)
    {
        var category = _config.Categories.FirstOrDefault(item =>
            item.Name.Equals(categoryName, StringComparison.OrdinalIgnoreCase));
        var template = GetLocalizedValue(category?.ReminderMessages, settings.Language)
                       ?? GetLocalizedValue(_config.ReminderMessages, settings.Language)
                       ?? "^1REMINDER:^3 Use {reportCommand} {target} <reason> to report suspected {category}.";

        var safeTarget = string.IsNullOrWhiteSpace(targetName) ? "<player>" : targetName.StripColors();
        return template
            .Replace("{target}", safeTarget, StringComparison.OrdinalIgnoreCase)
            .Replace("{player}", senderName.StripColors(), StringComparison.OrdinalIgnoreCase)
            .Replace("{category}", categoryName, StringComparison.OrdinalIgnoreCase)
            .Replace("{reportCommand}", settings.ReportCommand, StringComparison.OrdinalIgnoreCase)
            .Replace("{cooldown}",
                ((int)settings.PlayerCooldown.TotalSeconds).ToString(CultureInfo.InvariantCulture),
                StringComparison.OrdinalIgnoreCase)
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Trim();
    }

    private string? GetLocalizedValue(IReadOnlyDictionary<string, string>? messages, string language)
    {
        if (messages is null || messages.Count == 0)
            return null;

        static string? Find(IReadOnlyDictionary<string, string> source, string key) =>
            source.FirstOrDefault(pair => pair.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Value;

        var exact = Find(messages, language);
        if (!string.IsNullOrWhiteSpace(exact))
            return exact;

        var separatorIndex = language.IndexOfAny(['-', '_']);
        if (separatorIndex > 0)
        {
            var neutral = Find(messages, language[..separatorIndex]);
            if (!string.IsNullOrWhiteSpace(neutral))
                return neutral;
        }

        var configuredDefault = Find(messages, _config.DefaultLanguage);
        if (!string.IsNullOrWhiteSpace(configuredDefault))
            return configuredDefault;

        var english = Find(messages, "en");
        return !string.IsNullOrWhiteSpace(english) ? english : messages.Values.FirstOrDefault();
    }

    private EFClient? FindMentionedTarget(string message, EFClient origin, IReadOnlyList<EFClient> clients)
    {
        var normalizedMessage = DetectionEngine.Normalize(message, _config.EnableLeetNormalization);
        var candidates = clients
            .Where(client => client.ClientId != origin.ClientId)
            .Select(client => new
            {
                Client = client,
                NormalizedName = DetectionEngine.Normalize(client.CleanedName, _config.EnableLeetNormalization)
            })
            .Where(candidate => candidate.NormalizedName.Length >= _config.MinimumTargetNameLength)
            .Where(candidate => DetectionEngine.ContainsWholePhrase(normalizedMessage, candidate.NormalizedName))
            .OrderByDescending(candidate => candidate.NormalizedName.Length)
            .ToArray();

        if (candidates.Length == 0)
            return null;
        if (candidates.Length > 1 &&
            candidates[0].NormalizedName.Length == candidates[1].NormalizedName.Length)
            return null;

        return candidates[0].Client;
    }

    private async Task MaybeNotifyStaffAsync(
        IReadOnlyList<EFClient> clients,
        string serverId,
        string category,
        EFClient? target,
        CancellationToken token)
    {
        if (!_config.NotifyStaff)
            return;

        var now = _timeProvider.GetUtcNow();
        var window = TimeSpan.FromSeconds(Math.Max(1, _config.StaffAlertWindowSeconds));
        var threshold = Math.Max(1, _config.StaffAlertThreshold);
        var key = $"{serverId}|{target?.ClientId.ToString(CultureInfo.InvariantCulture) ?? "unknown"}|{category}";
        var state = _staffAlertWindows.GetOrAdd(key, _ => new StaffAlertWindow(now));
        int count;

        lock (state)
        {
            if (now - state.WindowStartedAt > window)
            {
                state.WindowStartedAt = now;
                state.Count = 0;
                state.AlertSent = false;
            }

            state.Count++;
            count = state.Count;
            if (state.AlertSent || count < threshold)
                return;
            state.AlertSent = true;
        }

        var message = _config.StaffAlertMessage
            .Replace("{target}", target?.CleanedName?.StripColors() ?? "<unknown>", StringComparison.OrdinalIgnoreCase)
            .Replace("{category}", category, StringComparison.OrdinalIgnoreCase)
            .Replace("{count}", count.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)
            .Replace("{window}", ((int)window.TotalSeconds).ToString(CultureInfo.InvariantCulture),
                StringComparison.OrdinalIgnoreCase)
            .Replace('\r', ' ')
            .Replace('\n', ' ');

        foreach (var staffMember in clients.Where(client => client.Level >= _config.StaffMinimumPermission))
            await staffMember.TellAsync(SplitMessage(message, Math.Clamp(_config.MaxMessageLength, 40, 1000)), token);

        _statistics.RecordStaffAlert();
    }

    private bool TryAcquireCooldown<TKey>(
        ConcurrentDictionary<TKey, long> cooldowns,
        TKey key,
        TimeSpan cooldown) where TKey : notnull
        => TryAcquireCooldown(cooldowns, key, cooldown, _timeProvider.GetUtcNow());

    internal static bool TryAcquireCooldown<TKey>(
        ConcurrentDictionary<TKey, long> cooldowns,
        TKey key,
        TimeSpan cooldown,
        DateTimeOffset now) where TKey : notnull
    {
        if (cooldown <= TimeSpan.Zero)
            return true;

        var nowTicks = now.UtcTicks;
        while (true)
        {
            if (!cooldowns.TryGetValue(key, out var previousTicks))
            {
                if (cooldowns.TryAdd(key, nowTicks))
                    return true;
                continue;
            }

            if (TimeSpan.FromTicks(nowTicks - previousTicks) < cooldown)
                return false;
            if (cooldowns.TryUpdate(key, nowTicks, previousTicks))
                return true;
        }
    }

    private void PruneCooldownsIfNeeded()
    {
        if (Interlocked.Increment(ref _messagesSincePrune) < 256)
            return;
        Interlocked.Exchange(ref _messagesSincePrune, 0);

        var maximumCooldown = Math.Max(_config.PlayerCooldownSeconds, _config.ServerCooldownSeconds);
        foreach (var serverOverride in _config.ServerOverrides.Values)
        {
            maximumCooldown = Math.Max(maximumCooldown, serverOverride.PlayerCooldownSeconds ?? 0);
            maximumCooldown = Math.Max(maximumCooldown, serverOverride.ServerCooldownSeconds ?? 0);
        }

        var cutoff = _timeProvider.GetUtcNow().Subtract(TimeSpan.FromSeconds(maximumCooldown + 300)).UtcTicks;
        foreach (var pair in _playerCooldowns.Where(pair => pair.Value < cutoff))
            _playerCooldowns.TryRemove(pair.Key, out _);
        foreach (var pair in _serverCooldowns.Where(pair => pair.Value < cutoff))
            _serverCooldowns.TryRemove(pair.Key, out _);

        var staffCutoff = _timeProvider.GetUtcNow()
            .Subtract(TimeSpan.FromSeconds(Math.Max(1, _config.StaffAlertWindowSeconds) + 300));
        foreach (var pair in _staffAlertWindows)
        {
            bool expired;
            lock (pair.Value)
                expired = pair.Value.WindowStartedAt < staffCutoff;

            if (expired)
                _staffAlertWindows.TryRemove(pair.Key, out _);
        }
    }

    private static bool IsReportCommand(string message, string reportCommand)
    {
        var cleanMessage = message.StripColors().TrimStart();
        return cleanMessage.Equals(reportCommand, StringComparison.OrdinalIgnoreCase) ||
               cleanMessage.StartsWith(reportCommand + " ", StringComparison.OrdinalIgnoreCase);
    }

    internal static string[] SplitMessage(string message, int maximumLength)
    {
        if (message.Length <= maximumLength)
            return [message];

        var result = new List<string>();
        var remaining = message;
        while (remaining.Length > maximumLength)
        {
            var splitAt = remaining.LastIndexOf(' ', maximumLength);
            if (splitAt < maximumLength / 2)
                splitAt = maximumLength;

            result.Add(remaining[..splitAt].TrimEnd());
            remaining = remaining[splitAt..].TrimStart();
        }

        if (remaining.Length > 0)
            result.Add(remaining);
        return result.ToArray();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _configurationHandler.Updated -= OnConfigurationUpdated;
    }

    private readonly record struct PlayerCooldownKey(string ServerId, int ClientId);

    private sealed class StaffAlertWindow(DateTimeOffset windowStartedAt)
    {
        public DateTimeOffset WindowStartedAt = windowStartedAt;
        public int Count;
        public bool AlertSent;
    }

    private sealed record EffectiveSettings(
        bool Enabled,
        ChatResponseMode ResponseMode,
        string ReportCommand,
        TimeSpan PlayerCooldown,
        TimeSpan ServerCooldown,
        string Language,
        string[] ExcludedPhrases);
}
