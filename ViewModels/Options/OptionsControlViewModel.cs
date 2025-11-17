using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CodingSeb.Localization;
using Avalonia.Threading;
using System;
using System.IO;

namespace LesserDashboardClient.ViewModels.Options;

public partial class OptionsControlViewModel : ViewModelBase
{
    public OptionsControlViewModel()
    {
        // Observa mudanças no DefaultPathToDownloadProfessionalTaskFiles do OptionsModel
        if (GlobalAppStateViewModel.options != null)
        {
            GlobalAppStateViewModel.options.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(GlobalAppStateViewModel.options.DefaultPathToDownloadProfessionalTaskFiles))
                {
                    // Atualiza o campo quando o diretório é alterado externamente (no UI thread)
                    Dispatcher.UIThread.Post(() =>
                    {
                        RefreshPathToDownload();
                    });
                }
            };
        }
    }

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

    /// <summary>
    /// Atualiza o campo quando o diretório é alterado externamente (ex: pelo dialog de validação)
    /// </summary>
    public void RefreshPathToDownload()
    {
        // Limpa o _pathToDownload para que o getter retorne o valor atualizado das opções
        _pathToDownload = string.Empty;
        OnPropertyChanged(nameof(PathToDownload));
    }

    private string _pathToDownload;
    public string PathToDownload
    {
        get
        {
            // Retornar o valor do campo se preenchido, senão retornar o padrão apenas para exibição
            // Não modificar _pathToDownload aqui para permitir validação correta
            if (string.IsNullOrEmpty(_pathToDownload))
            {
                return GlobalAppStateViewModel.options.DefaultPathToDownloadProfessionalTaskFiles ?? string.Empty;
            }
            return _pathToDownload;
        }
        set
        {
            // Aceitar null ou string vazia para permitir que o usuário limpe o campo
            _pathToDownload = value ?? string.Empty;
        }
    }
    [RelayCommand]
    public void SavePathToDownloadCommand()
    {
        // Validar se o campo está vazio - verificar diretamente o _pathToDownload
        // Se o campo estiver vazio, não permitir salvar mesmo que exista um valor padrão
        if (string.IsNullOrWhiteSpace(_pathToDownload))
        {
            GlobalAppStateViewModel.Instance.ShowDialogOk(Loc.Tr("Please select a directory path"));
            return;
        }

        // Usar a função centralizada de validação
        // Primeiro atualiza temporariamente o valor nas opções para validar
        string tempPath = GlobalAppStateViewModel.options.DefaultPathToDownloadProfessionalTaskFiles;
        GlobalAppStateViewModel.options.DefaultPathToDownloadProfessionalTaskFiles = _pathToDownload;
        
        var (isValid, errorMessage) = GlobalAppStateViewModel.Instance.ValidateDownloadDirectory();
        
        if (!isValid)
        {
            // Restaura o valor original
            GlobalAppStateViewModel.options.DefaultPathToDownloadProfessionalTaskFiles = tempPath;
            GlobalAppStateViewModel.Instance.ShowDialogOk(errorMessage);
            return;
        }

        // Se válido, salva permanentemente
        GlobalAppStateViewModel.options.Save();
        GlobalAppStateViewModel.Instance.ShowDialogOk(Loc.Tr("Path saved successfully"));
    }



}