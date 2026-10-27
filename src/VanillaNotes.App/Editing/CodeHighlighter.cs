using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;
using ColorCode;
using ColorCode.Common;
using ColorCode.Parsing;
using ColorCode.Styling;

namespace VanillaNotes.App.Editing;

/// <summary>
/// Colors a block of code of a note, with ColorCode: the language the block declares names the
/// parts of the code, and each part takes the color of its kind, in a light or a dark set. A
/// language ColorCode does not know is written plain.
/// </summary>
public static class CodeHighlighter
{
    /// <summary>The name ColorCode knows each language by, for the names a block of code may declare.</summary>
    private static readonly Dictionary<string, string> _languages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["c#"] = LanguageId.CSharp, ["cs"] = LanguageId.CSharp, ["csharp"] = LanguageId.CSharp,
        ["js"] = LanguageId.JavaScript, ["javascript"] = LanguageId.JavaScript, ["mjs"] = LanguageId.JavaScript,
        ["ts"] = LanguageId.TypeScript, ["typescript"] = LanguageId.TypeScript,
        ["py"] = LanguageId.Python, ["python"] = LanguageId.Python,
        ["sql"] = LanguageId.Sql, ["json"] = LanguageId.Json, ["html"] = LanguageId.Html, ["xml"] = LanguageId.Xml,
        ["css"] = LanguageId.Css, ["java"] = LanguageId.Java, ["cpp"] = LanguageId.Cpp, ["c++"] = LanguageId.Cpp,
        ["c"] = LanguageId.Cpp, ["php"] = LanguageId.Php, ["ps1"] = LanguageId.PowerShell,
        ["powershell"] = LanguageId.PowerShell, ["md"] = LanguageId.Markdown, ["markdown"] = LanguageId.Markdown,
        ["fs"] = LanguageId.FSharp, ["fsharp"] = LanguageId.FSharp, ["vb"] = LanguageId.VbDotNet,
    };

    /// <summary>
    /// The runs of a block of code, colored when its language is known.
    /// </summary>
    /// <param name="language">The language the block declares, or nothing.</param>
    /// <param name="dark">Whether the block is shown on a dark background.</param>
    public static IReadOnlyList<Inline> Color(string code, string? language, bool dark)
    {
        ILanguage? known = language is not null && _languages.TryGetValue(language.Trim(), out string? id) ? Languages.FindById(id) : null;
        if (known is null)
        {
            return Plain(code);
        }

        return new RunFormatter(dark ? StyleDictionary.DefaultDark : StyleDictionary.DefaultLight).Format(code, known);
    }

    /// <summary>A block of code with no color: its lines, with a break between them.</summary>
    private static List<Inline> Plain(string code)
    {
        var inlines = new List<Inline>();
        string[] lines = code.ReplaceLineEndings("\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0)
            {
                inlines.Add(new LineBreak());
            }

            inlines.Add(new Run(lines[i]));
        }

        return inlines;
    }

    /// <summary>
    /// Turns the parts ColorCode names into runs of WPF, since ColorCode writes HTML only. The
    /// innermost part a character belongs to gives it its color.
    /// </summary>
    private sealed class RunFormatter(StyleDictionary styles) : CodeColorizerBase(styles, null)
    {
        private readonly List<Inline> _inlines = [];

        public List<Inline> Format(string code, ILanguage language)
        {
            _inlines.Clear();
            languageParser.Parse(code.ReplaceLineEndings("\n"), language, Write);
            return [.. _inlines];
        }

        protected override void Write(string parsedSourceCode, IList<Scope> scopes)
        {
            var names = new string?[parsedSourceCode.Length];
            foreach (Scope scope in scopes)
            {
                Paint(names, scope);
            }

            int start = 0;
            for (int i = 1; i <= names.Length; i++)
            {
                if (i < names.Length && names[i] == names[start])
                {
                    continue;
                }

                string[] lines = parsedSourceCode[start..i].Split('\n');
                for (int line = 0; line < lines.Length; line++)
                {
                    if (line > 0)
                    {
                        _inlines.Add(new LineBreak());
                    }

                    if (lines[line].Length > 0)
                    {
                        _inlines.Add(Styled(new Run(lines[line]), names[start]));
                    }
                }

                start = i;
            }
        }

        private static void Paint(string?[] names, Scope scope)
        {
            Array.Fill(names, scope.Name, scope.Index, scope.Length);
            foreach (Scope child in scope.Children)
            {
                Paint(names, child);
            }
        }

        private Run Styled(Run run, string? scopeName)
        {
            if (scopeName is null || !Styles.Contains(scopeName))
            {
                return run;
            }

            ColorCode.Styling.Style style = Styles[scopeName];
            if (style.Foreground is not null)
            {
                run.Foreground = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString(style.Foreground));
            }

            if (style.Bold)
            {
                run.FontWeight = FontWeights.Bold;
            }

            if (style.Italic)
            {
                run.FontStyle = FontStyles.Italic;
            }

            return run;
        }
    }
}
