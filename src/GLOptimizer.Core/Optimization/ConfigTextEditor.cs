using System.Globalization;
using System.Text;

namespace GLOptimizer.Core.Optimization;

public static class ConfigTextEditor
{
    public static bool TryApply(string text, string format, IReadOnlyList<OptimizationEdit> edits, out string updated, out string? error)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(edits);
        updated = text;
        var kind = (format ?? string.Empty).Trim().TrimStart('.').ToLowerInvariant();
        foreach (var edit in edits)
        {
            bool applied;
            string next;
            if (kind is "ini" or "cfg" or "txt")
            {
                applied = TryIni(updated, edit, out next, out error);
            }
            else if (kind == "json")
            {
                applied = TryJson(updated, edit, out next, out error);
            }
            else if (kind == "xml")
            {
                applied = TryXml(updated, edit, out next, out error);
            }
            else
            {
                applied = Fail(out next, out error);
            }

            if (!applied)
            {
                updated = text;
                return false;
            }

            updated = next;
        }

        error = null;
        return true;
    }

    private static bool Fail(out string next, out string? error)
    {
        next = string.Empty;
        error = "This file format is not edited.";
        return false;
    }

    private static bool TryIni(string text, OptimizationEdit edit, out string updated, out string? error)
    {
        var replacements = 0;
        var mismatch = false;
        var builder = new StringBuilder(text.Length);
        var index = 0;
        while (index < text.Length)
        {
            var start = index;
            while (index < text.Length && text[index] is not '\n' and not '\r')
            {
                index++;
            }

            var line = text[start..index];
            var breakStart = index;
            if (index < text.Length && text[index] == '\r')
            {
                index++;
            }

            if (index < text.Length && text[index] == '\n')
            {
                index++;
            }

            if (!TryIniLine(line, edit, out var replaced, out var bad))
            {
                mismatch = bad;
                break;
            }

            builder.Append(replaced ?? line);
            if (replaced is not null)
            {
                replacements++;
            }

            builder.Append(text[breakStart..index]);
        }

        if (mismatch)
        {
            updated = text;
            error = "The current value no longer matches the preview.";
            return false;
        }

        if (replacements == 0)
        {
            updated = text;
            error = "The key was not found, so it was not added.";
            return false;
        }

        updated = builder.ToString();
        error = null;
        return true;
    }

    private static bool TryIniLine(string line, OptimizationEdit edit, out string? replaced, out bool mismatch)
    {
        replaced = null;
        mismatch = false;
        var trimmed = line.TrimStart();
        if (trimmed.Length == 0 || trimmed[0] is ';' or '#' or '[')
        {
            return true;
        }

        var split = line.IndexOf('=');
        if (split <= 0)
        {
            return true;
        }

        if (!line[..split].Trim().Equals(edit.Key, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var valueStart = split + 1;
        while (valueStart < line.Length && line[valueStart] is ' ' or '\t')
        {
            valueStart++;
        }

        var quoted = valueStart < line.Length && line[valueStart] is '"' or '\'';
        var tokenStart = quoted ? valueStart + 1 : valueStart;
        var tokenEnd = tokenStart;
        if (quoted)
        {
            while (tokenEnd < line.Length && line[tokenEnd] != line[valueStart])
            {
                tokenEnd++;
            }
        }
        else
        {
            while (tokenEnd < line.Length && line[tokenEnd] is not ' ' and not '\t')
            {
                tokenEnd++;
            }
        }

        var token = line[tokenStart..tokenEnd];
        if (!string.Equals(token, edit.CurrentRaw, StringComparison.Ordinal))
        {
            mismatch = !string.Equals(token, edit.NewRaw, StringComparison.Ordinal);
            return !mismatch;
        }

        replaced = string.Concat(line.AsSpan(0, tokenStart), edit.NewRaw, line.AsSpan(tokenEnd));
        return true;
    }

    private static bool TryJson(string text, OptimizationEdit edit, out string updated, out string? error)
    {
        var replacements = 0;
        var builder = new StringBuilder(text.Length);
        var index = 0;
        var depth = 0;
        while (index < text.Length)
        {
            if (text[index] == '"')
            {
                var nameStart = index;
                if (!TryReadJsonString(text, ref index, out var name))
                {
                    updated = text;
                    error = "The JSON value could not be read.";
                    return false;
                }

                if (depth is < 1 or > 3 || !IsPropertyName(text, nameStart) || !name.Equals(edit.Key, StringComparison.OrdinalIgnoreCase))
                {
                    builder.Append(text.AsSpan(nameStart, index - nameStart));
                    continue;
                }

                var cursor = index;
                while (cursor < text.Length && char.IsWhiteSpace(text[cursor]))
                {
                    cursor++;
                }

                if (cursor >= text.Length || text[cursor] != ':')
                {
                    builder.Append(text.AsSpan(nameStart, index - nameStart));
                    continue;
                }

                cursor++;
                while (cursor < text.Length && char.IsWhiteSpace(text[cursor]))
                {
                    cursor++;
                }

                if (!TryReadJsonValue(text, cursor, out var raw, out var valueEnd, out var kind))
                {
                    updated = text;
                    error = "The JSON value could not be read.";
                    return false;
                }

                if (!string.Equals(raw, edit.CurrentRaw, StringComparison.Ordinal))
                {
                    if (!string.Equals(raw, edit.NewRaw, StringComparison.Ordinal))
                    {
                        updated = text;
                        error = "The current value no longer matches the preview.";
                        return false;
                    }

                    builder.Append(text.AsSpan(nameStart, valueEnd - nameStart));
                    index = valueEnd;
                    continue;
                }

                builder.Append(text.AsSpan(nameStart, cursor - nameStart));
                builder.Append(FormatJson(kind, edit.NewRaw));
                replacements++;
                index = valueEnd;
                continue;
            }

            if (text[index] == '{')
            {
                depth++;
            }
            else if (text[index] == '}' && depth > 0)
            {
                depth--;
            }

            builder.Append(text[index]);
            index++;
        }

        if (replacements == 0)
        {
            updated = text;
            error = "The key was not found, so it was not added.";
            return false;
        }

        updated = builder.ToString();
        error = null;
        return true;
    }

    private static bool IsPropertyName(string text, int quoteIndex)
    {
        var cursor = quoteIndex - 1;
        while (cursor >= 0 && char.IsWhiteSpace(text[cursor]))
        {
            cursor--;
        }

        return cursor >= 0 && text[cursor] is '{' or ',';
    }

    private static bool TryReadJsonString(string text, ref int index, out string value)
    {
        value = string.Empty;
        if (index >= text.Length || text[index] != '"')
        {
            return false;
        }

        index++;
        var builder = new StringBuilder();
        while (index < text.Length)
        {
            var character = text[index++];
            if (character == '\\')
            {
                if (index >= text.Length)
                {
                    return false;
                }

                builder.Append(text[index++]);
                continue;
            }

            if (character == '"')
            {
                value = builder.ToString();
                return true;
            }

            builder.Append(character);
        }

        return false;
    }

    private static bool TryReadJsonValue(string text, int index, out string raw, out int end, out char kind)
    {
        raw = string.Empty;
        end = index;
        kind = '?';
        if (index >= text.Length)
        {
            return false;
        }

        if (text[index] == '"')
        {
            var cursor = index;
            if (!TryReadJsonString(text, ref cursor, out raw))
            {
                return false;
            }

            end = cursor;
            kind = '"';
            return true;
        }

        if (text.AsSpan(index).StartsWith("true"))
        {
            raw = "true";
            end = index + 4;
            kind = 'b';
            return true;
        }

        if (text.AsSpan(index).StartsWith("false"))
        {
            raw = "false";
            end = index + 5;
            kind = 'b';
            return true;
        }

        var finish = index;
        if (finish < text.Length && text[finish] == '-')
        {
            finish++;
        }

        var digits = finish;
        while (finish < text.Length && char.IsAsciiDigit(text[finish]))
        {
            finish++;
        }

        if (digits == finish)
        {
            return false;
        }

        raw = text[index..finish];
        end = finish;
        kind = 'n';
        return true;
    }

    private static string FormatJson(char kind, string value) => kind switch
    {
        '"' => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"",
        'b' => value,
        _ => value
    };

    private static bool TryXml(string text, OptimizationEdit edit, out string updated, out string? error)
    {
        if (edit.NewRaw.IndexOfAny(['<', '>', '&']) >= 0)
        {
            updated = text;
            error = "The replacement is not a plain XML value.";
            return false;
        }

        var replacements = 0;
        var builder = new StringBuilder(text.Length);
        var index = 0;
        while (index < text.Length)
        {
            if (text[index] == '<')
            {
                if (TryXmlElement(text, index, edit, out var elementEnd, out var elementText, out var mismatch))
                {
                    if (mismatch)
                    {
                        updated = text;
                        error = "The current value no longer matches the preview.";
                        return false;
                    }

                    builder.Append(elementText);
                    replacements++;
                    index = elementEnd;
                    continue;
                }
            }

            if (TryXmlAttribute(text, index, edit, out var attributeEnd, out var attributeText, out var attributeMismatch))
            {
                if (attributeMismatch)
                {
                    updated = text;
                    error = "The current value no longer matches the preview.";
                    return false;
                }

                builder.Append(attributeText);
                replacements++;
                index = attributeEnd;
                continue;
            }

            builder.Append(text[index]);
            index++;
        }

        if (replacements == 0)
        {
            updated = text;
            error = "The key was not found, so it was not added.";
            return false;
        }

        updated = builder.ToString();
        error = null;
        return true;
    }

    private static bool TryXmlElement(string text, int index, OptimizationEdit edit, out int end, out string replaced, out bool mismatch)
    {
        end = index;
        replaced = string.Empty;
        mismatch = false;
        if (text[index] != '<')
        {
            return false;
        }

        var nameStart = index + 1;
        var nameEnd = nameStart;
        while (nameEnd < text.Length && (char.IsLetterOrDigit(text[nameEnd]) || text[nameEnd] is '_' or '-' or '.'))
        {
            nameEnd++;
        }

        if (nameEnd == nameStart || nameEnd >= text.Length || text[nameEnd] != '>')
        {
            return false;
        }

        if (!text[nameStart..nameEnd].Equals(edit.Key, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var valueStart = nameEnd + 1;
        var close = "</" + text[nameStart..nameEnd] + ">";
        var closeAt = text.IndexOf(close, valueStart, StringComparison.OrdinalIgnoreCase);
        if (closeAt < 0)
        {
            return false;
        }

        var inner = text[valueStart..closeAt];
        var raw = inner.Trim();
        end = closeAt + close.Length;
        if (!string.Equals(raw, edit.CurrentRaw, StringComparison.Ordinal))
        {
            mismatch = !string.Equals(raw, edit.NewRaw, StringComparison.Ordinal);
            replaced = text[index..end];
            return true;
        }

        var leading = inner.Length - inner.TrimStart().Length;
        var trailing = inner.Length - inner.TrimEnd().Length;
        var suffix = trailing == 0 ? string.Empty : inner[^trailing..];
        replaced = text[index..valueStart] + inner[..leading] + edit.NewRaw + suffix + text[closeAt..end];
        return true;
    }

    private static bool TryXmlAttribute(string text, int index, OptimizationEdit edit, out int end, out string replaced, out bool mismatch)
    {
        end = index;
        replaced = string.Empty;
        mismatch = false;
        if (index > 0 && (char.IsLetterOrDigit(text[index - 1]) || text[index - 1] is '_' or '-' or '.'))
        {
            return false;
        }

        var nameEnd = index;
        while (nameEnd < text.Length && (char.IsLetterOrDigit(text[nameEnd]) || text[nameEnd] is '_' or '-' or '.'))
        {
            nameEnd++;
        }

        if (nameEnd == index || !text[index..nameEnd].Equals(edit.Key, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var cursor = nameEnd;
        while (cursor < text.Length && char.IsWhiteSpace(text[cursor]))
        {
            cursor++;
        }

        if (cursor >= text.Length || text[cursor] != '=')
        {
            return false;
        }

        cursor++;
        while (cursor < text.Length && char.IsWhiteSpace(text[cursor]))
        {
            cursor++;
        }

        if (cursor >= text.Length || text[cursor] is not '"' and not '\'')
        {
            return false;
        }

        var quote = text[cursor];
        var valueStart = cursor + 1;
        var valueEnd = text.IndexOf(quote, valueStart);
        if (valueEnd < 0)
        {
            return false;
        }

        end = valueEnd + 1;
        var raw = text[valueStart..valueEnd];
        if (!string.Equals(raw, edit.CurrentRaw, StringComparison.Ordinal))
        {
            mismatch = !string.Equals(raw, edit.NewRaw, StringComparison.Ordinal);
            replaced = text[index..end];
            return true;
        }

        replaced = string.Concat(text.AsSpan(index, valueStart - index), edit.NewRaw, text.AsSpan(valueEnd, 1));
        return true;
    }
}
