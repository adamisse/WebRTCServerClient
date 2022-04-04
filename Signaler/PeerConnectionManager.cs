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


        private readonly Mixer _mixer;
        private readonly Opusenc _opusenc = new Opusenc();
        private readonly Opusdec _opudec = new Opusdec();

        private int i = 0;
        private readonly object _lock = new { };

        private ConcurrentDictionary<string, List<RTCIceCandidate>> _candidates = new();
        private ConcurrentDictionary<string, RTCPeerConnection> _peerConnections = new();

        private string LastSentPktUser { get; set; }

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

            //Task.Run(_mixer.StartAudioProcess);
        }

        public async Task<RTCSessionDescriptionInit> CreateServerOffer(string id)
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
                    var candidates = _candidates.Where(x => x.Key == id).SingleOrDefault().Value;
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
                    var candidatesList = _candidates.Where(x => x.Key == id).SingleOrDefault();
                    if (candidatesList.Value is null)
                        _candidates.TryAdd(id, new List<RTCIceCandidate> { candidate });
                    else
                        candidatesList.Value.Add(candidate);
                }
            };

            peerConnection.onconnectionstatechange += (state) =>
            {
                if (state == RTCPeerConnectionState.closed || state == RTCPeerConnectionState.disconnected || state == RTCPeerConnectionState.failed)
                {
                    _peerConnections.TryRemove(id, out _);
                }
                else if (state == RTCPeerConnectionState.connected)
                {
                    _logger.LogDebug("Peer connection connected.");
                }
            };

            var offerSdp = peerConnection.createOffer(null);
            await peerConnection.setLocalDescription(offerSdp);
            _peerConnections.TryAdd(id, peerConnection);
            return offerSdp;
        }

        void IPeerConnectionManager.SetAudioRelay(RTCPeerConnection peerConnection, User connectionId, IList<User> usersToRelay)
        {
            SetAudioRelay(peerConnection, connectionId, usersToRelay);
        }

        public static void SetAudioRelay(RTCPeerConnection peerConnection, User clientUser, IList<User> usersToRelay)
        {
            peerConnection.OnRtpPacketReceived += (rep, media, pkt) =>
            {
                //peerConnection.
                //RTPPacket rTPPacket = new RTPPacket(data.Length + num);
                //var rtpChannel = peerConnection?.GetRtpChannel(media);
                //RTCPSession rtcpSession = rt
                //IPEndPoint iPEndPoint = (mediaType == SDPMediaTypesEnum.audio || m_isMediaMultiplexed) ? AudioDestinationEndPoint : VideoDestinationEndPoint;
                //MediaStreamTrack mediaStreamTrack = (mediaType == SDPMediaTypesEnum.video) ? VideoLocalTrack : AudioLocalTrack;
                //Queue<RTPPacket> packets = new();
                //pktBytes.Enqueue(pkt.GetBytes());

                foreach (var user in usersToRelay)
                {
                    //if (user.Id == clientUser.Id)
                    //{
                    //    continue;
                    //}
                    user.PeerConnection?.SendRtpRaw(SDPMediaTypesEnum.audio, pkt.Payload, pkt.Header.Timestamp, pkt.Header.MarkerBit, pkt.Header.PayloadType);
                    //user.PeerConnection?.SendAudio(pkt.Header.Timestamp, pkt.Payload);
                    Console.WriteLine("Pacote enviado pelo usuário: " + user.Username + "com a peerconnection" + user.PeerConnection);

                    //if (String.IsNullOrEmpty(LastSentPktUser))
                    //{
                    //    LastSentPktUser = user.Id;
                    //    user.PeerConnection?.SendRtpRaw(SDPMediaTypesEnum.audio, pkt.Payload, pkt.Header.Timestamp, pkt.Header.MarkerBit, pkt.Header.PayloadType);
                    //    Console.WriteLine("Primeiro envio foi do: " + user.Username + " com a peerconnection" + user.PeerConnection);
                    //    continue;
                    //}
                    //if (user.Id == LastSentPktUser)
                    //{
                    //    Console.WriteLine($"{user.Username} deve esperar a sua vez de enviar. . .");
                    //    continue;
                    //}
                    //if (user.Id != LastSentPktUser)
                    //{
                    //    LastSentPktUser = user.Id;
                    //    user.PeerConnection?.SendRtpRaw(SDPMediaTypesEnum.audio, pkt.Payload, pkt.Header.Timestamp, pkt.Header.MarkerBit, pkt.Header.PayloadType);
                    //    Console.WriteLine("Pacote enviado pelo usuário: " + user.Username + "com a peerconnection" + user.PeerConnection);
                    //}
                    //clientUser.PeerConnection?.SendRtpRaw(SDPMediaTypesEnum.audio, pkt.Payload, pkt.Header.Timestamp, pkt.Header.MarkerBit, pkt.Header.PayloadType);
                }
                //pkt.Payload = pktBytes.Dequeue();
                //user.PeerConnection.SendAudio(pkt.Header.Timestamp, pkt.Payload);

            };

            peerConnection.OnReceiveReport += (report, media, pkt) =>
            {
                foreach (var user in usersToRelay)
                {
                    if (user.Id == clientUser.Id)
                    {
                        continue;
                    }

                    user.PeerConnection?.SendRtcpFeedback(SDPMediaTypesEnum.audio, new RTCPFeedback(pkt.GetBytes()));
                    Console.WriteLine($"Feedback - USER {user.Username}");
                }
            };

            //_mixer.HasAudioData += (object e, TesteEventArgs args) =>
            //{
            //    peerConnection.SendRtpRaw(SDPMediaTypesEnum.audio, args.bytes, args.Timestamp, 0, 0);
            //};
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

        private RTPSession CreateRtpSession(List<SDPAudioVideoMediaFormat> audioFormats)
        {
            var rtpSession = new RTPSession(true, false, false, IPAddress.Loopback);
            bool hasAudio = false;

            if (audioFormats != null && audioFormats.Count > 0)
            {
                var audioTrack = new MediaStreamTrack(SDPMediaTypesEnum.audio, false, audioFormats, MediaStreamStatusEnum.SendRecv);
                rtpSession.addTrack(audioTrack);
                hasAudio = true;
            }

            var sdpOffer = rtpSession.CreateOffer(null);

            // Because the SDP being written to the file is the input to ffplay the connection ports need to be changed
            // to the ones ffplay will be listening on.
            if (hasAudio)
            {
                sdpOffer.Media.Single(x => x.Media == SDPMediaTypesEnum.audio).Port = 8082;
            }

            rtpSession.Start();
            rtpSession.SetDestination(SDPMediaTypesEnum.audio, new IPEndPoint(IPAddress.Loopback, 8082), new IPEndPoint(IPAddress.Loopback, 8083));

            return rtpSession;
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




//_opudec.DecodeRawAudio(pkt.Payload);
//_opusenc.EncodeRawAudioPacket(pkt.Payload);

//    //var encoder = OpusEncoder.Create(48000, 1, Concentus.Enums.OpusApplication.OPUS_APPLICATION_VOIP);
//    //encoder.EnableAnalysis = true;
//    //encoder.Bitrate = (64 * 1024);
//    //encoder.Complexity = 5;
//    //encoder.PacketLossPercent = 0;
//    //encoder.UseInbandFEC = true;
//    //encoder.UseVBR = false;
//    //encoder.UseConstrainedVBR = false;

//    //var outStream = new MemoryStream();
//    //var oggin = new OpusOggWriteStream(encoder, outStream);
//    //lock(_lock)
//    //{
//    //    var audioStream = new MemoryStream(pkt.Payload, 28, pkt.Payload.Length - 28, true, true);
//    //    using var fl = File.Create(@$"C:\temp\{i}.opus");
//    //    fl.Write(audioStream.GetBuffer(), 0, (int)audioStream.Length);
//    //    fl.Close();
//    //    i++;
//    //}

//    //var outStream = new MemoryStream();
//    //var mediaAnalisys = FFProbe.Analyse(audioStream, int.MaxValue, options);
//    //audioStream.Position = 0;

//    //FFMpegArguments
//    //    .FromPipeInput(new StreamPipeSource(audioStream), options =>
//    //    {
//    //        //options.WithAudioCodec("OPUS");
//    //        //options.WithDuration(mediaAnalisys.Duration);
//    //    })
//    //    //.AddPipeInput(new StreamPipeSource(audioStream2), options =>
//    //    //{
//    //    //    options.WithDuration(durationAudio2);
//    //    //})
//    //    .OutputToPipe(new StreamPipeSink(outStream), options =>
//    //    {
//    //        options.ForceFormat("mp3");
//    //        //options.WithCustomArgument(@"-filter_complex amerge=inputs=2 -ac 2");
//    //    })
//    //    .NotifyOnOutput((str, dt) =>
//    //    {
//    //        _logger.LogInformation(str);
//    //    })
//    //    .ProcessSynchronously(true, options);

//    //using (var fileIn = new MemoryStream(pkt.Payload))
//    //using (var pcmStream = new MemoryStream())
//    //{
//    //var decoder = opusdecoder.create(48000, 1);
//    //var oggin = new opusoggreadstream(decoder, filein);

//    //    while (oggIn.HasNextPacket)
//    //    {
//    //        short[] packet = oggIn.DecodeNextPacket();
//    //        if (packet != null)
//    //        {
//    //            for (int i = 0; i < packet.Length; i++)
//    //            {
//    //                var bytes = BitConverter.GetBytes(packet[i]);
//    //                pcmStream.Write(bytes, 0, bytes.Length);
//    //            }
//    //        }
//    //    }

//    //    pcmStream.Position = 0;

//    //    var wavStream = new RawSourceWaveStream(pcmStream, new WaveFormat(48000, 1));
//    //    var sampleProvider = wavStream.ToSampleProvider();
//    //    WaveFileWriter.CreateWaveFile16(Path.Combine(Directory.GetCurrentDirectory(), "AAAAAAAAAAAAAA.wav"), sampleProvider);
//    //}