namespace SlimDemo.Alice.Common;

/// <summary>
/// Odd/even reply logic shared by CLI and GUI.
/// </summary>
public static class OddEvenReply
{
    /// <summary>
    /// Returns "odd", "even", or "not a number" for the given input.
    /// </summary>
    public static string Compute(string text) =>
        long.TryParse(text, out var n) ? (n % 2 == 0 ? "even" : "odd") : "not a number";
}
