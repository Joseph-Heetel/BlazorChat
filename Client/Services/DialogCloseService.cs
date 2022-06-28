using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace BlazorChat.Client.Services
{
    /// <summary>
    /// A custom service for closing all open mudblazor dialogs (Since MudBlazor does not expose the option)
    /// </summary>
    public interface IDialogCloseService
    {
        /// <summary>
        /// Closes all open dialogs
        /// </summary>
        void CloseAll();
    }

    public class DialogCloseService : IDialogCloseService, IDisposable
    {
        private readonly MudBlazor.IDialogService _dialogService;
        private bool _disposed = false;

        /// <summary>
        /// Collection of all open dialogs
        /// </summary>
        private static readonly IDictionary<Guid, IDialogReference> _dialogs = new Dictionary<Guid, IDialogReference>();

        public DialogCloseService(MudBlazor.IDialogService dialogService)
        {
            _dialogService = dialogService;
            _dialogService.OnDialogInstanceAdded += dialogService_OnDialogInstanceAdded;
            _dialogService.OnDialogCloseRequested += dialogService_OnDialogCloseRequested;
        }

        private void dialogService_OnDialogCloseRequested(IDialogReference arg1, DialogResult arg2)
        {
            _dialogs.Remove(arg1.Id);
        }

        private void dialogService_OnDialogInstanceAdded(IDialogReference obj)
        {
            _dialogs.Add(obj.Id, obj);
        }

        public void CloseAll()
        {
            foreach (var dialog in _dialogs.Values)
            {
                dialog.Close();
            }
            _dialogs.Clear();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _dialogService.OnDialogInstanceAdded -= dialogService_OnDialogInstanceAdded;
                _dialogService.OnDialogCloseRequested -= dialogService_OnDialogCloseRequested;
                _disposed = true;
            }
        }
    }
}
