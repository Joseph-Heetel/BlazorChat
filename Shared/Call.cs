using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BlazorChat.Shared
{
    /// <summary>
    /// Helper type combining call Id and Id of the initiating user. Used when notifiying the receiving user.
    /// </summary>
    public class PendingCall : ItemBase
    {
        [JsonConverter(typeof(ItemIdConverter))]
        public ItemId CallerId { get; set; }
    }

    /// <summary>
    /// Type for transferring Sdp negotiation messages
    /// </summary>
    public class NegotiationMessage
    {
        [JsonPropertyName("type")]
        public string? Type { get; set; }
        [JsonPropertyName("sdp")]
        public string? Sdp { get; set; } = null;
        [JsonPropertyName("candidate")]
        public string? Candidate { get; set; } = null;
        [JsonPropertyName("sdpMid")]
        public string? SdpMid { get; set; } = null;
        [JsonPropertyName("sdpMLineIndex")]
        public double SdpMLineIndex { get; set; } = 0;
    }

    /// <summary>
    /// Type for transferring Ice configurations (Stun and Turn servers)
    /// </summary>
    public class IceConfiguration
    {
        [JsonPropertyName("credential")]
        public string? Credential { get; set; }
        [JsonPropertyName("credentialType")]
        public string? CredentialType { get; set; }
        [JsonPropertyName("urls")]
        public string[]? Urls { get; set; }
        [JsonPropertyName("username")]
        public string? Username { get; set; }
    }
}
