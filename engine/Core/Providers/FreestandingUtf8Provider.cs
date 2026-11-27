using System;
using System.Collections.Generic;

namespace Osb.Engine.Core.Providers;

/// <summary>
/// Freestanding UTF-8 implementation with no BCL dependencies.
/// Implements deterministic UTF-8 encoding/decoding with explicit invalid-sequence handling.
/// Invalid sequences are replaced with U+FFFD (replacement character) per Unicode standard.
/// </summary>
public class FreestandingUtf8Provider : IUtf8Provider
{
    // Replacement character for invalid UTF-8 sequences
    private const string ReplacementChar = "\uFFFD";
    private static readonly byte[] ReplacementBytes = [0xEF, 0xBF, 0xBD]; // UTF-8 for U+FFFD

    public byte[] GetBytes(string s)
    {
        if (s == null || s.Length == 0) return [];

        // First pass: count bytes needed (correctly handling surrogate pairs)
        int byteCount = 0;
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c <= 0x7F)
            {
                byteCount += 1;
            }
            else if (c <= 0x7FF)
            {
                byteCount += 2;
            }
            else if (c >= 0xD800 && c <= 0xDBFF)
            {
                if (i + 1 < s.Length && s[i + 1] >= 0xDC00 && s[i + 1] <= 0xDFFF)
                {
                    // Valid surrogate pair -> 4 bytes
                    byteCount += 4;
                    i++; // Skip low surrogate
                }
                else
                {
                    // Unpaired high surrogate -> replacement char (3 bytes)
                    byteCount += 3;
                }
            }
            else if (c >= 0xDC00 && c <= 0xDFFF)
            {
                // Unpaired low surrogate -> replacement char (3 bytes)
                byteCount += 3;
            }
            else
            {
                // BMP non-surrogate (0x800 - 0xFFFF) -> 3 bytes
                byteCount += 3;
            }
        }

        byte[] bytes = new byte[byteCount];
        int index = 0;

        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            int codePoint;

            if (c >= 0xD800 && c <= 0xDBFF)
            {
                if (i + 1 < s.Length && s[i + 1] >= 0xDC00 && s[i + 1] <= 0xDFFF)
                {
                    codePoint = ((c - 0xD800) << 10) + (s[i + 1] - 0xDC00) + 0x10000;
                    i++;
                }
                else
                {
                    // Unpaired high surrogate
                    EncodeReplacement(bytes, ref index);
                    continue;
                }
            }
            else if (c >= 0xDC00 && c <= 0xDFFF)
            {
                // Unpaired low surrogate
                EncodeReplacement(bytes, ref index);
                continue;
            }
            else
            {
                codePoint = c;
            }

            EncodeCodePoint(bytes, ref index, codePoint);
        }

        return bytes;
    }

    public string GetString(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return string.Empty;
        return GetString(new ReadOnlySpan<byte>(bytes));
    }

    public string GetString(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length == 0) return string.Empty;

        // Decode with replacement for invalid sequences.
        // Uses a manual growable char buffer (no List<T> / Array.Copy) because
        // freestanding NativeAOT's Array.Copy intrinsic is unreliable for List<T>
        // growth, which silently drops/zeroes elements.
        char[] buffer = new char[16];
        int count = 0;
        int i = 0;

        while (i < bytes.Length)
        {
            byte b = bytes[i];

            if (b <= 0x7F)
            {
                // ASCII
                EnsureCapacity(ref buffer, count + 1);
                buffer[count++] = (char)b;
                i++;
            }
            else if ((b & 0xE0) == 0xC0)
            {
                // 2-byte sequence
                if (i + 1 >= bytes.Length)
                {
                    EnsureCapacity(ref buffer, count + 1);
                    buffer[count++] = ReplacementChar[0];
                    i++;
                    continue;
                }
                byte b2 = bytes[i + 1];
                if ((b2 & 0xC0) != 0x80)
                {
                    EnsureCapacity(ref buffer, count + 1);
                    buffer[count++] = ReplacementChar[0];
                    i++;
                    continue;
                }
                int codePoint = ((b & 0x1F) << 6) | (b2 & 0x3F);
                if (codePoint < 0x80)
                {
                    // Overlong encoding
                    EnsureCapacity(ref buffer, count + 1);
                    buffer[count++] = ReplacementChar[0];
                    i += 2;
                    continue;
                }
                EnsureCapacity(ref buffer, count + 1);
                buffer[count++] = (char)codePoint;
                i += 2;
            }
            else if ((b & 0xF0) == 0xE0)
            {
                // 3-byte sequence
                if (i + 2 >= bytes.Length)
                {
                    EnsureCapacity(ref buffer, count + 1);
                    buffer[count++] = ReplacementChar[0];
                    i++;
                    continue;
                }
                byte b2 = bytes[i + 1];
                byte b3 = bytes[i + 2];
                if ((b2 & 0xC0) != 0x80 || (b3 & 0xC0) != 0x80)
                {
                    EnsureCapacity(ref buffer, count + 1);
                    buffer[count++] = ReplacementChar[0];
                    i++;
                    continue;
                }
                int codePoint = ((b & 0x0F) << 12) | ((b2 & 0x3F) << 6) | (b3 & 0x3F);
                if (codePoint < 0x800)
                {
                    // Overlong encoding
                    EnsureCapacity(ref buffer, count + 1);
                    buffer[count++] = ReplacementChar[0];
                    i += 3;
                    continue;
                }
                if (codePoint >= 0xD800 && codePoint <= 0xDFFF)
                {
                    // Surrogate code points are invalid in UTF-8
                    EnsureCapacity(ref buffer, count + 1);
                    buffer[count++] = ReplacementChar[0];
                    i += 3;
                    continue;
                }
                EnsureCapacity(ref buffer, count + 1);
                buffer[count++] = (char)codePoint;
                i += 3;
            }
            else if ((b & 0xF8) == 0xF0)
            {
                // 4-byte sequence
                if (i + 3 >= bytes.Length)
                {
                    EnsureCapacity(ref buffer, count + 1);
                    buffer[count++] = ReplacementChar[0];
                    i++;
                    continue;
                }
                byte b2 = bytes[i + 1];
                byte b3 = bytes[i + 2];
                byte b4 = bytes[i + 3];
                if ((b2 & 0xC0) != 0x80 || (b3 & 0xC0) != 0x80 || (b4 & 0xC0) != 0x80)
                {
                    EnsureCapacity(ref buffer, count + 1);
                    buffer[count++] = ReplacementChar[0];
                    i++;
                    continue;
                }
                int codePoint = ((b & 0x07) << 18) | ((b2 & 0x3F) << 12) | ((b3 & 0x3F) << 6) | (b4 & 0x3F);
                if (codePoint < 0x10000)
                {
                    // Overlong encoding
                    EnsureCapacity(ref buffer, count + 1);
                    buffer[count++] = ReplacementChar[0];
                    i += 4;
                    continue;
                }
                if (codePoint > 0x10FFFF)
                {
                    // Out of range
                    EnsureCapacity(ref buffer, count + 1);
                    buffer[count++] = ReplacementChar[0];
                    i += 4;
                    continue;
                }
                // Encode as surrogate pair
                codePoint -= 0x10000;
                EnsureCapacity(ref buffer, count + 2);
                buffer[count++] = (char)(0xD800 | (codePoint >> 10));
                buffer[count++] = (char)(0xDC00 | (codePoint & 0x3FF));
                i += 4;
            }
            else
            {
                // Invalid leading byte
                EnsureCapacity(ref buffer, count + 1);
                buffer[count++] = ReplacementChar[0];
                i++;
            }
        }

        return new string(buffer, 0, count);
    }

    private static void EnsureCapacity(ref char[] buffer, int required)
    {
        if (required <= buffer.Length) return;
        int newSize = buffer.Length * 2;
        if (newSize < required) newSize = required;
        var newBuffer = new char[newSize];
        // Manual copy (Array.Copy is unreliable in freestanding NativeAOT).
        for (int i = 0; i < buffer.Length; i++)
            newBuffer[i] = buffer[i];
        buffer = newBuffer;
    }

    public bool IsValidUtf8(ReadOnlySpan<byte> bytes)
    {
        int i = 0;
        while (i < bytes.Length)
        {
            byte b = bytes[i];

            if (b <= 0x7F)
            {
                i++;
            }
            else if ((b & 0xE0) == 0xC0)
            {
                if (i + 1 >= bytes.Length) return false;
                if ((bytes[i + 1] & 0xC0) != 0x80) return false;
                int cp = ((b & 0x1F) << 6) | (bytes[i + 1] & 0x3F);
                if (cp < 0x80) return false; // Overlong
                i += 2;
            }
            else if ((b & 0xF0) == 0xE0)
            {
                if (i + 2 >= bytes.Length) return false;
                if ((bytes[i + 1] & 0xC0) != 0x80) return false;
                if ((bytes[i + 2] & 0xC0) != 0x80) return false;
                int cp = ((b & 0x0F) << 12) | ((bytes[i + 1] & 0x3F) << 6) | (bytes[i + 2] & 0x3F);
                if (cp < 0x800) return false; // Overlong
                if (cp >= 0xD800 && cp <= 0xDFFF) return false; // Surrogate
                i += 3;
            }
            else if ((b & 0xF8) == 0xF0)
            {
                if (i + 3 >= bytes.Length) return false;
                if ((bytes[i + 1] & 0xC0) != 0x80) return false;
                if ((bytes[i + 2] & 0xC0) != 0x80) return false;
                if ((bytes[i + 3] & 0xC0) != 0x80) return false;
                int cp = ((b & 0x07) << 18) | ((bytes[i + 1] & 0x3F) << 12) | ((bytes[i + 2] & 0x3F) << 6) | (bytes[i + 3] & 0x3F);
                if (cp < 0x10000) return false; // Overlong
                if (cp > 0x10FFFF) return false; // Out of range
                i += 4;
            }
            else
            {
                return false;
            }
        }
        return true;
    }

    private static void EncodeCodePoint(byte[] bytes, ref int index, int codePoint)
    {
        if (codePoint <= 0x7F)
        {
            bytes[index++] = (byte)codePoint;
        }
        else if (codePoint <= 0x7FF)
        {
            bytes[index++] = (byte)(0xC0 | (codePoint >> 6));
            bytes[index++] = (byte)(0x80 | (codePoint & 0x3F));
        }
        else if (codePoint <= 0xFFFF)
        {
            bytes[index++] = (byte)(0xE0 | (codePoint >> 12));
            bytes[index++] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
            bytes[index++] = (byte)(0x80 | (codePoint & 0x3F));
        }
        else if (codePoint <= 0x10FFFF)
        {
            bytes[index++] = (byte)(0xF0 | (codePoint >> 18));
            bytes[index++] = (byte)(0x80 | ((codePoint >> 12) & 0x3F));
            bytes[index++] = (byte)(0x80 | ((codePoint >> 6) & 0x3F));
            bytes[index++] = (byte)(0x80 | (codePoint & 0x3F));
        }
        else
        {
            EncodeReplacement(bytes, ref index);
        }
    }

    private static void EncodeReplacement(byte[] bytes, ref int index)
    {
        bytes[index++] = ReplacementBytes[0];
        bytes[index++] = ReplacementBytes[1];
        bytes[index++] = ReplacementBytes[2];
    }
}