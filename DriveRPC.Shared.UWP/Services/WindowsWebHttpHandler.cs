using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UserPresenceRPC.Discord.Net.Interfaces;
using Windows.Storage.Streams;
using Windows.Web.Http;

namespace DriveRPC.Shared.UWP.Services
{
    public class WindowsWebHttpHandler : IHttpHandler
    {
        private readonly HttpClient _client;

        private static readonly SemaphoreSlim _httpLock = new SemaphoreSlim(1, 1);

        private readonly Dictionary<string, string> _customHeaders = new Dictionary<string, string>();

        public void SetHeader(string name, string value)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentNullException(nameof(name));

            _customHeaders[name] = value;
        }


        public WindowsWebHttpHandler()
        {
            _client = new HttpClient();
        }

        public async Task<DiscordHttpResponse> GetAsync(string url, string userToken = null)
        {
            await _httpLock.WaitAsync();
            try
            {
                Debug($"[GET] URL = {url}");
                Debug($"[GET] Token = {(string.IsNullOrEmpty(userToken) ? "<none>" : userToken)}");

                using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(8)))
                {
                    var uri = new Uri(url);
                    var request = new HttpRequestMessage(HttpMethod.Get, uri);

                    foreach (var kv in _customHeaders)
                    {
                        request.Headers.TryAppendWithoutValidation(kv.Key, kv.Value);
                    }

                    if (!string.IsNullOrEmpty(userToken))
                        request.Headers.Add("Authorization", userToken);


                    var response = await _client.SendRequestAsync(request).AsTask(cts.Token);
                    var body = await response.Content.ReadAsStringAsync();

                    return new DiscordHttpResponse
                    {
                        StatusCode = (int)response.StatusCode,
                        Body = body
                    };
                }
            }
            catch (OperationCanceledException)
            {
                Debug("[ERROR] GET timed out (canceled underlying request)");
                throw new TimeoutException("HTTP GET timed out");
            }
            finally
            {
                _httpLock.Release();
            }
        }

        public async Task<DiscordHttpResponse> PostJsonAsync(string url, string json, string userToken)
        {
            await _httpLock.WaitAsync(); try
            {
                try
                {
                    var requestTask = InternalPostJsonAsync(url, json, userToken);
                    var timeoutTask = Task.Delay(8000);

                    var completed = await Task.WhenAny(requestTask, timeoutTask);

                    if (completed == timeoutTask)
                        throw new TimeoutException("HTTP request timed out on W10M");

                    return await requestTask;
                }
                catch (Exception ex)
                {
                    Debug($"[ERROR] {ex}");
                    throw;
                }
            }
            finally
            {
                _httpLock.Release();
            }
        }

        private async Task<DiscordHttpResponse> InternalPostJsonAsync(string url, string json, string userToken)
        {
            Debug($"[POST] URL = {url}");
            Debug($"[POST] JSON = {json}");
            Debug($"[POST] Token = {(string.IsNullOrEmpty(userToken) ? "<none>" : userToken)}");

            try
            {
                var uri = new Uri(url);
                var content = new HttpStringContent(json, UnicodeEncoding.Utf8, "application/json");

                var request = new HttpRequestMessage(HttpMethod.Post, uri);
                request.Content = content;

                foreach (var kv in _customHeaders)
                {
                    request.Headers.TryAppendWithoutValidation(kv.Key, kv.Value);
                }

                if (!string.IsNullOrEmpty(userToken))
                {
                    request.Headers.Add("Authorization", userToken);
                }

                Debug("[POST] Request Headers:");
                foreach (var h in request.Headers)
                    Debug($"   {h.Key}: {string.Join(", ", h.Value)}");

                Debug("[POST] Sending request...");

                using (var response = await _client.SendRequestAsync(request))
                {
                    Debug($"[POST] Response Status = {(int)response.StatusCode} ({response.StatusCode})");

                    var body = await response.Content.ReadAsStringAsync();

                    Debug($"[POST] Response Body = {body}");

                    return new DiscordHttpResponse
                    {
                        StatusCode = (int)response.StatusCode,
                        Body = body
                    };
                }
            }
            catch (Exception ex)
            {
                Debug($"[ERROR] Exception in PostJsonAsync: {ex}");
                throw;
            }
        }

        public void Dispose()
        {
            Debug("[DISPOSE] WindowsWebHttpHandler disposed");
            _client?.Dispose();
        }

        private void Debug(string msg)
        {
            System.Diagnostics.Debug.WriteLine($"[WindowsWebHttpHandler] {DateTime.Now:HH:mm:ss.fff} {msg}");
        }
    }
}