using FFMpegCore;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Signaler.Hubs;
using Signaler.Models;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using System.Collections.Concurrent;
using System.Net;

namespace Signaler
{
    /*
       Instantiate the RTCPeerConnection instance,
       Add the audio and/or video tracks as required,
       Call the createOffer method to acquire an SDP offer that can be sent to the remote peer,

       Send the SDP offer and get the SDP answer from the remote peer (this exchange is not part of the WebRTC specification and can be done using any signalling layer, examples are SIP, web sockets etc),
       Once the SDP exchange has occurred the ICE checks can start in order to establish the optimal network path between the two peers. ICE candidates typically need to be passed between peers using the signalling layer,
       Once ICE has established a the DTLS handshake will occur,,
       If the DTLS handshake is successful the keying material it produces is used to initialise the SRTP contexts,
       After the SRTP contexts are initialised the RTP media and RTCP packets can be exchanged in the normal manner.
     */

    public class PeerConnectionManager : IPeerConnectionManager
    {
        private readonly IHubContext<WebRTCHub> _webRTCHub;
        private readonly ILogger<PeerConnectionManager> _logger;

        private readonly Mixer _mixer = new();

        private ConcurrentDictionary<string, List<RTCIceCandidate>> _candidates = new();
        private ConcurrentDictionary<string, RTCPeerConnection> _peerConnections = new();

        private static RTCConfiguration _config = new()
        {
            X_UseRtpFeedbackProfile = true,
            iceServers = new List<RTCIceServer>
            {
                new RTCIceServer
                {
                    urls = "stun:stun1.l.google.com:19302"
                },
                new RTCIceServer
                {
                    username = "webrtc",
                    credential = "webrtc",
                    credentialType = RTCIceCredentialType.password,
                    urls = "turn:turn.anyfirewall.com:443?transport=tcp"
                },
            }
        };

        private static FFOptions options = new()
        {
            UseCache = true,
            TemporaryFilesFolder = @"C:\temp",
            BinaryFolder = @"C:\ProgramData\chocolatey\lib\ffmpeg\tools\ffmpeg\bin",
        };

        public PeerConnectionManager(ILogger<PeerConnectionManager> logger, IHubContext<WebRTCHub> webRTCHub, Mixer mixer)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _webRTCHub = webRTCHub ?? throw new ArgumentNullException(nameof(webRTCHub));
            _peerConnections ??= new ConcurrentDictionary<string, RTCPeerConnection>();
            _mixer = mixer;

            Task.Run(_mixer.StartAudioProcess);
        }

        public async Task<RTCSessionDescriptionInit> CreateServerOffer(User user)
        {
            var peerConnection = new RTCPeerConnection(_config);

            MediaStreamTrack audioTrack = new MediaStreamTrack
                (SDPMediaTypesEnum.audio,
                false,
            new List<SDPAudioVideoMediaFormat> { new SDPAudioVideoMediaFormat(SDPWellKnownMediaFormatsEnum.PCMU) }, MediaStreamStatusEnum.SendRecv);

            audioTrack.MaximumBandwidth = uint.MaxValue;

            //MediaStreamTrack audioTrack = new MediaStreamTrack(audioSource.GetAudioSourceFormats(), MediaStreamStatusEnum.SendRecv);

            peerConnection.addTrack(audioTrack);

            peerConnection.onicegatheringstatechange += (RTCIceGatheringState obj) =>
            {
                if (peerConnection.signalingState == RTCSignalingState.have_local_offer ||
                    peerConnection.signalingState == RTCSignalingState.have_remote_offer)
                {
                    var candidates = _candidates.Where(x => x.Key == user.Id).SingleOrDefault().Value;
                    foreach (var candidate in candidates)
                    {
                        _webRTCHub.Clients.All.SendAsync("IceCandidateResult", candidate).GetAwaiter().GetResult();
                    }
                }
            };

            peerConnection.onicecandidate += (candidate) =>
            {
                if (peerConnection.signalingState == RTCSignalingState.have_local_offer ||
                    peerConnection.signalingState == RTCSignalingState.have_remote_offer)
                {
                    var candidatesList = _candidates.Where(x => x.Key == user.Id).SingleOrDefault();
                    if (candidatesList.Value is null)
                        _candidates.TryAdd(user.Id, new List<RTCIceCandidate> { candidate });
                    else
                        candidatesList.Value.Add(candidate);
                }
            };

            peerConnection.onconnectionstatechange += (state) =>
            {
                if (state == RTCPeerConnectionState.closed || state == RTCPeerConnectionState.disconnected || state == RTCPeerConnectionState.failed)
                {
                    _peerConnections.TryRemove(user.Id, out _);
                }
                else if (state == RTCPeerConnectionState.connected)
                {
                    _logger.LogDebug("Peer connection connected.");
                }
            };

            GenerateRtcLogs(peerConnection, user);

            var offerSdp = peerConnection.createOffer(null);
            await peerConnection.setLocalDescription(offerSdp);
            _peerConnections.TryAdd(user.Id, peerConnection);
            _mixer.AddUsersToRelay(user);
            return offerSdp;
        }

        public void GenerateRtcLogs(RTCPeerConnection pc, User user)
        {
            pc.OnReceiveReport += (re, media, rr) =>
            {
                Console.WriteLine($"{user.Username}: RTCP Receive for {media} from {re}\n{rr.GetDebugSummary()}");
            };

            pc.OnSendReport += (media, sr) => Console.WriteLine($"{user.Username}: RTCP Send for {media}\n{sr.GetDebugSummary()}");
            pc.GetRtpChannel().OnStunMessageReceived += (msg, ep, isRelay) => Console.WriteLine($"{user.Username}: STUN {msg.Header.MessageType} received from {ep}.");
            pc.oniceconnectionstatechange += (state) => Console.WriteLine($"{user.Username}: ICE connection state change to {state}.");
        }

        public void SetAudioRelay(User user, IList<User> usersToRelay)
        {
            user.PeerConnection.OnRtpPacketReceived += (rep, media, pkt) =>
            {
                if (media == SDPMediaTypesEnum.audio)
                {
                    var conns = _peerConnections.Where(p => p.Key != user.PeerConnection.SessionID).Select(s => s.Value);
                    foreach (var pc in conns)
                    {
                        if (pc.localDescription.sdp.ToString() == user.PeerConnection.localDescription.sdp.ToString())
                        {
                            continue;
                        }
                        else
                        {
                            pc.SendRtpRaw(SDPMediaTypesEnum.audio, pkt.Payload, pkt.Header.Timestamp, pkt.Header.MarkerBit, pkt.Header.PayloadType);
                        }
                    }
                }
            };
        }

        public void SetRemoteDescription(string id, RTCSessionDescriptionInit rtcSessionDescriptionInit)
        {
            if (!_peerConnections.TryGetValue(id, out var pc)) return;
            pc.setRemoteDescription(rtcSessionDescriptionInit);
        }

        public void AddIceCandidate(string id, RTCIceCandidateInit iceCandidate)
        {
            if (!_peerConnections.TryGetValue(id, out var pc)) return;
            pc.addIceCandidate(iceCandidate);
        }

        public RTCPeerConnection Get(string id)
        {
            var pc = _peerConnections.Where(p => p.Key == id).SingleOrDefault();
            if (pc.Value != null) return pc.Value;
            return null;
        }
    }

    public static class StreamExtension
    {
        public static byte[] ToArray(this Stream stream)
        {
            byte[] buffer = new byte[4096];
            int reader = 0;
            MemoryStream memoryStream = new MemoryStream();
            while ((reader = stream.Read(buffer, 0, buffer.Length)) != 0)
                memoryStream.Write(buffer, 0, reader);
            return memoryStream.ToArray();
        }
    }
}