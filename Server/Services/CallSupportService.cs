﻿using BlazorChat.Server.Hubs;
using BlazorChat.Shared;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Text.Json;

namespace BlazorChat.Server.Services
{
    public interface ICallSupportService
    {
        public Task<ItemId> InitiateCall(ItemId callerId, ItemId calleeId);
        public Task<IList<PendingCall>> GetPendingCalls(ItemId userId);
        public Task TerminateCall(ItemId callId);
        public Task<bool> IsOngoingCall(ItemId callId);
        public Task<bool> IsInCall(ItemId callId, ItemId userId, bool checkpending = true, bool checkongoing = true);
        public Task ElevateToOngoing(ItemId callId);
        public Task<IceConfiguration[]> GetIceConfigurations();
    }

    public class PendingCallModel : CallModel
    {
        public DateTimeOffset Initiated { get; set; }
        public DateTimeOffset Expires { get => Initiated + TimeSpan.FromMinutes(5); }
    }

    public class CallModel : ItemBase
    {
        public ItemId CallerId { get; set; }
        public ItemId CalleeId { get; set; }
    }

    public class CallSupportService : ICallSupportService
    {
        private readonly SemaphoreSlim _pendingListSemaphore = new SemaphoreSlim(1);
        private readonly SemaphoreSlim _ongoingListSemaphore = new SemaphoreSlim(1);

        private readonly Dictionary<ItemId, PendingCallModel> _pendingCalls = new Dictionary<ItemId, PendingCallModel>();
        private readonly Dictionary<ItemId, CallModel> _ongoingCalls = new Dictionary<ItemId, CallModel>();

        private readonly IIdGeneratorService _idGenService;
        private readonly IHubContext<ChatHub> _HubContext;

        private IceConfiguration[]? _iceConfigs;

        public CallSupportService(IIdGeneratorService idgen, IHubContext<ChatHub> hub)
        {
            _idGenService = idgen;
            _HubContext = hub;
            _ = Task.Run(checkExpirations);
        }

        public async Task<IList<PendingCall>> GetPendingCalls(ItemId userId)
        {
            List<PendingCall> result = new List<PendingCall>();
            using (var disposablelock = await _pendingListSemaphore.WaitAsyncDisposable())
            {
                foreach (var call in _pendingCalls.Values)
                {
                    if (call.CalleeId == userId)
                    {
                        result.Add(new PendingCall() { Id = call.Id, CallerId = call.CallerId });
                    }
                }
            }
            return result;
        }

        public async Task<ItemId> InitiateCall(ItemId callerId, ItemId calleeId)
        {
            PendingCallModel call = new PendingCallModel()
            {
                Id = _idGenService.Generate(),
                CallerId = callerId,
                CalleeId = calleeId,
                Initiated = DateTimeOffset.UtcNow
            };
            using (var disposablelock = await _pendingListSemaphore.WaitAsyncDisposable())
            {
                _pendingCalls.Add(call.Id, call);
            }
            await _HubContext.Clients.Clients(await getCalleeConnections(call)).SendAsync(SignalRConstants.CALLS_PENDINGCALLSLISTCHANGED);
            return call.Id;
        }

        public async Task<bool> IsInCall(ItemId callId, ItemId userId, bool checkpending = true, bool checkongoing = true)
        {
            bool result = false;
            if (checkpending)
            {
                using (var disposablelock = await _pendingListSemaphore.WaitAsyncDisposable())
                {
                    if (_pendingCalls.TryGetValue(callId, out PendingCallModel? callmodel))
                    {
                        result = result || callmodel.CalleeId == userId || callmodel.CallerId == userId;
                    }
                }
            }
            if (checkongoing && !result)
            {
                using (var disposablelock = await _ongoingListSemaphore.WaitAsyncDisposable())
                {
                    if (_ongoingCalls.TryGetValue(callId, out CallModel? callmodel))
                    {
                        result = result || callmodel.CalleeId == userId || callmodel.CallerId == userId;
                    }
                }
            }
            return result;
        }

        public async Task<bool> IsOngoingCall(ItemId callId)
        {
            bool result = false;
            using (var disposablelock = await _ongoingListSemaphore.WaitAsyncDisposable())
            {
                result = _ongoingCalls.ContainsKey(callId);
            }
            return result;
        }

        public async Task TerminateCall(ItemId callId)
        {
            using (var disposablelock = await _pendingListSemaphore.WaitAsyncDisposable())
            {
                if (_pendingCalls.TryGetValue(callId, out PendingCallModel? callModel))
                {
                    _pendingCalls.Remove(callId);
                    await _HubContext.Clients.Clients(await getParticipantConnections(callModel)).SendAsync(SignalRConstants.CALL_TERMINATED, callId);
                    return;
                }

            }
            using (var disposablelock = await _ongoingListSemaphore.WaitAsyncDisposable())
            {
                if (_ongoingCalls.TryGetValue(callId, out CallModel? callModel))
                {
                    _ongoingCalls.Remove(callId);
                    await _HubContext.Clients.Clients(await getParticipantConnections(callModel)).SendAsync(SignalRConstants.CALL_TERMINATED, callId);
                    return;
                }
            }
        }
        public async Task ElevateToOngoing(ItemId callId)
        {
            PendingCallModel? callModel = null;
            using (var disposablelock = await _pendingListSemaphore.WaitAsyncDisposable())
            {
                _pendingCalls.TryGetValue(callId, out callModel);
                _pendingCalls.Remove(callId);

            }
            if (callModel != null)
            {
                await _HubContext.Clients.Clients(await getCalleeConnections(callModel)).SendAsync(SignalRConstants.CALLS_PENDINGCALLSLISTCHANGED);
                using (var disposablelock = await _ongoingListSemaphore.WaitAsyncDisposable())
                {
                    _ongoingCalls.Add(callId, callModel);
                }
            }
        }

        private static async Task<List<string>> getParticipantConnections(CallModel callModel)
        {
            List<string> connections = new List<string>();
            using (var userConnections = await ConnectionMap.Users.GetConnections(callModel.CalleeId))
            {
                if (userConnections != null)
                {
                    connections.AddRange(userConnections);
                }
            }
            using (var userConnections = await ConnectionMap.Users.GetConnections(callModel.CallerId))
            {
                if (userConnections != null)
                {
                    connections.AddRange(userConnections);
                }
            }
            return connections;
        }

        private static async Task<List<string>> getCalleeConnections(CallModel callModel)
        {
            List<string> connections = new List<string>();
            using (var userConnections = await ConnectionMap.Users.GetConnections(callModel.CalleeId))
            {
                if (userConnections != null)
                {
                    connections.AddRange(userConnections);
                }
            }
            return connections;
        }

        private static async Task<List<string>> getCallerConnections(CallModel callModel)
        {
            List<string> connections = new List<string>();
            using (var userConnections = await ConnectionMap.Users.GetConnections(callModel.CallerId))
            {
                if (userConnections != null)
                {
                    connections.AddRange(userConnections);
                }
            }
            return connections;
        }

        private async Task checkExpirations()
        {
            // Leaving this thread in a perpetual loop is a potential memory and resource leak. However not a problem, since 
            // CallSupportService is instantiated once and stays alive for the entire runtime of the server
            // (added as singleton to the servers servicecollection)
            while (true)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                using (var disposableLock = await _pendingListSemaphore.WaitAsyncDisposable())
                {
                    List<ItemId> toremove = new List<ItemId>();
                    foreach (var call in _pendingCalls.Values)
                    {
                        if (call.Expires < DateTimeOffset.UtcNow)
                        {
                            toremove.Add(call.Id);
                        }
                    }
                    foreach (var call in toremove)
                    {
                        _pendingCalls.Remove(call);
                    }
                }
            }
        }

        public Task<IceConfiguration[]> GetIceConfigurations()
        {
            if (_iceConfigs != null)
            {
                return Task.FromResult(_iceConfigs);
            }
            string? raw = Environment.GetEnvironmentVariable(EnvironmentVarKeys.ICECONFIGURATIONS);
            if (!string.IsNullOrEmpty(raw))
            {
                _iceConfigs = JsonSerializer.Deserialize<IceConfiguration[]>(raw);
            }
            _iceConfigs ??= Array.Empty<IceConfiguration>();
            return Task.FromResult(_iceConfigs);
        }
    }
}
