using ThreeDManager.Domain.Entities;

namespace ThreeDManager.Web.Presentation;

/// <summary>
/// <see cref="PrintImport.ErrorMessage"/> carries two different things: the parser warnings of a
/// successfully parsed import, or the reason an import failed. Only <see cref="PrintImport.Status"/>
/// tells them apart, so classification lives here instead of being re-derived in each view.
/// </summary>
public static class PrintImportMessagePresentation
{
    /// <summary>
    /// Separator used to fold several parser warnings into the single
    /// <see cref="PrintImport.ErrorMessage"/> column, and to split them back out for display.
    /// </summary>
    public const string MessageSeparator = " | ";

    public static bool HasMessage(string? errorMessage)
    {
        return !string.IsNullOrWhiteSpace(errorMessage);
    }

    /// <summary>
    /// A message on a parsed import is advisory: the file was read, but some fields were missing.
    /// </summary>
    public static bool IsWarning(string? status, string? errorMessage)
    {
        return HasMessage(errorMessage)
            && PrintImportStatus.Normalize(status) == PrintImportStatus.Parsed;
    }

    /// <summary>
    /// Any message that is not a parsed-import warning is a failure, so an unexpected status still
    /// surfaces its message rather than silently hiding it.
    /// </summary>
    public static bool IsFailure(string? status, string? errorMessage)
    {
        return HasMessage(errorMessage) && !IsWarning(status, errorMessage);
    }

    public static IReadOnlyList<string> SplitMessages(string? errorMessage)
    {
        if (!HasMessage(errorMessage))
        {
            return Array.Empty<string>();
        }

        return errorMessage!.Split(
            MessageSeparator,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }
}
