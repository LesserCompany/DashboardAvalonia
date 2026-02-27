using Avalonia.Threading;
using System;
using System.Threading.Tasks;

namespace LesserDashboardClient.Helpers
{
    /// <summary>
    /// Helper para executar instalações de aplicativos em background, evitando travada da UI
    /// </summary>
    public static class InstallerRunner
    {
        /// <summary>
        /// Executa a instalação de um aplicativo em background, mantendo a UI responsiva
        /// </summary>
        /// <param name="appName">Nome do aplicativo a ser instalado</param>
        /// <param name="onUiProgress">Callback para atualizar progresso na UI</param>
        /// <param name="args">Argumentos para passar ao aplicativo</param>
        /// <param name="onUiDone">Callback executado quando a instalação termina com sucesso</param>
        /// <param name="onUiError">Callback executado em caso de erro</param>
        public static void RunInBackground(
            string appName,
            Action<int> onUiProgress,   // deve atualizar UI
            string args = "",
            Action onUiDone = null,     // será chamado no fim, na UI
            Action<string> onUiError = null)
        {
            SharedClientSide.Helpers.AppInstaller.MsixLog($"InstallerRunner.RunInBackground ENTRANDO appName='{appName}' args='{args}'");

            // Encapsula o "marshal" para a UI thread
            void ProgressFromBg(int p) => Dispatcher.UIThread.Post(() => onUiProgress?.Invoke(p));

            var ai = new SharedClientSide.Helpers.AppInstaller(appName, ProgressFromBg);
            SharedClientSide.Helpers.AppInstaller.MsixLog($"InstallerRunner: AppInstaller criado, chamando startApp...");

            _ = Task.Run(async () =>
            {
                try
                {
                    SharedClientSide.Helpers.AppInstaller.MsixLog($"InstallerRunner: Task.Run iniciado, await startApp...");
                    await ai.startApp(args, () =>
                    {
                        // Garantir execução do callback final na UI thread
                        Dispatcher.UIThread.Post(() => onUiDone?.Invoke());
                    });
                }
                catch (Exception ex)
                {
                    SharedClientSide.Helpers.AppInstaller.MsixLog($"InstallerRunner ERRO: {ex.GetType().Name} - {ex.Message}");
                    Dispatcher.UIThread.Post(() => onUiError?.Invoke(ex.Message));
                }
            });
        }
    }
}
