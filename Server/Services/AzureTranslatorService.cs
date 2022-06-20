using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorChat.Server.Services
{
    public class AzureTranslatorService : ITranslationService
    {
        private readonly string KEY = "";
        private readonly string LOCATION = "";
        private readonly string ENDPOINT = "https://api.cognitive.microsofttranslator.com/";

        public AzureTranslatorService()
        {
            KEY = Environment.GetEnvironmentVariable(EnvironmentVarKeys.AZURETRANSLATORKEY)!;
            LOCATION = Environment.GetEnvironmentVariable(EnvironmentVarKeys.AZURETRANSLATORLOCATION)!;
        }

        class TranslateRequest
        {
            [JsonPropertyName("text")]
            public string? Text { get; set; }

            public TranslateRequest() { }
            public TranslateRequest(string? text)
            {
                Text = text;
            }
        }

        class TranslateResponse
        {
            [JsonPropertyName("text")]
            public string? Text { get; set; }
            [JsonPropertyName("to")]
            public string? To { get; set; }
        }

        class LanguageDetectResponse
        {
            [JsonPropertyName("language")]
            public string? Language { get; set; }
            [JsonPropertyName("score")]
            public float? Score { get; set; }
        }

        class TranslateResponseContainer
        {
            [JsonPropertyName("detectedLanguage")]
            public LanguageDetectResponse? DetectedLanguage { get; set; }

            [JsonPropertyName("translations")]
            public TranslateResponse[]? Translations { get; set; }
        }

        public async Task<string?> Translate(string source, string language, string? sourceLanguage = null)
        {
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(language) || string.IsNullOrEmpty(KEY) || string.IsNullOrEmpty(LOCATION))
            {
                return null;
            }

            // Input and output languages are defined as parameters.
            string route = $"/translate?api-version=3.0&to={language}";
            if (!string.IsNullOrEmpty(sourceLanguage))
            {
                route += $"&from={sourceLanguage}";
            }
            TranslateRequest[] translateRequests = new TranslateRequest[] { new TranslateRequest(source) };
            string requestBody = JsonSerializer.Serialize(translateRequests);

            using var client = new HttpClient();
            using var request = new HttpRequestMessage();
            // Build the request.
            request.Method = HttpMethod.Post;
            request.RequestUri = new Uri(ENDPOINT + route);
            request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
            request.Headers.Add("Ocp-Apim-Subscription-Key", KEY);
            request.Headers.Add("Ocp-Apim-Subscription-Region", LOCATION);

            // Send the request and get response.
            using HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
            // Read response as a string.
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }
            Console.WriteLine(await response.Content.ReadAsStringAsync());
            TranslateResponseContainer[]? responseContainers = await response.Content.ReadFromJsonAsync<TranslateResponseContainer[]>();
            TranslateResponseContainer? responseContainer = responseContainers?.FirstOrDefault();
            if (responseContainer == null || responseContainer.Translations == null || responseContainer.Translations.Length == 0)
            {
                return null;
            }
            return responseContainer.Translations.First().Text;
        }
    }
}
