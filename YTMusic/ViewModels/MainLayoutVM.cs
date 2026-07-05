using Microsoft.JSInterop;
using YTMusic.BLL.Abstractions;
using YTMusic.BLL.Ports;
using YTMusic.Infrastructure.Storage;
using YTMusic.Services;

namespace YTMusic.ViewModels;

public sealed class MainLayoutVM : ViewModelBase, IDisposable
{
    private readonly UiPreferencesService _uiPreferences;
    private readonly AppResetService _appResetService;
    private readonly GlobalStateService _globalState;
    private readonly INetworkErrorService _networkErrorService;
    private readonly IDialogHost _dialogHost;
    private readonly IUiNotifier _notifier;

    public int ThemeIndex { get; private set; }
    public bool IsThemePanelOpen { get; private set; }

    public MainLayoutVM(
        UiPreferencesService uiPreferences,
        AppResetService appResetService,
        GlobalStateService globalState,
        INetworkErrorService networkErrorService,
        IDialogHost dialogHost,
        IUiNotifier notifier)
    {
        _uiPreferences = uiPreferences;
        _appResetService = appResetService;
        _globalState = globalState;
        _networkErrorService = networkErrorService;
        _dialogHost = dialogHost;
        _notifier = notifier;
    }

    public void Initialize()
    {
        if (ThemePresets.All.Length > 0)
        {
            ThemeIndex = Math.Clamp(_uiPreferences.ThemeIndex, 0, ThemePresets.All.Length - 1);
        }

        _uiPreferences.OnChange += OnUiPreferencesChanged;
        _networkErrorService.NotificationRequested += OnNetworkNotification;
    }

    public ThemePresets.ThemePreset ActiveTheme
    {
        get
        {
            if (ThemePresets.All.Length == 0)
            {
                return ThemePresets.Fallback;
            }

            var safeIndex = Math.Clamp(ThemeIndex, 0, ThemePresets.All.Length - 1);
            if (safeIndex != ThemeIndex)
            {
                ThemeIndex = safeIndex;
            }

            return ThemePresets.All[safeIndex];
        }
    }

    public bool CurrentIsDark => ActiveTheme.IsDark;
    public MudBlazor.MudTheme CurrentMudTheme => ActiveTheme.Theme;
    public string CurrentThemeCssVars => ActiveTheme.CssVars;

    public void ToggleThemePanel() => SetThemePanelOpen(!IsThemePanelOpen);

    public void CloseThemePanel() => SetThemePanelOpen(false);

    public void SelectTheme(int index)
    {
        if (ThemePresets.All.Length == 0)
        {
            return;
        }

        ThemeIndex = Math.Clamp(index, 0, ThemePresets.All.Length - 1);
        _uiPreferences.SetThemeIndex(ThemeIndex);
        SetThemePanelOpen(false);
    }

    public string GetThemeItemClass(int index)
        => index == ThemeIndex ? "ytm-theme-item is-active" : "ytm-theme-item";

    public Task OnShowFavoriteCardImagesChanged(bool value)
    {
        _uiPreferences.SetShowFavoriteCardImages(value);
        return Task.CompletedTask;
    }

    public Task OnMediaTitleTwoLinesChanged(bool value)
    {
        _uiPreferences.SetMediaTitleTwoLines(value);
        return Task.CompletedTask;
    }

    public Task OnPrefetchRemoteVideoChanged(bool value)
    {
        _uiPreferences.SetPrefetchRemoteVideo(value);
        return Task.CompletedTask;
    }

    public Task OnRemoteVideoStreamQualityChanged(RemoteVideoStreamQuality value)
    {
        _uiPreferences.SetRemoteVideoStreamQuality(value);
        return Task.CompletedTask;
    }

    public async Task CopyDownloadPathAsync(IJSRuntime jsRuntime)
    {
        var downloadPath = StoragePaths.GetDownloadedMusicDirectory();
        try
        {
            await jsRuntime.InvokeVoidAsync("navigator.clipboard.writeText", downloadPath);
            _notifier.Success("Download path copied.");
        }
        catch (Exception ex)
        {
            _notifier.Error($"Copy failed: {ex.Message}");
        }
    }

    public async Task ResetToDefaultsAsync()
    {
        SetThemePanelOpen(false);
        NotifyChanged();

        if (!await _dialogHost.ConfirmAppResetAsync())
        {
            return;
        }

        try
        {
            _globalState.ShowLoading();
            await _appResetService.ResetToDefaultsAsync();
            if (ThemePresets.All.Length > 0)
            {
                ThemeIndex = 0;
            }

            SetThemePanelOpen(false);
            _notifier.Success("已还原默认设置并清空本地数据。");
        }
        catch (Exception ex)
        {
            _notifier.Error($"还原默认失败: {ex.Message}");
        }
        finally
        {
            _globalState.HideLoading();
            NotifyChanged();
        }
    }

    public void Dispose()
    {
        _networkErrorService.NotificationRequested -= OnNetworkNotification;
        _uiPreferences.OnChange -= OnUiPreferencesChanged;
    }

    private void OnUiPreferencesChanged()
    {
        if (ThemePresets.All.Length > 0)
        {
            ThemeIndex = Math.Clamp(_uiPreferences.ThemeIndex, 0, ThemePresets.All.Length - 1);
        }

        NotifyChanged();
    }

    private void OnNetworkNotification(string message)
    {
        _globalState.HideLoading();
        _notifier.Error(message);
        NotifyChanged();
    }

    private void SetThemePanelOpen(bool open)
    {
        IsThemePanelOpen = open;
        NotifyChanged();
    }
}
