using MahApps.Metro.Controls;
using System.ComponentModel;

namespace NavmeshCollector;

public partial class MainWindow : MetroWindow
{
    public MainWindow()
    {
        InitializeComponent();
        var viewModel = new MainViewModel();
        DataContext = viewModel;
        
        // Auto-scroll to bottom when OutputText changes
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.OutputText))
        {
            // Scroll to bottom when output text changes
            Dispatcher.InvokeAsync(() => OutputScrollViewer.ScrollToBottom(), System.Windows.Threading.DispatcherPriority.Background);
        }
    }
}
