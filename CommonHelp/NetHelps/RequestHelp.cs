using global::CommonTool.JsonHelps;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace CommonTool.NetHelps
{
    public class RequestHelp
    {
        private static readonly HttpClient _httpClient = new HttpClient();

        /// <summary>
        ///
        /// </summary>
        /// <param name="url"></param>
        /// <param name="body"></param>
        /// <param name="action">修改请求头</param>
        /// <returns></returns>
        public static async Task<int> SendRequestAsync(string url, string body, Action<HttpRequestHeaders> action)
        {
            // 1. 创建请求消息对象，指明方法和地址
            using var request = new HttpRequestMessage(HttpMethod.Post, url);

            // 2. 在消息级别设置 Header（线程安全，仅对本次请求有效）
            //request.Headers.Add("X-Custom-Header", "client");
            //request.Headers.Add("Authorization", "Bearer token_here");

            action.Invoke(request.Headers);

            // 3. 设置内容
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            // 4. 发送这个特定的请求对象
            using var response = await _httpClient.SendAsync(request);

            return (int)response.StatusCode;
        }
    }

    /// <summary>
    /// 用法通过注入使用
    /// </summary>
    /// <param name="httpClientFactory"></param>
    public class HttpService : IHttpService
    {
        protected readonly IHttpClientFactory httpClientFactory;
        protected readonly ILogger<IHttpService> logger;

        public HttpService(IHttpClientFactory httpClientFactory, ILogger<IHttpService> logger)
        {
            this.httpClientFactory = httpClientFactory;
            this.logger = logger;
        }

        public virtual async Task<T?> PostRequestAsync<T>(string url, string body, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null) where T : class
        {
            var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            action?.Invoke(request.Headers);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            // 1. 创建超时取消令牌
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                // 2. 发送请求时传入令牌
                using var response = await client.SendAsync(request, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    if (typeof(T) == typeof(string))
                    {
                        return (T?)(object)content;
                    }

                    // 假设 JsonDe 是你的反序列化扩展方法
                    return content.JsonDe<T>();
                }
                return default;
            }
            catch (OperationCanceledException)
            {
                logger.LogError("在请求里面捕获 OperationCanceledException");
                // 3. 专门捕获超时异常

                return default;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "在请求里面捕获 OperationCanceledException");
                return default;
            }
        }

        public virtual async Task<T?> PutRequestAsync<T>(string url, string body, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null) where T : class
        {
            var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Put, url);
            action?.Invoke(request.Headers);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            // 1. 创建超时取消令牌
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                // 2. 发送请求时传入令牌
                using var response = await client.SendAsync(request, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    if (typeof(T) == typeof(string))
                    {
                        return (T?)(object)content;
                    }

                    // 假设 JsonDe 是你的反序列化扩展方法
                    return content.JsonDe<T>();
                }
                return default;
            }
            catch (OperationCanceledException)
            {
                // 3. 专门捕获超时异常
                throw;
                //return default;
            }
            catch (Exception ex)
            {
                return default;
            }
        }

        public virtual async Task<T?> DeleteRequestAsync<T>(string url, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null) where T : class
        {
            var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Delete, url);
            action?.Invoke(request.Headers);

            // 1. 创建超时取消令牌
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                // 2. 发送请求时传入令牌
                using var response = await client.SendAsync(request, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    if (typeof(T) == typeof(string))
                    {
                        return (T?)(object)content;
                    }

                    // 假设 JsonDe 是你的反序列化扩展方法
                    return content.JsonDe<T>();
                }
                return default;
            }
            catch (OperationCanceledException)
            {
                logger.LogError("在请求里面捕获 OperationCanceledException");
                // 3. 专门捕获超时异常

                return default;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "在请求里面捕获 OperationCanceledException");
                return default;
            }
        }

        public virtual async Task<T?> GetRequestAsync<T>(string url, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null) where T : class
        {
            var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            action?.Invoke(request.Headers);

            // 1. 创建超时取消令牌
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

            try
            {
                // 2. 发送请求时传入令牌
                using var response = await client.SendAsync(request, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();

                    if (typeof(T) == typeof(string))
                    {
                        return (T?)(object)content;
                    }

                    // 假设 JsonDe 是你的反序列化扩展方法
                    return content.JsonDe<T>();
                }
                return default;
            }
            catch (OperationCanceledException)
            {
                logger.LogError("在请求里面捕获 OperationCanceledException");
                // 3. 专门捕获超时异常

                return default;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "在请求里面捕获 OperationCanceledException");
                return default;
            }
        }

        public virtual async Task<T?> PostFormRequestAsync<T>(string url, Dictionary<string, object> formData, Action<HttpRequestHeaders>? action = null, int timeoutSeconds = 60 * 10) where T : class
        {
            var client = httpClientFactory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            action?.Invoke(request.Headers);
            request.Content = new FormUrlEncodedContent(formData.ToDictionary(k => k.Key, v => v.Value?.ToString() ?? ""));
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
            try
            {
                using var response = await client.SendAsync(request, cts.Token);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    if (typeof(T) == typeof(string))
                    {
                        return (T?)(object)content;
                    }
                    return content.JsonDe<T>();
                }
                return default;
            }
            catch (OperationCanceledException)
            {
                logger.LogError("PostFormRequestAsync OperationCanceledException");
                return default;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "PostFormRequestAsync Exception");
                return default;
            }
        }
    }

    public interface IHttpService
    {
        /// <summary>
        /// post 请求
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url"></param>
        /// <param name="body"></param>
        /// <param name="timeoutSeconds"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        Task<T?> PostRequestAsync<T>(string url, string body, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null) where T : class;

        /// <summary>
        /// put 请求
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url"></param>
        /// <param name="body"></param>
        /// <param name="timeoutSeconds"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        Task<T?> PutRequestAsync<T>(string url, string body, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null) where T : class;

        /// <summary>
        /// put 请求
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url"></param>
        /// <param name="body"></param>
        /// <param name="timeoutSeconds"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        Task<T?> DeleteRequestAsync<T>(string url, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null) where T : class;

        /// <summary>
        /// get 请求
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="url"></param>
        /// <param name="timeoutSeconds"></param>
        /// <param name="action"></param>
        /// <returns></returns>
        Task<T?> GetRequestAsync<T>(string url, int timeoutSeconds = 30, Action<HttpRequestHeaders>? action = null) where T : class;

        Task<T?> PostFormRequestAsync<T>(string url, Dictionary<string, object> formData, Action<HttpRequestHeaders>? action = null, int timeoutSeconds = 60 * 3) where T : class;
    }
}