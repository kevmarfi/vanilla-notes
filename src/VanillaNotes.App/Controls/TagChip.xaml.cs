using System.Windows;
using System.Windows.Input;

namespace VanillaNotes.App.Controls;

/// <summary>The chip of a tag, colored from its name, with a button to take it off when a command for it is given.</summary>
public partial class TagChip
{
    /// <summary>The name of the tag.</summary>
    public static readonly DependencyProperty TagNameProperty = DependencyProperty.Register(nameof(TagName), typeof(string), typeof(TagChip), new PropertyMetadata(""));

    /// <summary>What taking the tag off does; with none, the chip has no button.</summary>
    public static readonly DependencyProperty RemoveCommandProperty = DependencyProperty.Register(
        nameof(RemoveCommand), typeof(ICommand), typeof(TagChip), new PropertyMetadata(null, (chip, _) => ((TagChip)chip).CanRemove = ((TagChip)chip).RemoveCommand is not null));

    /// <summary>Whether the chip has a button to take the tag off.</summary>
    public static readonly DependencyProperty CanRemoveProperty = DependencyProperty.Register(nameof(CanRemove), typeof(bool), typeof(TagChip));

    public TagChip()
    {
        InitializeComponent();
    }

    /// <summary>The name of the tag.</summary>
    public string TagName
    {
        get => (string)GetValue(TagNameProperty);
        set => SetValue(TagNameProperty, value);
    }

    /// <summary>What taking the tag off does.</summary>
    public ICommand? RemoveCommand
    {
        get => (ICommand?)GetValue(RemoveCommandProperty);
        set => SetValue(RemoveCommandProperty, value);
    }

    /// <summary>Whether the chip has a button to take the tag off.</summary>
    public bool CanRemove
    {
        get => (bool)GetValue(CanRemoveProperty);
        private set => SetValue(CanRemoveProperty, value);
    }
}
