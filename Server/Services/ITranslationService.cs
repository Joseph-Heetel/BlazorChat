namespace BlazorChat.Server.Services
{
    public interface ITranslationService
    {
        public Task<string?> Translate(string source, string language, string? sourceLanguage = null);
    }
}
