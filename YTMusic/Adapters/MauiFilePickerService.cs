using YTMusic.BLL.Ports;

namespace YTMusic.Adapters;

public sealed class MauiFilePickerService : IFilePickerService
{
    public async Task<IReadOnlyList<PickedFile>> PickMultipleAsync(string pickerTitle)
    {
        var files = await FilePicker.Default.PickMultipleAsync(new PickOptions
        {
            PickerTitle = pickerTitle
        });

        if (files == null)
        {
            return Array.Empty<PickedFile>();
        }

        return files
            .Where(file => file != null)
            .Select(file => new PickedFile
            {
                FileName = file!.FileName,
                FullPath = file.FullPath
            })
            .ToArray();
    }
}
