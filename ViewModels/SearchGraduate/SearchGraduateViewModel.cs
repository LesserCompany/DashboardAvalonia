using SharedClientSide.ServerInteraction;

namespace LesserDashboardClient.ViewModels.SearchGraduate
{
    public partial class SearchGraduateViewModel : ViewModelBase
    {
        public bool IsDarkMode => GlobalAppStateViewModel.Instance?.AppIsDarkMode ?? false;
        public string loginToken
        {
            get => LesserFunctionClient.loginFileResult?.User?.loginToken ?? "";
        }

        //private string baseUrl = "http://localhost:5173";
        private string baseUrl = "https://graduates-explorer.lesser.biz/";

        private int _selectedSectionIndex;

        public int SelectedSectionIndex
        {
            get => _selectedSectionIndex;
            set
            {
                if (_selectedSectionIndex != value)
                {
                    _selectedSectionIndex = value;
                    OnPropertyChanged(nameof(SelectedSectionIndex));
                    OnPropertyChanged(nameof(ActiveUrl));
                    OnPropertyChanged(nameof(ActiveUrlWeb));
                }
            }
        }

        public string ActiveUrl => SelectedSectionIndex switch
        {
            0 => UrlSearchCPF,
            1 => UrlReviewPhotos,
            2 => UrlPhotosForTreatment,
            3 => UrlProtectedCpf,
            _ => "about:blank"
        };

        public string ActiveUrlWeb => SelectedSectionIndex switch
        {
            0 => UrlSearchCPFWeb,
            1 => UrlReviewPhotosWeb,
            2 => UrlPhotosForTreatmentWeb,
            3 => UrlProtectedCpfWeb,
            _ => ""
        };

        public SearchGraduateViewModel()
        {
            _selectedSectionIndex = SearchGraduateNavigationState.HasPendingCpfs ? 3 : 0;
        }

        public void NotifyActiveUrlChanged()
        {
            OnPropertyChanged(nameof(ActiveUrl));
            OnPropertyChanged(nameof(ActiveUrlWeb));
        }

        // ======================== URLs embarcadas (com hideNavbar) ========================

        public string UrlSearchCPF
        {
            get
            {
                if (string.IsNullOrEmpty(loginToken)) return "about:blank";
                return baseUrl + "/search-graduates/" + $"?token={loginToken}" + DarkModeParam + "&hideNavbar=true";
            }
        }

        public string UrlReviewPhotos
        {
            get
            {
                if (string.IsNullOrEmpty(loginToken)) return "about:blank";
                return baseUrl + "/photos-chosen-by-cpfs/" + $"?token={loginToken}" + DarkModeParam + "&hideNavbar=true";
            }
        }

        public string UrlPhotosForTreatment
        {
            get
            {
                if (string.IsNullOrEmpty(loginToken)) return "about:blank";
                return baseUrl + "/photos-for-treatment/" + $"?token={loginToken}" + DarkModeParam + "&hideNavbar=true";
            }
        }

        public string UrlProtectedCpf
        {
            get
            {
                if (string.IsNullOrEmpty(loginToken)) return "about:blank";
                return baseUrl + "/protected-cpfs/" + $"?token={loginToken}" + DarkModeParam + "&hideNavbar=true";
            }
        }

        // ======================== URLs para abrir na web (sem hideNavbar) ========================

        public string UrlSearchCPFWeb
        {
            get
            {
                if (string.IsNullOrEmpty(loginToken)) return "";
                return baseUrl + "/search-graduates/" + $"?token={loginToken}" + DarkModeParam;
            }
        }

        public string UrlReviewPhotosWeb
        {
            get
            {
                if (string.IsNullOrEmpty(loginToken)) return "";
                return baseUrl + "/photos-chosen-by-cpfs/" + $"?token={loginToken}" + DarkModeParam;
            }
        }

        public string UrlPhotosForTreatmentWeb
        {
            get
            {
                if (string.IsNullOrEmpty(loginToken)) return "";
                return baseUrl + "/photos-for-treatment/" + $"?token={loginToken}" + DarkModeParam;
            }
        }

        public string UrlProtectedCpfWeb
        {
            get
            {
                if (string.IsNullOrEmpty(loginToken)) return "";
                return baseUrl + "/protected-cpfs/" + $"?token={loginToken}" + DarkModeParam;
            }
        }

        // ======================== Helpers ========================

        private string DarkModeParam => IsDarkMode ? "&darkMode=true" : "&darkMode=false";
    }
}
