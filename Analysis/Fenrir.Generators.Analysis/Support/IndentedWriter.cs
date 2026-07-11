using System.Text;

namespace Fenrir.Generators.Analysis.Support;

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
