using System.Text;

namespace DocFlow.Documentation.Markdown;

/// <summary>
/// Tiny helper on top of <see cref="StringBuilder"/> that enforces LF line endings and
/// guards against trailing whitespace so generated Markdown passes common linters.
/// </summary>
internal sealed class MarkdownWriter
{
    private readonly StringBuilder _buffer = new();

    public void Line() => _buffer.Append('\n');

    public void Line(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            _buffer.Append(text.TrimEnd());
        }
        _buffer.Append('\n');
    }

    public void Heading(int level, string text)
    {
        _buffer.Append(new string('#', Math.Clamp(level, 1, 6)));
        _buffer.Append(' ');
        _buffer.Append(text.Trim());
        _buffer.Append('\n');
    }

    public void Raw(string text)
    {
        // Collapse Windows-style line endings while preserving internal structure.
        foreach (var line in text.Replace("\r\n", "\n").Replace("\r", "\n").Split('\n'))
        {
            _buffer.Append(line.TrimEnd());
            _buffer.Append('\n');
        }
    }

    public override string ToString() => _buffer.ToString();
}
