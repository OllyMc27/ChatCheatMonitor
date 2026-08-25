using System.Collections.Concurrent;

namespace ChatCheatMonitor;

public sealed record StatisticsSnapshot(
    DateTimeOffset StartedAt,
    long MessagesInspected,
    long Matches,
    long PrivateReminders,
    long PublicReminders,
    long PlayerCooldownSuppressions,
    long ServerCooldownSuppressions,
    long IgnoredMessages,
    long TargetsResolved,
    long StaffAlerts,
    IReadOnlyList<CategoryStatisticsSnapshot> Categories,
    IReadOnlyList<RecentDetectionEvent> RecentEvents);

public sealed record CategoryStatisticsSnapshot(
    string ServerId,
    string ServerName,
    string Game,
    string Category,
    long Matches,
    long PrivateReminders,
    long PublicReminders,
    long Suppressions);

public sealed record RecentDetectionEvent(
    DateTimeOffset When,
    string ServerId,
    string ServerName,
    string Game,
    string Category,
    bool TargetResolved,
    string Outcome);

public readonly record struct ServerDisplayInfo(string Id, string Name, string Game);

public sealed class ChatCheatMonitorStatistics
{
    private readonly ChatCheatMonitorConfig _config;
    private readonly ConcurrentDictionary<(string ServerId, string Category), CategoryCounters> _categories = new();
    private readonly ConcurrentQueue<RecentDetectionEvent> _recentEvents = new();
    private DateTimeOffset _startedAt = DateTimeOffset.UtcNow;
    private long _messagesInspected;
    private long _matches;
    private long _privateReminders;
    private long _publicReminders;
    private long _playerCooldownSuppressions;
    private long _serverCooldownSuppressions;
    private long _ignoredMessages;
    private long _targetsResolved;
    private long _staffAlerts;

    public ChatCheatMonitorStatistics(ChatCheatMonitorConfig config)
    {
        _config = config;
    }

    public void RecordInspected() => Interlocked.Increment(ref _messagesInspected);
    public void RecordIgnored() => Interlocked.Increment(ref _ignoredMessages);

    public void RecordMatch(ServerDisplayInfo server, string category, bool targetResolved)
    {
        Interlocked.Increment(ref _matches);
        if (targetResolved)
            Interlocked.Increment(ref _targetsResolved);

        var counters = GetCategory(server, category);
        Interlocked.Increment(ref counters.Matches);
        AddRecent(server, category, targetResolved, "matched");
    }

    public void RecordPrivateReminder(ServerDisplayInfo server, string category, bool targetResolved)
    {
        Interlocked.Increment(ref _privateReminders);
        Interlocked.Increment(ref GetCategory(server, category).PrivateReminders);
        AddRecent(server, category, targetResolved, "private reminder");
    }

    public void RecordPublicReminder(ServerDisplayInfo server, string category, bool targetResolved)
    {
        Interlocked.Increment(ref _publicReminders);
        Interlocked.Increment(ref GetCategory(server, category).PublicReminders);
        AddRecent(server, category, targetResolved, "public reminder");
    }

    public void RecordPlayerCooldownSuppression(ServerDisplayInfo server, string category, bool targetResolved)
    {
        Interlocked.Increment(ref _playerCooldownSuppressions);
        Interlocked.Increment(ref GetCategory(server, category).Suppressions);
        AddRecent(server, category, targetResolved, "player cooldown");
    }

    public void RecordServerCooldownSuppression(ServerDisplayInfo server, string category, bool targetResolved)
    {
        Interlocked.Increment(ref _serverCooldownSuppressions);
        Interlocked.Increment(ref GetCategory(server, category).Suppressions);
        AddRecent(server, category, targetResolved, "server cooldown");
    }

    public void RecordStaffAlert() => Interlocked.Increment(ref _staffAlerts);

    public StatisticsSnapshot Snapshot()
    {
        var categories = _categories
            .Select(pair => new CategoryStatisticsSnapshot(
                pair.Key.ServerId,
                pair.Value.ServerName,
                pair.Value.Game,
                pair.Key.Category,
                Interlocked.Read(ref pair.Value.Matches),
                Interlocked.Read(ref pair.Value.PrivateReminders),
                Interlocked.Read(ref pair.Value.PublicReminders),
                Interlocked.Read(ref pair.Value.Suppressions)))
            .OrderBy(snapshot => snapshot.ServerId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(snapshot => snapshot.Category, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new StatisticsSnapshot(
            _startedAt,
            Interlocked.Read(ref _messagesInspected),
            Interlocked.Read(ref _matches),
            Interlocked.Read(ref _privateReminders),
            Interlocked.Read(ref _publicReminders),
            Interlocked.Read(ref _playerCooldownSuppressions),
            Interlocked.Read(ref _serverCooldownSuppressions),
            Interlocked.Read(ref _ignoredMessages),
            Interlocked.Read(ref _targetsResolved),
            Interlocked.Read(ref _staffAlerts),
            categories,
            _recentEvents.ToArray().OrderByDescending(item => item.When).ToArray());
    }

    public void Reset()
    {
        _categories.Clear();
        while (_recentEvents.TryDequeue(out _))
        {
        }

        Interlocked.Exchange(ref _messagesInspected, 0);
        Interlocked.Exchange(ref _matches, 0);
        Interlocked.Exchange(ref _privateReminders, 0);
        Interlocked.Exchange(ref _publicReminders, 0);
        Interlocked.Exchange(ref _playerCooldownSuppressions, 0);
        Interlocked.Exchange(ref _serverCooldownSuppressions, 0);
        Interlocked.Exchange(ref _ignoredMessages, 0);
        Interlocked.Exchange(ref _targetsResolved, 0);
        Interlocked.Exchange(ref _staffAlerts, 0);
        _startedAt = DateTimeOffset.UtcNow;
    }

    private CategoryCounters GetCategory(ServerDisplayInfo server, string category)
    {
        var counters = _categories.GetOrAdd(
            (server.Id, category),
            _ => new CategoryCounters(server.Name, server.Game));
        counters.ServerName = server.Name;
        counters.Game = server.Game;
        return counters;
    }

    private void AddRecent(ServerDisplayInfo server, string category, bool targetResolved, string outcome)
    {
        var limit = Math.Clamp(_config.RecentEventLimit, 0, 500);
        if (limit == 0)
            return;

        _recentEvents.Enqueue(new RecentDetectionEvent(
            DateTimeOffset.UtcNow,
            server.Id,
            server.Name,
            server.Game,
            category,
            targetResolved,
            outcome));

        while (_recentEvents.Count > limit && _recentEvents.TryDequeue(out _))
        {
        }
    }

    private sealed class CategoryCounters(string serverName, string game)
    {
        public string ServerName = serverName;
        public string Game = game;
        public long Matches;
        public long PrivateReminders;
        public long PublicReminders;
        public long Suppressions;
    }
}
