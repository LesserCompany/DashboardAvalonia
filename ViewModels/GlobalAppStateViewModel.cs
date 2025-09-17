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
    public static void LoadLesserFunctionClient()
    {
        LesserFunctionClient.DefaultClient.InitFromFile((string ack) => { });
        _lfc = LesserFunctionClient.DefaultClient;
    }
    private void ChangeAppTheme(string theme)
    {
        var app = App.Current as App;
        app?.SwitchCurrentTheme(theme);
    }
    private void ChangeAppLanguage(string lang)
    {
        var app = App.Current as App;
        app?.SetCurrentLang(lang);
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