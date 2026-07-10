using System.Text;
using HoofMark.CSharpTemplating.Abstractions;

namespace HoofMark.CSharpTemplating.Core;

/// <summary>
/// Concrete implementation of <see cref="IOutputWriter"/>.
/// Tracks indentation state and builds content into a <see cref="StringBuilder"/>.
/// </summary>
internal sealed class OutputWriter : IOutputWriter
{
    private readonly StringBuilder _sb = new();
    private readonly string _indentUnit;
    private int _indentLevel;
    private bool _atLineStart = true; // true when the next Write should prepend indentation

    public OutputWriter(string indentUnit = "    ")
    {
        _indentUnit = indentUnit;
    }

    public int IndentLevel => _indentLevel;

    public IOutputWriter Write(string text)
    {
        if (string.IsNullOrEmpty(text))
            return this;

        if (_atLineStart)
        {
            WriteIndent();
            _atLineStart = false;
        }

        _sb.Append(text);
        return this;
    }

    public IOutputWriter WriteLine(string text = "")
    {
        if (!string.IsNullOrEmpty(text))
        {
            if (_atLineStart)
                WriteIndent();

            _sb.Append(text);
        }

        _sb.AppendLine();
        _atLineStart = true;
        return this;
    }

    public IOutputWriter Indent(int levels = 1)
    {
        _indentLevel = Math.Max(0, _indentLevel + levels);
        return this;
    }

    public IOutputWriter Dedent(int levels = 1)
    {
        _indentLevel = Math.Max(0, _indentLevel - levels);
        return this;
    }

    public IOutputWriter Block(string header, Action<IOutputWriter> body, bool trailingSemicolon = false)
    {
        WriteLine(header);
        WriteLine("{");
        Indent();
        body(this);
        Dedent();
        WriteLine(trailingSemicolon ? "};" : "}");
        return this;
    }

    public override string ToString() => _sb.ToString();

    private void WriteIndent()
    {
        for (var i = 0; i < _indentLevel; i++)
            _sb.Append(_indentUnit);
    }
}
