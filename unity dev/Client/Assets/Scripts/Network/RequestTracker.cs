using System;
using System.Collections.Generic;
using UnityEngine;

namespace Cabo.Client.Network
{
    /// <summary>
    /// Tracks in-flight requests by request_id. Supports timeout.
    /// Used by ProtoGateway to match responses to pending requests.
    /// </summary>
    public sealed class RequestTracker
    {
        private const float DefaultTimeoutSeconds = 5f;

        private struct PendingRequest
        {
            public long RequestId;
            public float SentTime;
            public Action OnResponse;
            public Action<string> OnTimeout;
        }

        private readonly Dictionary<long, PendingRequest> pending = new Dictionary<long, PendingRequest>();
        private readonly List<long> toRemove = new List<long>();

        /// <summary>
        /// Register a new pending request.
        /// </summary>
        public void Register(long requestId, Action onResponse, Action<string> onTimeout)
        {
            pending[requestId] = new PendingRequest
            {
                RequestId = requestId,
                SentTime = Time.time,
                OnResponse = onResponse,
                OnTimeout = onTimeout
            };
        }

        /// <summary>
        /// Resolve a pending request. Returns true if found.
        /// </summary>
        public bool Resolve(long requestId)
        {
            if (pending.TryGetValue(requestId, out var req))
            {
                pending.Remove(requestId);
                req.OnResponse?.Invoke();
                return true;
            }
            return false;
        }

        /// <summary>
        /// Call periodically (e.g., from MonoBehaviour.Update or a coroutine)
        /// to timeout stale requests.
        /// </summary>
        public void Tick(float currentTime)
        {
            toRemove.Clear();
            foreach (var kv in pending)
            {
                if (currentTime - kv.Value.SentTime > DefaultTimeoutSeconds)
                {
                    kv.Value.OnTimeout?.Invoke($"Request {kv.Key} timed out after {DefaultTimeoutSeconds}s");
                    toRemove.Add(kv.Key);
                }
            }
            foreach (var id in toRemove)
                pending.Remove(id);
        }

        public void Clear()
        {
            foreach (var kv in pending)
                kv.Value.OnTimeout?.Invoke("Connection lost");
            pending.Clear();
        }

        public int PendingCount => pending.Count;
    }
}
