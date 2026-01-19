using CommunityToolkit.Mvvm.ComponentModel;
using SharedClientSide.ServerInteraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LesserDashboardClient.ViewModels.Invoices
{
    public partial class InvoicesViewModel : ViewModelBase
    {
        // Somente leitura - evita loops de binding
        public bool IsDarkMode => GlobalAppStateViewModel.Instance.AppIsDarkMode;
        public string loginToken
        {
            get => LesserFunctionClient.loginFileResult?.User?.loginToken ?? "";
        }

        //private string baseUrl = "http://localhost:5173/invoices/";
        private string baseUrl = "https://graduates-explorer.lesser.biz/invoices/";
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

        // URL para abrir na web (sem hideNavbar)
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
