namespace FryWorld.ViewModels;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FryWorld.Sdk.AI;
using FryWorld.Services.AI;
using Microsoft.Extensions.AI;

public partial class AiAssistantViewModel : ViewModelBase
{
    private readonly IAiService _aiService;
    private readonly List<ChatMessage> _conversationHistory = new();
    private CancellationTokenSource? _activeCts;

    [ObservableProperty]
    private string _inputText = string.Empty;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string _contextTitle = "Workspace Context";

    [ObservableProperty]
    private string? _activeDocumentText;

    [ObservableProperty]
    private string _statusText = "Ready";

    public ObservableCollection<AiChatMessageViewModel> Messages { get; } = new();
    public ObservableCollection<AiPromptDescriptor> QuickPrompts { get; } = new();

    public AiAssistantViewModel(IAiService aiService)
    {
        _aiService = aiService ?? throw new ArgumentNullException(nameof(aiService));

        // Welcome greeting
        Messages.Add(new AiChatMessageViewModel(ChatRole.Assistant,
            "Hello! I am your **FryWorld AI Copilot**, powered by `Microsoft.Extensions.AI`.\n\n" +
            "I am grounded in your currently active workspace, can invoke tools, design circuits, and assist with workflows.\n" +
            "How can I help you today?"));

        // Subscribe to active workspace context updates
        _aiService.ContextManager.ActiveContextChanged += OnActiveContextChanged;
        _aiService.Prompts.PromptsChanged += RefreshQuickPrompts;

        UpdateContextBadge();
        RefreshQuickPrompts();
    }

    private void OnActiveContextChanged(AiContextSnapshot? snapshot)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            if (snapshot != null)
            {
                ContextTitle = snapshot.Title;
                ActiveDocumentText = snapshot.ActiveDocumentName ?? snapshot.PluginId;
            }
            else
            {
                ContextTitle = "Suite Launcher";
                ActiveDocumentText = null;
            }
        });
    }

    private async void UpdateContextBadge()
    {
        var snapshot = await _aiService.ContextManager.GetActiveContextAsync();
        OnActiveContextChanged(snapshot);
    }

    private void RefreshQuickPrompts()
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            QuickPrompts.Clear();
            foreach (var prompt in _aiService.Prompts.GetRegisteredPrompts())
            {
                QuickPrompts.Add(prompt);
            }
        });
    }

    [RelayCommand]
    public async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(InputText) || IsBusy) return;

        var userPrompt = InputText.Trim();
        InputText = string.Empty;

        var userMessage = new AiChatMessageViewModel(ChatRole.User, userPrompt);
        Messages.Add(userMessage);

        var assistantMessage = new AiChatMessageViewModel(ChatRole.Assistant, string.Empty)
        {
            IsStreaming = true
        };
        Messages.Add(assistantMessage);

        IsBusy = true;
        StatusText = "Thinking...";
        _activeCts = new CancellationTokenSource();

        try
        {
            // Build system prompt with live active context
            var basePrompt = (_aiService as AiService)?.Settings.CustomSystemPrompt ??
                "You are the intelligent copilot of FryWorld, a modular desktop environment.";
            var fullSystemPrompt = await _aiService.ContextManager.BuildSystemPromptAsync(basePrompt, _activeCts.Token);

            var requestMessages = new List<ChatMessage>
            {
                new(ChatRole.System, fullSystemPrompt)
            };

            // Add recent conversation turns
            foreach (var historyMsg in _conversationHistory)
            {
                requestMessages.Add(historyMsg);
            }
            requestMessages.Add(new ChatMessage(ChatRole.User, userPrompt));

            // Attach tools
            var tools = _aiService.Tools.GetAiTools();
            var chatOptions = new ChatOptions
            {
                Temperature = (_aiService as AiService)?.Settings.Temperature ?? 0.2f,
                MaxOutputTokens = (_aiService as AiService)?.Settings.MaxOutputTokens ?? 4096,
                Tools = tools.Count > 0 ? new List<AITool>(tools) : null
            };

            var textBuilder = new StringBuilder();
            var reasoningBuilder = new StringBuilder();
            bool inThinkTag = false;

            await foreach (var update in _aiService.ChatClient.GetStreamingResponseAsync(requestMessages, chatOptions, _activeCts.Token))
            {
                // Check for reasoning update
                if (update.AdditionalProperties != null)
                {
                    if (update.AdditionalProperties.TryGetValue("reasoning_content", out var rc) && rc != null)
                    {
                        reasoningBuilder.Append(rc.ToString());
                        assistantMessage.HasReasoning = true;
                        assistantMessage.ReasoningContent = reasoningBuilder.ToString();
                    }
                }

                if (update.Text != null)
                {
                    var text = update.Text;
                    if (inThinkTag)
                    {
                        int closeIdx = text.IndexOf("</think>", StringComparison.OrdinalIgnoreCase);
                        if (closeIdx >= 0)
                        {
                            var thinkPart = text[..closeIdx];
                            var regularPart = text[(closeIdx + 8)..];
                            inThinkTag = false;

                            if (!string.IsNullOrEmpty(thinkPart))
                            {
                                reasoningBuilder.Append(thinkPart);
                                assistantMessage.HasReasoning = true;
                                assistantMessage.ReasoningContent = reasoningBuilder.ToString();
                            }
                            if (!string.IsNullOrEmpty(regularPart))
                            {
                                textBuilder.Append(regularPart);
                                assistantMessage.Text = textBuilder.ToString();
                            }
                        }
                        else
                        {
                            reasoningBuilder.Append(text);
                            assistantMessage.HasReasoning = true;
                            assistantMessage.ReasoningContent = reasoningBuilder.ToString();
                        }
                    }
                    else if (text.Contains("<think>", StringComparison.OrdinalIgnoreCase))
                    {
                        int openIdx = text.IndexOf("<think>", StringComparison.OrdinalIgnoreCase);
                        var beforePart = text[..openIdx];
                        var thinkPart = text[(openIdx + 7)..];
                        inThinkTag = true;

                        if (!string.IsNullOrEmpty(beforePart))
                        {
                            textBuilder.Append(beforePart);
                            assistantMessage.Text = textBuilder.ToString();
                        }
                        if (!string.IsNullOrEmpty(thinkPart))
                        {
                            reasoningBuilder.Append(thinkPart);
                            assistantMessage.HasReasoning = true;
                            assistantMessage.ReasoningContent = reasoningBuilder.ToString();
                        }
                    }
                    else
                    {
                        textBuilder.Append(text);
                        assistantMessage.Text = textBuilder.ToString();
                    }
                }
            }

            assistantMessage.IsStreaming = false;
            assistantMessage.Text = textBuilder.ToString();

            // Save to history
            _conversationHistory.Add(new ChatMessage(ChatRole.User, userPrompt));
            _conversationHistory.Add(new ChatMessage(ChatRole.Assistant, assistantMessage.Text));

            StatusText = "Ready";
        }
        catch (OperationCanceledException)
        {
            assistantMessage.IsStreaming = false;
            assistantMessage.Text += "\n\n*(Generation stopped by user)*";
            StatusText = "Stopped";
        }
        catch (Exception ex)
        {
            assistantMessage.IsStreaming = false;
            assistantMessage.Text += $"\n\n**Error:** {ex.Message}";
            StatusText = "Error";
        }
        finally
        {
            IsBusy = false;
            _activeCts?.Dispose();
            _activeCts = null;
        }
    }

    [RelayCommand]
    public void Stop()
    {
        _activeCts?.Cancel();
    }

    [RelayCommand]
    public void ClearChat()
    {
        _conversationHistory.Clear();
        Messages.Clear();
        Messages.Add(new AiChatMessageViewModel(ChatRole.Assistant,
            "Chat history cleared. Grounded in **" + ContextTitle + "**.\nHow can I help you?"));
    }

    [RelayCommand]
    public void ApplyPrompt(AiPromptDescriptor prompt)
    {
        if (prompt == null) return;
        InputText = prompt.Template;
    }
}
