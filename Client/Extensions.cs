using BlazorChat.Shared;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace BlazorChat.Client
{
    public static class Extensions
    {
        public static async Task<Tout?> GetFromJSONAsyncNoExcept<Tout>(this HttpClient client, string path)
        {
            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Get, path);
            HttpResponseMessage response = await client.SendAsync(message);

            if (response != null && response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<Tout>();
            }
#if APIDEBUGLOGGING
            else if (response != null)
            {
                Console.WriteLine($"HTTP Get \"{path}\" error <{response.StatusCode}> content \"{await response.Content.ReadAsStringAsync()}\"");
            }
            else
            {
                Console.WriteLine($"HTTP Get \"{path}\" error {nameof(HttpClient)}.{nameof(HttpClient.SendAsync)} return null");
            }
#endif
            return default;
        }
        public static async Task<Tout?> PostGetFromJSONAsync<Tin, Tout>(this HttpClient client, string path, Tin requestContent)
        {
            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, path);
            message.Content = new StringContent(JsonSerializer.Serialize<Tin>(requestContent), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.SendAsync(message);

            if (response != null && response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<Tout>();
            }
#if APIDEBUGLOGGING
            else if (response != null)
            {
                Console.WriteLine($"HTTP Post+Get \"{path}\"<{nameof(Tin)}> error <{response.StatusCode}> content \"{await response.Content.ReadAsStringAsync()}\"");
            }
            else
            {
                Console.WriteLine($"HTTP Post+Get \"{path}\"<{nameof(Tin)}> error {nameof(HttpClient)}.{nameof(HttpClient.SendAsync)} return null");
            }
#endif
            return default;
        }
        public static async Task<Tout?> PostGetFromJSONAsync<Tout>(this HttpClient client, string path)
        {
            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, path);
            HttpResponseMessage response = await client.SendAsync(message);

            if (response != null && response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<Tout>();
            }
            else if (response != null)
            {
                Console.WriteLine($"HTTP Post+Get \"{path}\" error <{response.StatusCode}> content \"{await response.Content.ReadAsStringAsync()}\"");
            }
            else
            {
                Console.WriteLine($"HTTP Post+Get \"{path}\" error {nameof(HttpClient)}.{nameof(HttpClient.SendAsync)} return null");
            }
            return default;
        }
        public static async Task<string?> PostGetStringAsync<Tin>(this HttpClient client, string path, Tin requestContent)
        {
            HttpRequestMessage message = new HttpRequestMessage(HttpMethod.Post, path);
            message.Content = new StringContent(JsonSerializer.Serialize<Tin>(requestContent), Encoding.UTF8, "application/json");
            HttpResponseMessage response = await client.SendAsync(message);

            if (response != null && response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
#if APIDEBUGLOGGING
            else if (response != null)
            {
                Console.WriteLine($"HTTP Post+Get \"{path}\"<{nameof(Tin)}> error <{response.StatusCode}> content \"{await response.Content.ReadAsStringAsync()}\"");
            }
            else
            {
                Console.WriteLine($"HTTP Post+Get \"{path}\"<{nameof(Tin)}> error {nameof(HttpClient)}.{nameof(HttpClient.SendAsync)} return null");
            }
#endif
            return default;
        }

        public static Task InvokeAsync(this Func<Task>? func)
        {
            if (func == null)
            {
                return Task.CompletedTask;
            }
            var tasks = func.GetInvocationList().Select(f => ((Func<Task>)f)());
            return Task.WhenAll(tasks);
        }

        public static Task InvokeAsync<T>(this Func<T, Task>? func, T arg0)
        {
            if (func == null)
            {
                return Task.CompletedTask;
            }
            var tasks = func.GetInvocationList().Select(f => ((Func<T, Task>)f)(arg0));
            return Task.WhenAll(tasks);
        }

        public static Task InvokeAsync<T0, T1>(this Func<T0, T1, Task>? func, T0 arg0, T1 arg1)
        {
            if (func == null)
            {
                return Task.CompletedTask;
            }
            var tasks = func.GetInvocationList().Select(f => ((Func<T0, T1, Task>)f)(arg0, arg1));
            return Task.WhenAll(tasks);
        }

        public static Task InvokeAsync<T0, T1, T2>(this Func<T0, T1, T2, Task>? func, T0 arg0, T1 arg1, T2 arg2)
        {
            if (func == null)
            {
                return Task.CompletedTask;
            }
            var tasks = func.GetInvocationList().Select(f => ((Func<T0, T1, T2, Task>)f)(arg0, arg1, arg2));
            return Task.WhenAll(tasks);
        }
    }
}
