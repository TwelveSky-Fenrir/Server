using System.Text;

namespace Fenrir.Generators.Analysis.Support;

/// <summary>
///     Small indented text-source assembler (4 spaces/level) — avoids scattering indentation literals across the
///     emitters.
/// </summary>
internal sealed class IndentedWriter
{
    private readonly StringBuilder _builder = new();
    private int _indent;

    public void Line(string text = "")
    {
        if (text.Length == 0)
        {
            _builder.Append('\n');
            return;
        }

        _builder.Append(' ', _indent * 4).Append(text).Append('\n');
    }

    public void OpenBrace()
    {
        Line("{");
        _indent++;
    }

    public void CloseBrace()
    {
        _indent--;
        Line("}");
    }

    /// <summary>Closes a block initialized as an expression (e.g. <c>new T { ... }</c>): emits <c>};</c>.</summary>
    public void CloseBraceSemicolon()
    {
        _indent--;
        Line("};");
    }

    public override string ToString()
    {
        return _builder.ToString();
    }
}
