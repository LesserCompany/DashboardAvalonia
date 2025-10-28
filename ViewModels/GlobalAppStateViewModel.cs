using Avalonia.Controls;
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
}