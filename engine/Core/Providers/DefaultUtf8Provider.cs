namespace Osb.Engine.Core.Providers;

/// <summary>
/// Default UTF-8 provider implementation using .NET BCL.
/// For freestanding targets, this should be replaced with a custom implementation.
/// </summary>
internal sealed class DefaultUtf8Provider : IUtf8Provider
{
    public byte[] GetBytes(string s) => System.Text.Encoding.UTF8.GetBytes(s ?? string.Empty);

    public string GetString(byte[] bytes) => System.Text.Encoding.UTF8.GetString(bytes ?? []);

    public string GetString(ReadOnlySpan<byte> bytes) => System.Text.Encoding.UTF8.GetString(bytes);

    public bool IsValidUtf8(ReadOnlySpan<byte> bytes)
    {
        try
        {
            System.Text.Encoding.UTF8.GetString(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }
}