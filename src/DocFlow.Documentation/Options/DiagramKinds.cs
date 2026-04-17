namespace DocFlow.Documentation.Options;

/// <summary>
/// Bitmask selecting which diagram kinds to emit in the documentation bundle.
/// </summary>
[Flags]
public enum DiagramKinds
{
    None = 0,
    Class = 1 << 0,
    Er = 1 << 1,
    Sequence = 1 << 2,
    Context = 1 << 3,
    Flow = 1 << 4,
    All = Class | Er | Sequence | Context | Flow
}
