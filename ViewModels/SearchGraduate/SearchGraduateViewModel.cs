using SharedClientSide.ServerInteraction;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LesserDashboardClient.ViewModels.SearchGraduate
{
    public partial class SearchGraduateViewModel : ViewModelBase
    {
        public bool IsDarkMode
        {
            get => GlobalAppStateViewModel.Instance.AppIsDarkMode;
            set => GlobalAppStateViewModel.Instance.AppIsDarkMode = value;
        }
        public string loginToken
        {
            get => LesserFunctionClient.loginFileResult.User.loginToken;
        }

        //private string baseUrl = "http://localhost:5173";
        private string baseUrl = "https://graduates-explorer.lesser.biz/";
        private string _urlSearchCPF = "";
        public string UrlSearchCPF
        {
            get
            {
                if (IsDarkMode)
                {
                    _urlSearchCPF = baseUrl + "/search-graduates/" + $"?token={loginToken}" + "&darkMode=true" + "&hideNavbar=true";
                }
                else
                {
                    _urlSearchCPF = baseUrl + "/search-graduates/" + $"?token={loginToken}" + "&darkMode=false" + "&hideNavbar=true";
                }
                return _urlSearchCPF;
            }
        }
        private string _urlReviewPhotos = "";
        public string UrlReviewPhotos
        {
            get
            {
                if (IsDarkMode)
                {
                    _urlReviewPhotos = baseUrl + "/photos-chosen-by-cpfs/" + $"?token={loginToken}" + "&darkMode=true" + "&hideNavbar=true";
                }
                else
                {
                    _urlReviewPhotos = baseUrl + "/photos-chosen-by-cpfs/" + $"?token={loginToken}" + "&darkMode=false" + "&hideNavbar=true";
                }
                return _urlReviewPhotos;
            }
        }
        private string _urlPhotosForTreatment = "";
        public string UrlPhotosForTreatment
        {
            get
            {
                if (IsDarkMode)
                {
                    _urlPhotosForTreatment = baseUrl + "/photos-for-treatment/" + $"?token={loginToken}" + "&darkMode=true" + "&hideNavbar=true";
                }
                else
                {
                    _urlPhotosForTreatment = baseUrl + "/photos-for-treatment/" + $"?token={loginToken}" + "&darkMode=false" + "&hideNavbar=true";
                }
                return _urlPhotosForTreatment;
            }
        }
    }
}
