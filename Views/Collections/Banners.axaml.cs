using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media.Imaging;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace LesserDashboardClient.Views.Collections;

public partial class Banners : UserControl
{
    public static Banners Instance;
    public Banners()
    {
        InitializeComponent();
        Instance = this;

    }
}