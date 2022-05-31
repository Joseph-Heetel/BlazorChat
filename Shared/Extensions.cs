using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace BlazorChat.Shared
{
    /// <summary>
    /// A helper struct result of <see cref="Extensions.WaitAsyncDisposable(SemaphoreSlim)"/>. 
    /// When disposed, releases the semaphore. 
    /// <code>using (await semaphore.WaitAsyncDisposable()){ ... }</code>
    /// will automatically release the semaphore upon exiting the control block
    /// </summary>
    public struct SemaphoreAccessDisposable : IDisposable
    {
        private readonly SemaphoreSlim _Semaphore;
        private bool _Disposed;

        public SemaphoreAccessDisposable(SemaphoreSlim semaphore)
        {
            _Semaphore = semaphore;
            _Disposed = false;
        }

        public void Dispose()
        {
            if (!_Disposed)
            {
                _Semaphore.Release();
                _Disposed = true;
            }
        }
    }

    public static class Extensions
    {
        /// <summary>
        /// Acquires a lock on the semaphore in form of a struct, which when disposed, releases the semaphore.
        /// <code>using (await semaphore.WaitAsyncDisposable()){ ... }</code>
        /// will automatically release the semaphore upon exiting the control block
        /// </summary>
        public static async ValueTask<SemaphoreAccessDisposable> WaitAsyncDisposable(this SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync();
            return new SemaphoreAccessDisposable(semaphore);
        }

        /// <summary>
        /// Acquires the users user Id (which if present indicates that the user is authenticated)
        /// </summary>
        public static bool GetUserLogin(this ClaimsPrincipal principal, out ItemId userId)
        {
            userId = default;
            if (!(principal.Identity?.IsAuthenticated) ?? false)
            {
                return false;
            }
            string? userIdStr = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (ItemId.TryParse(userIdStr, out userId))
            {
                return true;
            }
            return false;
        }
    }
}
