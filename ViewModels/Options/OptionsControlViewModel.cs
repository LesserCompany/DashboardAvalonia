using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace LesserDashboardClient.ViewModels.Options;

public partial class OptionsControlViewModel : ViewModelBase
{


    public bool IsDarkMode
    {
        get => GlobalAppStateViewModel.Instance.AppIsDarkMode;
        set => GlobalAppStateViewModel.Instance.AppIsDarkMode = value;
    }

    public bool IsLanguageEnglish
    {
        get
        {
            if(GlobalAppStateViewModel.Instance.AppLanguage == "en-US")
                return true;
            else
                return false;
        }
        set
        {
            string newLanguage = value ? "en-US" : "pt-BR";
            string currentLanguage = GlobalAppStateViewModel.Instance.AppLanguage;
            
            if (currentLanguage != newLanguage)
            {
                Console.WriteLine($"OptionsControlViewModel: Alterando idioma de '{currentLanguage}' para '{newLanguage}'");
                
                // Alterar o idioma usando o método do App para disparar eventos
                App.SetCurrentLang(newLanguage);
            }
        }
    }


    public void ChangePathToDownloadApp(string path)
    {
        if (!string.IsNullOrEmpty(path))
        {
            PathToDownload = path;
            OnPropertyChanged(nameof(PathToDownload));
        }
    }
    private string _pathToDownload;
    public string PathToDownload
    {
        get
        {
            if (string.IsNullOrEmpty(_pathToDownload))
            {
                _pathToDownload = GlobalAppStateViewModel.options.DefaultPathToDownloadProfessionalTaskFiles;
            }
            return _pathToDownload;
        }
        set
        {
            if(value != null)
            {
                _pathToDownload = value;
            }
        }
    }
    [RelayCommand]
    public void SavePathToDownloadCommand()
    {
        GlobalAppStateViewModel.options.DefaultPathToDownloadProfessionalTaskFiles = PathToDownload;
        GlobalAppStateViewModel.options.Save();
    }



}