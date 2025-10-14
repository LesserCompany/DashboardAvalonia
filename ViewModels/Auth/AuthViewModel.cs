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
        public bool IsDarkMode
        {
            get => GlobalAppStateViewModel.Instance.AppIsDarkMode;
            set => GlobalAppStateViewModel.Instance.AppIsDarkMode = value;
        }

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


                await GlobalAppStateViewModel.lfc.loginAsync(lr);


                if (GlobalAppStateViewModel.lfc.loginResult == null || GlobalAppStateViewModel.lfc.loginResult.loginFailed == true || GlobalAppStateViewModel.lfc.loginResult.success == false)
                {
                    IsIncorrectCredentials = true;
                    ErroMessage = GlobalAppStateViewModel.lfc.loginResult.message != null ? GlobalAppStateViewModel.lfc.loginResult.message : Loc.Tr("Incorrect credentials. Please try again.");
                }
                else
                {
                    // Substitui a janela atual pela janela principal
                    if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                    {
                        // Fecha a janela de login atual
                        desktop.MainWindow?.Close();
                        
                        // Cria uma nova janela principal
                        var mainWindow = new MainWindow
                        {
                            DataContext = new MainWindowViewModel(),
                        };
                        
                        // Define a janela principal como a janela principal
                        desktop.MainWindow = mainWindow;
                        
                        // Mostra a nova janela principal
                        mainWindow.Show();
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
