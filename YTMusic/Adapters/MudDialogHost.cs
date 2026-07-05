using MudBlazor;
using YTMusic.BLL.Ports;
using YTMusic.Components.Dialogs;

namespace YTMusic.Adapters;

public sealed class MudDialogHost(IDialogService dialogService) : IDialogHost
{
    public async Task ShowFavoriteFolderDialogAsync(FavoritePickRequest request, string title)
    {
        var parameters = new DialogParameters<FavoriteFolderDialog>
        {
            { x => x.VideoId, request.VideoId },
            { x => x.Title, request.Title },
            { x => x.Author, request.Author },
            { x => x.ThumbnailUrl, request.ThumbnailUrl },
            { x => x.LocalFilePath, request.LocalFilePath }
        };

        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await dialogService.ShowAsync<FavoriteFolderDialog>(title, parameters, options);
        await dialog.Result;
    }

    public async Task<string?> PromptCreateFolderNameAsync()
    {
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await dialogService.ShowAsync<CreateFavoriteFolderDialog>("新建收藏夹", options);
        var result = await dialog.Result;
        if (result == null || result.Canceled || result.Data is not string name || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return name.Trim();
    }

    public async Task<bool> ConfirmDeleteLocalFilesAsync(int count)
    {
        var parameters = new DialogParameters<ConfirmDeleteLocalFilesDialog>
        {
            { x => x.Count, count }
        };
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true
        };

        var dialog = await dialogService.ShowAsync<ConfirmDeleteLocalFilesDialog>("删除本地文件", parameters, options);
        var result = await dialog.Result;
        return result is { Canceled: false };
    }

    public async Task<bool> ConfirmDeleteFolderAsync(string folderName)
    {
        var parameters = new DialogParameters<ConfirmDeleteFolderDialog>
        {
            { x => x.FolderName, folderName }
        };
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true
        };

        var dialog = await dialogService.ShowAsync<ConfirmDeleteFolderDialog>("删除收藏夹", parameters, options);
        var result = await dialog.Result;
        return result is { Canceled: false };
    }

    public async Task<bool> ConfirmAppResetAsync()
    {
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.ExtraSmall,
            FullWidth = true
        };

        var dialog = await dialogService.ShowAsync<ConfirmResetDialog>("还原默认", options);
        var result = await dialog.Result;
        return result is { Canceled: false };
    }

    public async Task<bool> ConfirmRemoteVideoPlayAsync(string trackTitle)
    {
        var parameters = new DialogParameters<RemoteVideoConfirmDialog>
        {
            { x => x.TrackTitle, trackTitle }
        };
        var options = new DialogOptions
        {
            CloseOnEscapeKey = true,
            MaxWidth = MaxWidth.Small,
            FullWidth = true
        };

        var dialog = await dialogService.ShowAsync<RemoteVideoConfirmDialog>("播放视频", parameters, options);
        var result = await dialog.Result;
        return result is { Canceled: false };
    }
}
