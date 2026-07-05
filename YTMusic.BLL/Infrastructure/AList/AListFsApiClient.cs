using System.Net;
using System.Net.Http.Headers;
using YTMusic.BLL.Abstractions;

namespace YTMusic.BLL.Infrastructure.AList;

/// <summary>AList 文件系统 REST API 的 HTTP 传输实现。</summary>
public sealed class AListFsApiClient
{
    private readonly IAListUploadSettingsService _settings;

    public AListFsApiClient(IAListUploadSettingsService settings)
    {
        _settings = settings;
    }

    public async Task<HttpResponseMessage> SendDownloadRequestAsync(string downloadUrl, CancellationToken cancellationToken)
    {
        using var firstRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        var response = await AListHttpClients.Default.SendAsync(firstRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (response.IsSuccessStatusCode)
        {
            return response;
        }

        if (response.StatusCode != HttpStatusCode.Unauthorized && response.StatusCode != HttpStatusCode.Forbidden)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            response.Dispose();
            throw new InvalidOperationException(AListApiHelpers.GetErrorMessage(body, $"Download failed with HTTP {(int)response.StatusCode}."));
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        response.Dispose();

        using var secondRequest = new HttpRequestMessage(HttpMethod.Get, downloadUrl);
        secondRequest.Headers.TryAddWithoutValidation("Authorization", _settings.Token);

        var retryResponse = await AListHttpClients.Default.SendAsync(secondRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (retryResponse.IsSuccessStatusCode)
        {
            return retryResponse;
        }

        var retryBody = await retryResponse.Content.ReadAsStringAsync(cancellationToken);
        retryResponse.Dispose();
        throw new InvalidOperationException(AListApiHelpers.GetErrorMessage(retryBody, AListApiHelpers.GetErrorMessage(responseBody, "Download failed with HTTP 401/403.")));
    }

    public async Task UploadStreamToPathAsync(Stream sourceStream, string remoteFilePath, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(remoteFilePath))
        {
            throw new InvalidOperationException("Remote upload path is required.");
        }

        var encodedRemoteFilePath = AListApiHelpers.EncodeRemotePath(remoteFilePath);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"{_settings.BaseUrl}/api/fs/put");
        request.Headers.TryAddWithoutValidation("Authorization", _settings.Token);
        request.Headers.TryAddWithoutValidation("File-Path", encodedRemoteFilePath);
        request.Content = new ProgressStreamContent(sourceStream, progress, disposeSource: false);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        using var response = await AListHttpClients.Upload.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureUploadSuccessAsync(response, cancellationToken);
    }

    public async Task UploadLargeFileToPathAsync(string localFilePath, string remoteFilePath, CancellationToken cancellationToken)
    {
        EnsureConfigured();
        if (string.IsNullOrWhiteSpace(remoteFilePath))
        {
            throw new InvalidOperationException("Remote upload path is required.");
        }

        await using var sourceStream = File.OpenRead(localFilePath);
        var encodedRemoteFilePath = AListApiHelpers.EncodeRemotePath(remoteFilePath);

        using var request = new HttpRequestMessage(HttpMethod.Put, $"{_settings.BaseUrl}/api/fs/put");
        request.Headers.TryAddWithoutValidation("Authorization", _settings.Token);
        request.Headers.TryAddWithoutValidation("File-Path", encodedRemoteFilePath);

        var streamContent = new StreamContent(sourceStream);
        streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        streamContent.Headers.ContentLength = sourceStream.Length;
        request.Content = streamContent;

        using var response = await AListHttpClients.Upload.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        await EnsureUploadSuccessAsync(response, cancellationToken);
    }

    private void EnsureConfigured()
    {
        if (!_settings.IsConfigured)
        {
            throw new InvalidOperationException("Please complete AList server, token, and remote directory settings first.");
        }
    }

    private static async Task EnsureUploadSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(AListApiHelpers.GetErrorMessage(body, $"Upload failed with HTTP {(int)response.StatusCode}."));
        }

        var apiMessage = AListApiHelpers.GetApiMessage(body);
        if (!string.IsNullOrWhiteSpace(apiMessage) &&
            !apiMessage.Equals("success", StringComparison.OrdinalIgnoreCase) &&
            !apiMessage.Equals("ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(apiMessage);
        }
    }
}
