using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace VanillaNotes.App.Controls;

/// <summary>
/// The spell checker of Windows, for one language: the words of a text it does not know, what it
/// suggests in their place, and a word added to the dictionary of the person, which Windows keeps
/// for every application. It is the one the text boxes of Windows use, reached through its COM
/// interface, part of the system since Windows 8; no package brings it.
/// </summary>
public sealed class WindowsSpelling
{
    /// <summary>The class of the factory of spell checkers of Windows.</summary>
    private static readonly Guid _factoryClass = new("7AB36653-1796-484B-BDFA-E74F1DB7C1DC");

    /// <summary>What Windows says of a word it does not mark: it asks for nothing (the others ask for suggestions, a replacement, or to be deleted, as a word written twice is).</summary>
    private const int _noAction = 0;

    /// <summary>The factory of spell checkers, made once, or nothing when this Windows has none.</summary>
    private static readonly Lazy<ISpellCheckerFactory?> _factory = new(CreateFactory);

    private readonly ISpellChecker _checker;

    private WindowsSpelling(ISpellChecker checker)
    {
        _checker = checker;
    }

    /// <summary>
    /// The spell checker of a language, as Windows names it ("es-MX"), or of the language without its
    /// region when that one is not there ("es"); or nothing when Windows checks neither.
    /// </summary>
    public static WindowsSpelling? For(string languageTag)
    {
        if (_factory.Value is not ISpellCheckerFactory factory)
        {
            return null;
        }

        foreach (string tag in new[] { languageTag, languageTag.Split('-')[0] }.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (factory.IsSupported(tag) != 0)
            {
                return new WindowsSpelling(factory.CreateSpellChecker(tag));
            }
        }

        return null;
    }

    /// <summary>The words of a text the checker does not know, by where they start and how long they are.</summary>
    public IReadOnlyList<(int Start, int Length)> Errors(string text)
    {
        var errors = new List<(int Start, int Length)>();
        IEnumSpellingError found = _checker.Check(text);
        while (found.Next(out ISpellingError? error) == 0 && error is not null)
        {
            if (error.CorrectiveAction != _noAction)
            {
                errors.Add(((int)error.StartIndex, (int)error.Length));
            }
        }

        return errors;
    }

    /// <summary>The words the checker suggests in place of one it does not know, best first.</summary>
    public IReadOnlyList<string> Suggestions(string word)
    {
        var suggestions = new List<string>();
        IEnumString found = _checker.Suggest(word);
        string[] one = new string[1];
        while (found.Next(1, one, IntPtr.Zero) == 0)
        {
            suggestions.Add(one[0]);
        }

        return suggestions;
    }

    /// <summary>Adds a word to the dictionary of the person, so that it is known from now on, here and in other applications.</summary>
    public void Add(string word) => _checker.Add(word);

    private static ISpellCheckerFactory? CreateFactory()
    {
        try
        {
            return Type.GetTypeFromCLSID(_factoryClass) is Type type ? Activator.CreateInstance(type) as ISpellCheckerFactory : null;
        }
        catch (COMException)
        {
            // A Windows with no spell checker: the box is not checked.
            return null;
        }
    }

    // -- The interfaces of Windows, in the order of their methods; only the first ones used are declared -- //

    [ComImport]
    [Guid("8E018A9D-2415-4677-BF08-794EA61F94BB")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISpellCheckerFactory
    {
        IEnumString SupportedLanguages { get; }

        int IsSupported([MarshalAs(UnmanagedType.LPWStr)] string languageTag);

        ISpellChecker CreateSpellChecker([MarshalAs(UnmanagedType.LPWStr)] string languageTag);
    }

    [ComImport]
    [Guid("B6FD0B71-E2BC-4653-8D05-F197E412770B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISpellChecker
    {
        string LanguageTag { [return: MarshalAs(UnmanagedType.LPWStr)] get; }

        IEnumSpellingError Check([MarshalAs(UnmanagedType.LPWStr)] string text);

        IEnumString Suggest([MarshalAs(UnmanagedType.LPWStr)] string word);

        void Add([MarshalAs(UnmanagedType.LPWStr)] string word);
    }

    [ComImport]
    [Guid("803E3BD4-2828-4410-8290-418D1D73C762")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IEnumSpellingError
    {
        [PreserveSig]
        int Next(out ISpellingError? value);
    }

    [ComImport]
    [Guid("B7C82D61-FBE8-4B47-9B27-6C0D2E0DE0A3")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface ISpellingError
    {
        uint StartIndex { get; }

        uint Length { get; }

        int CorrectiveAction { get; }
    }
}
