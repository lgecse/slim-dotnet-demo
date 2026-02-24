using Agntcy.Slim;

namespace SlimDemo.Common;

/// <summary>
/// Shared session handling for the odd/even demo. Used by both CLI and GUI.
/// </summary>
public static class SessionHandler
{
    /// <summary>
    /// Runs the odd/even session loop: receive number, reply odd/even, log via callback.
    /// </summary>
    /// <param name="session">The SLIM session.</param>
    /// <param name="log">Callback for log messages (received, replied, errors).</param>
    /// <param name="cancellationToken">Optional cancellation token.</param>
    public static async Task RunAsync(
        SlimSession session,
        Action<string> log,
        CancellationToken cancellationToken = default)
    {
        try
        {
            using (session)
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var msg = await session.GetMessageAsync(TimeSpan.FromSeconds(60));
                        log($"  << Received: {msg.Text}");

                        var reply = OddEvenReply.Compute(msg.Text);
                        await session.ReplyAsync(msg, reply);
                        log($"  >> Replied : {reply}");
                    }
                    catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        log($"[session] Ended: {ex.Message}");
                        break;
                    }
                }
            }
            log("[session] Closed");
        }
        catch (Exception ex)
        {
            log($"[session] Error: {ex.Message}");
        }
    }
}
