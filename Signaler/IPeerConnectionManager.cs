using Signaler.Models;
using SIPSorcery.Net;

namespace Signaler
{
    public interface IPeerConnectionManager
    {
        RTCPeerConnection Get(string id);
        Task<RTCSessionDescriptionInit> CreateServerOffer(string id);
        void AddIceCandidate(string id, RTCIceCandidateInit iceCandidate);
        void SetRemoteDescription(string id, RTCSessionDescriptionInit rtcSessionDescriptionInit);
        void SetAudioRelay(RTCPeerConnection peerConnection, User connectionId, IList<User> usersToRelay);
    }
}