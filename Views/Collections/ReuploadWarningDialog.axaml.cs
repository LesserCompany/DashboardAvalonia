using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace LesserDashboardClient.Views.Collections;

public partial class ReuploadWarningDialog : Window
{
    public bool Result { get; private set; } = false;
    
    public RelayCommand YesCommand { get; }
    public RelayCommand NoCommand { get; }

    public ReuploadWarningDialog()
    {
        InitializeComponent();
        
        YesCommand = new RelayCommand(() => {
            Result = true;
            Close();
        });
        
        NoCommand = new RelayCommand(() => {
            Result = false;
            Close();
        });
        
        DataContext = this;
    }
}





