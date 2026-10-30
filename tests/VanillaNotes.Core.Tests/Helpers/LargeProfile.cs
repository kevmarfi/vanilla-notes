using System.Diagnostics;
using System.Globalization;
using System.Text;
using VanillaNotes.Core.Data.Repositories;
using VanillaNotes.Core.Domain;
using VanillaNotes.Core.Export;
using VanillaNotes.Core.Models;

namespace VanillaNotes.Core.Tests.Helpers;

/// <summary>
/// A profile with a password and ten thousand notes, made once and shared by the measures of how
/// long the application takes with that many. The notes are brought in from a data file, as an
/// import of a person who arrives with their notes would, and how long that took is kept as the
/// measure of an import. They are spread over folders and tags, and a tenth of them hold a word
/// the others do not, so that a search finds many.
/// </summary>
public sealed class LargeProfile : IDisposable
{
    /// <summary>How many notes the profile holds: the number the plan names for a person with many notes.</summary>
    public const int NoteCount = 10_000;

    /// <summary>The word only every tenth note holds, which a search looks for.</summary>
    public const string RareWord = "lantern";

    /// <summary>How many folders the notes are spread over.</summary>
    private const int _folderCount = 20;

    /// <summary>How many tags the notes are spread over, two to a note.</summary>
    private const int _tagCount = 50;

    /// <summary>How many words the text of a note has: about a page of writing.</summary>
    private const int _wordsPerNote = 250;

    /// <summary>The words the text of the notes is made of.</summary>
    private static readonly string[] _words =
    [
        "meeting", "groceries", "project", "budget", "travel", "recipe", "garden", "invoice",
        "report", "client", "weekend", "book", "music", "health", "family", "school",
        "deadline", "idea", "draft", "review", "plan", "summer", "winter", "coffee",
        "train", "ticket", "museum", "river", "mountain", "letter", "phone", "office",
    ];

    public LargeProfile()
    {
        App = new TestApplication();
        Profile = App.CreateProfile("Ana", TestApplication.Password);
        byte[] file = DataFile();

        var importing = Stopwatch.StartNew();
        int imported = new ImportService(
            new NoteRepository(Profile.Session.Database),
            new FolderRepository(Profile.Session.Database),
            Profile.Attachments,
            App.DiskSpace).Import(file);
        Importing = importing.Elapsed;

        if (imported != NoteCount)
        {
            throw new InvalidOperationException($"The import brought in {imported} notes, not {NoteCount}.");
        }
    }

    /// <summary>The application, on a data folder of its own.</summary>
    public TestApplication App { get; }

    /// <summary>The profile with the ten thousand notes, open.</summary>
    public TestApplication.OpenProfile Profile { get; }

    /// <summary>How long bringing in the ten thousand notes took.</summary>
    public TimeSpan Importing { get; }

    public void Dispose() => App.Dispose();

    /// <summary>The data file the notes are brought in from, written as the application exports one.</summary>
    private static byte[] DataFile()
    {
        DateTimeOffset start = new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var notes = new List<Note>(NoteCount);
        for (int index = 0; index < NoteCount; index++)
        {
            DateTimeOffset moment = start.AddMinutes(index);
            notes.Add(new Note(
                Id: index + 1,
                Title: string.Create(CultureInfo.InvariantCulture, $"Note {index} about {_words[index % _words.Length]}"),
                Content: Text(index),
                FolderId: index % (_folderCount + 1) == 0 ? null : index % (_folderCount + 1),
                Tags: [Tag(index), Tag(index * 7 + 3)],
                Version: 1,
                CreatedAt: moment,
                UpdatedAt: moment));
        }

        Dictionary<long, string> folderNames = Enumerable.Range(1, _folderCount)
            .ToDictionary(id => (long)id, id => string.Create(CultureInfo.InvariantCulture, $"Folder {id}"));
        return NoteExporter.Export(notes, folderNames, new Dictionary<long, (string, byte[])>(), ExportFormat.Data, start);
    }

    /// <summary>The text of one note: a heading, and words taken in an order that changes from note to note.</summary>
    private static string Text(int index)
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"# Note {index}\n\n");
        for (int word = 0; word < _wordsPerNote; word++)
        {
            text.Append(_words[(index * 31 + word * 17) % _words.Length]);
            text.Append(word % 12 == 11 ? ".\n\n" : " ");
        }

        if (index % 10 == 0)
        {
            text.Append(RareWord);
        }

        return text.ToString();
    }

    private static string Tag(int index) => string.Create(CultureInfo.InvariantCulture, $"tag{index % _tagCount}");
}
