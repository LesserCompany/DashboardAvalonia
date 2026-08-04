using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Reactive.Linq;

namespace LesserDashboardClient.Views.Collections;

public partial class SeparatorProfessionalRepresentation : UserControl
{
    public static readonly StyledProperty<string> FriendlyNameProperty =
        AvaloniaProperty.Register<SeparatorProfessionalRepresentation, string>(nameof(FriendlyName));

    public static readonly StyledProperty<string> LoginNameProperty =
        AvaloniaProperty.Register<SeparatorProfessionalRepresentation, string>(nameof(LoginName));

    public static readonly StyledProperty<string> ProfessionalNameProperty =
        AvaloniaProperty.Register<SeparatorProfessionalRepresentation, string>(nameof(ProfessionalName));

    public string FriendlyName
    {
        get => GetValue(FriendlyNameProperty);
        set => SetValue(FriendlyNameProperty, value);
    }

    public string LoginName
    {
        get => GetValue(LoginNameProperty);
        set => SetValue(LoginNameProperty, value);
    }

    public string ProfessionalName
    {
        get => GetValue(ProfessionalNameProperty);
        set => SetValue(ProfessionalNameProperty, value);
    }

    public string PrimaryText =>
        !string.IsNullOrWhiteSpace(FriendlyName) ? FriendlyName.Trim()
        : !string.IsNullOrWhiteSpace(LoginName) ? LoginName.Trim()
        : !string.IsNullOrWhiteSpace(ProfessionalName) ? ProfessionalName.Trim()
        : "?";

    public string SecondaryText =>
        !string.IsNullOrWhiteSpace(FriendlyName) && !string.IsNullOrWhiteSpace(LoginName)
        && !string.Equals(FriendlyName.Trim(), LoginName.Trim(), StringComparison.OrdinalIgnoreCase)
            ? LoginName.Trim()
            : string.Empty;

    public bool HasSecondaryText => !string.IsNullOrWhiteSpace(SecondaryText);

    public string ToolTipText =>
        HasSecondaryText ? $"{PrimaryText} ({SecondaryText})" : PrimaryText;

    public string Initials => GetInitials(PrimaryText);

    public IBrush AvatarColor => GetColorFromName(PrimaryText);

    public SeparatorProfessionalRepresentation()
    {
        InitializeComponent();

        this.GetObservable(FriendlyNameProperty).Subscribe(_ => NotifyDisplayChanged());
        this.GetObservable(LoginNameProperty).Subscribe(_ => NotifyDisplayChanged());
        this.GetObservable(ProfessionalNameProperty).Subscribe(v =>
        {
            if (!string.IsNullOrWhiteSpace(v) && string.IsNullOrWhiteSpace(LoginName))
                LoginName = v;
            NotifyDisplayChanged();
        });
    }

    private void NotifyDisplayChanged()
    {
        RaisePropertyChanged(PrimaryTextProperty, null, PrimaryText);
        RaisePropertyChanged(SecondaryTextProperty, null, SecondaryText);
        RaisePropertyChanged(HasSecondaryTextProperty, false, HasSecondaryText);
        RaisePropertyChanged(ToolTipTextProperty, null, ToolTipText);
        RaisePropertyChanged(InitialsProperty, null, Initials);
        RaisePropertyChanged(AvatarColorProperty, null, AvatarColor);
    }

    private static string GetInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";

        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return parts[0][0].ToString().ToUpperInvariant();

        return (parts[0][0].ToString() + parts[^1][0].ToString()).ToUpperInvariant();
    }

    private static IBrush GetColorFromName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return Brushes.Gray;

        int hash = StableHash(name);
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
                hash = hash * 31 + c;
            return hash;
        }
    }

    public static readonly DirectProperty<SeparatorProfessionalRepresentation, string> PrimaryTextProperty =
        AvaloniaProperty.RegisterDirect<SeparatorProfessionalRepresentation, string>(nameof(PrimaryText), o => o.PrimaryText);

    public static readonly DirectProperty<SeparatorProfessionalRepresentation, string> SecondaryTextProperty =
        AvaloniaProperty.RegisterDirect<SeparatorProfessionalRepresentation, string>(nameof(SecondaryText), o => o.SecondaryText);

    public static readonly DirectProperty<SeparatorProfessionalRepresentation, bool> HasSecondaryTextProperty =
        AvaloniaProperty.RegisterDirect<SeparatorProfessionalRepresentation, bool>(nameof(HasSecondaryText), o => o.HasSecondaryText);

    public static readonly DirectProperty<SeparatorProfessionalRepresentation, string> ToolTipTextProperty =
        AvaloniaProperty.RegisterDirect<SeparatorProfessionalRepresentation, string>(nameof(ToolTipText), o => o.ToolTipText);

    public static readonly DirectProperty<SeparatorProfessionalRepresentation, string> InitialsProperty =
        AvaloniaProperty.RegisterDirect<SeparatorProfessionalRepresentation, string>(nameof(Initials), o => o.Initials);

    public static readonly DirectProperty<SeparatorProfessionalRepresentation, IBrush> AvatarColorProperty =
        AvaloniaProperty.RegisterDirect<SeparatorProfessionalRepresentation, IBrush>(nameof(AvatarColor), o => o.AvatarColor);
}
