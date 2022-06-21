using BlazorChat.Server.Services;
using BlazorChat.Shared;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Azure.Cosmos;
using System.Collections;
using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BlazorChat
{
    public static class MiscExtensions { 

        public static async Task<Session> SigninSession(this HttpContext context, TimeSpan? expireDelay, Shared.User user)
        {
            // Create claim (UserId)
            Claim claim = new Claim(ClaimTypes.NameIdentifier, user.Id.ToString());
            // Create claimsIdentity
            ClaimsIdentity claimsIdentity = new ClaimsIdentity(new[] { claim }, "serverAuth");
            // Create claimPrincipal
            ClaimsPrincipal claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
            DateTimeOffset expires = DateTimeOffset.UtcNow + TimeSpan.FromHours(8);
            if (expireDelay.HasValue)
            {
                TimeSpan max = TimeSpan.FromDays(2);
                TimeSpan min = TimeSpan.FromMinutes(5);
                expireDelay = TimeSpan.FromMinutes(Math.Clamp(expireDelay.Value.TotalMinutes, min.TotalMinutes, max.TotalMinutes));
                expires = DateTimeOffset.UtcNow + expireDelay.Value;
            }
            await AuthenticationHttpContextExtensions.SignInAsync(context, claimsPrincipal, new AuthenticationProperties() { ExpiresUtc = expires });
            return new Session(expires, user);
        }

        /// <summary>
        /// Type which maintains a lock on an enumerable, which is released when object is disposed
        /// </summary>
        public class LockedEnumerable<T> : IEnumerable<T>, IDisposable
        {
            private IEnumerable<T> _Enumerable;
            private SemaphoreAccessDisposable _SemaphoreAccess;
            public LockedEnumerable(IEnumerable<T> enumerable, SemaphoreAccessDisposable semaphoreAccess)
            {
                _Enumerable = enumerable;
                _SemaphoreAccess = semaphoreAccess;
            }

            public void Dispose()
            {
                ((IDisposable)_SemaphoreAccess).Dispose();
                GC.SuppressFinalize(this);
            }

            public IEnumerator<T> GetEnumerator()
            {
                return _Enumerable.GetEnumerator();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return ((IEnumerable)_Enumerable).GetEnumerator();
            }
        }

        /// <summary>
        /// Acquires semaphore and returns the semaphore lock and enumerable as one <see cref="LockedEnumerable{T}"/>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="enumerable"></param>
        /// <param name="semaphore"></param>
        /// <returns></returns>
        public static async Task<LockedEnumerable<T>> AsLockedEnumerable<T>(this IEnumerable<T> enumerable, SemaphoreSlim semaphore)
        {
            var semaphoreAccess = await semaphore.WaitAsyncDisposable();
            return new LockedEnumerable<T>(enumerable, semaphoreAccess);
        }
    }
}