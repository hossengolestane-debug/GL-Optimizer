using GLOptimizer.Core.Backup;
using GLOptimizer.Core.Detection;

namespace GLOptimizer.Core.Updates;

public static class UpdateMetadataRules
{
    public static string? Validate(string? version, string? sha256, string? packageUrl)
    {
        if (PackageVersion.Parse(version) is null)
        {
            return "The update version is not unambiguous.";
        }

        if (!BackupPathRules.IsSha256(sha256))
        {
            return "The update SHA-256 must be 64 hex characters.";
        }

        if (!Uri.TryCreate(packageUrl, UriKind.Absolute, out var uri) || !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            return "The update package URL must be HTTPS.";
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return "The update package URL must not include credentials.";
        }

        return null;
    }
}
