using Microsoft.Extensions.DependencyInjection;
using SharedLibraryCore;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ChatCheatMonitor;

public class Plugin : IPluginV2
{
    private readonly CheatMonitorService _service;
    private readonly ChatCheatMonitorConfig _config;

    public string Name => "ChatCheatMonitor";
    public string Author => "OllyMc27";
    public string Version => "1.0.0";

    public Plugin(CheatMonitorService service, ChatCheatMonitorConfig config)
    {
        _service = service;
        _config = config;

        IManagementEventSubscriptions.Load += OnLoad;
    }

    public static void RegisterDependencies(IServiceCollection services)
    {
        services.AddSingleton<CheatMonitorService>();
        services.AddConfiguration("ChatCheatMonitor", new ChatCheatMonitorConfig());
    }

    private Task OnLoad(IManager manager, CancellationToken token)
    {
        Console.WriteLine($"[{Name}] by OllyMc27 Loaded. Version {Version}");

        if (_config.Debug)
            Console.WriteLine($"[{Name}][DEBUG] Debug mode enabled — verbose logging active.");

        IGameEventSubscriptions.ClientMessaged += OnClientMessage;

        return Task.CompletedTask;
    }

    private async Task OnClientMessage(ClientMessageEvent chatEvent, CancellationToken token)
    {
        await _service.HandleClientMessageAsync(chatEvent, token);
    }
}
