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
            // Encapsula o "marshal" para a UI thread
            void ProgressFromBg(int p) => Dispatcher.UIThread.Post(() => onUiProgress?.Invoke(p));

            var ai = new SharedClientSide.Helpers.AppInstaller(appName, ProgressFromBg);

            _ = Task.Run(async () =>
            {
                try
                {
                    await ai.startApp(args, () =>
                    {
                        // Garantir execução do callback final na UI thread
                        Dispatcher.UIThread.Post(() => onUiDone?.Invoke());
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.UIThread.Post(() => onUiError?.Invoke(ex.Message));
                }
            });
        }
    }
}
