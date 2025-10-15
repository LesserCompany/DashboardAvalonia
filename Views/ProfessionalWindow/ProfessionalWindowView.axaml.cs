using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using LesserDashboardClient.ViewModels.ProfessionalWindow;
using SharedClientSide.ServerInteraction;

namespace LesserDashboardClient.Views.ProfessionalWindow
{
    public partial class ProfessionalWindowView : Window
    {
        public ProfessionalWindowView()
        {
            InitializeComponent();
        }

        public ProfessionalWindowView(LesserFunctionClient lfc) : this()
        {
            DataContext = new ProfessionalWindowViewModel(lfc);
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}


