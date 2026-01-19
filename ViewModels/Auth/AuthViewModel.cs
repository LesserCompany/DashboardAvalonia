using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Svg.Skia;
using Avalonia.Threading;
using CodingSeb.Localization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LesserDashboardClient.ViewModels;
using LesserDashboardClient.Views;
using Newtonsoft.Json;
using SharedClientSide.ServerInteraction;
using SharedClientSide.ServerInteraction.Users.Login;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LesserDashboardClient.ViewModels.Auth
{
    public partial class AuthViewModel : ViewModelBase
    {
        // Somente leitura - evita loops de binding
        public bool IsDarkMode => GlobalAppStateViewModel.Instance.AppIsDarkMode;

        public string Lang
        {
            get { return GlobalAppStateViewModel.Instance.AppLanguage == "en-US" ? "en" : "pt"; }
        }

        private string baseUrl = "https://login-and-registration.lesser.biz/?";
        private string _url_register = "";
        public string URL_REGISTER
        {
            get
            {
                if (IsDarkMode)
                {
                    _url_register = baseUrl + "&darkMode=true" + $"&lang={Lang}";
                }
                else
                {
                    _url_register = baseUrl + "&darkMode=false" + $"&lang={Lang}";
                }
                return _url_register;
            }
        }
        private string _url_forgot_password = "";
        public string URL_FORGOT_PASSWORD
        {
            get
            {
                if (IsDarkMode)
                {
                    _url_forgot_password = baseUrl + "&darkMode=true" + $"&lang={Lang}" + "&route=FORGOT_PASSWORD";
                }
                else
                {
                    _url_forgot_password = baseUrl + "&darkMode=false" + $"&lang={Lang}" + "&route=FORGOT_PASSWORD";
                }
                return _url_forgot_password;
            }
        }

        private SvgSource _pathLogo;
        public SvgSource PathLogo
        {
            get
            {
                var file = IsDarkMode
                    ? "avares://LesserDashboard/Assets/Logo V2 light.svg"
                    : "avares://LesserDashboard/Assets/Logo V1 dark.svg";

                _pathLogo = SvgSource.Load(file);
                return _pathLogo;
            }
        }


        [ObservableProperty] public bool authWindowIsEnabled = true;
        [ObservableProperty] public string tbUserName = "";
        [ObservableProperty] public string tbPassword = "";
        [ObservableProperty] public bool loginIsRunning = false;
        [ObservableProperty] public bool isIncorrectCredentials = false;
        [ObservableProperty] public string erroMessage = "";
        const string eyeOpen = "/Assets/icons/eye.svg";
        const string eyeClosed = "/Assets/icons/eye-off.svg";
        [ObservableProperty] public string iconEyes = eyeClosed;
        [ObservableProperty] public bool showPassword = false;
        [ObservableProperty] public string passwordChar = "*";
        partial void OnShowPasswordChanged(bool oldValue, bool newValue)
        {
            if (newValue)
            {
                IconEyes = eyeClosed; // Quando senha está visível, mostra ícone de ocultar
                PasswordChar = "";
            }
            else
            {
                IconEyes = eyeOpen; // Quando senha está oculta, mostra ícone de mostrar
                PasswordChar = "*";
            }
        }


        [RelayCommand]
        public async Task LoginCommand()
        {
            try
            {
                AuthWindowIsEnabled = false;
                IsIncorrectCredentials = false;
                LoginIsRunning = true;


                if(string.IsNullOrEmpty(TbUserName) || string.IsNullOrEmpty(TbPassword))
                {
                    IsIncorrectCredentials = true;
                    return;
                }

                LoginRequest lr = new LoginRequest() { username = TbUserName, password = TbPassword };

                // Garante que estamos usando uma instância atualizada do LesserFunctionClient
                // Importante: Após um logout, a instância antiga é resetada, então uma nova será criada aqui
                
                // CORREÇÃO: Verifica se lfc está null e cria uma nova instância se necessário
                if (GlobalAppStateViewModel.lfc == null)
                {
                    Console.WriteLine("AuthViewModel.LoginCommand: lfc está null, recarregando...");
                    GlobalAppStateViewModel.ResetLesserFunctionClient();
                    // Força o getter a criar uma nova instância
                    var _ = GlobalAppStateViewModel.lfc;
                }
                
                await GlobalAppStateViewModel.lfc.loginAsync(lr);

                if (GlobalAppStateViewModel.lfc.loginResult == null || GlobalAppStateViewModel.lfc.loginResult.loginFailed == true || GlobalAppStateViewModel.lfc.loginResult.success == false)
                {
                    IsIncorrectCredentials = true;
                    ErroMessage = GlobalAppStateViewModel.lfc.loginResult.message != null ? GlobalAppStateViewModel.lfc.loginResult.message : Loc.Tr("Incorrect credentials. Please try again.");
                }
                else
                {
                    // Substitui a janela atual pela janela apropriada baseada no tipo de usuário
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        var oldWindow = desktop.MainWindow;
                        
                        // NÃO reinicializar configurações - elas já estão aplicadas
                        // As configurações só devem ser alteradas manualmente pelo usuário nas Opções
                        
                        // Verifica o tipo de usuário para determinar qual janela mostrar
                        if (GlobalAppStateViewModel.lfc.loginResult.User.userType == "professionals")
                        {
                            // Para separadores, mostra a ProfessionalWindow
                            var professionalWindow = new Views.ProfessionalWindow.ProfessionalWindowView(GlobalAppStateViewModel.lfc);
                            desktop.MainWindow = professionalWindow;
                            professionalWindow.Show();
                        }
                        else
                        {
                            // Para empresas, mostra a MainWindow normal
                            var mainWindow = new MainWindow
                            {
                                DataContext = new MainWindowViewModel(),
                            };
                            desktop.MainWindow = mainWindow;
                            mainWindow.Show();
                        }
                        
                        // Espera um tick para garantir que a UI da nova janela iniciou
                        await Task.Delay(150);
                        
                        // Agora fecha a janela antiga (libera Dispatcher antigo)
                        (oldWindow?.DataContext as IDisposable)?.Dispose();
                        oldWindow?.Close();
                        
                        // Fecha também a instância da AuthWindow se ela existir
                        if (App.AuthWindowInstance != null)
                        {
                            App.AuthWindowInstance.Close();
                            App.AuthWindowInstance = null;
                        }

                        // Valida o diretório de downloads após login bem-sucedido
                        await Task.Delay(300); // Pequeno delay para garantir que a janela está totalmente carregada
                        await GlobalAppStateViewModel.Instance.ValidateAndPromptDownloadDirectoryIfNeeded();
                    }
                }

            }
            catch(Exception e)
            {

            }
            finally
            {
                LoginIsRunning = false;
                AuthWindowIsEnabled = true;
            }
        }
        [RelayCommand]
        public void ChangeVisibilityPassword()
        {
            ShowPassword = !ShowPassword;
        }
        [RelayCommand]
        public void OpenRegisterPageCommand()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = URL_REGISTER,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
            }
        }
        [RelayCommand]
        public void OpenForgotPasswordPageCommand()
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = URL_FORGOT_PASSWORD,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                // Log or handle the exception as needed
            }
        }
    }
}
