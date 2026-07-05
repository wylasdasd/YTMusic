namespace YTMusic.BLL.Ports;

public interface IDialogHost
{
    Task ShowFavoriteFolderDialogAsync(FavoritePickRequest request, string title);
    Task<string?> PromptCreateFolderNameAsync();
    Task<bool> ConfirmDeleteLocalFilesAsync(int count);
    Task<bool> ConfirmDeleteFolderAsync(string folderName);
}
