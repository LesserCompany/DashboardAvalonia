using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using System;
using System.Linq;

namespace LesserDashboardClient.Views;

public partial class AuthWindow : Window
{
    public AuthWindow()
    {
        InitializeComponent();
        KeyDown += AuthWindow_KeyDown;
        Loaded += AuthWindow_Loaded;
    }

    private void AuthWindow_Loaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var tbUserName = this.FindControl<TextBox>("TbUserName");
        if (tbUserName != null)
        {
            tbUserName.KeyDown += TbUserName_KeyDown;
            tbUserName.TextChanged += TbUserName_TextChanged;
        }
    }

    private void TbUserName_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Space)
            e.Handled = true;
    }

    private void TbUserName_TextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && !string.IsNullOrEmpty(textBox.Text) && textBox.Text.Contains(' '))
        {
            int caretIndex = textBox.CaretIndex;
            string original = textBox.Text;
            string semEspacos = original.Replace(" ", "");
            int espacosAntesCursor = original.Substring(0, Math.Min(caretIndex, original.Length)).Count(c => c == ' ');
            textBox.Text = semEspacos;
            textBox.CaretIndex = Math.Max(0, caretIndex - espacosAntesCursor);
        }
    }

    private void AuthWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            if (DataContext is ViewModels.Auth.AuthViewModel vm)
            {
                vm.LoginCommandCommand.Execute(null);
            }
        }
    }
}