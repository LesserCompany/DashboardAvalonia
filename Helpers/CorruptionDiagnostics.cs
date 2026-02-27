using System;
using System.IO;
using System.Reflection;

namespace LesserDashboardClient.Helpers;

/// <summary>
/// Log de diagnóstico para descobrir o que está corrompendo o app (idioma travando em inglês, etc.).
/// Arquivo: Documents\Separacao\apps\LesserDashboard\CORRUPTION_DIAG.txt
///
/// Por que apagar só a pasta da versão (ex: v\192.3) e baixar de novo "corrige":
/// - As configurações (idioma, tema) ficam em Documents\Separacao\app\settings.json, NÃO dentro da pasta da versão.
/// - Ao apagar v\192.3 e baixar de novo, você volta a executar o app (novo processo). Todo estado em memória
///   (Loc.Instance.CurrentLanguage, estado interno do DataGrid, etc.) é zerado, por isso o comportamento volta ao normal.
/// - Ou seja: a "corrupção" é estado em memória que acumula durante o uso; reiniciar o processo limpa esse estado.
/// </summary>
public static class CorruptionDiagnostics
{
    private static readonly object _lock = new object();

    public static void Log(string message)
    {
        try
        {
            lock (_lock)
            {
                string assemblyName = Assembly.GetExecutingAssembly().GetName().Name ?? "LesserDashboard";
                string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                string logDirectory = Path.Combine(documentsPath, "Separacao", "apps", assemblyName);
                string logFilePath = Path.Combine(logDirectory, "CORRUPTION_DIAG.txt");

                if (!Directory.Exists(logDirectory))
                    Directory.CreateDirectory(logDirectory);

                string line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} | {message}{Environment.NewLine}";
                File.AppendAllText(logFilePath, line);
            }
        }
        catch
        {
            // Não falhar o app por causa do log de diagnóstico
        }
    }
}
