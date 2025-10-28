using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
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
            
            // CORREÇÃO: Aplica as configurações de tema e idioma após inicializar tudo
            Console.WriteLine("ProfessionalWindowView: Aplicando configurações de tema e idioma...");
            App.ReapplySettings();
            Console.WriteLine("ProfessionalWindowView: Configurações aplicadas com sucesso");
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}


