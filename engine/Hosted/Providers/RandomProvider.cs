using Osb.Engine.Core.Providers;

namespace Osb.Engine.Hosted.Providers;

/// <summary>
/// Hosted BCL implementation of IRandomProvider.
/// </summary>
public sealed class RandomProvider : IRandomProvider
{
    private readonly Random _random = new();

    public double NextDouble() => _random.NextDouble();

    public int Next(int maxValue) => _random.Next(maxValue);

    public int Next(int minValue, int maxValue) => _random.Next(minValue, maxValue);
}