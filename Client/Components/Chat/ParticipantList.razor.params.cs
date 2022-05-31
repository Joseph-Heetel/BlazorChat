using CustomBlazorApp.Shared;

namespace CustomBlazorApp.Client.Components
{
    public struct ParticipantListParams
    {
        public ISet<Participation>? Participants { get; set; } = null;
        public Channel? Channel { get; set; } = null;

        public ParticipantListParams() { }
    }
}
