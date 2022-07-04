namespace BlazorChat.Server.Services
{
    public interface ITranslationService
    {
        /// <summary>
        /// Translates <paramref name="source"/> to <paramref name="language"/>
        /// </summary>
        /// <param name="source"></param>
        /// <param name="language"></param>
        /// <param name="sourceLanguage">If specified can help improve accuracy</param>
        /// <returns></returns>
        public Task<string?> Translate(string source, string language, string? sourceLanguage = null);
    }
}
