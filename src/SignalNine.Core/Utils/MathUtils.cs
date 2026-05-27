namespace SignalNine.Core.Utils;

/// <summary>
/// Provides mathematical utility functions.
/// </summary>
public static class MathUtils
{
    /// <summary>
    /// Performs linear interpolation between two values.
    /// </summary>
    /// <param name="a">The starting value.</param>
    /// <param name="b">The ending value.</param>
    /// <param name="t">The interpolation factor (0 to 1).</param>
    /// <returns>The interpolated value.</returns>
    public static float Lerp(float a, float b, float t)
        => a + (b - a) * t;
}
