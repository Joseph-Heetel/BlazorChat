using BlazorChat.Shared;
using Microsoft.AspNetCore.Components.Forms;

namespace BlazorChat.Client.Services
{
    /// <summary>
    /// Represents the state of a queued message dispatch operation
    /// </summary>
    public enum EMessageDispatchState
    {
        /// <summary>
        /// The dispatch operation has not yet been processed
        /// </summary>
        Initial,
        /// <summary>
        /// The message was successfully delivered, and the process dequeued.
        /// </summary>
        Delivered,
        /// <summary>
        /// The message delivery failed due to network errors, and was queued.
        /// </summary>
        Queued,
        /// <summary>
        /// The message delivery failed with HTTP error code, and was dequeued (check Result structs)
        /// </summary>
        Failure
    }
    public interface IMessageDispatchState
    {
        /// <summary>
        /// State of the dispatch operation
        /// </summary>
        public EMessageDispatchState State { get; }
        /// <summary>
        /// ApiResult describing file upload process result, if the message dispatch includes a file to be uploaded and upload had been started
        /// </summary>
        public ApiResult<FileAttachment>? UploadFileResult { get; }
        /// <summary>
        /// ApiResult describing message send process result, if it has been started already.
        /// </summary>
        public ApiResult<Message>? SendMessageResult { get; }
        /// <summary>
        /// Awaitable task for when the state changes
        /// </summary>
        public Task<EMessageDispatchState> Task { get; }
    }

    internal class MessageDispatchProcess : IMessageDispatchState
    {
        /// <summary>
        /// The <see cref="IMessageDispatchState.Task"/> task is manually resolved through this
        /// </summary>
        public TaskCompletionSource<EMessageDispatchState> Tcs { get; set; }

        public EMessageDispatchState State { get; set; }

        public ApiResult<FileAttachment>? UploadFileResult { get; set; }

        public ApiResult<Message>? SendMessageResult { get; set; }

        /// <summary>
        /// File that is to be uploaded
        /// </summary>
        public IBrowserFile? File { get; set; }
        /// <summary>
        /// Message body
        /// </summary>
        public string? Body { get; set; }
        /// <summary>
        /// Channel to send to
        /// </summary>
        public ItemId ChannelId { get; set; }

        public Task<EMessageDispatchState> Task { get; set; }

        public MessageDispatchProcess()
        {
            Tcs = new TaskCompletionSource<EMessageDispatchState>();
            Task = Tcs.Task;
        }
    }

    /// <summary>
    /// Service for asynchronously posting messages without losing order. Internally queues messages until they have succeeded sending.
    /// </summary>
    public interface IMessageDispatchService
    {
        /// <summary>
        /// Queues (and immediately attempts to post) a message
        /// </summary>
        /// <param name="channelId"></param>
        /// <param name="body"></param>
        /// <param name="file"></param>
        /// <returns></returns>
        public IMessageDispatchState Postmessage(ItemId channelId, string? body = null, IBrowserFile? file = null);
        /// <summary>
        /// Count of messages currently queued
        /// </summary>
        public IReadOnlyObservable<int> Count { get; }
        /// <summary>
        /// Collection of currently queued messages
        /// </summary>
        public IReadOnlyObservable<IReadOnlyCollection<IMessageDispatchState>> ActiveMessageDispatches { get; }
    }

    public class MessageDispatchService : IMessageDispatchService, IDisposable
    {
        /// <summary>
        /// CTS for when the service is disposed, allowing the looping task to stop
        /// </summary>
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        /// <summary>
        /// "Semaphore" for accessing the sender function. Blazor WASM is guaranteed completely thread safe, 
        /// but asynchronous tasks could cause multiple send processes to execute "simultaneously".
        /// </summary>
        private bool _isProcessingMessages = false;

        private readonly ILocalCacheService _cacheService;
        private readonly IChatApiService _apiService;
        private readonly IChatHubService _hubService;

        /// <summary>
        /// The internal queue for all messages that require posting
        /// </summary>
        private readonly Queue<MessageDispatchProcess> _dispatchProcesses = new Queue<MessageDispatchProcess>();

        public MessageDispatchService(ILocalCacheService cacheService, IChatApiService apiService, IChatHubService hubService)
        {
            _cacheService = cacheService;
            _apiService = apiService;
            _hubService = hubService;
            _ = Task.Run(LoopProcessQueue);
        }

        private readonly Observable<int> _count = new Observable<int>(0);
        private readonly Observable<IReadOnlyCollection<IMessageDispatchState>> _activeMessageDispatches = new Observable<IReadOnlyCollection<IMessageDispatchState>>(new List<IMessageDispatchState>());
        public IReadOnlyObservable<int> Count => _count;

        public IReadOnlyObservable<IReadOnlyCollection<IMessageDispatchState>> ActiveMessageDispatches => _activeMessageDispatches;

        public void Dispose()
        {
            _cts.Cancel();
            _cts.Dispose();
        }

        public IMessageDispatchState Postmessage(ItemId channelId, string? body, IBrowserFile? file)
        {
            MessageDispatchProcess dispatchProcess = new MessageDispatchProcess()
            {
                Body = body,
                File = file,
                ChannelId = channelId,
                State = EMessageDispatchState.Initial
            };
            _dispatchProcesses.Enqueue(dispatchProcess);
            UpdateState();
            // Immediately trigger a queue processing task
            _ = Task.Run(ProcessQueue);
            return dispatchProcess;
        }

        /// <summary>
        /// Continously attempts to clear the queue
        /// </summary>
        /// <returns></returns>
        private async Task LoopProcessQueue()
        {
            while (!_cts.IsCancellationRequested)
            {
                await ProcessQueue();
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        /// <summary>
        /// Attempts to clear the queue. Bails out when a network exception occurs (aka no internet connection)
        /// </summary>
        private async Task ProcessQueue()
        {
            // Simple dumb semaphore
            if (_isProcessingMessages || !_hubService.Connected.State)
            {
                return;
            }
            _isProcessingMessages = true;

            while (_dispatchProcesses.Count > 0 && !_cts.IsCancellationRequested)
            {
                MessageDispatchProcess current = _dispatchProcesses.Peek();

                // If there is a file that needs to be uploaded:
                if (current.File != null && current.UploadFileResult == null)
                {
                    // Upload file
                    ApiResult<FileAttachment> upload = await _apiService.UploadFile(current.ChannelId, current.File);
                    current.UploadFileResult = upload;

                    // Process upload result
                    if (upload.StatusCode == EApiStatusCode.NetException)
                    {
                        // Internet connection has failed, stop processing messages
                        current.State = EMessageDispatchState.Queued;
                        current.Tcs.SetResult(current.State);
                        current.Tcs = new TaskCompletionSource<EMessageDispatchState>();
                        current.Task = current.Tcs.Task;
                        break;
                    }
                    else if (!upload.IsSuccess)
                    {
                        // A different failure has occured (lack of authorization, malformed input, ...)
                        // Set the dispatch process as failure, continue on
                        current.State = EMessageDispatchState.Failure;
                        current.Tcs.SetResult(current.State);
                    }
                }
                // Only attempt sending message if a previous task (file upload) hasn't failed
                if (current.State == EMessageDispatchState.Initial || current.State == EMessageDispatchState.Queued)
                {
                    // Send the message
                    FileAttachment? attachment = current.UploadFileResult?.Result;
                    ApiResult<Message> send = await _apiService.CreateMessage(current.ChannelId, current.Body ?? string.Empty, attachment);
                    current.SendMessageResult = send;

                    // Process the message creation result
                    if (send.StatusCode == EApiStatusCode.NetException)
                    {
                        // Internet connection has failed, stop processing messages
                        current.State = EMessageDispatchState.Queued;
                        current.Tcs.SetResult(current.State);
                        current.Tcs = new TaskCompletionSource<EMessageDispatchState>();
                        current.Task = current.Tcs.Task;
                        break;
                    }
                    else
                    {
                        if (send.IsSuccess)
                        {
                            // Update the process with success
                            current.State = EMessageDispatchState.Delivered;
                            current.Tcs.SetResult(current.State);
                        }
                        else
                        {
                            // A different failure has occured (lack of authorization, malformed input, ...)
                            // Set the dispatch process as failure, continue on
                            current.State = EMessageDispatchState.Failure;
                            current.Tcs.SetResult(current.State);
                        }
                    }
                }
                _ = _dispatchProcesses.Dequeue();
                // Update UI state
                UpdateState();
            }
            _isProcessingMessages = false;
        }

        /// <summary>
        /// Updates the observables
        /// </summary>
        private void UpdateState()
        {
            _count.State = _dispatchProcesses.Count;
            _activeMessageDispatches.TriggerChange(_dispatchProcesses);
        }
    }
}
