using System.Text.RegularExpressions;
using Markdig.Renderers;

namespace VanillaNotes.Core.Markdown;

/// <summary>
/// The coloring of blocks of code in an exported web page, as the web version colors them: three
/// families of languages, and in each one comments, strings, numbers, and keywords. A language it
/// does not know is written plain.
/// </summary>
internal static class CodeColoring
{
    /// <summary>The words of the languages written like C, JavaScript, or Java.</summary>
    private const string _cLikeKeywords = "async|await|break|case|catch|class|const|continue|default|delete|do|else|export|extends|false|finally|for|from|function|if|import|in|instanceof|let|new|null|of|return|static|super|switch|this|throw|true|try|typeof|undefined|var|void|while|yield";

    /// <summary>The words of the languages whose comments start with a hash: Python, the shell, YAML.</summary>
    private const string _hashKeywords = "and|as|case|class|def|do|done|elif|else|esac|except|export|False|fi|finally|for|from|function|if|import|in|is|lambda|None|not|or|pass|raise|return|then|True|try|while|with|yield";

    /// <summary>The words of SQL.</summary>
    private const string _sqlKeywords = "select|from|where|insert|into|values|update|set|delete|create|table|index|join|left|right|inner|outer|on|and|or|not|null|order|by|group|having|limit|offset|as|primary|key|references|default|unique|in|is|like|drop";

    /// <summary>The parts of a block of code each family tells apart, by family.</summary>
    private static readonly Dictionary<string, Regex> _families = new(StringComparer.Ordinal)
    {
        ["cLike"] = Family(@"//[^\n]*|/\*[\s\S]*?\*/", "\"'`", _cLikeKeywords, RegexOptions.None),
        ["hash"] = Family(@"#[^\n]*", "\"'", _hashKeywords, RegexOptions.None),
        ["sql"] = Family(@"--[^\n]*", "'", _sqlKeywords, RegexOptions.IgnoreCase),
    };

    /// <summary>The family of each language a block of code may declare.</summary>
    private static readonly Dictionary<string, string> _languages = new(StringComparer.Ordinal)
    {
        ["js"] = "cLike", ["javascript"] = "cLike", ["mjs"] = "cLike", ["ts"] = "cLike", ["typescript"] = "cLike",
        ["json"] = "cLike", ["java"] = "cLike", ["c"] = "cLike", ["cpp"] = "cLike", ["css"] = "cLike",
        ["py"] = "hash", ["python"] = "hash", ["sh"] = "hash", ["bash"] = "hash", ["shell"] = "hash",
        ["yaml"] = "hash", ["yml"] = "hash", ["sql"] = "sql",
    };

    /// <summary>The names of the parts a family tells apart, which are also the names of their colors in the page.</summary>
    private static readonly string[] _kinds = ["comment", "string", "number", "keyword"];

    /// <summary>
    /// Writes a block of code, with every part its language tells apart in a span of its kind.
    /// </summary>
    public static void Write(HtmlRenderer renderer, string code, string language)
    {
        if (!_languages.TryGetValue(language, out string? family))
        {
            renderer.WriteEscape(code);
            return;
        }

        int position = 0;
        foreach (Match match in _families[family].Matches(code))
        {
            string kind = _kinds.First(name => match.Groups[name].Success);
            renderer.WriteEscape(code[position..match.Index]);
            renderer.Write($"<span class=\"token token--{kind}\">").WriteEscape(match.Value).Write("</span>");
            position = match.Index + match.Length;
        }

        renderer.WriteEscape(code[position..]);
    }

    private static Regex Family(string comment, string quotes, string keywords, RegexOptions options)
    {
        string strings = string.Join('|', quotes.Select(quote => $@"{quote}(?:\\[\s\S]|[^{quote}\\])*{quote}"));
        return new Regex($@"(?<comment>{comment})|(?<string>{strings})|(?<number>\b\d+(?:\.\d+)?\b)|(?<keyword>\b(?:{keywords})\b)", options | RegexOptions.CultureInvariant);
    }
}
