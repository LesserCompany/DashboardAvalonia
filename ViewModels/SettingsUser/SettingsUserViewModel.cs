using CommunityToolkit.Mvvm.ComponentModel;
using SharedClientSide.ServerInteraction;
using System;

namespace LesserDashboardClient.ViewModels.SettingsUser;

public partial class SettingsUserViewModel : ViewModelBase
    {
        public bool IsDarkMode => GlobalAppStateViewModel.Instance.AppIsDarkMode;

        public string loginToken
        {
            get => LesserFunctionClient.loginFileResult?.User?.loginToken ?? "";
        }

        private const string BaseUrl = "https://graduates-explorer.lesser.biz/settings-user/";
        //private const string BaseUrl = "http://localhost:5173/settings-user/";
        private string _url = "";

        public string Url
        {
            get
            {
                if (string.IsNullOrEmpty(loginToken))
                {
                    return "about:blank";
                }

                if (IsDarkMode)
                {
                    _url = BaseUrl + $"?token={loginToken}&darkMode=true&hideNavbar=true";
                }
                else
                {
                    _url = BaseUrl + $"?token={loginToken}&darkMode=false&hideNavbar=true";
                }
                return _url;
            }
        }

        /// <summary>URL para abrir no navegador (sem hideNavbar).</summary>
        public string UrlWeb
        {
            get
            {
                if (string.IsNullOrEmpty(loginToken))
                {
                    return "";
                }

                if (IsDarkMode)
                {
                    return BaseUrl + $"?token={loginToken}&darkMode=true";
                }
                return BaseUrl + $"?token={loginToken}&darkMode=false";
            }
        }
    }
