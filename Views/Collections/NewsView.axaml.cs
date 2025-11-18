using Avalonia.Controls;
using System.Threading.Tasks;
using System;
using SharedClientSide.ServerInteraction;
using LesserDashboardClient.ViewModels;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Styling;
using CodingSeb.Localization;

namespace LesserDashboardClient.Views.Collections;

/// <summary>
/// Interaction logic for NewsView.axaml
/// </summary>
public partial class NewsView : UserControl
{
    // Constante configurável para o tempo limite de exibição do indicativo "NEW"
    // Pode ser facilmente alterada para 48 horas, 12 horas, etc.
    private static readonly TimeSpan NEW_INDICATOR_TIME_LIMIT = TimeSpan.FromDays(7);
    
    public NewsView()
    {
        InitializeComponent();
        Loaded += NewsView_Loaded;
    }

    private async void NewsView_Loaded(object sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        await LoadNewsAsync();
    }

    private async Task LoadNewsAsync()
    {
        try
        {
            if (ViewModels.GlobalAppStateViewModel.lfc == null)
            {
                System.Diagnostics.Debug.WriteLine("NewsView: lfc é null");
                return;
            }

            // Encontrar o painel de notícias
            var newsPanel = this.FindControl<StackPanel>("NewsPanel");
            System.Diagnostics.Debug.WriteLine($"NewsView: NewsPanel encontrado: {newsPanel != null}");
            
            if (newsPanel == null)
            {
                System.Diagnostics.Debug.WriteLine("NewsView: NewsPanel não encontrado!");
                return;
            }

            // Mostrar loading placeholder (já está no XAML)
            System.Diagnostics.Debug.WriteLine("NewsView: Mostrando loading...");

            // Obter o idioma atual das configurações
            string currentLanguage = GetCurrentLanguageCode();
            System.Diagnostics.Debug.WriteLine($"NewsView: Idioma atual: {currentLanguage}");
            
            // Chamar a API diretamente
            var result = await ViewModels.GlobalAppStateViewModel.lfc.GetAllNewsByLanguage(currentLanguage);
            
            // Verificar se o resultado é null
            if (result == null)
            {
                System.Diagnostics.Debug.WriteLine("NewsView: API retornou null");
                ShowErrorMessage(newsPanel, Loc.Tr("Error loading news. Please try again."));
                return;
            }
            
            System.Diagnostics.Debug.WriteLine($"NewsView: API retornou success={result.success}, Content count={result.Content?.Count ?? 0}");
            
            if (!result.success)
            {
                System.Diagnostics.Debug.WriteLine($"NewsView: API falhou com mensagem: {result.message}");
                // Mostrar mensagem de erro no lugar do loading
                ShowErrorMessage(newsPanel, Loc.Tr("Error loading news. Please try again."));
                return;
            }

            if (result.Content != null && result.Content.Count > 0)
            {
                System.Diagnostics.Debug.WriteLine("NewsView: Limpando skeleton loading e adicionando notícias da API");
                
                // Limpar o skeleton loading
                newsPanel.Children.Clear();

                // Processar e adicionar notícias da API
                var newsItems = new List<dynamic>();
                foreach (var item in result.Content)
                {
                    try
                    {
                        var json = JsonConvert.SerializeObject(item);
                        var news = JsonConvert.DeserializeObject<dynamic>(json);
                        if (news != null)
                        {
                            newsItems.Add(news);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"NewsView: Erro ao deserializar item: {ex.Message}");
                    }
                }

                System.Diagnostics.Debug.WriteLine($"NewsView: {newsItems.Count} notícias desserializadas");

                // Ordenar por data (mais recente primeiro)
                var sortedNews = newsItems.OrderByDescending(n => 
                {
                    try
                    {
                        return DateTime.Parse(n.publishDate.ToString());
                    }
                    catch
                    {
                        return DateTime.MinValue;
                    }
                });

                // Adicionar cada notícia ao painel
                foreach (var news in sortedNews)
                {
                    try
                    {
                        var newsItem = CreateNewsItem(news);
                        newsPanel.Children.Add(newsItem);
                        System.Diagnostics.Debug.WriteLine($"NewsView: Notícia adicionada. Total: {newsPanel.Children.Count}");
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"NewsView: Erro ao criar notícia: {ex.Message}");
                    }
                }
                
                System.Diagnostics.Debug.WriteLine($"NewsView: Total de notícias adicionadas: {newsPanel.Children.Count}");
            }
            else
            {
                // Nenhuma notícia encontrada
                System.Diagnostics.Debug.WriteLine("NewsView: Nenhuma notícia encontrada");
                ShowErrorMessage(newsPanel, Loc.Tr("No news available at the moment."));
            }
            
            // Forçar atualização da interface
            this.InvalidateVisual();
            this.InvalidateArrange();
            this.InvalidateMeasure();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NewsView: Erro geral: {ex.Message}");
            
            // Mostrar mensagem de erro
            var newsPanel = this.FindControl<StackPanel>("NewsPanel");
            if (newsPanel != null)
            {
                ShowErrorMessage(newsPanel, Loc.Tr("Error loading news. Please try again."));
            }
        }
    }

    /// <summary>
    /// Verifica se uma notícia é considerada "nova" baseada na data de publicação
    /// </summary>
    /// <param name="publishDateString">Data de publicação no formato string</param>
    /// <returns>True se a notícia foi publicada dentro do tempo limite configurado</returns>
    private bool IsNewsNew(string publishDateString)
    {
        try
        {
            if (DateTime.TryParse(publishDateString, out DateTime publishDate))
            {
                // Converter para horário de Brasília (UTC-3)
                var brasiliaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
                var currentTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, brasiliaTimeZone);
                
                // Calcular a diferença de tempo
                var timeDifference = currentTime - publishDate;
                
                // Verificar se está dentro do limite configurado
                return timeDifference <= NEW_INDICATOR_TIME_LIMIT;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NewsView: Erro ao verificar se notícia é nova: {ex.Message}");
        }
        
        return false;
    }

    /// <summary>
    /// Cria o badge visual "NEW" para notícias recentes
    /// </summary>
    /// <param name="isDarkMode">Se está em modo escuro</param>
    /// <returns>Border com o badge "NEW"</returns>
    private Border CreateNewBadge(bool isDarkMode)
    {
        var badge = new Border
        {
            Background = Avalonia.Media.Brushes.Red,
            CornerRadius = new Avalonia.CornerRadius(4),
            Padding = new Avalonia.Thickness(6, 2),
            Margin = new Avalonia.Thickness(0, 0, 0, 0)
        };

        var badgeText = new TextBlock
        {
            Text = "NEW",
            FontSize = 10,
            FontWeight = Avalonia.Media.FontWeight.Bold,
            Foreground = Avalonia.Media.Brushes.White,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
        };

        badge.Child = badgeText;
        return badge;
    }

    private Border CreateNewsItem(dynamic news)
    {
        try
        {
            string dateStr = ParseDate(news.publishDate.ToString());
            string contentStr = news.content.ToString();
            string publishDateString = news.publishDate.ToString();
            
            // Verificar se a notícia é nova
            bool isNew = IsNewsNew(publishDateString);
            
            System.Diagnostics.Debug.WriteLine($"NewsView: Criando notícia - Data: {dateStr}, Content length: {contentStr.Length}, IsNew: {isNew}");
            
            // Detectar o tema atual
            var currentTheme = Application.Current?.ActualThemeVariant;
            bool isDarkMode = currentTheme == ThemeVariant.Dark;
            
            // Definir background baseado no tema
            Avalonia.Media.IBrush backgroundBrush;
            if (isDarkMode)
            {
                backgroundBrush = Avalonia.Media.Brushes.Transparent; // Dark mode: transparente
            }
            else
            {
                backgroundBrush = this.FindResource("CardBackgroundBrush") as Avalonia.Media.IBrush ?? 
                                this.FindResource("CardBackgroundInactiveBrush") as Avalonia.Media.IBrush ?? 
                                Avalonia.Media.Brushes.White; // Light mode: fundo claro
            }
            
            // Definir cores do texto baseado no tema
            Avalonia.Media.IBrush primaryTextBrush;
            Avalonia.Media.IBrush secondaryTextBrush;
            
            if (isDarkMode)
            {
                primaryTextBrush = this.FindResource("TextPrimaryBrush") as Avalonia.Media.IBrush ?? 
                                 this.FindResource("Foreground") as Avalonia.Media.IBrush ?? 
                                 Avalonia.Media.Brushes.White;
                secondaryTextBrush = this.FindResource("TextSecondaryBrush") as Avalonia.Media.IBrush ?? 
                                   this.FindResource("TextBlockForeground") as Avalonia.Media.IBrush ?? 
                                   Avalonia.Media.Brushes.LightGray;
            }
            else
            {
                primaryTextBrush = Avalonia.Media.Brushes.Black; // Light mode: texto preto
                secondaryTextBrush = Avalonia.Media.Brushes.Black; // Light mode: texto preto
            }
            
            // Criar o Border principal com estilo adaptável ao tema
            var border = new Border
            {
                CornerRadius = new Avalonia.CornerRadius(8),
                Padding = new Avalonia.Thickness(20),
                Margin = new Avalonia.Thickness(0, 0, 0, 20),
                Background = backgroundBrush,
                BorderThickness = new Avalonia.Thickness(1),
                BorderBrush = this.FindResource("SeparatorBrush") as Avalonia.Media.IBrush ?? 
                             this.FindResource("BorderBrush") as Avalonia.Media.IBrush ?? 
                             Avalonia.Media.Brushes.Gray
            };

            // Criar o StackPanel interno
            var stackPanel = new StackPanel 
            { 
                Spacing = 8,
                Orientation = Avalonia.Layout.Orientation.Vertical
            };

            // Criar um StackPanel horizontal para a data e o indicativo NEW
            var headerPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                Spacing = 0,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };

            // Criar o TextBlock da data (adaptável ao tema)
            var dateText = new TextBlock
            {
                Text = dateStr,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                FontSize = 16,
                Foreground = primaryTextBrush
            };

            // Criar o TextBlock da hora (pequeno e bonito)
            var timeStr = ParseTime(publishDateString);
            var timeText = new TextBlock
            {
                Text = timeStr,
                FontSize = 11,
                FontWeight = Avalonia.Media.FontWeight.Normal,
                Foreground = secondaryTextBrush,
                Opacity = 0.8,
                Margin = new Avalonia.Thickness(6, 2, 0, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };

            // Adicionar a data e hora ao header
            headerPanel.Children.Add(dateText);
            if (!string.IsNullOrEmpty(timeStr))
            {
                headerPanel.Children.Add(timeText);
            }

            // Adicionar indicativo "NEW" se a notícia for nova
            if (isNew)
            {
                var newBadge = CreateNewBadge(isDarkMode);
                headerPanel.Children.Add(newBadge);
            }

            // Criar o SelectableTextBlock do conteúdo (adaptável ao tema)
            var contentText = new SelectableTextBlock
            {
                Text = contentStr,
                FontSize = 14,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Foreground = secondaryTextBrush
            };

            // Adicionar os elementos ao StackPanel
            stackPanel.Children.Add(headerPanel);
            stackPanel.Children.Add(contentText);
            
            // Definir o StackPanel como filho do Border
            border.Child = stackPanel;

            System.Diagnostics.Debug.WriteLine($"NewsView: Notícia criada com sucesso");
            return border;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NewsView: Erro em CreateNewsItem: {ex.Message}\n{ex.StackTrace}");
            
            // Detectar o tema atual para o erro
            var currentTheme = Application.Current?.ActualThemeVariant;
            bool isDarkMode = currentTheme == ThemeVariant.Dark;
            
            // Definir background e texto para erro baseado no tema
            Avalonia.Media.IBrush errorBackgroundBrush;
            Avalonia.Media.IBrush errorTextBrush;
            
            if (isDarkMode)
            {
                errorBackgroundBrush = Avalonia.Media.Brushes.Transparent;
                errorTextBrush = this.FindResource("TextSecondaryBrush") as Avalonia.Media.IBrush ?? 
                               this.FindResource("TextBlockForeground") as Avalonia.Media.IBrush ?? 
                               Avalonia.Media.Brushes.LightGray;
            }
            else
            {
                errorBackgroundBrush = this.FindResource("CardBackgroundBrush") as Avalonia.Media.IBrush ?? 
                                     this.FindResource("CardBackgroundInactiveBrush") as Avalonia.Media.IBrush ?? 
                                     Avalonia.Media.Brushes.White;
                errorTextBrush = Avalonia.Media.Brushes.Black;
            }
            
            // Retornar um Border de erro com estilo adaptável ao tema
            var errorBorder = new Border
            {
                CornerRadius = new Avalonia.CornerRadius(8),
                Padding = new Avalonia.Thickness(20),
                Margin = new Avalonia.Thickness(0, 0, 0, 20),
                Background = errorBackgroundBrush,
                BorderThickness = new Avalonia.Thickness(1),
                BorderBrush = this.FindResource("SeparatorBrush") as Avalonia.Media.IBrush ?? 
                             this.FindResource("BorderBrush") as Avalonia.Media.IBrush ?? 
                             Avalonia.Media.Brushes.Gray,
                Child = new SelectableTextBlock
                {
                    Text = "Erro ao carregar notícia",
                    Foreground = errorTextBrush,
                    FontSize = 14
                }
            };
            return errorBorder;
        }
    }

    private string GetCurrentLanguageCode()
    {
        try
        {
            string currentLanguage = ViewModels.GlobalAppStateViewModel.options?.Language ?? "en-US";
            return currentLanguage.StartsWith("pt") ? "pt-BR" : "en";
        }
        catch
        {
            return "en";
        }
    }

    private string ParseDate(string dateString)
    {
        try
        {
            if (DateTime.TryParse(dateString, out DateTime date))
            {
                // Converter para o fuso horário de Brasília
                var brasiliaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
                var brasiliaDate = TimeZoneInfo.ConvertTimeFromUtc(date.ToUniversalTime(), brasiliaTimeZone);
                
                return brasiliaDate.ToString("dd/MM/yy");
            }
            return "Data inválida";
        }
        catch
        {
            return "Data inválida";
        }
    }

    private string ParseDateWithTime(string dateString)
    {
        try
        {
            if (DateTime.TryParse(dateString, out DateTime date))
            {
                // Converter para o fuso horário de Brasília
                var brasiliaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
                var brasiliaDate = TimeZoneInfo.ConvertTimeFromUtc(date.ToUniversalTime(), brasiliaTimeZone);
                
                return brasiliaDate.ToString("dd/MM/yy");
            }
            return "Data inválida";
        }
        catch
        {
            return "Data inválida";
        }
    }

    private string ParseTime(string dateString)
    {
        try
        {
            if (DateTime.TryParse(dateString, out DateTime date))
            {
                // Converter para o fuso horário de Brasília
                var brasiliaTimeZone = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
                var brasiliaDate = TimeZoneInfo.ConvertTimeFromUtc(date.ToUniversalTime(), brasiliaTimeZone);
                
                return brasiliaDate.ToString("HH:mm");
            }
            return "";
        }
        catch
        {
            return "";
        }
    }

    private void ShowErrorMessage(StackPanel newsPanel, string message)
    {
        try
        {
            // Limpar o painel
            newsPanel.Children.Clear();

            // Detectar o tema atual
            var currentTheme = Application.Current?.ActualThemeVariant;
            bool isDarkMode = currentTheme == ThemeVariant.Dark;

            // Definir cores baseado no tema
            Avalonia.Media.IBrush backgroundBrush;
            Avalonia.Media.IBrush textBrush;

            if (isDarkMode)
            {
                backgroundBrush = Avalonia.Media.Brushes.Transparent;
                textBrush = this.FindResource("TextSecondaryBrush") as Avalonia.Media.IBrush ?? 
                           this.FindResource("TextBlockForeground") as Avalonia.Media.IBrush ?? 
                           Avalonia.Media.Brushes.LightGray;
            }
            else
            {
                backgroundBrush = this.FindResource("CardBackgroundBrush") as Avalonia.Media.IBrush ?? 
                                this.FindResource("CardBackgroundInactiveBrush") as Avalonia.Media.IBrush ?? 
                                Avalonia.Media.Brushes.White;
                textBrush = Avalonia.Media.Brushes.Black;
            }

            // Criar StackPanel interno para mensagem e botão
            var contentPanel = new StackPanel
            {
                Spacing = 16,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };

            // Criar TextBlock da mensagem
            var messageText = new SelectableTextBlock
            {
                Text = message,
                Foreground = textBrush,
                FontSize = 14,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
            };

            // Criar botão de refresh
            var refreshButton = new Button
            {
                Content = Loc.Tr("Atualizar"),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                Padding = new Avalonia.Thickness(16, 8),
                Margin = new Avalonia.Thickness(0, 8, 0, 0),
                Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand)
            };

            // Definir estilo do botão baseado no tema
            if (isDarkMode)
            {
                // Tema escuro: usar cor escura com texto branco
                refreshButton.Background = this.FindResource("SystemChromeBlackMediumLowColor") as Avalonia.Media.IBrush ?? 
                                          Avalonia.Media.Brushes.DarkGray;
                refreshButton.Foreground = Avalonia.Media.Brushes.White;
            }
            else
            {
                // Tema claro: usar cor de destaque (accent) que se adapta bem ao tema claro
                refreshButton.Background = this.FindResource("SystemControlBackgroundAccentBrush") as Avalonia.Media.IBrush ?? 
                                          Avalonia.Media.Brushes.SlateBlue;
                refreshButton.Foreground = Avalonia.Media.Brushes.White;
            }

            // Adicionar evento de clique para recarregar os avisos
            refreshButton.Click += async (sender, e) =>
            {
                try
                {
                    refreshButton.IsEnabled = false;
                    refreshButton.Content = Loc.Tr("Carregando...");
                    await LoadNewsAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"NewsView: Erro ao recarregar avisos: {ex.Message}");
                }
                finally
                {
                    refreshButton.IsEnabled = true;
                    refreshButton.Content = Loc.Tr("Atualizar");
                }
            };

            // Adicionar mensagem e botão ao painel de conteúdo
            contentPanel.Children.Add(messageText);
            contentPanel.Children.Add(refreshButton);

            // Criar border de erro
            var errorBorder = new Border
            {
                CornerRadius = new Avalonia.CornerRadius(8),
                Padding = new Avalonia.Thickness(20),
                Margin = new Avalonia.Thickness(0, 0, 0, 20),
                Background = backgroundBrush,
                BorderThickness = new Avalonia.Thickness(1),
                BorderBrush = this.FindResource("SeparatorBrush") as Avalonia.Media.IBrush ?? 
                             this.FindResource("BorderBrush") as Avalonia.Media.IBrush ?? 
                             Avalonia.Media.Brushes.Gray,
                Child = contentPanel
            };

            newsPanel.Children.Add(errorBorder);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NewsView: Erro ao mostrar mensagem de erro: {ex.Message}");
        }
    }
}

