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
        
        // Tentar obter o MainWindowViewModel através da árvore visual se o DataContext não for ele
        this.GetObservable(DataContextProperty).Subscribe(async _ =>
        {
            // Se o DataContext mudou e a view está visível, recarregar
            if (IsVisible)
            {
                await LoadMessagesAsync();
            }
        });
    }
    
    /// <summary>
    /// Obtém o MainWindowViewModel através do DataContext ou da árvore visual
    /// </summary>
    private MainWindowViewModel? GetMainWindowViewModel()
    {
        // Primeiro, tenta usar o DataContext direto
        if (DataContext is MainWindowViewModel mainVM)
        {
            return mainVM;
        }
        
        // Se não for, busca na árvore visual (Window -> MainWindowViewModel)
        var parent = this.Parent;
        while (parent != null)
        {
            if (parent is Window window && window.DataContext is MainWindowViewModel windowVM)
            {
                return windowVM;
            }
            parent = parent.Parent;
        }
        
        // Fallback: usar o singleton (menos ideal, mas funcional)
        return MainWindowViewModel.Instance;
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

            // Obter o ViewModel via DataContext ou árvore visual (MVVM-friendly)
            var viewModel = GetMainWindowViewModel();
            if (viewModel == null)
            {
                System.Diagnostics.Debug.WriteLine("MessagesView: Não foi possível obter MainWindowViewModel");
                return;
            }

            // A View apenas solicita o carregamento - a lógica de "precisa carregar?" está no ViewModel
            await viewModel.LoadUserMessagesAsync();

            System.Diagnostics.Debug.WriteLine($"MessagesView: Total de mensagens: {viewModel.UserMessages?.Count ?? 0}");

            // Limpar skeleton loading
            messagesPanel.Children.Clear();

            // Usar SortedUserMessages do ViewModel (ordenação está no VM, não na View)
            var sortedMessages = viewModel.SortedUserMessages;

            var messagesList = sortedMessages?.ToList() ?? new System.Collections.Generic.List<UserMessage>();
            var unreadCount = messagesList.Count(m => !m.IsRead);
            UpdateUnreadSummary(messagesList.Count, unreadCount);

            // Verificar se há mensagens
            if (messagesList.Any())
            {
                // Não lidas primeiro (ordem do ViewModel), cada grupo com o seu cabeçalho.
                bool unreadHeaderAdded = false, readHeaderAdded = false;
                foreach (var message in messagesList)
                {
                    if (!message.IsRead && !unreadHeaderAdded)
                    {
                        messagesPanel.Children.Add(CreateSectionHeader($"{Loc.Tr("Unread section")} ({unreadCount})", highlighted: true));
                        unreadHeaderAdded = true;
                    }
                    else if (message.IsRead && !readHeaderAdded && unreadCount > 0)
                    {
                        messagesPanel.Children.Add(CreateSectionHeader(Loc.Tr("Read section"), highlighted: false));
                        readHeaderAdded = true;
                    }

                    var messageItem = CreateMessageItem(message, viewModel);
                    messagesPanel.Children.Add(messageItem);
                }

                // Mostrar botão "Marcar todas como lidas" se houver mensagens não lidas
                if (markAllButton != null)
                {
                    markAllButton.IsVisible = unreadCount > 0;
                    // Remover handler anterior para evitar duplicação
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
            // Obter ViewModel via método helper (MVVM-friendly)
            var viewModel = GetMainWindowViewModel();
            if (viewModel != null)
            {
                // Usar o Command do ViewModel (já existe como RelayCommand)
                await viewModel.MarkAllMessagesAsReadAsync();
                // Recarregar a view para atualizar a UI
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
            
            // Não lida: fundo levemente na cor de destaque + barra lateral; lida: sem fundo e mais apagada.
            var accent = GetAccentColor();
            IBrush backgroundBrush = message.IsRead
                ? Avalonia.Media.Brushes.Transparent
                : new SolidColorBrush(accent, isDarkMode ? 0.18 : 0.10);
            
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
                BorderThickness = message.IsRead ? new Thickness(1) : new Thickness(6, 1, 1, 1),
                BorderBrush = message.IsRead
                    ? this.FindResource("SeparatorBrush") as IBrush ?? Avalonia.Media.Brushes.Gray
                    : new SolidColorBrush(accent),
                Opacity = message.IsRead ? 0.75 : 1
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

            // Converter createdAt para horário local (o backend normalmente envia UTC)
            var createdAt = message.CreatedDate;
            if (createdAt.Kind == DateTimeKind.Unspecified)
                createdAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc);
            var createdAtLocal = createdAt.ToLocalTime();

            // Criar o TextBlock da data
            var dateText = new TextBlock
            {
                Text = createdAtLocal.ToString("dd/MM/yy"),
                FontWeight = FontWeight.Bold,
                FontSize = 16,
                Foreground = primaryTextBrush
            };

            // Criar o TextBlock da hora
            var timeText = new TextBlock
            {
                Text = createdAtLocal.ToString("HH:mm"),
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

            // Selo de situação: "● NÃO LIDA" em destaque, ou "✓ Lida" discreto.
            if (!message.IsRead)
            {
                headerPanel.Children.Add(new Border
                {
                    Background = new SolidColorBrush(accent),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(8, 2),
                    Margin = new Thickness(10, 0, 0, 0),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Child = new TextBlock
                    {
                        Text = "● " + Loc.Tr("Unread badge"),
                        FontSize = 11,
                        FontWeight = FontWeight.Bold,
                        Foreground = Avalonia.Media.Brushes.White
                    }
                });
            }
            else
            {
                headerPanel.Children.Add(new TextBlock
                {
                    Text = "✓ " + Loc.Tr("Read badge"),
                    FontSize = 11,
                    Foreground = secondaryTextBrush,
                    Opacity = 0.8,
                    Margin = new Thickness(10, 2, 0, 0),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
                });
            }

            // Criar o TextBlock do título
            var titleText = new TextBlock
            {
                Text = message.Title,
                FontWeight = message.IsRead ? FontWeight.SemiBold : FontWeight.Bold,
                FontSize = message.IsRead ? 15 : 17,
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
                // Botão com texto e cor de destaque (antes era só um "✓" solto, que não parecia clicável).
                var markAsReadButton = new Button
                {
                    Content = "✓  " + Loc.Tr("Mark as Read"),
                    FontSize = 13,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = Avalonia.Media.Brushes.White,
                    Background = new SolidColorBrush(accent),
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(14, 8),
                    Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                    Margin = new Thickness(16, 0, 0, 0),
                    [Grid.ColumnProperty] = 1
                };

                string messageId = message.Id;
                markAsReadButton.Click += async (s, e) =>
                {
                    try
                    {
                        // Usar o Command do ViewModel (já existe como RelayCommand)
                        await viewModel.MarkMessageAsReadAsync(messageId);
                        // Recarregar a view para atualizar a UI
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

    /// <summary>Cor de destaque do tema (laranja LetsPic); fallback se o recurso não existir.</summary>
    private Color GetAccentColor() =>
        (this.FindResource("SystemControlBackgroundAccentBrush") as ISolidColorBrush)?.Color ?? Color.Parse("#FF9600");

    /// <summary>Faixa do topo: quantas mensagens faltam ler (em destaque) ou "tudo em dia".</summary>
    private void UpdateUnreadSummary(int totalCount, int unreadCount)
    {
        var summary = this.FindControl<Border>("UnreadSummaryBorder");
        var icon = this.FindControl<TextBlock>("UnreadSummaryIcon");
        var text = this.FindControl<TextBlock>("UnreadSummaryText");
        var hint = this.FindControl<TextBlock>("UnreadSummaryHint");
        if (summary == null || icon == null || text == null || hint == null)
            return;

        summary.IsVisible = totalCount > 0;
        if (totalCount == 0)
            return;

        var accent = GetAccentColor();
        var isDarkMode = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
        if (unreadCount > 0)
        {
            summary.Background = new SolidColorBrush(accent, isDarkMode ? 0.22 : 0.14);
            summary.BorderBrush = new SolidColorBrush(accent);
            icon.Text = "✉";
            icon.Foreground = new SolidColorBrush(accent);
            text.Text = unreadCount == 1
                ? Loc.Tr("Unread messages one")
                : string.Format(Loc.Tr("Unread messages many"), unreadCount);
            hint.Text = Loc.Tr("Unread messages hint");
            hint.IsVisible = true;
        }
        else
        {
            summary.Background = Avalonia.Media.Brushes.Transparent;
            summary.BorderBrush = this.FindResource("SeparatorBrush") as IBrush ?? Avalonia.Media.Brushes.Gray;
            icon.Text = "✓";
            icon.Foreground = this.FindResource("TextSecondaryBrush") as IBrush ?? Avalonia.Media.Brushes.Gray;
            text.Text = Loc.Tr("All messages read");
            hint.IsVisible = false;
        }
    }

    private TextBlock CreateSectionHeader(string title, bool highlighted) => new TextBlock
    {
        Text = title.ToUpperInvariant(),
        FontSize = 12,
        FontWeight = FontWeight.Bold,
        LetterSpacing = 0.5,
        Foreground = highlighted
            ? new SolidColorBrush(GetAccentColor())
            : this.FindResource("TextSecondaryBrush") as IBrush ?? Avalonia.Media.Brushes.Gray,
        Margin = new Thickness(2, 4, 0, -8)
    };

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
