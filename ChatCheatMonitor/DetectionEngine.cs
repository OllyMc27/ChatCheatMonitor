using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using SharedLibraryCore;

namespace ChatCheatMonitor;

public enum ConfigurationIssueSeverity
{
    Warning,
    Error
}

public sealed record ConfigurationIssue(ConfigurationIssueSeverity Severity, string Message);

public sealed record DetectionMatch(string Category, string Pattern, bool IsRegex);

public sealed class DetectionEngine
{
    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(100);
    private volatile DetectionSnapshot _snapshot = DetectionSnapshot.Empty;

    public IReadOnlyList<ConfigurationIssue> Issues => _snapshot.Issues;

    public void Reload(ChatCheatMonitorConfig config)
    {
        var issues = ChatCheatMonitorConfigurationValidator.Validate(config);
        var categories = new List<CompiledCategory>();

        foreach (var category in config.Categories.Where(category => category.Enabled))
        {
            var phraseSources = category.Phrases
                .Select(phrase => (Phrase: phrase, Mode: category.MatchMode));
            if (category.Name.Equals("cheating", StringComparison.OrdinalIgnoreCase))
            {
                phraseSources = phraseSources.Concat(
                    config.CommunityReportPhrases.Select(phrase => (Phrase: phrase, Mode: PhraseMatchMode.WholeWord)));
            }

            var phrases = phraseSources
                .Where(source => !string.IsNullOrWhiteSpace(source.Phrase))
                .Select(source => new CompiledPhrase(
                    source.Phrase,
                    Normalize(source.Phrase, config.EnableLeetNormalization),
                    source.Mode))
                .Where(phrase => phrase.Normalized.Length > 0)
                .DistinctBy(phrase => (phrase.Normalized, phrase.Mode))
                .ToArray();

            var regexes = new List<CompiledRegex>();
            foreach (var pattern in category.RegexPatterns.Where(pattern => !string.IsNullOrWhiteSpace(pattern)))
            {
                try
                {
                    regexes.Add(new CompiledRegex(pattern,
                        new Regex(pattern, RegexOptions.Compiled | RegexOptions.IgnoreCase |
                                           RegexOptions.CultureInvariant, RegexTimeout)));
                }
                catch (ArgumentException)
                {
                    // The validator reports the exact invalid rule. Keep the remaining rules active.
                }
            }

            categories.Add(new CompiledCategory(category.Name, phrases, regexes.ToArray()));
        }

        _snapshot = new DetectionSnapshot(
            config.EnableLeetNormalization,
            categories.ToArray(),
            NormalizeMany(
                config.ExcludedPhrases.Concat(config.CommunityReportExclusions),
                config.EnableLeetNormalization),
            issues.ToArray());
    }

    public DetectionMatch? Detect(string message, IEnumerable<string>? additionalExclusions = null)
    {
        var snapshot = _snapshot;
        var cleanMessage = (message ?? string.Empty).StripColors().Normalize(NormalizationForm.FormKC);
        var normalizedMessage = Normalize(cleanMessage, snapshot.EnableLeetNormalization);

        if (normalizedMessage.Length == 0)
        {
            return null;
        }

        var exclusions = snapshot.Exclusions;
        if (exclusions.Any(exclusion => ContainsWholePhrase(normalizedMessage, exclusion)) ||
            NormalizeMany(additionalExclusions, snapshot.EnableLeetNormalization)
                .Any(exclusion => ContainsWholePhrase(normalizedMessage, exclusion)))
        {
            return null;
        }

        foreach (var category in snapshot.Categories)
        {
            foreach (var phrase in category.Phrases)
            {
                var matched = phrase.Mode == PhraseMatchMode.Substring
                    ? normalizedMessage.Contains(phrase.Normalized, StringComparison.Ordinal)
                    : ContainsWholePhrase(normalizedMessage, phrase.Normalized);

                if (matched)
                {
                    return new DetectionMatch(category.Name, phrase.Original, false);
                }
            }

            foreach (var regex in category.RegexPatterns)
            {
                try
                {
                    if (regex.Regex.IsMatch(cleanMessage))
                    {
                        return new DetectionMatch(category.Name, regex.Original, true);
                    }
                }
                catch (RegexMatchTimeoutException)
                {
                    // Treat an expensive expression as a non-match for this message.
                }
            }
        }

        return null;
    }

    public static string Normalize(string value, bool enableLeetNormalization)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var source = value.StripColors().Normalize(NormalizationForm.FormKC);
        var builder = new StringBuilder(source.Length);
        var previousWasSpace = true;

        foreach (var rawCharacter in source)
        {
            var character = char.ToLower(rawCharacter, CultureInfo.InvariantCulture);
            if (enableLeetNormalization)
            {
                character = character switch
                {
                    '0' => 'o',
                    '1' or '!' or '|' => 'i',
                    '3' => 'e',
                    '4' or '@' => 'a',
                    '5' or '$' => 's',
                    '7' => 't',
                    '9' => 'g',
                    _ => character
                };
            }

            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
                previousWasSpace = false;
                continue;
            }

            if (!previousWasSpace)
            {
                builder.Append(' ');
                previousWasSpace = true;
            }
        }

        return CollapseSeparatedLetters(builder.ToString().Trim());
    }

    private static string CollapseSeparatedLetters(string value)
    {
        var tokens = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 3)
            return value;

        var result = new List<string>(tokens.Length);
        for (var index = 0; index < tokens.Length;)
        {
            var end = index;
            while (end < tokens.Length && tokens[end].Length == 1 && char.IsLetter(tokens[end][0]))
                end++;

            if (end - index >= 3)
            {
                result.Add(string.Concat(tokens[index..end]));
                index = end;
                continue;
            }

            result.Add(tokens[index]);
            index++;
        }

        return string.Join(' ', result);
    }

    internal static bool ContainsWholePhrase(string message, string phrase)
    {
        if (message.Length == 0 || phrase.Length == 0)
        {
            return false;
        }

        var startIndex = 0;
        while ((startIndex = message.IndexOf(phrase, startIndex, StringComparison.Ordinal)) >= 0)
        {
            var beforeIsBoundary = startIndex == 0 || !char.IsLetterOrDigit(message[startIndex - 1]);
            var endIndex = startIndex + phrase.Length;
            var afterIsBoundary = endIndex == message.Length || !char.IsLetterOrDigit(message[endIndex]);

            if (beforeIsBoundary && afterIsBoundary)
            {
                return true;
            }

            startIndex++;
        }

        return false;
    }

    private static string[] NormalizeMany(IEnumerable<string>? values, bool enableLeetNormalization) =>
        values?.Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Normalize(value, enableLeetNormalization))
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray() ?? [];

    private sealed record CompiledPhrase(string Original, string Normalized, PhraseMatchMode Mode);
    private sealed record CompiledRegex(string Original, Regex Regex);
    private sealed record CompiledCategory(string Name, CompiledPhrase[] Phrases, CompiledRegex[] RegexPatterns);

    private sealed record DetectionSnapshot(
        bool EnableLeetNormalization,
        CompiledCategory[] Categories,
        string[] Exclusions,
        ConfigurationIssue[] Issues)
    {
        public static readonly DetectionSnapshot Empty = new(false, [], [], []);
    }
}

public static class ChatCheatMonitorConfigurationValidator
{
    public static IReadOnlyList<ConfigurationIssue> Validate(ChatCheatMonitorConfig config)
    {
        var issues = new List<ConfigurationIssue>();

        if (string.IsNullOrWhiteSpace(config.ReportCommand))
            issues.Add(Error("ReportCommand cannot be empty."));
        if (config.PlayerCooldownSeconds < 0)
            issues.Add(Error("PlayerCooldownSeconds cannot be negative."));
        if (config.ServerCooldownSeconds < 0)
            issues.Add(Error("ServerCooldownSeconds cannot be negative."));
        if (config.MinimumTargetNameLength is < 2 or > 32)
            issues.Add(Error("MinimumTargetNameLength must be between 2 and 32."));
        if (config.MaxMessageLength is < 40 or > 1000)
            issues.Add(Error("MaxMessageLength must be between 40 and 1000."));
        if (config.StaffAlertThreshold < 1)
            issues.Add(Error("StaffAlertThreshold must be at least 1."));
        if (config.StaffAlertWindowSeconds < 1)
            issues.Add(Error("StaffAlertWindowSeconds must be at least 1."));
        if (config.RecentEventLimit is < 0 or > 500)
            issues.Add(Error("RecentEventLimit must be between 0 and 500."));
        if (config.Categories.Count == 0)
            issues.Add(Error("At least one detection category is required."));
        if (config.ReminderMessages.Count == 0)
            issues.Add(Error("At least one reminder message is required."));
        if (config.CommunityReportPhrases.Count == 0)
            issues.Add(Warning("CommunityReportPhrases is empty; report-derived detection is disabled."));
        if (!config.ReminderMessages.ContainsKey(config.DefaultLanguage))
            issues.Add(Warning($"No global reminder message exists for DefaultLanguage '{config.DefaultLanguage}'."));

        var seenCategories = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPhrases = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var category in config.Categories)
        {
            if (string.IsNullOrWhiteSpace(category.Name))
            {
                issues.Add(Error("A detection category has an empty name."));
                continue;
            }

            if (!seenCategories.Add(category.Name))
                issues.Add(Error($"Detection category '{category.Name}' is duplicated."));
            if (category.Enabled && category.Phrases.Count == 0 && category.RegexPatterns.Count == 0)
                issues.Add(Warning($"Enabled category '{category.Name}' has no phrases or regex patterns."));

            foreach (var phrase in category.Phrases.Where(phrase => !string.IsNullOrWhiteSpace(phrase)))
            {
                var normalized = DetectionEngine.Normalize(phrase, config.EnableLeetNormalization);
                if (seenPhrases.TryGetValue(normalized, out var existingCategory))
                {
                    issues.Add(Warning(
                        $"Phrase '{phrase}' in '{category.Name}' duplicates a rule in '{existingCategory}'."));
                }
                else
                {
                    seenPhrases[normalized] = category.Name;
                }
            }

            foreach (var pattern in category.RegexPatterns.Where(pattern => !string.IsNullOrWhiteSpace(pattern)))
            {
                try
                {
                    _ = new Regex(pattern, RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
                }
                catch (ArgumentException exception)
                {
                    issues.Add(Error($"Invalid regex in '{category.Name}': {exception.Message}"));
                }
            }
        }

        foreach (var (serverId, serverOverride) in config.ServerOverrides)
        {
            if (string.IsNullOrWhiteSpace(serverId))
                issues.Add(Error("ServerOverrides contains an empty server ID."));
            if (serverOverride.PlayerCooldownSeconds < 0)
                issues.Add(Error($"Server override '{serverId}' has a negative player cooldown."));
            if (serverOverride.ServerCooldownSeconds < 0)
                issues.Add(Error($"Server override '{serverId}' has a negative server cooldown."));
        }

        return issues;
    }

    private static ConfigurationIssue Error(string message) =>
        new(ConfigurationIssueSeverity.Error, message);

    private static ConfigurationIssue Warning(string message) =>
        new(ConfigurationIssueSeverity.Warning, message);
}
