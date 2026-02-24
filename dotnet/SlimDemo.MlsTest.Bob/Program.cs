using Agntcy.Slim;
using SlimDemo.Common;

namespace SlimDemo.MlsTest.Bob;

class Program
{
    static async Task Main(string[] args)
    {
        var server = GetArg(args, "--server") ?? DemoConfig.DefaultServer;
        var secret = GetArg(args, "--shared-secret") ?? DemoConfig.DefaultSecret;
        var enableMls = HasFlag(args, "--enable-mls");

        Console.WriteLine("=== SLIM MLS Test: Bob (.NET Receiver) — Odd/Even ===");
        Console.WriteLine();
        Console.WriteLine($"  Identity : org/bob/v1");
        Console.WriteLine($"  Server   : {server}");
        Console.WriteLine($"  MLS      : {(enableMls ? "ENABLED" : "disabled")}");
        Console.WriteLine();

        Slim.Initialize();

        using var appName = SlimName.Parse("org/bob/v1");
        using var service = Slim.GetGlobalService();
        var app = service.CreateApp(appName, secret);

        var connId = Slim.Connect(server);
        app.Subscribe(app.Name, connId);

        Console.WriteLine($"  Conn ID  : {connId}");
        Console.WriteLine();
        Console.WriteLine("Waiting for incoming sessions from Alice...");
        Console.WriteLine();

        while (true)
        {
            SlimSession session;
            try
            {
                session = await app.ListenForSessionAsync();
            }
            catch
            {
                continue;
            }

            Console.WriteLine("[session] New session established!");
            _ = Task.Run(() => SessionHandler.RunAsync(
                session,
                Console.WriteLine));
        }
    }

    static string? GetArg(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
                return args[i + 1];
        }
        return null;
    }

    static bool HasFlag(string[] args, string name) =>
        args.Any(a => a == name);
}
