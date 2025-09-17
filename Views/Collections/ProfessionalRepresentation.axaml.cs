using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using System;
using System.Reactive.Linq; // <-- necessário para usar Subscribe com lambda


namespace LesserDashboardClient.Views.Collections;

public partial class ProfessionalRepresentation : UserControl
{
    public static readonly StyledProperty<string> ProfessionalNameProperty = 
        AvaloniaProperty.Register<ProfessionalRepresentation, string>(nameof(ProfessionalName));
    public string ProfessionalName
    {
        get => GetValue(ProfessionalNameProperty);
        set => SetValue(ProfessionalNameProperty, value);
    }
    public string Initials => GetInitials(ProfessionalName);
    public IBrush AvatarColor => GetColorFromName(ProfessionalName);
    public ProfessionalRepresentation()
    {
        InitializeComponent();

        // Quando o nome mudar, notifica Initials e AvatarColor
        this.GetObservable(ProfessionalNameProperty).Subscribe(_ =>
        {
            RaisePropertyChanged(InitialsProperty, null, Initials);
            RaisePropertyChanged(AvatarColorProperty, null, AvatarColor);
        });
    }


    private static string GetInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";

        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return parts[0][0].ToString().ToUpper();

        return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpper();
    }
    private static IBrush GetColorFromName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return Brushes.Gray;

        int hash = StableHash(name); // usa hash estável
        var colors = new[]
        {
        Brushes.SteelBlue,
        Brushes.CadetBlue,
        Brushes.MediumSeaGreen,
        Brushes.DarkOrange,
        Brushes.IndianRed,
        Brushes.MediumPurple,
        Brushes.Goldenrod,
        Brushes.Teal
    };

        return colors[Math.Abs(hash) % colors.Length];
    }
    private static int StableHash(string input)
    {
        unchecked
        {
            int hash = 23;
            foreach (char c in input)
            {
                hash = hash * 31 + c;
            }
            return hash;
        }
    }


    // Propriedade Avalonia para binding
    public static readonly DirectProperty<ProfessionalRepresentation, string> InitialsProperty =
        AvaloniaProperty.RegisterDirect<ProfessionalRepresentation, string>(
            nameof(Initials),
            o => o.Initials);

    public static readonly DirectProperty<ProfessionalRepresentation, IBrush> AvatarColorProperty =
        AvaloniaProperty.RegisterDirect<ProfessionalRepresentation, IBrush>(
            nameof(AvatarColor),
            o => o.AvatarColor);
}