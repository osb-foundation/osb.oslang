namespace Osb.Engine.Core.Providers;

/// <summary>
/// Random number provider. Implemented by hosted BCL or freestanding platform.
/// </summary>
public interface IRandomProvider
{
    /// <summary>
    /// Returns a random double in [0.0, 1.0).
    /// </summary>
    double NextDouble();

    /// <summary>
    /// Returns a random integer in [0, maxValue).
    /// </summary>
    int Next(int maxValue);

    /// <summary>
    /// Returns a random integer in [minValue, maxValue).
    /// </summary>
    int Next(int minValue, int maxValue);
}