using System.Windows;
using Microsoft.Win32;

namespace StatsInfo.Views;

public interface ISettingsDialogs
{
    bool Confirm(Window owner, string text, string title);
    string? ExportPath(Window owner, bool json);
}

public sealed class SettingsDialogs : ISettingsDialogs
{
    public bool Confirm(Window owner, string text, string title) =>
        MessageBox.Show(owner, text, title, MessageBoxButton.OKCancel) == MessageBoxResult.OK;
    public string? ExportPath(Window owner, bool json)
    {
        var dialog = new SaveFileDialog
        {
            Filter = json ? "JSON|*.json" : "CSV|*.csv",
            FileName = $"StatsInfo-{DateTime.Now:yyyyMMdd-HHmmss}.{(json ? "json" : "csv")}"
        };
        return dialog.ShowDialog(owner) == true ? dialog.FileName : null;
    }
}
