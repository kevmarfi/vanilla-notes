using System.Windows;
using System.Windows.Input;
using VanillaNotes.App.ViewModels;

namespace VanillaNotes.App.Controls;

/// <summary>
/// What can be dragged in the window and where it lands: files from Windows dropped onto an
/// element, which a command receives by their paths; and a note of the list dropped onto a place
/// that takes notes, such as a folder of the sidebar. Files are taken before the controls inside
/// the element see them, so that a box of text does not take a file for text to paste.
/// </summary>
public static class Dropping
{
    /// <summary>The name a note travels under while it is dragged, so that only places that take notes accept it.</summary>
    private const string _noteFormat = "VanillaNotes.Note";

    /// <summary>What receives the paths of the files dropped onto an element.</summary>
    public static readonly DependencyProperty FilesCommandProperty = DependencyProperty.RegisterAttached(
        "FilesCommand",
        typeof(ICommand),
        typeof(Dropping),
        new PropertyMetadata(null, OnFilesCommandChanged));

    /// <summary>The number of the note an element of the list stands for, which dragging it carries.</summary>
    public static readonly DependencyProperty NoteProperty = DependencyProperty.RegisterAttached(
        "Note",
        typeof(long?),
        typeof(Dropping),
        new PropertyMetadata(null, OnNoteChanged));

    /// <summary>What receives a note dropped onto an element, with the target of the element.</summary>
    public static readonly DependencyProperty NoteCommandProperty = DependencyProperty.RegisterAttached(
        "NoteCommand",
        typeof(ICommand),
        typeof(Dropping),
        new PropertyMetadata(null, OnNoteCommandChanged));

    /// <summary>What a note dropped onto an element lands in, such as a folder; nothing is the notes in no folder.</summary>
    public static readonly DependencyProperty NoteTargetProperty = DependencyProperty.RegisterAttached(
        "NoteTarget",
        typeof(object),
        typeof(Dropping));

    /// <summary>Where the mouse was pressed on a note, to tell a drag from a click.</summary>
    private static readonly DependencyProperty _pressedAtProperty = DependencyProperty.RegisterAttached(
        "PressedAt",
        typeof(Point?),
        typeof(Dropping));

    /// <summary>What receives the paths of the files dropped onto an element.</summary>
    public static ICommand? GetFilesCommand(DependencyObject element) => (ICommand?)element.GetValue(FilesCommandProperty);

    /// <summary>Sets what receives the paths of the files dropped onto an element.</summary>
    public static void SetFilesCommand(DependencyObject element, ICommand? value) => element.SetValue(FilesCommandProperty, value);

    /// <summary>The number of the note an element stands for.</summary>
    public static long? GetNote(DependencyObject element) => (long?)element.GetValue(NoteProperty);

    /// <summary>Sets the number of the note an element stands for.</summary>
    public static void SetNote(DependencyObject element, long? value) => element.SetValue(NoteProperty, value);

    /// <summary>What receives a note dropped onto an element.</summary>
    public static ICommand? GetNoteCommand(DependencyObject element) => (ICommand?)element.GetValue(NoteCommandProperty);

    /// <summary>Sets what receives a note dropped onto an element.</summary>
    public static void SetNoteCommand(DependencyObject element, ICommand? value) => element.SetValue(NoteCommandProperty, value);

    /// <summary>What a note dropped onto an element lands in.</summary>
    public static object? GetNoteTarget(DependencyObject element) => element.GetValue(NoteTargetProperty);

    /// <summary>Sets what a note dropped onto an element lands in.</summary>
    public static void SetNoteTarget(DependencyObject element, object? value) => element.SetValue(NoteTargetProperty, value);

    // -- Files -- //

    private static void OnFilesCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs change)
    {
        if (target is not UIElement element)
        {
            return;
        }

        element.PreviewDragOver -= OnFilesOver;
        element.PreviewDrop -= OnFilesDropped;
        if (change.NewValue is not null)
        {
            element.AllowDrop = true;
            element.PreviewDragOver += OnFilesOver;
            element.PreviewDrop += OnFilesDropped;
        }
    }

    private static void OnFilesOver(object sender, DragEventArgs drag)
    {
        if (!drag.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        bool takes = GetFilesCommand((DependencyObject)sender)?.CanExecute(null) == true;
        drag.Effects = takes ? DragDropEffects.Copy : DragDropEffects.None;
        drag.Handled = true;
    }

    private static void OnFilesDropped(object sender, DragEventArgs drop)
    {
        if (drop.Data.GetData(DataFormats.FileDrop) is not string[] paths)
        {
            return;
        }

        drop.Handled = true;
        ICommand? command = GetFilesCommand((DependencyObject)sender);
        if (command?.CanExecute(paths) == true)
        {
            command.Execute(paths);
        }
    }

    // -- Notes -- //

    private static void OnNoteChanged(DependencyObject target, DependencyPropertyChangedEventArgs change)
    {
        if (target is not UIElement element)
        {
            return;
        }

        element.PreviewMouseLeftButtonDown -= OnNotePressed;
        element.PreviewMouseMove -= OnNoteMoved;
        if (change.NewValue is not null)
        {
            element.PreviewMouseLeftButtonDown += OnNotePressed;
            element.PreviewMouseMove += OnNoteMoved;
        }
    }

    private static void OnNotePressed(object sender, MouseButtonEventArgs press) =>
        ((UIElement)sender).SetValue(_pressedAtProperty, press.GetPosition((UIElement)sender));

    /// <summary>A note is dragged once the mouse, pressed on it, has moved as far as Windows asks for a drag.</summary>
    private static void OnNoteMoved(object sender, MouseEventArgs move)
    {
        var element = (UIElement)sender;
        if (move.LeftButton != MouseButtonState.Pressed || element.GetValue(_pressedAtProperty) is not Point pressedAt || GetNote(element) is not long noteId)
        {
            return;
        }

        Vector moved = move.GetPosition(element) - pressedAt;
        if (Math.Abs(moved.X) < SystemParameters.MinimumHorizontalDragDistance && Math.Abs(moved.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        element.ClearValue(_pressedAtProperty);
        DragDrop.DoDragDrop(element, new DataObject(_noteFormat, noteId), DragDropEffects.Move);
    }

    private static void OnNoteCommandChanged(DependencyObject target, DependencyPropertyChangedEventArgs change)
    {
        if (target is not UIElement element)
        {
            return;
        }

        element.DragOver -= OnNoteOver;
        element.Drop -= OnNoteDropped;
        if (change.NewValue is not null)
        {
            element.AllowDrop = true;
            element.DragOver += OnNoteOver;
            element.Drop += OnNoteDropped;
        }
    }

    private static void OnNoteOver(object sender, DragEventArgs drag)
    {
        drag.Effects = drag.Data.GetDataPresent(_noteFormat) ? DragDropEffects.Move : DragDropEffects.None;
        drag.Handled = true;
    }

    private static void OnNoteDropped(object sender, DragEventArgs drop)
    {
        if (drop.Data.GetData(_noteFormat) is not long noteId)
        {
            return;
        }

        drop.Handled = true;
        var element = (DependencyObject)sender;
        var dropped = new DroppedNote(noteId, GetNoteTarget(element));
        ICommand? command = GetNoteCommand(element);
        if (command?.CanExecute(dropped) == true)
        {
            command.Execute(dropped);
        }
    }
}
