using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using SharedClientSide_AVALONIA.Services;
using System;

namespace LesserDashboardClient.Views
{
    public partial class EnvironmentBadgeControl : UserControl
    {

        public string EnvironmentName
        {
            get => (string)GetValue(EnvironmentNameProperty);
            set => SetValue(EnvironmentNameProperty, value);
        }

        public string BadgeColor
        {
            get => (string)GetValue(BadgeColorProperty);
            set => SetValue(BadgeColorProperty, value);
        }

        public string TooltipText
        {
            get => (string)GetValue(TooltipTextProperty);
            set => SetValue(TooltipTextProperty, value);
        }

        public static readonly Avalonia.AvaloniaProperty<string> EnvironmentNameProperty =
            Avalonia.AvaloniaProperty.Register<EnvironmentBadgeControl, string>(nameof(EnvironmentName), "");

        public static readonly Avalonia.AvaloniaProperty<string> BadgeColorProperty =
            Avalonia.AvaloniaProperty.Register<EnvironmentBadgeControl, string>(nameof(BadgeColor), "#808080");

        public static readonly Avalonia.AvaloniaProperty<string> TooltipTextProperty =
            Avalonia.AvaloniaProperty.Register<EnvironmentBadgeControl, string>(nameof(TooltipText), "");

        public EnvironmentBadgeControl()
        {
            InitializeComponent();
            DataContext = this;
            UpdateEnvironmentInfo();
            
            // Atualiza periodicamente para refletir mudanças no override
            var timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            timer.Tick += (s, e) => UpdateEnvironmentInfo();
            timer.Start();
        }

        private void UpdateEnvironmentInfo()
        {
            try
            {
                string activeEnv = EndpointOverrideService.GetActiveEnvironment();
                string displayName = EndpointOverrideService.GetActiveEnvironmentDisplayName();
                string badgeColor = EndpointOverrideService.GetEnvironmentBadgeColor();
                bool hasOverride = EndpointOverrideService.HasSessionOverride();
                string buildEnv = EndpointOverrideService.GetBuildEnvironment();

                EnvironmentName = displayName;
                BadgeColor = badgeColor;

                if (hasOverride)
                {
                    TooltipText = $"Ambiente ativo: {activeEnv} (Override da sessão)\nAmbiente do build: {buildEnv}\n\nEsta troca vale apenas nesta sessão.";
                }
                else
                {
                    TooltipText = $"Ambiente ativo: {activeEnv} (Do build)\n\nPressione Ctrl+Shift+L para alterar o endpoint.";
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao atualizar badge de ambiente: {ex.Message}");
            }
        }
    }
}

