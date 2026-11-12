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
    // Cabeçalho "Não lidas (N)" da lista atual — o número é atualizado ao marcar uma mensagem sem recarregar.
    private TextBlock? _unreadSectionHeader;

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

            _unreadSectionHeader = null;
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
                        _unreadSectionHeader = CreateSectionHeader($"{Loc.Tr("Unread section")} ({unreadCount})", highlighted: true);
                        messagesPanel.Children.Add(_unreadSectionHeader);
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

    // Cartão no estilo caixa de e-mail: fechado mostra data, selo, título e 1 linha de prévia; clicar no título
    // abre o texto e marca como lida; "Marcar como lida" ao lado do título marca sem abrir. Marcar não recarrega a
    // lista (a mensagem fica onde está e só muda de seção no próximo carregamento) — só troca o visual do cartão e
    // atualiza o resumo do topo (o contador do sino segue o ViewModel).
    private Border CreateMessageItem(UserMessage message, MainWindowViewModel viewModel)
    {
        try
        {
            var isDarkMode = Application.Current?.ActualThemeVariant == ThemeVariant.Dark;
            var accent = GetAccentColor();
            IBrush primaryTextBrush = isDarkMode
                ? this.FindResource("TextPrimaryBrush") as IBrush ?? Avalonia.Media.Brushes.White
                : Avalonia.Media.Brushes.Black;
            IBrush secondaryTextBrush = isDarkMode
                ? this.FindResource("TextSecondaryBrush") as IBrush ?? Avalonia.Media.Brushes.LightGray
                : Avalonia.Media.Brushes.Black;
            var handCursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand);

            var border = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20, 16),
                Margin = new Thickness(0, 0, 0, 12)
            };

            // ---- Cabeçalho clicável: data/hora + selo, seta, título e prévia ----
            var createdAt = message.CreatedDate;
            if (createdAt.Kind == DateTimeKind.Unspecified)
                createdAt = DateTime.SpecifyKind(createdAt, DateTimeKind.Utc);
            var createdAtLocal = createdAt.ToLocalTime();

            var badgeHost = new ContentControl { Margin = new Thickness(10, 0, 0, 0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            var dateLine = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Children =
                {
                    new TextBlock { Text = createdAtLocal.ToString("dd/MM/yy"), FontWeight = FontWeight.Bold, FontSize = 13, Foreground = primaryTextBrush, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
                    new TextBlock { Text = createdAtLocal.ToString("HH:mm"), FontSize = 11, Foreground = secondaryTextBrush, Opacity = 0.8, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center },
                    badgeHost
                }
            };

            var chevron = new TextBlock { Text = "▸", FontSize = 14, Foreground = secondaryTextBrush, Margin = new Thickness(0, 1, 8, 0), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };
            var titleText = new TextBlock { Text = message.Title, Foreground = primaryTextBrush, TextWrapping = TextWrapping.Wrap };
            var titleLine = new DockPanel { Children = { chevron, titleText } };
            DockPanel.SetDock(chevron, Avalonia.Controls.Dock.Left);

            var preview = new TextBlock
            {
                Text = (message.Content ?? "").Replace("\r", " ").Replace("\n", " "),
                FontSize = 13,
                Foreground = secondaryTextBrush,
                Opacity = 0.75,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxLines = 1,
                Margin = new Thickness(22, 0, 0, 0)
            };

            var clickArea = new StackPanel
            {
                Spacing = 6,
                Background = Avalonia.Media.Brushes.Transparent, // recebe o clique no espaço vazio também
                Cursor = handCursor,
                Children = { dateLine, titleLine, preview },
                [ToolTip.TipProperty] = Loc.Tr("Open message"),
                [Grid.ColumnProperty] = 0
            };

            // ---- Botão ao lado do título: marca sem abrir ----
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
                Cursor = handCursor,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                Margin = new Thickness(16, 0, 0, 0),
                [Grid.ColumnProperty] = 1
            };

            var headerGrid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), Children = { clickArea, markAsReadButton } };

            // ---- Corpo (aberto ao clicar) ----
            var contentText = new SelectableTextBlock
            {
                Text = message.Content,
                FontSize = 14,
                LineHeight = 21,
                TextWrapping = TextWrapping.Wrap,
                Foreground = primaryTextBrush,
                Margin = new Thickness(22, 12, 0, 0),
                IsVisible = false
            };

            border.Child = new StackPanel { Children = { headerGrid, contentText } };

            // ---- Visual por situação ----
            void ApplyState()
            {
                var read = message.IsRead;
                border.Background = read ? Avalonia.Media.Brushes.Transparent : new SolidColorBrush(accent, isDarkMode ? 0.18 : 0.10);
                border.BorderThickness = read ? new Thickness(1) : new Thickness(6, 1, 1, 1);
                border.BorderBrush = read
                    ? this.FindResource("SeparatorBrush") as IBrush ?? Avalonia.Media.Brushes.Gray
                    : new SolidColorBrush(accent);
                titleText.FontWeight = read ? FontWeight.SemiBold : FontWeight.Bold;
                titleText.FontSize = read ? 15 : 17;
                markAsReadButton.IsVisible = !read;
                badgeHost.Content = read
                    ? new TextBlock { Text = "✓ " + Loc.Tr("Read badge"), FontSize = 11, Foreground = secondaryTextBrush, Opacity = 0.8 }
                    : new Border
                    {
                        Background = new SolidColorBrush(accent),
                        CornerRadius = new CornerRadius(10),
                        Padding = new Thickness(8, 2),
                        Child = new TextBlock { Text = "● " + Loc.Tr("Unread badge"), FontSize = 11, FontWeight = FontWeight.Bold, Foreground = Avalonia.Media.Brushes.White }
                    };
                // Lida e fechada fica mais apagada; aberta fica legível.
                border.Opacity = read && !contentText.IsVisible ? 0.75 : 1;
            }

            async Task MarkAsReadAsync()
            {
                if (message.IsRead)
                    return;
                try
                {
                    await viewModel.MarkMessageAsReadAsync(message.Id);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"MessagesView: Erro ao marcar mensagem como lida: {ex.Message}");
                }
                ApplyState();
                RefreshUnreadIndicators(viewModel);
            }

            clickArea.PointerPressed += async (_, e) =>
            {
                if (!e.GetCurrentPoint(clickArea).Properties.IsLeftButtonPressed)
                    return;
                var open = !contentText.IsVisible;
                contentText.IsVisible = open;
                preview.IsVisible = !open;
                chevron.Text = open ? "▾" : "▸";
                ApplyState();
                if (open)
                    await MarkAsReadAsync();
            };

            markAsReadButton.Click += async (_, _) => await MarkAsReadAsync();

            ApplyState();
            return border;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"MessagesView: Erro ao criar item de mensagem: {ex.Message}");

            return new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(20),
                Margin = new Thickness(0, 0, 0, 12),
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

    // Depois de marcar uma mensagem sem recarregar a lista: resumo do topo, contador da seção e "Marcar todas".
    private void RefreshUnreadIndicators(MainWindowViewModel viewModel)
    {
        var total = viewModel.UserMessages?.Count ?? 0;
        var unread = viewModel.UserMessages?.Count(m => !m.IsRead) ?? 0;
        UpdateUnreadSummary(total, unread);
        if (_unreadSectionHeader != null)
            _unreadSectionHeader.Text = $"{Loc.Tr("Unread section")} ({unread})".ToUpperInvariant();
        var markAllButton = this.FindControl<Button>("MarkAllAsReadButton");
        if (markAllButton != null)
            markAllButton.IsVisible = unread > 0;
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
