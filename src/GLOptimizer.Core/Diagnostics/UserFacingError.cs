namespace GLOptimizer.Core.Diagnostics;

public static class UserFacingError
{
    public const string Unexpected =
        "GL Optimizer ran into an unexpected problem. The details were written to the log.";

    public const string FileAccess =
        "GL Optimizer could not read or write a file in its local data folder.";

    public const string Permission =
        "GL Optimizer does not have permission to use its local data folder.";

    public const string MissingPath =
        "A required local folder or file for GL Optimizer is missing.";

    public static string From(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return exception switch
        {
            UnauthorizedAccessException => Permission,
            FileNotFoundException or DirectoryNotFoundException => MissingPath,
            IOException => FileAccess,
            _ => Unexpected
        };
    }
}
