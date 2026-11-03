using CommunityToolkit.Mvvm.ComponentModel;
using StatsInfo.Models;
using StatsInfo.Services;

namespace StatsInfo.ViewModels;

public partial class MetricItemViewModel : ObservableObject
{
    private readonly SettingsService _settings;
    public MetricDefinition Definition { get; }
    public string Id => Definition.Id;
    public string Group => Definition.Group;
    public string Label => Definition.Label;
    public string Description => Definition.Description;
    public sealed record RoundingChoice(int Value, string Label);
    public RoundingChoice[] RoundingOptions { get; } = [new(-1, "Auto"), new(0, "0 decimals"),
        new(1, "1 decimal"), new(2, "2 decimals"), new(3, "3 decimals")];
    public string DisplayLabel => string.IsNullOrWhiteSpace(CustomLabel) ? Definition.ShortLabel : CustomLabel;
    public string[] UnitOptions => Definition.Unit switch
    {
        "°C" => ["Default", "°C", "°F"],
        "Mbps" => ["Default", "Mbps", "MB/s"],
        "GiB" => Id is "memory.physical.used" or "gpu.vram.used"
            ? ["Default", "GiB", "GB", "%"] : ["Default", "GiB", "GB"],
        "MHz" or "GHz" => ["Default", "MHz", "GHz"],
        _ => ["Default", Definition.Unit]
    };
    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private string _unit = "Default";
    [ObservableProperty] private string _customLabel = "";
    [ObservableProperty] private int _decimals = -1;
    [ObservableProperty] private bool _showSparkline;
    [ObservableProperty] private MetricSample[] _trend = [];
    public bool HasSparkline => ShowSparkline || Id == "game.frame_time.graph";
    [ObservableProperty] private string _valueText = "N/A";
    [ObservableProperty] private string _status = "Not sampled";
    [ObservableProperty] private string _availabilityReason = "Enable this metric or open Diagnostics to sample it.";
    [ObservableProperty] private string _tooltip = "";
    [ObservableProperty] private bool _isAvailable;
    [ObservableProperty] private bool _isAlert;
    [ObservableProperty] private string _source;
    public int Order => _settings.Settings.Metrics[Id].Order;
    public MetricSample? Sample { get; private set; }
    private bool _loading;

    public MetricItemViewModel(MetricDefinition definition, SettingsService settings)
    {
        Definition = definition;
        _settings = settings;
        _source = definition.Source;
        Reload();
    }

    public void Reload()
    {
        _loading = true;
        var preference = _settings.Settings.Metrics[Id];
        IsEnabled = preference.Visible;
        Unit = preference.Unit.Length == 0 ? "Default" : preference.Unit;
        CustomLabel = preference.Label;
        Decimals = preference.Decimals;
        ShowSparkline = preference.Sparkline;
        OnPropertyChanged(nameof(Order));
        _loading = false;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (_loading) return;
        MetricSettings.SetVisible(_settings.Settings, Id, value);
        _settings.Settings.Preset = "Custom";
        _settings.SaveSettings(_settings.Settings);
    }
    partial void OnUnitChanged(string value)
    {
        if (_loading) return;
        _settings.Settings.Metrics[Id].Unit = value;
        _settings.SaveSettings(_settings.Settings);
    }
    partial void OnCustomLabelChanged(string value)
    {
        OnPropertyChanged(nameof(DisplayLabel));
        if (_loading) return;
        _settings.Settings.Metrics[Id].Label = value;
        _settings.SaveSettings(_settings.Settings);
    }
    partial void OnDecimalsChanged(int value)
    {
        if (_loading) return;
        _settings.Settings.Metrics[Id].Decimals = Math.Clamp(value, -1, 3);
        _settings.SaveSettings(_settings.Settings);
    }
    partial void OnShowSparklineChanged(bool value)
    {
        OnPropertyChanged(nameof(HasSparkline));
        if (_loading) return;
        _settings.Settings.Metrics[Id].Sparkline = value;
        _settings.SaveSettings(_settings.Settings);
    }

    public void Update(MetricSample sample, IReadOnlyDictionary<string, MetricSample> samples)
    {
        Sample = sample;
        ValueText = MetricFormatter.Format(sample, _settings.Settings.Metrics[Id], _settings.Settings, samples);
        Source = sample.Source;
        IsAvailable = sample.Quality == SampleQuality.Valid;
        Status = sample.Quality == SampleQuality.Valid ? "Available" : sample.Quality.ToString();
        AvailabilityReason = sample.Reason ?? "";
        string values = sample.Values == null ? "" : string.Join("\n", sample.Values.Select(p => $"{p.Key}: {p.Value:0.##} {sample.Unit}"));
        Tooltip = $"{DisplayLabel} · {Definition.Label}\n{Description}\n{sample.Source}\n" +
            $"{sample.SampledAt.ToLocalTime():yyyy-MM-dd HH:mm:ss zzz} · {sample.Quality}\n{sample.Reason}\n{values}";
    }
}
