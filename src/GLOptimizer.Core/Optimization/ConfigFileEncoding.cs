using System.Text;

namespace GLOptimizer.Core.Optimization;

public static class ConfigFileEncoding
{
    public readonly record struct Decoded(string Text, Encoding Encoding, bool Bom);

    public static Decoded Decode(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length >= 2 && bytes[0] == 0xFF && bytes[1] == 0xFE)
        {
            return new Decoded(Encoding.Unicode.GetString(bytes, 2, bytes.Length - 2), Encoding.Unicode, true);
        }

        if (bytes.Length >= 2 && bytes[0] == 0xFE && bytes[1] == 0xFF)
        {
            return new Decoded(Encoding.BigEndianUnicode.GetString(bytes, 2, bytes.Length - 2), Encoding.BigEndianUnicode, true);
        }

        if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
        {
            return new Decoded(Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3), Encoding.UTF8, true);
        }

        var strict = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
        try
        {
            return new Decoded(strict.GetString(bytes), Encoding.UTF8, false);
        }
        catch (DecoderFallbackException)
        {
            return new Decoded(Encoding.Latin1.GetString(bytes), Encoding.Latin1, false);
        }
    }

    public static byte[] Encode(string text, Encoding encoding, bool bom)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(encoding);
        var body = encoding.GetBytes(text);
        if (!bom)
        {
            return body;
        }

        var preamble = encoding.GetPreamble();
        var result = new byte[preamble.Length + body.Length];
        preamble.CopyTo(result, 0);
        body.CopyTo(result, preamble.Length);
        return result;
    }
}
