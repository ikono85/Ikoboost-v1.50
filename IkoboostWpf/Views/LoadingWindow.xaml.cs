using System.Windows;
using System.Windows.Media.Animation;

namespace IkoboostWpf.Views;

public partial class LoadingWindow : Window
{
    public LoadingWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (Resources["LoadingStoryboard"] is Storyboard sb)
                sb.Begin(this);
        };
    }
}
