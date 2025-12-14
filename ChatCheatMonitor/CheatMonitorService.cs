using Microsoft.Extensions.Logging;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ChatCheatMonitor;

public class CheatMonitorService
{
    private readonly ChatCheatMonitorConfig _config;
    private readonly ILogger<CheatMonitorService> _logger;

    private readonly Dictionary<int, DateTime> _cooldowns = new();

    public CheatMonitorService(ChatCheatMonitorConfig config, ILogger<CheatMonitorService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public async Task HandleClientMessageAsync(ClientMessageEvent chatEvent, CancellationToken token)
    {
        var client = chatEvent.Origin;
        var server = client?.CurrentServer;
        var msg = chatEvent.Message ?? string.Empty;

        if (client == null || server == null)
            return;

        if (string.IsNullOrWhiteSpace(msg))
            return;

        if (_config.Debug)
            _logger.LogDebug("[ChatCheatMonitor] Received chat from {Player}: \"{Message}\"", client.Name, msg);

        if (!IsCheatRelated(msg))
        {
            if (_config.Debug)
                _logger.LogDebug("[ChatCheatMonitor] No cheat phrase detected.");
            return;
        }

        if (_config.Debug)
            _logger.LogDebug("[ChatCheatMonitor] Cheat-related message detected for {Player}.", client.Name);

        if (_cooldowns.TryGetValue(client.ClientId, out var last))
        {
            var since = (DateTime.UtcNow - last).TotalSeconds;
            if (since < _config.AlertCooldownSeconds)
            {
                if (_config.Debug)
                    _logger.LogDebug(
                        "[ChatCheatMonitor] Cooldown active for {Player}. Remaining: {Remaining:0.0}s",
                        client.Name, _config.AlertCooldownSeconds - since
                    );
                return;
            }
        }

        _cooldowns[client.ClientId] = DateTime.UtcNow;

        if (_config.Debug)
            _logger.LogDebug("[ChatCheatMonitor] Cooldown passed. Sending reminder.");

        await server.ExecuteCommandAsync($"say {_config.ReminderMessage}", token);
    }

    private bool IsCheatRelated(string message)
    {
        var lower = message.ToLowerInvariant();

        bool match = _config.CheatPhrases.Any(p =>
            !string.IsNullOrWhiteSpace(p) &&
            lower.Contains(p.ToLowerInvariant())
        );

        if (_config.Debug)
            _logger.LogDebug("[ChatCheatMonitor] Phrase match result: {Match}", match);

        return match;
    }
}
