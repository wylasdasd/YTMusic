namespace YTMusic.BLL.Ports;

public interface IFilePickerService
{
    Task<IReadOnlyList<PickedFile>> PickMultipleAsync(string pickerTitle);
}
