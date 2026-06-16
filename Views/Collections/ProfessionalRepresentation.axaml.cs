using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;
using System.Reactive.Linq;

namespace LesserDashboardClient.Views.Collections;

public partial class ProfessionalRepresentation : UserControl
{
    public static readonly StyledProperty<string> FriendlyNameProperty =
        AvaloniaProperty.Register<ProfessionalRepresentation, string>(nameof(FriendlyName));

    public static readonly StyledProperty<string> LoginNameProperty =
        AvaloniaProperty.Register<ProfessionalRepresentation, string>(nameof(LoginName));

    public static readonly StyledProperty<string> EmailProperty =
        AvaloniaProperty.Register<ProfessionalRepresentation, string>(nameof(Email));

    public static readonly StyledProperty<bool> ShowEmailProperty =
        AvaloniaProperty.Register<ProfessionalRepresentation, bool>(nameof(ShowEmail), defaultValue: false);

    public static readonly StyledProperty<double> AvatarSizeProperty =
        AvaloniaProperty.Register<ProfessionalRepresentation, double>(nameof(AvatarSize), defaultValue: 44);

    public static readonly StyledProperty<double> AvatarFontSizeProperty =
        AvaloniaProperty.Register<ProfessionalRepresentation, double>(nameof(AvatarFontSize), defaultValue: 16);

    public static readonly StyledProperty<double> PrimaryFontSizeProperty =
        AvaloniaProperty.Register<ProfessionalRepresentation, double>(nameof(PrimaryFontSize), defaultValue: 14);

    public static readonly StyledProperty<bool> IsCompactProperty =
        AvaloniaProperty.Register<ProfessionalRepresentation, bool>(nameof(IsCompact), defaultValue: false);

    /// <summary>Compatibilidade: define LoginName quando usado sozinho.</summary>
    public static readonly StyledProperty<string> ProfessionalNameProperty =
        AvaloniaProperty.Register<ProfessionalRepresentation, string>(nameof(ProfessionalName));

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

    public string Email
    {
        get => GetValue(EmailProperty);
        set => SetValue(EmailProperty, value);
    }

    public bool ShowEmail
    {
        get => GetValue(ShowEmailProperty);
        set => SetValue(ShowEmailProperty, value);
    }

    public double AvatarSize
    {
        get => GetValue(AvatarSizeProperty);
        set => SetValue(AvatarSizeProperty, value);
    }

    public double AvatarFontSize
    {
        get => GetValue(AvatarFontSizeProperty);
        set => SetValue(AvatarFontSizeProperty, value);
    }

    public double PrimaryFontSize
    {
        get => GetValue(PrimaryFontSizeProperty);
        set => SetValue(PrimaryFontSizeProperty, value);
    }

    public bool IsCompact
    {
        get => GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }

    public string ProfessionalName
    {
        get => GetValue(ProfessionalNameProperty);
        set => SetValue(ProfessionalNameProperty, value);
    }

    public double AvatarCornerRadius => AvatarSize / 2;

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

    public string Initials => GetInitials(PrimaryText);

    public IBrush AvatarColor => GetColorFromName(PrimaryText);

    public ProfessionalRepresentation()
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
        this.GetObservable(AvatarSizeProperty).Subscribe(_ => RaisePropertyChanged(AvatarCornerRadiusProperty, 0d, AvatarCornerRadius));
    }

    private void NotifyDisplayChanged()
    {
        RaisePropertyChanged(PrimaryTextProperty, null, PrimaryText);
        RaisePropertyChanged(SecondaryTextProperty, null, SecondaryText);
        RaisePropertyChanged(HasSecondaryTextProperty, false, HasSecondaryText);
        RaisePropertyChanged(InitialsProperty, null, Initials);
        RaisePropertyChanged(AvatarColorProperty, null, AvatarColor);
    }

    private static string GetInitials(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "?";

        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return parts[0][..Math.Min(2, parts[0].Length)].ToUpperInvariant();

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

    public static readonly DirectProperty<ProfessionalRepresentation, string> PrimaryTextProperty =
        AvaloniaProperty.RegisterDirect<ProfessionalRepresentation, string>(nameof(PrimaryText), o => o.PrimaryText);

    public static readonly DirectProperty<ProfessionalRepresentation, string> SecondaryTextProperty =
        AvaloniaProperty.RegisterDirect<ProfessionalRepresentation, string>(nameof(SecondaryText), o => o.SecondaryText);

    public static readonly DirectProperty<ProfessionalRepresentation, bool> HasSecondaryTextProperty =
        AvaloniaProperty.RegisterDirect<ProfessionalRepresentation, bool>(nameof(HasSecondaryText), o => o.HasSecondaryText);

    public static readonly DirectProperty<ProfessionalRepresentation, string> InitialsProperty =
        AvaloniaProperty.RegisterDirect<ProfessionalRepresentation, string>(nameof(Initials), o => o.Initials);

    public static readonly DirectProperty<ProfessionalRepresentation, IBrush> AvatarColorProperty =
        AvaloniaProperty.RegisterDirect<ProfessionalRepresentation, IBrush>(nameof(AvatarColor), o => o.AvatarColor);

    public static readonly DirectProperty<ProfessionalRepresentation, double> AvatarCornerRadiusProperty =
        AvaloniaProperty.RegisterDirect<ProfessionalRepresentation, double>(nameof(AvatarCornerRadius), o => o.AvatarCornerRadius);
}
