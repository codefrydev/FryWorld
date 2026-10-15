using System;
using FryWorld.Sdk.Services;

namespace FryWorld.Services;

public sealed class NotificationService : INotificationService
{
    public event Action<string, string, string?>? ToastRequested;

    public void ShowToast(string message, string title = "Information", string? iconKind = null)
    {
        ToastRequested?.Invoke(message, title, iconKind);
    }

    public void ShowError(string message, string title = "Error")
    {
        ToastRequested?.Invoke(message, title, "AlertCircle");
    }
}
