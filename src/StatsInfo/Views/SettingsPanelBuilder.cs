using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Data;

namespace StatsInfo.Views;

internal sealed class SettingsPanelBuilder
{
    public StackPanel Panel { get; } = new() { Margin = new Thickness(22, 18, 22, 18) };
    public SettingsPanelBuilder(string title, object context)
    {
        Panel.DataContext = context;
        Panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 14)
        });
    }
    public void Hint(string text) => Panel.Children.Add(new TextBlock
    {
        Text = text,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 8, 0, 14)
    });
    public TextBox Text(string label, string path)
    {
        Label(label);
        var input = new TextBox();
        Bind(input, TextBox.TextProperty, path);
        Name(input, label);
        Panel.Children.Add(input);
        return input;
    }
    public CheckBox Check(string label, string path)
    {
        var input = new CheckBox { Content = label };
        Bind(input, System.Windows.Controls.Primitives.ToggleButton.IsCheckedProperty, path);
        Name(input, label);
        Panel.Children.Add(input);
        return input;
    }
    public ComboBox Choice(string label, string path, IEnumerable values,
        string? display = null, string? selectedValue = null)
    {
        Label(label);
        var input = new ComboBox { ItemsSource = values };
        if (display != null) input.DisplayMemberPath = display;
        if (selectedValue != null) input.SelectedValuePath = selectedValue;
        Bind(input, selectedValue == null ? ComboBox.SelectedItemProperty : ComboBox.SelectedValueProperty, path);
        Name(input, label);
        Panel.Children.Add(input);
        return input;
    }
    public Slider Slider(string label, string path, double min, double max)
    {
        Label(label);
        var input = new Slider
        {
            Minimum = min,
            Maximum = max,
            TickFrequency = 0.05,
            IsSnapToTickEnabled = false,
            Margin = new Thickness(0, 6, 0, 12)
        };
        Bind(input, System.Windows.Controls.Primitives.RangeBase.ValueProperty, path);
        Name(input, label);
        Panel.Children.Add(input);
        return input;
    }
    public Button Action(string label, RoutedEventHandler action)
    {
        var button = new Button { Content = label, HorizontalAlignment = HorizontalAlignment.Left };
        button.Click += action;
        Name(button, label);
        Panel.Children.Add(button);
        return button;
    }
    public void Label(string label) => Panel.Children.Add(new TextBlock
    {
        Text = label,
        Margin = new Thickness(0, 8, 0, 2)
    });
    public static void Bind(FrameworkElement input, DependencyProperty property, string path) =>
        input.SetBinding(property, new Binding(path)
        {
            Mode = BindingMode.TwoWay,
            UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
            ValidatesOnExceptions = true
        });
    public static void Name(DependencyObject input, string name) => AutomationProperties.SetName(input, name);
}
