namespace FryWorld.ViewModels;

using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.AI;

public enum AiStepStatus
{
    Running,
    Completed,
    Failed
}

public partial class AiStepItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _toolName = string.Empty;

    [ObservableProperty]
    private AiStepStatus _status = AiStepStatus.Running;

    [ObservableProperty]
    private string? _output;

    [ObservableProperty]
    private bool _isExpanded;
}

public partial class AiChatMessageViewModel : ObservableObject
{
    [ObservableProperty]
    private ChatRole _role;

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private string? _reasoningContent;

    [ObservableProperty]
    private bool _isStreaming;

    [ObservableProperty]
    private bool _hasReasoning;

    [ObservableProperty]
    private bool _isReasoningExpanded;

    public bool IsUser => Role == ChatRole.User;
    public bool IsAssistant => Role == ChatRole.Assistant;

    public ObservableCollection<AiStepItemViewModel> Steps { get; } = new();

    public AiChatMessageViewModel(ChatRole role, string text = "")
    {
        Role = role;
        Text = text;
    }
}
