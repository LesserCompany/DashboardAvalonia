using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using LesserDashboardClient.ViewModels;
using LesserDashboardClient.ViewModels.ProfessionalWindow;
using SharedClientSide.ServerInteraction;
using SharedClientSide.ServerInteraction.Users;
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

        private void ListBox_DoubleTapped(object? sender, RoutedEventArgs e)
        {
            if (DataContext is ProfessionalWindowViewModel viewModel && sender is ListBox listBox)
            {
                // Obtém o item selecionado (já deve estar selecionado pelo duplo clique)
                var clickedItem = listBox.SelectedItem as ProfessionalTask;
                
                // Se não houver item selecionado, tenta obter do evento
                if (clickedItem == null && e.Source is Control source)
                {
                    var parent = source.Parent;
                    while (parent != null)
                    {
                        if (parent is ListBoxItem listBoxItem)
                        {
                            clickedItem = listBoxItem.DataContext as ProfessionalTask;
                            break;
                        }
                        parent = parent.Parent;
                    }
                }
                
                // Garante que o item está selecionado e executa o comando
                if (clickedItem != null)
                {
                    // Atualiza a seleção no ViewModel se necessário
                    if (viewModel.SelectedContract != clickedItem)
                    {
                        viewModel.SelectedContract = clickedItem;
                    }
                    
                    // Executa o mesmo comando que o botão "Selecionar"
                    viewModel.SelectContractCommand();
                }
            }
        }
    }
}


