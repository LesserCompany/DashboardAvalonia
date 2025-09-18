using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using FluentAvalonia.UI.Media;
using LesserDashboardClient.Models;
using System.Windows.Input;

namespace LesserDashboardClient.Views.Collections;

public partial class CardCombo : UserControl
{
    public CardCombo()
    {
        InitializeComponent();
    }
    public static readonly StyledProperty<CollectionComboOptions> CollectionComboProperty =
        AvaloniaProperty.Register<CardCombo, CollectionComboOptions>(nameof(CollectionCombo));

    public CollectionComboOptions CollectionCombo
    {
        get => GetValue(CollectionComboProperty);
        set => SetValue(CollectionComboProperty, value);
    }


    public static readonly StyledProperty<ICommand> CommandProperty =
        AvaloniaProperty.Register<CardCombo, ICommand>(nameof(Command));
    public ICommand Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }
}