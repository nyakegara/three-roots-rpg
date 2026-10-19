namespace Game.Core.Content;

/// <summary>A content-loading diagnostic tied to a file and, for syntax errors, a source location.</summary>
public sealed record ContentError(string FilePath, string Message, long? LineNumber = null, long? ColumnNumber = null)
{
    public override string ToString() => LineNumber is null
        ? $"{FilePath}: {Message}"
        : $"{FilePath} (line {LineNumber}, column {ColumnNumber}): {Message}";
}
