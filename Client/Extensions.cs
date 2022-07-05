using BlazorChat.Shared;

namespace BlazorChat.Client
{
    public static partial class Extensions
    {
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
