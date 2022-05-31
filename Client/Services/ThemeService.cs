using Blazored.LocalStorage;

namespace CustomBlazorApp.Client.Services
{
    public interface IThemeService
    {
        public IReadOnlyObservable<bool> IsDarkMode { get; }
        public void SetDarkMode(bool darkMode);
        public void ToggleDarkMode() { SetDarkMode(!IsDarkMode.State); }
    }
    public class ThemeService : IThemeService, IDisposable
    {
        private readonly Observable<bool> _isDarkMode = new Observable<bool>(true);
        private readonly IServiceScope _serviceScope;
        private readonly ILocalStorageService _storageService;

        public IReadOnlyObservable<bool> IsDarkMode => _isDarkMode;

        public ThemeService(IServiceScopeFactory scopeFactory)
        {
            this._serviceScope = scopeFactory.CreateScope();
            this._storageService = this._serviceScope.ServiceProvider.GetRequiredService<ILocalStorageService>();
            _ = Task.Run(GetPreference);
        }

        private async Task GetPreference()
        {
            try
            {
                if (!await this._storageService.ContainKeyAsync("darkmode"))
                {
                    _isDarkMode.TriggerChange(true);
                    _ = Task.Run(SavePreference);
                }
                else
                {
                    bool preference = await this._storageService.GetItemAsync<bool>("darkmode");
                    _isDarkMode.TriggerChange(preference);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to read preference for darkmode: {ex}");
                _ = Task.Run(SavePreference);
            }
        }

        private async Task SavePreference()
        {
            try
            {
                await this._storageService.SetItemAsync<bool>("darkmode", _isDarkMode.State);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to set preference for darkmode: {ex}");
            }
        }

        public void SetDarkMode(bool darkMode)
        {
            _isDarkMode.State = darkMode;
            _ = Task.Run(SavePreference);
        }

        public void Dispose()
        {
            _serviceScope.Dispose();
        }
    }
}
