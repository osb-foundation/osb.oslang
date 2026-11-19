namespace Osb.Engine.Core.Providers;

/// <summary>
/// Default UTF-8 provider implementation using the freestanding codec.
/// This is the platform-independent default for Core. Hosted layer may substitute BCL implementation.
/// </summary>
internal sealed class DefaultUtf8Provider : FreestandingUtf8Provider
{
}