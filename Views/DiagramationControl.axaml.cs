using Avalonia.Controls;
using LesserDashboardClient.ViewModels;

namespace LesserDashboardClient.Views;

public partial class DiagramationControl : UserControl
{
    public DiagramationControl()
    {
        InitializeComponent();
        DataContext = new DiagramationViewModel();
    }
}
