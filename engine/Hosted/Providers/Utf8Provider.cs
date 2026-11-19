using System.Text;
using Osb.Engine.Core.Providers;

namespace Osb.Engine.Hosted.Providers;

/// <summary>
/// Hosted BCL implementation of IUtf8Provider.
/// </summary>
public sealed class Utf8Provider : IUtf8Provider
{
    private static readonly Encoding Utf8 = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    public byte[] GetBytes(string s) => Utf8.GetBytes(s ?? string.Empty);

    public string GetString(byte[] bytes) => Utf8.GetString(bytes ?? []);

    public string GetString(ReadOnlySpan<byte> bytes) => Utf8.GetString(bytes);

    public bool IsValidUtf8(ReadOnlySpan<byte> bytes)
    {
        try
        {
            Utf8.GetString(bytes);
            return true;
        }
        catch
        {
            return false;
        }
    }
}