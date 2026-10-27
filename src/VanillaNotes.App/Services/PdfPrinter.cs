using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using VanillaNotes.App.Editing;

namespace VanillaNotes.App.Services;

/// <summary>
/// A note through the print dialog of Windows: to paper, or to a PDF file with the printer
/// "Microsoft Print to PDF". The note is laid out as it is read, with its title above it, and
/// always in the colors of the light theme, which is what paper has.
/// </summary>
public sealed class PdfPrinter
{
    /// <summary>The colors a note is printed in, whatever the theme on screen.</summary>
    private static readonly Uri _paperColors = new("pack://application:,,,/Themes/Light.xaml", UriKind.Absolute);

    /// <summary>The room around the text of a printed page, in pixels of a 96th of an inch: half an inch.</summary>
    private const double _pageMargin = 48;

    /// <summary>
    /// Asks the person for a printer, and prints a note on it.
    /// </summary>
    /// <param name="imageOf">The picture of an attached image, by its number.</param>
    /// <returns>Whether it was printed, rather than the dialog cancelled.</returns>
    public bool Print(string title, string markdown, Func<long, ImageSource?> imageOf)
    {
        var dialog = new PrintDialog();
        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        FlowDocument document = MarkdownToDocument.Convert(markdown, imageOf, dark: false);
        document.Resources.MergedDictionaries.Add(new ResourceDictionary { Source = _paperColors });
        var heading = new Paragraph(new Run(title));
        MarkdownToDocument.MakeHeading(heading, 1);
        if (document.Blocks.FirstBlock is Block first)
        {
            document.Blocks.InsertBefore(first, heading);
        }
        else
        {
            document.Blocks.Add(heading);
        }

        document.PageWidth = dialog.PrintableAreaWidth;
        document.PageHeight = dialog.PrintableAreaHeight;
        document.ColumnWidth = dialog.PrintableAreaWidth;
        document.PagePadding = new Thickness(_pageMargin);
        dialog.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, title);
        return true;
    }
}
