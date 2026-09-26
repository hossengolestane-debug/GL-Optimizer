using CommunityToolkit.Mvvm.ComponentModel;
using GLOptimizer.Core.Logging;

namespace GLOptimizer.App.ViewModels;

public partial class ChoiceItemViewModel : ObservableObject
{
    public required string Label { get; init; }

    public required LogSeverity Level { get; init; }

    [ObservableProperty]
    private bool _isSelected;
}
