using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using System.Collections.ObjectModel;
using System.Linq;

namespace LesserDashboardClient.Views.Collections;

public partial class SkeletonList : UserControl
{
    public ObservableCollection<string> Test { get; } = new ObservableCollection<string> { "a","b","c"};
    public SkeletonList()
    {
        InitializeComponent();
        animals.ItemsSource = new string[]
    {"cat", "camel", "cow", "chameleon", "mouse", "lion", "zebra" }
.OrderBy(x => x);
    }
}
