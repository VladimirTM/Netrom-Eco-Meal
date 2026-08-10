using Netrom_Eco_Meal.Controllers;
using Netrom_Eco_Meal.Entities;

namespace Netrom_Eco_Meal.Services;

// Per-circuit notification bell state, shared between the trigger button (rendered in the
// sidebar/header) and the popup panel (rendered at the layout's top level — see NotificationPanel).
// They can't be one component: the panel must render outside the sidebar's DOM subtree to avoid
// being trapped in its stacking context (position:sticky always creates one, which silently
// paints position:fixed descendants under <main> regardless of z-index), so trigger and panel
// need a shared source of truth instead of parent/child state. Same scoped-service + OnChange
// pattern as ManagedBusinessContext.
public class NotificationPanelState(NotificationController notificationController) : IDisposable
{
    private Timer? _pollTimer;
    // Trigger and panel both call InitializeAsync from their own OnInitializedAsync — caching the
    // in-flight task means whichever runs second awaits the same load instead of double-starting
    // the poll timer. Same fix as ManagedBusinessContext.EnsureLoadedAsync.
    private Task? _initTask;

    public bool IsOpen { get; private set; }
    public int UnreadCount { get; private set; }
    public List<Notification>? Notifications { get; private set; }

    public event Action? OnChange;

    public Task InitializeAsync() => _initTask ??= LoadAsync();

    private async Task LoadAsync()
    {
        await RefreshUnreadCountAsync();
        _pollTimer = new Timer(async _ => await SafeRefreshUnreadCountAndNotifyAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    // The Timer callback is effectively an async void running on a raw ThreadPool thread — any
    // exception that escapes it (e.g. a transient DB blip, or the circuit tearing down mid-poll)
    // is unobserved and crashes the whole process, not just this circuit. Must never rethrow.
    private async Task SafeRefreshUnreadCountAndNotifyAsync()
    {
        try
        {
            await RefreshUnreadCountAsync();
            OnChange?.Invoke();
        }
        catch (Exception)
        {
            // Best-effort poll — next tick in 30s will retry. Nothing to surface a failure to here.
        }
    }

    private async Task RefreshUnreadCountAsync()
    {
        UnreadCount = (await notificationController.GetMyUnreadCountAsync()).Value;
    }

    public async Task ToggleAsync()
    {
        IsOpen = !IsOpen;
        if (IsOpen)
        {
            Notifications = null;
            OnChange?.Invoke();
            Notifications = (await notificationController.GetMyNotificationsAsync(20)).Value ?? [];
        }

        OnChange?.Invoke();
    }

    public void Close()
    {
        IsOpen = false;
        OnChange?.Invoke();
    }

    public async Task MarkAllReadAsync()
    {
        await notificationController.MarkAllAsReadAsync();
        foreach (var notification in Notifications ?? [])
            notification.IsRead = true;
        UnreadCount = 0;
        OnChange?.Invoke();
    }

    public async Task MarkAsReadAsync(Notification notification)
    {
        if (notification.IsRead)
            return;

        await notificationController.MarkAsReadAsync(notification.Id);
        notification.IsRead = true;
        UnreadCount = Math.Max(0, UnreadCount - 1);
        OnChange?.Invoke();
    }

    public void Dispose()
    {
        _pollTimer?.Dispose();
    }
}
