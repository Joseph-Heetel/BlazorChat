using BlazorChat.Shared;

namespace BlazorChat.Client
{
    public delegate void ItemChanged<T>(T value);

    /// <summary>
    /// A readonly observable value. Access to current <see cref="State"/> and <see cref="StateChanged"/> event
    /// </summary>
    /// <typeparam name="T">Type of observed value</typeparam>
    public interface IReadOnlyObservable<T>
    {
        /// <summary>
        /// Gets current value
        /// </summary>
        public T State { get; }
        /// <summary>
        /// Count observers
        /// </summary>
        public int Count { get; }
        /// <summary>
        /// Event invoked whenever <see cref="State"/> changes
        /// </summary>
        public event ItemChanged<T>? StateChanged;
    }

    /// <summary>
    /// An observable value. Maintains an internal value, notifying all observers on change.
    /// </summary>
    /// <typeparam name="T">Type of observed value</typeparam>
    public interface IObservable<T> : IReadOnlyObservable<T>
    {
        /// <summary>
        /// Gets current value. On set, <see cref="StateChanged"/> is invoked if the value is different than previous
        /// </summary>
        public new T State { get; set; }
        /// <summary>
        /// Manually trigger <see cref="StateChanged"/> (for example when altering member variables of <see cref="State"/>)
        /// </summary>
        public void TriggerChange();
        /// <summary>
        /// Manually trigger <see cref="StateChanged"/> (for example when altering member variables of <see cref="State"/>)
        /// </summary>
        public void TriggerChange(T newState);
    }

    /// <summary>
    /// Helper class wrapping a value state with an automatic change event
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class Observable<T> : IObservable<T>
    {
        private T _state;

        public T State
        {
            get
            {
                return _state;
            }
            set
            {
                if ((_state == null && value == null)
                    || (_state != null && _state.Equals(value)))
                {
                    return; // No need to invoke state changed if the value hasn't changed
                }
                _state = value;
                StateChanged?.Invoke(_state);
            }
        }
        public int Count
        {
            get
            {
                if (StateChanged == null)
                {
                    return 0;
                }
                return StateChanged.GetInvocationList().Length;
            }
        }

        public event ItemChanged<T>? StateChanged;
        public void TriggerChange() { StateChanged?.Invoke(_state); }
        public void TriggerChange(T newState) { _state = newState; StateChanged?.Invoke(_state); }
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="initial">Initial value assigned to <see cref="State"/></param>
        public Observable(T initial)
        {
            _state = initial;
        }
    }
}
