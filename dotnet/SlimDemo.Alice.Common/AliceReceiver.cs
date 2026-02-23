using Agntcy.Slim;

namespace SlimDemo.Alice.Common;

/// <summary>
/// Shared SLIM setup for the Alice demo. Initializes Slim, creates app, connects, and subscribes.
/// </summary>
public static class AliceReceiver
{
    /// <summary>
    /// Initializes SLIM and creates a connected Alice app ready to listen for sessions.
    /// </summary>
    /// <param name="server">SLIM server endpoint (e.g. http://localhost:46357).</param>
    /// <param name="secret">Shared secret (min 32 chars).</param>
    /// <returns>The created app and connection ID.</returns>
    public static (SlimApp app, ulong connId) CreateAndConnect(string server, string secret)
    {
        Slim.Initialize();

        using var appName = SlimName.Parse(DemoConfig.Identity);
        using var service = Slim.GetGlobalService();
        var app = service.CreateApp(appName, secret);
        var connId = Slim.Connect(server);
        app.Subscribe(app.Name, connId);

        return (app, connId);
    }
}
