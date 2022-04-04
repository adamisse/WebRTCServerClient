using Signaler.Models;
using SIPSorcery.Net;

namespace Signaler
{
    public interface IPeerConnectionManager
    {
        RTCPeerConnection Get(string id);
        Task<RTCSessionDescriptionInit> CreateServerOffer(User user);
        void AddIceCandidate(string id, RTCIceCandidateInit iceCandidate);
        void SetRemoteDescription(string id, RTCSessionDescriptionInit rtcSessionDescriptionInit);
        void SetAudioRelay(User connectionId, IList<User> usersToRelay);
    }
}