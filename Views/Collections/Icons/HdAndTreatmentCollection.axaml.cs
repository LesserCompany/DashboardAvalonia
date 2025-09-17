using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace LesserDashboardClient.Views.Collections;

public partial class HdAndTreatmentCollection : UserControl
{
    public HdAndTreatmentCollection()
    {
        InitializeComponent();
    }

    public static readonly StyledProperty<bool> AutoTreatmentProperty =
    AvaloniaProperty.Register<HdAndTreatmentCollection, bool>(nameof(AutoTreatment));
    public bool AutoTreatment
    {
        get => GetValue(AutoTreatmentProperty);
        set => SetValue(AutoTreatmentProperty, value);
    }
    public static readonly StyledProperty<bool> OcrProperty =
    AvaloniaProperty.Register<HdAndTreatmentCollection, bool>(nameof(Ocr));
    public bool Ocr
    {
        get => GetValue(OcrProperty);
        set => SetValue(OcrProperty, value);
    }
}