using SlimDemo.Common;

namespace SlimDemo.Alice;

class Program
{
    static async Task Main(string[] args)
    {
        var server = GetArg(args, "--server") ?? DemoConfig.DefaultServer;
        var secret = GetArg(args, "--shared-secret") ?? DemoConfig.DefaultSecret;

        Console.WriteLine("=== SLIM Demo: Alice (.NET Receiver) ===");
        Console.WriteLine();

        var (app, connId) = AliceReceiver.CreateAndConnect(server, secret);

        Console.WriteLine($"  Identity : {DemoConfig.Identity}");
        Console.WriteLine($"  Server   : {server}");
        Console.WriteLine($"  Conn ID  : {connId}");
        Console.WriteLine();
        Console.WriteLine("Waiting for incoming sessions from Bob...");
        Console.WriteLine();

        while (true)
        {
            try
            {
                var session = await app.ListenForSessionAsync();
                Console.WriteLine("[session] New session established!");
                _ = Task.Run(() => SessionHandler.RunAsync(session, Console.WriteLine));
            }
            catch (Exception)
            {
                // timeout — just retry
            }
        }
    }

    static string? GetArg(string[] args, string name)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == name)
                return args[i + 1];
        }
        return null;
    }
}
