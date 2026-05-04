using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace LesserDashboardClient.Views.Collections;

public partial class DuplicatePhotosWarningWindow : Window
{
    private static readonly IBrush EventosReconhecimentosBrush = new SolidColorBrush(Color.FromRgb(230, 81, 0)); // #e65100 laranja

    public bool ContinueRequested { get; private set; } = false;

    public DuplicatePhotosWarningWindow()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Preenche o conteúdo com a mensagem e a lista de duplicados, destacando "Eventos:" / "Reconhecimentos:".
    /// Também injeta botões "Renomear" para abrir o Explorer já selecionando o arquivo.
    /// </summary>
    public void SetContent(
        string introMessage,
        IReadOnlyList<(string fileName, string eventFullPath, string recFullPath)> duplicates,
        int maxShow = 15,
        int moreCount = 0)
    {
        ContinueRequested = false;
        IntroText.Text = introMessage;

        DuplicatesPanel.Children.Clear();
        for (int i = 0; i < duplicates.Count && i < maxShow; i++)
        {
            var (fileName, eventFullPath, recFullPath) = duplicates[i];

            var itemPanel = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 8) };

            // Linha: • nome do ficheiro
            var nameLine = new TextBlock { Text = "• " + fileName, TextWrapping = TextWrapping.Wrap };
            itemPanel.Children.Add(nameLine);

            // Linha: Eventos [Renomear] [label] [caminho selecionável/copiável]
            var eventRow = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
            eventRow.Children.Add(CreateRenameButton("Renomear", eventFullPath));
            eventRow.Children.Add(CreateLabelAndCopyablePath("Eventos:", eventFullPath));
            itemPanel.Children.Add(eventRow);

            // Linha: Reconhecimentos [Renomear] [label] [caminho selecionável/copiável]
            var recRow = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
            recRow.Children.Add(CreateRenameButton("Renomear", recFullPath));
            recRow.Children.Add(CreateLabelAndCopyablePath("Reconhecimentos:", recFullPath));
            itemPanel.Children.Add(recRow);

            DuplicatesPanel.Children.Add(itemPanel);
        }

        if (moreCount > 0)
        {
            var moreText = new TextBlock
            {
                Text = $"… e mais {moreCount} foto(s). Renomeie todas e tente novamente.",
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 4, 0, 0)
            };
            DuplicatesPanel.Children.Add(moreText);
        }

        OkButton.Click -= OkButtonOnClick;
        OkButton.Click += OkButtonOnClick;
    }

    /// <summary>Label em laranja/negrito + caminho em SelectableTextBlock (caminho em linha própria para quebrar e copiar).</summary>
    private StackPanel CreateLabelAndCopyablePath(string label, string fullPath)
    {
        var labelTb = new TextBlock
        {
            Text = label + " ",
            Foreground = EventosReconhecimentosBrush,
            FontWeight = FontWeight.Bold
        };
        // MaxWidth obriga o texto a quebrar linha; sem isso o caminho fica numa linha e trunca (janela 560 - margens 24*2, botão ~90, espaço ~8)
        const double pathMaxWidth = 420;
        var pathBlock = new SelectableTextBlock
        {
            Text = fullPath,
            TextWrapping = TextWrapping.Wrap,
            MaxWidth = pathMaxWidth,
            Margin = new Thickness(0, 2, 0, 0)
        };
        var panel = new StackPanel
        {
            Orientation = Avalonia.Layout.Orientation.Vertical,
            Spacing = 0,
            MaxWidth = pathMaxWidth
        };
        panel.Children.Add(labelTb);
        panel.Children.Add(pathBlock);
        return panel;
    }

    private Button CreateRenameButton(string text, string fullPath)
    {
        var btn = new Button
        {
            Content = text,
            Padding = new Thickness(10, 2),
            MinWidth = 90,
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
        };
        btn.Click += (_, _) => RevealFileInExplorer(fullPath);
        return btn;
    }

    private static void RevealFileInExplorer(string fullPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fullPath))
                return;

            var fullPathTrimmed = fullPath.Trim();
            if (File.Exists(fullPathTrimmed))
            {
                var args = $"/select,\"{fullPathTrimmed}\"";
                Process.Start(new ProcessStartInfo("explorer.exe", args) { UseShellExecute = true });
                return;
            }

            // Fallback: abrir pasta
            var dir = Path.GetDirectoryName(fullPathTrimmed);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
        }
        catch
        {
            // best-effort: não quebrar o fluxo se o Explorer falhar
        }
    }

    private void OkButtonOnClick(object? sender, RoutedEventArgs e)
    {
        ContinueRequested = true;
        Close();
    }
}
