using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LesserDashboardClient.Views.Collections;

public partial class PhotoSalesIcon : UserControl
{
    public PhotoSalesIcon()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<double> SizeIconIconProperty =
AvaloniaProperty.Register<HdAndTreatmentCollection, double>(nameof(SizeIcon));
    public double SizeIcon
    {
        get => GetValue(SizeIconIconProperty);
        set => SetValue(SizeIconIconProperty, value);
    }

}