using System.Windows.Input;

namespace AutobotUpdater.Components;

public partial class Titlebar
{
    public Titlebar()
    {
        InitializeComponent();
    }

    private void DragArea_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left)
        {
            MainWindow.GetInstance()?.DragMove();
        }
    }
}