namespace Osb.Engine.Core.Providers;

/// <summary>
/// UTF-8 encoding/decoding provider. Implemented by hosted BCL or freestanding platform.
/// </summary>
public interface IUtf8Provider
{
    /// <summary>
    /// Encodes a string to UTF-8 bytes.
    /// </summary>
    byte[] GetBytes(string s);

    /// <summary>
    /// Decodes UTF-8 bytes to a string.
    /// </summary>
    string GetString(byte[] bytes);

    /// <summary>
    /// Decodes UTF-8 bytes to a string (span version).
    /// </summary>
    string GetString(ReadOnlySpan<byte> bytes);

    /// <summary>
    /// Validates that a byte sequence is valid UTF-8.
    /// </summary>
    bool IsValidUtf8(ReadOnlySpan<byte> bytes);
}