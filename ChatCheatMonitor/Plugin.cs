using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SharedLibraryCore;
using SharedLibraryCore.Events.Game;
using SharedLibraryCore.Events.Management;
using SharedLibraryCore.Interfaces;
using SharedLibraryCore.Interfaces.Events;

namespace ChatCheatMonitor;

public sealed class Plugin : IPluginV2
{
    private readonly CheatMonitorService _service;
    private readonly ChatCheatMonitorWebfront _webfront;
    private readonly ILogger<Plugin> _logger;
    private bool _disposed;

    public string Name => "ChatCheatMonitor";
    public string Author => "OllyMc27";
    public string Version => Utilities.GetVersionAsString();

    public static void RegisterDependencies(IServiceCollection services)
    {
        services.AddConfiguration(
            "ChatCheatMonitor",
            new ChatCheatMonitorConfig());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<DetectionEngine>();
        services.AddSingleton<ChatCheatMonitorStatistics>();
        services.AddSingleton<CheatMonitorService>();
        services.AddSingleton<ChatCheatMonitorWebfront>();
    }

    public Plugin(
        CheatMonitorService service,
        ChatCheatMonitorWebfront webfront,
        ILogger<Plugin> logger)
    {
        _service = service;
        _webfront = webfront;
        _logger = logger;

        IManagementEventSubscriptions.Load += OnLoad;
        IGameEventSubscriptions.ClientMessaged += OnClientMessage;
        IManagementEventSubscriptions.ClientStateDisposed += OnClientStateDisposed;
        _webfront.Register();

        _logger.LogInformation(
            "[{Name}] {Version} by {Author} loaded with {IssueCount} configuration issue(s)",
            Name,
            Version,
            Author,
            _service.ConfigurationIssues.Count);
    }

    private Task OnLoad(IManager _, CancellationToken __)
    {
        Console.WriteLine($"[{Name}] by {Author} loaded. Version: {Version}");

        if (_service.ConfigurationIssues.Count > 0)
        {
            Console.WriteLine(
                $"[{Name}] configuration contains {_service.ConfigurationIssues.Count} issue(s); check the logs or webfront dashboard.");
        }

        return Task.CompletedTask;
    }

    private Task OnClientMessage(ClientMessageEvent chatEvent, CancellationToken token) =>
        _service.HandleClientMessageAsync(chatEvent, token);

    private Task OnClientStateDisposed(ClientStateDisposeEvent clientEvent, CancellationToken _)
    {
        _service.RemoveClientCooldowns(clientEvent.Client.ClientId);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        IManagementEventSubscriptions.Load -= OnLoad;
        IGameEventSubscriptions.ClientMessaged -= OnClientMessage;
        IManagementEventSubscriptions.ClientStateDisposed -= OnClientStateDisposed;
        _webfront.Dispose();
        _service.Dispose();
        _logger.LogInformation("[{Name}] unloaded", Name);
    }
}
