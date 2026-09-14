using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using DiskOptimizer.Models;

namespace DiskOptimizer.Views;

public partial class HardwareView : UserControl
{
    public event EventHandler? AdvancedRequested;
    public HardwareView() => InitializeComponent();
    private void Advanced_Click(object sender, RoutedEventArgs e) => AdvancedRequested?.Invoke(this, EventArgs.Empty);
    private void OpenSource_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DriverAssessment row } && row.CanOpenSource)
        {
            try { Process.Start(new ProcessStartInfo(row.SourceUrl) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Nie otwarto źródła"); }
        }
    }
}
