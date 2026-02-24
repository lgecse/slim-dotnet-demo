using Agntcy.Slim;
using SlimDemo.Common;

namespace SlimDemo.Bob;

class Program
{
    static async Task Main(string[] args)
    {
        var server = GetArg(args, "--server") ?? DemoConfig.DefaultServer;
        var secret = GetArg(args, "--shared-secret") ?? DemoConfig.DefaultSecret;
        var remote = GetArg(args, "--remote") ?? "org/alice/v1";
        var iterations = int.Parse(GetArg(args, "--iterations") ?? "10");
        var minNum = int.Parse(GetArg(args, "--min") ?? "1");
        var maxNum = int.Parse(GetArg(args, "--max") ?? "100");
        var noMls = HasFlag(args, "--no-mls");
        var enableMls = !noMls;

        Console.WriteLine("=== SLIM Demo: Bob (Sender) — Odd/Even ===");
        Console.WriteLine();
        Console.WriteLine($"  Language : .NET (C#)");

        Slim.Initialize();

        using var appName = SlimName.Parse("org/bob/v1");
        using var service = Slim.GetGlobalService();
        var app = service.CreateApp(appName, secret);

        var connId = Slim.Connect(server);
        app.Subscribe(app.Name, connId);

        Console.WriteLine($"  Identity : org/bob/v1");
        Console.WriteLine($"  Server   : {server}");
        Console.WriteLine($"  Remote   : {remote}");
        Console.WriteLine($"  Conn ID  : {connId}");
        Console.WriteLine($"  Range    : {minNum}–{maxNum}");
        Console.WriteLine($"  MLS      : {(enableMls ? "ENABLED" : "disabled")}");
        Console.WriteLine();

        using var remoteName = SlimName.Parse(remote);
        app.SetRoute(remoteName, connId);
        Console.WriteLine($"Route set to {remote}");

        var config = new SlimSessionConfig
        {
            SessionType = SlimSessionType.PointToPoint,
            EnableMls = enableMls,
        };

        Console.WriteLine($"Creating session to {remote}...");
        var session = await app.CreateSessionAsync(remoteName, config);
        Console.WriteLine("Ready!");
        Console.WriteLine();

        await Task.Delay(100);

        var rng = new Random();
        for (var i = 0; i < iterations; i++)
        {
            var n = rng.Next(minNum, maxNum + 1);
            var msg = n.ToString();

            try
            {
                await session.PublishAsync(msg);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  !! Error sending message {i + 1}/{iterations}: {ex.Message}");
                continue;
            }

            Console.WriteLine($"  >> Sent    : {n} ({i + 1}/{iterations})");

            try
            {
                var reply = await session.GetMessageAsync(TimeSpan.FromSeconds(5));
                Console.WriteLine($"  << Received: {reply.Text} ({i + 1}/{iterations})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  !! No reply for message {i + 1}/{iterations}: {ex.Message}");
            }

            await Task.Delay(1000);
        }

        Console.WriteLine();
        Console.WriteLine("Done.");
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
