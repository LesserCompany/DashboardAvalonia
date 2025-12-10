using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using LesserDashboardClient.ViewModels;
using LesserDashboardClient.ViewModels.ProfessionalWindow;
using SharedClientSide.ServerInteraction;
using System;

namespace LesserDashboardClient.Views.ProfessionalWindow
{
    public partial class ProfessionalWindowView : Window
    {
        public ProfessionalWindowView()
        {
            InitializeComponent();
        }

        public ProfessionalWindowView(LesserFunctionClient lfc)
        {
            InitializeComponent();
            
            DataContext = new ProfessionalWindowViewModel(lfc);
            
            // NÃO reinicializar configurações - elas já estão aplicadas desde o início
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}


