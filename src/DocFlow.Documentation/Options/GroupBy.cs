namespace DocFlow.Documentation.Options;

/// <summary>How endpoint pages are grouped.</summary>
public enum GroupBy
{
    /// <summary>Group by the operation's first tag (fallback: <c>Untagged</c>).</summary>
    Tag,

    /// <summary>Group by the first segment of the operation's URL path.</summary>
    Path
}
