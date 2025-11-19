using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CodingSeb.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using LesserDashboardClient.Models.Company;
using LesserDashboardClient.Views;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Models;
using SharedClientSide.ServerInteraction;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace LesserDashboardClient.ViewModels;

public partial class GlobalAppStateViewModel : ObservableObject
{
    public GlobalAppStateViewModel()
    {
        Instance = this;
    }
    public static GlobalAppStateViewModel _instance;
    public static GlobalAppStateViewModel Instance
    {
        get
        {
            if(_instance == null)
                _instance = new GlobalAppStateViewModel();
            return _instance;
        }
        private set { _instance = value; }
    }

    [ObservableProperty] public bool appIsDarkMode;
    partial void OnAppIsDarkModeChanged(bool value)
    {
        if (value == true)
            ChangeAppTheme("DarkMode");
        else
            ChangeAppTheme("LightMode");
    }
    [ObservableProperty] public string appLanguage;
    partial void OnAppLanguageChanged(string value)
    {
        ChangeAppLanguage(value);
    }

    private static LesserFunctionClient? _lfc;
    public static LesserFunctionClient lfc
    {
        get
        {
            if(_lfc  ==  null)
                LoadLesserFunctionClient();
            return _lfc;
        }
        private set => _lfc = value;
    }

    private static OptionsModel? _options;
    public static OptionsModel options
    {
        get
        {
            if (_options == null)
                LoadOptionsModel();
            return _options;
        }
        private set => _options = value;
    }
    public static void LoadOptionsModel()
    {
        _options = OptionsModel.Load();
    }
    private static bool isRedirectingToLogin = false; // Flag para evitar redirecionamentos duplos
    
    public static void LoadLesserFunctionClient()
    {
        // IMPORTANTE: Configura o callback para redirecionar para login quando o token falhar em qualquer API
        var lfc = new LesserFunctionClient(async (client) => 
        {
            // Este callback é chamado quando uma API retorna loginFailed = true
            Console.WriteLine("LoadLesserFunctionClient: Token falhou durante chamada de API");
            
            // Evita redirecionamento duplo
            if (isRedirectingToLogin)
            {
                Console.WriteLine("LoadLesserFunctionClient: Já redirecionando, ignorando callback duplicado");
                return null;
            }
            
            isRedirectingToLogin = true;
            Console.WriteLine("LoadLesserFunctionClient: Redirecionando para login...");
            
            // Redireciona para o login no UI thread com mensagem
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
            {
                try
                {
                    // Mostra mensagem primeiro usando tradução
                    var box = MessageBoxManager.GetMessageBoxStandard(
                        Loc.Tr("Session expired"),
                        Loc.Tr("Your session has expired or is invalid. Please login again."),
                        MsBox.Avalonia.Enums.ButtonEnum.Ok
                    );
                    
                    await box.ShowAsync();
                    
                    // Agora redireciona
                    App.RedirectToLoginScreen();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Erro ao mostrar mensagem e redirecionar: {ex.Message}");
                    App.RedirectToLoginScreen();
                }
                finally
                {
                    // Aguarda um pouco antes de resetar o flag para garantir que a transição seja feita
                    await Task.Delay(1000);
                    isRedirectingToLogin = false;
                }
            });
            
            // Retorna null para indicar que o login não foi feito automaticamente
            return null;
        });
        
        lfc.InitFromFile((string ack) => 
        { 
            // Callback é chamado quando o token está inválido/expirado na inicialização
            Console.WriteLine($"LoadLesserFunctionClient InitFromFile callback: {ack}");
        });
        
        // CORREÇÃO: Sempre atribui o lfc, mesmo se InitFromFile falhou
        // Isso garante que temos uma instância válida para fazer login
        _lfc = lfc;
        LesserFunctionClient.DefaultClient = lfc; // Atualiza o DefaultClient também
        
        if (lfc.InitFromFileFailed)
        {
            Console.WriteLine("LoadLesserFunctionClient: InitFromFile falhou, mas lfc foi criado para permitir novo login");
        }
        else
        {
            Console.WriteLine("LoadLesserFunctionClient: lfc inicializado com sucesso a partir do arquivo");
        }
    }
    
    /// <summary>
    /// Reseta a instância do LesserFunctionClient.
    /// Deve ser chamado após o logout para garantir que uma nova instância seja criada no próximo login.
    /// </summary>
    public static void ResetLesserFunctionClient()
    {
        Console.WriteLine("ResetLesserFunctionClient: Resetando lfc...");
        _lfc = null;
        LesserFunctionClient.DefaultClient = null;
        
        // CORREÇÃO: Cria imediatamente uma nova instância limpa para permitir novo login
        // Isso evita NullReferenceException quando tentar fazer login novamente
        Console.WriteLine("ResetLesserFunctionClient: Criando nova instância limpa...");
        LoadLesserFunctionClient();
        Console.WriteLine($"ResetLesserFunctionClient: Nova instância criada: {_lfc != null}");
    }
    private void ChangeAppTheme(string theme)
    {
        var app = App.Current as App;
        app?.SwitchCurrentTheme(theme);
    }
    private void ChangeAppLanguage(string lang)
    {
        var app = App.Current as App;
        App.SetCurrentLang(lang);
    }

    public void ShowDialogOk(string msg = "", string title = "")
    {
        var msgParams = new MessageBoxStandardParams
        {
            MaxWidth = 500,
            MaxHeight = 800,
            ShowInCenter = true,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            SizeToContent = SizeToContent.WidthAndHeight,
            ContentMessage = msg,
            ContentTitle = title
        };
        if (MainWindow.instance != null)
        {
            var bbox = MessageBoxManager
                .GetMessageBoxStandard(msgParams);
            var result = bbox.ShowWindowDialogAsync(MainWindow.instance);
        }
        else
        {
            var bbox = MessageBoxManager
                .GetMessageBoxStandard(msgParams);
            var result = bbox.ShowAsync();
        }
    }
    public async Task<bool> ShowDialogYesNo(string msg, string title = "")
    {
        if (MainWindow.instance != null)
        {
            MessageBoxCustomParams bbCustomParamsYesNo = new MessageBoxCustomParams
            {
                MaxWidth = 500,
                MaxHeight = 800,
                ContentMessage = msg,
                ShowInCenter = true,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ContentTitle = title,
                ButtonDefinitions = new List<ButtonDefinition>
                {
                    new ButtonDefinition { Name = Loc.Tr("Yes", "Yes"),},
                    new ButtonDefinition { Name = Loc.Tr("No", "No") },
                },
            };
            var bbox = MessageBoxManager
                .GetMessageBoxCustom(bbCustomParamsYesNo);
            var result = await bbox.ShowWindowDialogAsync(MainWindow.instance);
            bool resultBool = result == Loc.Tr("Yes", "Yes") ? true : false;
            return resultBool;
        }
        else
        {
            ShowDialogOk("Fail to create msgBox");
            return false;
        }
    }
    private void SaveOptions()
    {
        options.Save();
    }

    /// <summary>
    /// Valida se o diretório de downloads configurado é válido e acessível
    /// Lê diretamente do arquivo settings.json para garantir que verifica o valor salvo
    /// </summary>
    /// <returns>Tupla com (isValid, errorMessage)</returns>
    public (bool isValid, string errorMessage) ValidateDownloadDirectory()
    {
        try
        {
            // Lê diretamente do arquivo settings.json ao invés de usar o objeto em memória
            string downloadPath = GetDownloadPathFromSettingsFile();

            // Verifica se o caminho está vazio
            if (string.IsNullOrWhiteSpace(downloadPath))
            {
                return (false, Loc.Tr("Download directory is not configured"));
            }

            // Verifica se o diretório existe
            if (!Directory.Exists(downloadPath))
            {
                return (false, Loc.Tr("Configured download directory does not exist"));
            }

            // Verifica se o diretório é acessível (tenta listar conteúdo)
            try
            {
                var _ = Directory.GetFiles(downloadPath);
            }
            catch (UnauthorizedAccessException)
            {
                return (false, Loc.Tr("Download directory is not accessible (permission denied)"));
            }
            catch (Exception ex)
            {
                return (false, Loc.Tr("Download directory is not accessible") + $": {ex.Message}");
            }

            return (true, string.Empty);
        }
        catch (Exception ex)
        {
            return (false, $"Error validating directory: {ex.Message}");
        }
    }

    /// <summary>
    /// Lê o path de downloads diretamente do arquivo settings.json
    /// </summary>
    /// <returns>O path configurado ou string vazia se não existir</returns>
    private string GetDownloadPathFromSettingsFile()
    {
        try
        {
            string settingsFilePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), 
                "Separacao", 
                "app", 
                "settings.json"
            );

            if (!File.Exists(settingsFilePath))
            {
                return string.Empty;
            }

            string json = File.ReadAllText(settingsFilePath);
            
            // Deserializa diretamente para OptionsModel que já sabe mapear os campos corretamente
            var settingsModel = Newtonsoft.Json.JsonConvert.DeserializeObject<OptionsModel>(json);
            
            if (settingsModel != null && !string.IsNullOrWhiteSpace(settingsModel.DefaultPathToDownloadProfessionalTaskFiles))
            {
                return settingsModel.DefaultPathToDownloadProfessionalTaskFiles;
            }

            return string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao ler settings.json: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Valida o diretório de downloads e mostra dialog se inválido
    /// </summary>
    public async Task ValidateAndPromptDownloadDirectoryIfNeeded()
    {
        var (isValid, errorMessage) = ValidateDownloadDirectory();
        
        if (!isValid)
        {
            await ShowInvalidDownloadDirectoryDialog(errorMessage);
        }
    }

    /// <summary>
    /// Mostra dialog quando o diretório de downloads é inválido e obriga escolher novo
    /// </summary>
    private async Task ShowInvalidDownloadDirectoryDialog(string errorMessage)
    {
        try
        {
            if (MainWindow.instance != null)
            {
                // Loop até que o usuário selecione um diretório válido
                bool directoryValid = false;
                while (!directoryValid)
                {
                    MessageBoxCustomParams customParams = new MessageBoxCustomParams
                    {
                        MaxWidth = 550,
                        MaxHeight = 800,
                        ContentMessage = errorMessage + "\n\n" + Loc.Tr("Please select a valid directory to continue."),
                        ShowInCenter = true,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        SizeToContent = SizeToContent.WidthAndHeight,
                        ContentTitle = Loc.Tr("Invalid Download Directory"),
                        ButtonDefinitions = new List<ButtonDefinition>
                        {
                            new ButtonDefinition { Name = Loc.Tr("Select Directory") },
                        },
                    };
                    
                    var bbox = MessageBoxManager.GetMessageBoxCustom(customParams);
                    var result = await bbox.ShowWindowDialogAsync(MainWindow.instance);
                    
                    // O usuário só pode clicar em "Selecionar Diretório" (única opção)
                    if (result == Loc.Tr("Select Directory"))
                    {
                        // Abre o folder picker
                        bool validDirectorySelected = await OpenFolderPickerForDownloadDirectory();
                        
                        if (validDirectorySelected)
                        {
                            // Verifica novamente se o diretório selecionado é válido
                            var (isValid, newErrorMessage) = ValidateDownloadDirectory();
                            if (isValid)
                            {
                                directoryValid = true;
                            }
                            else
                            {
                                // Se ainda for inválido, atualiza a mensagem de erro e tenta novamente
                                errorMessage = newErrorMessage;
                            }
                        }
                        else
                        {
                            // Se o usuário cancelou o folder picker, mostra o dialog novamente
                            // (não permite cancelar, deve selecionar um diretório)
                            continue;
                        }
                    }
                }
            }
            else
            {
                // Se MainWindow.instance for null, loga o erro mas não bloqueia
                // A validação no CollectionsViewModel vai bloquear mesmo assim
                Console.WriteLine($"Erro: MainWindow.instance é null, não é possível mostrar o diálogo de validação de diretório.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao mostrar dialog de diretório inválido: {ex.Message}");
        }
    }

    /// <summary>
    /// Abre folder picker para selecionar novo diretório de downloads
    /// </summary>
    /// <returns>True se um diretório válido foi selecionado, False caso contrário</returns>
    private async Task<bool> OpenFolderPickerForDownloadDirectory()
    {
        try
        {
            if (MainWindow.instance != null)
            {
                var topLevel = TopLevel.GetTopLevel(MainWindow.instance);
                if (topLevel?.StorageProvider == null)
                    return false;

                var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    AllowMultiple = false,
                    Title = Loc.Tr("Select Download Directory")
                });

                var folder = folders.FirstOrDefault();
                if (folder != null)
                {
                    string selectedPath = folder.Path.LocalPath;
                    
                    // Valida se o diretório existe antes de salvar
                    if (Directory.Exists(selectedPath))
                    {
                        // Salva o novo path no arquivo settings.json
                        options.DefaultPathToDownloadProfessionalTaskFiles = selectedPath;
                        options.Save();
                        
                        // Recarrega as opções para garantir sincronização
                        LoadOptionsModel();
                        
                        // Valida lendo diretamente do arquivo settings.json
                        var (isValid, errorMessage) = ValidateDownloadDirectory();
                        
                        if (isValid)
                        {
                            ShowDialogOk(Loc.Tr("Download directory saved successfully"));
                            return true;
                        }
                        else
                        {
                            // Se a validação falhar após salvar, mostra erro
                            ShowDialogOk(errorMessage);
                            return false;
                        }
                    }
                    else
                    {
                        ShowDialogOk(Loc.Tr("Selected directory is not valid"));
                        return false;
                    }
                }
                else
                {
                    // Usuário cancelou o folder picker, mas não pode cancelar o processo
                    return false;
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro ao abrir folder picker: {ex.Message}");
            ShowDialogOk($"Error: {ex.Message}");
            return false;
        }
    }
}