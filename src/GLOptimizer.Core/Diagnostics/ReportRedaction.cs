namespace GLOptimizer.Core.Diagnostics;

public static class ReportRedaction
{
    public const string UserProfileToken = "%USERPROFILE%";

    public static string Redact(string? text, string? userProfile)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(userProfile))
        {
            return text;
        }

        var trimmed = userProfile.Trim().TrimEnd('\\', '/');
        if (trimmed.Length == 0)
        {
            return text;
        }

        var result = Replace(text, trimmed, UserProfileToken);
        var slashed = trimmed.Replace('\\', '/');
        if (!string.Equals(slashed, trimmed, StringComparison.Ordinal))
        {
            result = Replace(result, slashed, UserProfileToken);
        }

        return result;
    }

    private static string Replace(string text, string search, string token)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        var index = 0;
        while (index < text.Length)
        {
            var found = text.IndexOf(search, index, StringComparison.OrdinalIgnoreCase);
            if (found < 0)
            {
                builder.Append(text, index, text.Length - index);
                break;
            }

            builder.Append(text, index, found - index);
            builder.Append(token);
            index = found + search.Length;
        }

        return builder.ToString();
    }
}
