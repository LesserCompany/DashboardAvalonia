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

namespace LesserDashboardClient.Views.Collections;

/// <summary>
/// Interaction logic for NewsView.axaml
/// </summary>
public partial class NewsView : UserControl
{
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
            
            System.Diagnostics.Debug.WriteLine($"NewsView: API retornou success={result.success}, Content count={result.Content?.Count ?? 0}");
            
            if (!result.success)
            {
                System.Diagnostics.Debug.WriteLine($"NewsView: API falhou com mensagem: {result.message}");
                // Mostrar mensagem de erro no lugar do loading
                ShowErrorMessage(newsPanel, "Erro ao carregar notícias. Tente novamente.");
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
                ShowErrorMessage(newsPanel, "Nenhuma notícia disponível no momento.");
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
                ShowErrorMessage(newsPanel, "Erro ao carregar notícias. Tente novamente.");
            }
        }
    }

    private Border CreateNewsItem(dynamic news)
    {
        try
        {
            string dateStr = ParseDate(news.publishDate.ToString());
            string contentStr = news.content.ToString();
            
            System.Diagnostics.Debug.WriteLine($"NewsView: Criando notícia - Data: {dateStr}, Content length: {contentStr.Length}");
            
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

            // Criar o TextBlock da data (adaptável ao tema)
            var dateText = new TextBlock
            {
                Text = dateStr,
                FontWeight = Avalonia.Media.FontWeight.Bold,
                FontSize = 16,
                Foreground = primaryTextBrush
            };

            // Criar o SelectableTextBlock do conteúdo (adaptável ao tema)
            var contentText = new SelectableTextBlock
            {
                Text = contentStr,
                FontSize = 14,
                TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                Foreground = secondaryTextBrush
            };

            // Adicionar os elementos ao StackPanel
            stackPanel.Children.Add(dateText);
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
                return date.ToString("dd/MM/yy");
            }
            return "Data inválida";
        }
        catch
        {
            return "Data inválida";
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
                Child = new SelectableTextBlock
                {
                    Text = message,
                    Foreground = textBrush,
                    FontSize = 14,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                }
            };

            newsPanel.Children.Add(errorBorder);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"NewsView: Erro ao mostrar mensagem de erro: {ex.Message}");
        }
    }
}

