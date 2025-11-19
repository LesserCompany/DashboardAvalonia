using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Styling;
using CodingSeb.Localization;
using LesserDashboardClient.Models;
using LesserDashboardClient.ViewModels;
using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;

namespace LesserDashboardClient.Views.Collections;

/// <summary>
/// Página de mensagens do usuário
/// </summary>
public partial class MessagesView : UserControl
{
    public MessagesView()
    {
        InitializeComponent();
        Loaded += MessagesView_Loaded;
        
        // Observar mudanças na propriedade IsVisible
        this.GetObservable(IsVisibleProperty).Subscribe(async isVisible =>
        {
            if (isVisible)
            {
                System.Diagnostics.Debug.WriteLine("MessagesView: IsVisible mudou para true. Carregando mensagens.");
                await LoadMessagesAsync();
            }
        });
    }

    private async void MessagesView_Loaded(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine("MessagesView: Loaded event disparado.");
        await LoadMessagesAsync();
    }

    private async Task LoadMessagesAsync()
    {
        try
        {
            // Encontrar o painel de mensagens
            var messagesPanel = this.FindControl<StackPanel>("MessagesPanel");
            var markAllButton = this.FindControl<Button>("MarkAllAsReadButton");
            
            System.Diagnostics.Debug.WriteLine($"MessagesView: MessagesPanel encontrado: {messagesPanel != null}");
            
            if (messagesPanel == null)
            {
                System.Diagnostics.Debug.WriteLine("MessagesView: MessagesPanel não encontrado!");
                return;
            }

            // Obter o ViewModel
            var viewModel = MainWindowViewModel.Instance;
            if (viewModel == null)
            {
                System.Diagnostics.Debug.WriteLine("MessagesView: MainWindowViewModel.Instance é null");
                return;
            }

            // Carregar as mensagens se necessário
            if (viewModel.UserMessages == null || viewModel.UserMessages.Count == 0)
            {
                await viewModel.LoadUserMessagesAsync();
            }

            System.Diagnostics.Debug.WriteLine($"MessagesView: Total de mensagens: {viewModel.UserMessages?.Count ?? 0}");

            // Limpar skeleton loading
            messagesPanel.Children.Clear();

            // Verificar se há mensagens
            if (viewModel.UserMessages != null && viewModel.UserMessages.Count > 0)
            {
                // Ordenar mensagens: não lidas primeiro, depois por data
                var sortedMessages = viewModel.UserMessages
                    .OrderBy(m => m.IsRead)
                    .ThenByDescending(m => m.CreatedDate);

                // Adicionar cada mensagem ao painel
                foreach (var message in sortedMessages)
                {
                    var messageItem = CreateMessageItem(message, viewModel);
                    messagesPanel.Children.Add(messageItem);
                }

                // Mostrar botão "Marcar todas como lidas" se houver mensagens não lidas
                if (markAllButton != null)
                {
                    markAllButton.IsVisible = viewModel.HasUnreadMessages;
                    markAllButton.Click -= MarkAllAsReadButton_Click;
                    markAllButton.Click += MarkAllAsReadButton_Click;
                }

                System.Diagnostics.Debug.WriteLine($"MessagesView: {messagesPanel.Children.Count} mensagens adicionadas");
            }
            else
            {
                // Nenhuma mensagem encontrada
                ShowNoMessagesPlaceholder(messagesPanel);
            }

            // Forçar atualização da interface
            this.InvalidateVisual();
            this.InvalidateArrange();
            this.InvalidateMeasure();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MessagesView: Erro ao carregar mensagens: {ex.Message}");
        }
    }

    private async void MarkAllAsReadButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var viewModel = MainWindowViewModel.Instance;
            if (viewModel != null)
            {
                await viewModel.MarkAllMessagesAsReadAsync();
                // Recarregar a view
                await LoadMessagesAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MessagesView: Erro ao marcar todas como lidas: {ex.Message}");
        }
    }

    private Border CreateMessageItem(UserMessage message, MainWindowViewModel viewModel)
    {
        try
        {
            // Detectar o tema atual
            var currentTheme = Application.Current?.ActualThemeVariant;
            bool isDarkMode = currentTheme == ThemeVariant.Dark;
            
            // Definir background baseado no tema e status de leitura
            IBrush backgroundBrush;
            if (message.IsRead)
            {
                // Mensagem lida: background mais sutil
                backgroundBrush = Avalonia.Media.Brushes.Transparent;
            }
            else
            {
                // Mensagem não lida: background mais destacado
                if (isDarkMode)
                {
                    backgroundBrush = this.FindResource("CardBackgroundBrush") as IBrush ?? 
                                    Avalonia.Media.Brushes.Transparent;
                }
                else
                {
                    backgroundBrush = this.FindResource("CardBackgroundInactiveBrush") as IBrush ?? 
                                    Avalonia.Media.Brushes.White;
                }
            }
            
            // Definir cores do texto baseado no tema
            IBrush primaryTextBrush;
            IBrush secondaryTextBrush;
            
            if (isDarkMode)
            {
                primaryTextBrush = this.FindResource("TextPrimaryBrush") as IBrush ?? 
                                 Avalonia.Media.Brushes.White;
                secondaryTextBrush = this.FindResource("TextSecondaryBrush") as IBrush ?? 
                                   Avalonia.Media.Brushes.LightGray;
            }
            else
            {
                primaryTextBrush = Avalonia.Media.Brushes.Black;
                secondaryTextBrush = Avalonia.Media.Brushes.Black;
            }
            
            // Criar o Border principal
            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 20),
                Background = backgroundBrush,
                BorderThickness = new Thickness(1),
                BorderBrush = this.FindResource("SeparatorBrush") as IBrush ?? 
                             Avalonia.Media.Brushes.Gray
            };

            // Criar Grid principal com 2 colunas: conteúdo e botão
            var mainGrid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,Auto")
            };

            // Criar o StackPanel de conteúdo
            var contentPanel = new StackPanel 
            { 
                Spacing = 8,
                [Grid.ColumnProperty] = 0
            };

            // Criar um StackPanel horizontal para a data, hora e badge NEW
            var headerPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 0,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };

            // Criar o TextBlock da data
            var dateText = new TextBlock
            {
                Text = message.CreatedDate.ToString("dd/MM/yy"),
                FontWeight = FontWeight.Bold,
                FontSize = 16,
                Foreground = primaryTextBrush
            };

            // Criar o TextBlock da hora
            var timeText = new TextBlock
            {
                Text = message.CreatedDate.ToString("HH:mm"),
                FontSize = 11,
                FontWeight = FontWeight.Normal,
                Foreground = secondaryTextBrush,
                Opacity = 0.8,
                Margin = new Thickness(6, 2, 0, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };

            // Adicionar data e hora ao header
            headerPanel.Children.Add(dateText);
            headerPanel.Children.Add(timeText);

            // Adicionar badge "NEW" se não for lida
            if (!message.IsRead)
            {
                var newBadge = new Border
                {
                    Background = Avalonia.Media.Brushes.Red,
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2),
                    Margin = new Thickness(8, 0, 0, 0)
                };

                var badgeText = new TextBlock
                {
                    Text = "NEW",
                    FontSize = 10,
                    FontWeight = FontWeight.Bold,
                    Foreground = Avalonia.Media.Brushes.White
                };

                newBadge.Child = badgeText;
                headerPanel.Children.Add(newBadge);
            }

            // Criar o TextBlock do título
            var titleText = new TextBlock
            {
                Text = message.Title,
                FontWeight = FontWeight.Bold,
                FontSize = 15,
                Foreground = primaryTextBrush,
                TextWrapping = TextWrapping.Wrap
            };

            // Criar o SelectableTextBlock do conteúdo com altura máxima
            var contentText = new SelectableTextBlock
            {
                Text = message.Content,
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                Foreground = secondaryTextBrush,
                MaxHeight = 100 // Altura máxima de 3-4 linhas aproximadamente
            };

            // Variável para controlar o estado de expansão
            bool isExpanded = false;
            
            // Estimar se o texto precisa ser truncado (aproximadamente 150 caracteres = ~3-4 linhas)
            bool needsExpansion = message.Content.Length > 150;

            // Criar botão "Ver mais" / "Ver menos" se necessário
            Button? expandButton = null;
            if (needsExpansion)
            {
                expandButton = new Button
                {
                    Content = "... Ver mais",
                    FontSize = 12,
                    Background = Avalonia.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(0),
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                    Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                    Foreground = this.FindResource("SystemControlBackgroundAccentBrush") as IBrush ?? 
                                Avalonia.Media.Brushes.DodgerBlue,
                    Margin = new Thickness(0, 4, 0, 0)
                };

                expandButton.Click += (s, e) =>
                {
                    isExpanded = !isExpanded;
                    if (isExpanded)
                    {
                        contentText.MaxHeight = double.PositiveInfinity;
                        expandButton.Content = "Ver menos";
                    }
                    else
                    {
                        contentText.MaxHeight = 100;
                        expandButton.Content = "... Ver mais";
                    }
                };
            }

            // Adicionar os elementos ao StackPanel de conteúdo
            contentPanel.Children.Add(headerPanel);
            contentPanel.Children.Add(titleText);
            contentPanel.Children.Add(contentText);
            
            // Adicionar botão de expandir se necessário
            if (expandButton != null)
            {
                contentPanel.Children.Add(expandButton);
            }

            // Criar botão "Marcar como lida" (só aparece se não estiver lida)
            if (!message.IsRead)
            {
                var markAsReadButton = new Button
                {
                    Content = "✓",
                    FontSize = 20,
                    FontWeight = FontWeight.Bold,
                    Background = Avalonia.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Padding = new Thickness(8),
                    Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Margin = new Thickness(0, 0, 0, 0),
                    [Grid.ColumnProperty] = 1,
                    [ToolTip.TipProperty] = Loc.Tr("Mark as Read")
                };

                // Definir cor do botão baseado no tema
                if (isDarkMode)
                {
                    markAsReadButton.Foreground = this.FindResource("SystemControlBackgroundAccentBrush") as IBrush ?? 
                                                Avalonia.Media.Brushes.LightBlue;
                }
                else
                {
                    markAsReadButton.Foreground = this.FindResource("SystemControlBackgroundAccentBrush") as IBrush ?? 
                                                Avalonia.Media.Brushes.DodgerBlue;
                }

                string messageId = message.Id;
                markAsReadButton.Click += async (s, e) =>
                {
                    try
                    {
                        await viewModel.MarkMessageAsReadAsync(messageId);
                        await LoadMessagesAsync();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"MessagesView: Erro ao marcar mensagem como lida: {ex.Message}");
                    }
                };

                mainGrid.Children.Add(markAsReadButton);
            }

            // Adicionar conteúdo ao grid
            mainGrid.Children.Add(contentPanel);
            
            // Definir o Grid como filho do Border
            border.Child = mainGrid;

            return border;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MessagesView: Erro ao criar item de mensagem: {ex.Message}");
            
            // Retornar um Border de erro
            return new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 20),
                Background = Avalonia.Media.Brushes.Transparent,
                BorderThickness = new Thickness(1),
                BorderBrush = this.FindResource("SeparatorBrush") as IBrush ?? 
                             Avalonia.Media.Brushes.Gray,
                Child = new TextBlock
                {
                    Text = Loc.Tr("Error loading message"),
                    FontSize = 14
                }
            };
        }
    }

    private void ShowNoMessagesPlaceholder(StackPanel messagesPanel)
    {
        // Detectar o tema atual
        var currentTheme = Application.Current?.ActualThemeVariant;
        bool isDarkMode = currentTheme == ThemeVariant.Dark;

        // Definir cores baseado no tema
        IBrush backgroundBrush;
        IBrush textBrush;

        if (isDarkMode)
        {
            backgroundBrush = Avalonia.Media.Brushes.Transparent;
            textBrush = this.FindResource("TextSecondaryBrush") as IBrush ?? 
                       Avalonia.Media.Brushes.LightGray;
        }
        else
        {
            backgroundBrush = this.FindResource("CardBackgroundInactiveBrush") as IBrush ?? 
                            Avalonia.Media.Brushes.White;
            textBrush = Avalonia.Media.Brushes.Black;
        }

        var placeholderBorder = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(40),
            Background = backgroundBrush,
            BorderThickness = new Thickness(1),
            BorderBrush = this.FindResource("SeparatorBrush") as IBrush ?? 
                         Avalonia.Media.Brushes.Gray,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Margin = new Thickness(0, 50, 0, 0)
        };

        var placeholderPanel = new StackPanel
        {
            Spacing = 12,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
        };

        var iconText = new TextBlock
        {
            Text = "✉",
            FontSize = 48,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Foreground = textBrush,
            Opacity = 0.5
        };

        var messageText = new TextBlock
        {
            Text = Loc.Tr("No messages available"),
            FontSize = 16,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            Foreground = textBrush
        };

        placeholderPanel.Children.Add(iconText);
        placeholderPanel.Children.Add(messageText);
        placeholderBorder.Child = placeholderPanel;

        messagesPanel.Children.Add(placeholderBorder);
    }
}
