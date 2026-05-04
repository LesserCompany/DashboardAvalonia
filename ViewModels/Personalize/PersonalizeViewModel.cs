using CommunityToolkit.Mvvm.ComponentModel;
using SharedClientSide.ServerInteraction;
using System;

namespace LesserDashboardClient.ViewModels.Personalize
{
    public partial class PersonalizeViewModel : ViewModelBase
    {
        public bool IsDarkMode => GlobalAppStateViewModel.Instance.AppIsDarkMode;

        public string loginToken
        {
            get => LesserFunctionClient.loginFileResult?.User?.loginToken ?? "";
        }

        private string baseUrl = "https://graduates-explorer.lesser.biz/personalize/";
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
                    _url = baseUrl + $"?token={loginToken}" + "&darkMode=true" + "&hideNavbar=true";
                }
                else
                {
                    _url = baseUrl + $"?token={loginToken}" + "&darkMode=false" + "&hideNavbar=true";
                }
                return _url;
            }
        }

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
                    return baseUrl + $"?token={loginToken}" + "&darkMode=true";
                }
                else
                {
                    return baseUrl + $"?token={loginToken}" + "&darkMode=false";
                }
            }
        }
    }
}
