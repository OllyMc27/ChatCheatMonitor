using System.Collections.Concurrent;
using System.Text.Json;
using Xunit;

namespace ChatCheatMonitor.Tests;

public sealed class DetectionEngineTests
{
    [Theory]
    [InlineData("that player is a hacker")]
    [InlineData("^1H4CK3R spotted")]
    [InlineData("possible h-a-c-k-e-r")]
    public void DetectsNormalizedWholeWordPhrases(string message)
    {
        var engine = CreateEngine("hacker");

        var result = engine.Detect(message);

        Assert.NotNull(result);
        Assert.Equal("cheating", result.Category);
    }

    [Theory]
    [InlineData("hackathon")]
    [InlineData("hacksaw")]
    [InlineData("shellhacker")]
    public void WholeWordModeAvoidsSubstringFalsePositives(string message)
    {
        var engine = CreateEngine("hack");

        Assert.Null(engine.Detect(message));
    }

    [Fact]
    public void SubstringModeCanBeEnabledExplicitly()
    {
        var config = CreateConfig("hack");
        config.Categories[0].MatchMode = PhraseMatchMode.Substring;
        var engine = new DetectionEngine();
        engine.Reload(config);

        Assert.NotNull(engine.Detect("hackathon"));
    }

    [Fact]
    public void ExclusionsSuppressOtherwiseValidMatches()
    {
        var config = CreateConfig("cheat");
        config.ExcludedPhrases.Add("anti cheat");
        var engine = new DetectionEngine();
        engine.Reload(config);

        Assert.Null(engine.Detect("the anti-cheat is enabled"));
        Assert.NotNull(engine.Detect("that player is a cheat"));
    }

    [Fact]
    public void InvalidRegexIsReportedWithoutDisablingValidPhrases()
    {
        var config = CreateConfig("cheat");
        config.Categories[0].RegexPatterns.Add("[");
        var engine = new DetectionEngine();

        engine.Reload(config);

        Assert.Contains(engine.Issues,
            issue => issue.Severity == ConfigurationIssueSeverity.Error &&
                     issue.Message.Contains("Invalid regex", StringComparison.Ordinal));
        Assert.NotNull(engine.Detect("cheat"));
    }

    [Fact]
    public void DuplicateRulesAreReported()
    {
        var config = CreateConfig("cheat");
        config.Categories.Add(new DetectionCategoryConfig
        {
            Name = "second",
            Phrases = ["CHEAT"]
        });
        var engine = new DetectionEngine();

        engine.Reload(config);

        Assert.Contains(engine.Issues, issue => issue.Message.Contains("duplicates", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("he is wallhacking")]
    [InlineData("olly has walls")]
    [InlineData("definitely using wh")]
    [InlineData("that looks like magnetic aim")]
    [InlineData("he is aimbotting")]
    [InlineData("using esp")]
    [InlineData("those are chams")]
    [InlineData("he has redboxes")]
    [InlineData("obvious aimlock")]
    [InlineData("hallhacking again")]
    public void DefaultRulesDetectRecurringHistoricalReportTerms(string message)
    {
        var engine = new DetectionEngine();
        engine.Reload(new ChatCheatMonitorConfig());

        var result = engine.Detect(message);

        Assert.NotNull(result);
        Assert.Equal("cheating", result.Category);
    }

    [Theory]
    [InlineData("the anti-cheat is enabled")]
    [InlineData("anticheat warning received")]
    public void DefaultRulesIgnoreOrdinaryAntiCheatDiscussion(string message)
    {
        var engine = new DetectionEngine();
        engine.Reload(new ChatCheatMonitorConfig());

        Assert.Null(engine.Detect(message));
    }

    private static DetectionEngine CreateEngine(string phrase)
    {
        var engine = new DetectionEngine();
        engine.Reload(CreateConfig(phrase));
        return engine;
    }

    private static ChatCheatMonitorConfig CreateConfig(string phrase) =>
        new()
        {
            Categories =
            [
                new DetectionCategoryConfig
                {
                    Name = "cheating",
                    Phrases = [phrase]
                }
            ]
        };
}

public sealed class ConfigurationMigrationTests
{
    [Fact]
    public void VersionOneConfigurationKeysAreMappedToVersionTwoSettings()
    {
        const string json = """
            {
              "CheatPhrases": ["legacy phrase"],
              "ReminderMessage": "legacy reminder",
              "AlertCooldownSeconds": 90,
              "Debug": true
            }
            """;
        var config = JsonSerializer.Deserialize<ChatCheatMonitorConfig>(json);

        Assert.NotNull(config);
        Assert.True(config.ApplyLegacySettings());
        Assert.Equal(["legacy phrase"], config.Categories[0].Phrases);
        Assert.Equal("legacy reminder", config.ReminderMessages[config.DefaultLanguage]);
        Assert.Equal(90, config.PlayerCooldownSeconds);
        Assert.True(config.Debug);
        Assert.Null(config.LegacyCheatPhrases);
        Assert.Null(config.LegacyReminderMessage);
        Assert.Null(config.LegacyAlertCooldownSeconds);
    }

    [Fact]
    public void ExistingVersionTwoConfigurationInheritsCommunityReportRules()
    {
        const string json = """
            {
              "Categories": [
                {
                  "Name": "cheating",
                  "Enabled": true,
                  "MatchMode": "WholeWord",
                  "Phrases": ["cheat"],
                  "RegexPatterns": [],
                  "ReminderMessages": {}
                }
              ]
            }
            """;
        var options = new JsonSerializerOptions();
        options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
        var config = JsonSerializer.Deserialize<ChatCheatMonitorConfig>(json, options);
        var engine = new DetectionEngine();

        Assert.NotNull(config);
        Assert.Contains("wh", config.CommunityReportPhrases);
        engine.Reload(config);
        Assert.NotNull(engine.Detect("using wh"));
    }
}

public sealed class CooldownTests
{
    [Fact]
    public void ConcurrentAttemptsOnlyAcquireOnce()
    {
        var cooldowns = new ConcurrentDictionary<int, long>();
        var now = new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        var acquired = 0;

        Parallel.For(0, 100, _ =>
        {
            if (CheatMonitorService.TryAcquireCooldown(cooldowns, 42, TimeSpan.FromSeconds(30), now))
                Interlocked.Increment(ref acquired);
        });

        Assert.Equal(1, acquired);
    }

    [Fact]
    public void CooldownCanBeAcquiredAfterExpiry()
    {
        var cooldowns = new ConcurrentDictionary<int, long>();
        var first = new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);

        Assert.True(CheatMonitorService.TryAcquireCooldown(cooldowns, 42, TimeSpan.FromSeconds(30), first));
        Assert.False(CheatMonitorService.TryAcquireCooldown(
            cooldowns, 42, TimeSpan.FromSeconds(30), first.AddSeconds(29)));
        Assert.True(CheatMonitorService.TryAcquireCooldown(
            cooldowns, 42, TimeSpan.FromSeconds(30), first.AddSeconds(30)));
    }
}

public sealed class MessageSplittingTests
{
    [Fact]
    public void LongMessagesAreSplitWithinTheConfiguredLimit()
    {
        const string message =
            "This is a deliberately long reminder that should be split on sensible word boundaries for game chat.";

        var result = CheatMonitorService.SplitMessage(message, 40);

        Assert.True(result.Length > 1);
        Assert.All(result, part => Assert.InRange(part.Length, 1, 40));
        Assert.Equal(message, string.Join(' ', result));
    }
}

public sealed class StatisticsTests
{
    [Fact]
    public void SnapshotContainsOnlyAggregateAndNonContentEventData()
    {
        var statistics = new ChatCheatMonitorStatistics(new ChatCheatMonitorConfig());
        var server = new ServerDisplayInfo("127.0.0.1:28960", "YaMa Test Server", "IW5");
        statistics.RecordInspected();
        statistics.RecordMatch(server, "cheating", true);
        statistics.RecordPrivateReminder(server, "cheating", true);

        var snapshot = statistics.Snapshot();

        Assert.Equal(1, snapshot.MessagesInspected);
        Assert.Equal(1, snapshot.Matches);
        Assert.Equal(1, snapshot.PrivateReminders);
        var category = Assert.Single(snapshot.Categories);
        Assert.Equal("cheating", category.Category);
        Assert.Equal("YaMa Test Server", category.ServerName);
        Assert.Equal("IW5", category.Game);
        Assert.All(snapshot.RecentEvents, item => Assert.False(string.IsNullOrWhiteSpace(item.Outcome)));
    }
}

public sealed class WebfrontDashboardTests
{
    [Fact]
    public void DashboardEncodesRuntimeValuesAndShowsConfigurationHealth()
    {
        var now = new DateTimeOffset(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        var snapshot = new StatisticsSnapshot(
            now,
            1,
            1,
            1,
            0,
            0,
            0,
            0,
            1,
            0,
            [new CategoryStatisticsSnapshot("127.0.0.1:28960", "<script>alert(1)</script>", "IW5", "cheating", 1, 1, 0, 0)],
            [new RecentDetectionEvent(now, "127.0.0.1:28960", "<script>alert(1)</script>", "IW5", "cheating", true, "private reminder")]);

        var html = ChatCheatMonitorWebfront.RenderDashboard(
            new ChatCheatMonitorConfig(),
            snapshot,
            []);

        Assert.Contains("Configuration valid", html, StringComparison.Ordinal);
        Assert.Contains("&lt;script&gt;alert(1)&lt;/script&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>alert(1)</script>", html, StringComparison.Ordinal);
        Assert.Contains("IW5", html, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1:28960", html, StringComparison.Ordinal);
        Assert.Contains("Raw chat is never stored", html, StringComparison.Ordinal);
    }
}
