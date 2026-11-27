using System;
using Avalonia.Controls;
using LesserDashboardClient.ViewModels.Update;

namespace LesserDashboardClient.Views;

/// <summary>Tela de instruções de atualização. Quem decide o modo é UpdateInstructionsPolicy (ver App.axaml.cs).</summary>
public partial class UpdateInstructionsWindow : Window
{
    public UpdateInstructionsWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is UpdateInstructionsViewModel vm)
        {
            vm.OwnerWindowHandle = () => TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            vm.CloseRequested -= OnCloseRequested;
            vm.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, EventArgs e) => Close();
}
