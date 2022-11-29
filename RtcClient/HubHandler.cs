using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using SIPSorcery.Media;
using SIPSorcery.Net;

namespace asdfkjkjansv
{
    public class RtcClientInitializer
    {
        private static async Task Main()
        {
            var hub1 = new HubHandler();
            await hub1.StartConnection();

            await hub1.CreateUser("user1");
            await hub1.CreateRoom("teste");
            await hub1.JoinRoom("teste", true);

            //var hub2 = new HubHandler();
            //await hub2.StartConnection();

            //await hub2.CreateUser("user2");
            //await hub2.JoinRoom("teste", false);

            while (true)
            {
                await Task.Delay(int.MaxValue);
            }

            //criar a peerconnection passando as configs e a track
            //fazer o get offer do server

            //setar a answer passando a descrição remota
            //retornar a local peerconnection description passando a answer
            //setar a local description pelo hub passando o sdp recebido
            //offer e answer setadas -> basta trafegar os dados
        }
    }

    public class HubHandler : Hub
    {
        public HubConnection HubConnection { get; set; }
        public RTCPeerConnection PeerConnection { get; set; }
        public List<RTCIceCandidateInit> IceCandidates { get; set; }

        private static RTCConfiguration _Config = new()
        {
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

        public HubHandler()
        {
            HubConnection = new HubConnectionBuilder()
            .WithUrl("https://localhost:7237/rtc")
            .WithAutomaticReconnect()
            .Build();
            HubConnection.ServerTimeout = TimeSpan.FromSeconds(300);
            IceCandidates = new List<RTCIceCandidateInit>();
        }

        public async Task StartConnection()
        {
            try
            {
                await HubConnection.StartAsync();
                //Console.WriteLine("Hub Connection Started, ID:" + id);

                var iceInit = new RTCIceCandidateInit();
                IceCandidates.Append(iceInit);

                Console.WriteLine("Local icecandidate initialized");
            }
            catch (Exception ex)
            {
                throw new Exception("Error", ex);
            }
        }

        public async Task CreateUser(string username)
        {
            try
            {
                await HubConnection.InvokeAsync("CreateUser", username);
                Console.WriteLine("User " + username + "created");
            }
            catch (Exception ex)
            {
                throw new Exception("Error", ex);
            }
        }

        public async Task CreateRoom(string roomName)
        {
            try
            {
                await HubConnection.InvokeAsync("CreateRoom", roomName);
                Console.WriteLine("Room " + roomName + "created");
            }
            catch (Exception ex)
            {
                throw new Exception("Error", ex);
            }
        }

        public async Task<RTCPeerConnection> CreateRTCPeerConnection(bool isMusic)
        {
            var pc = new RTCPeerConnection(_Config);

            var audioSource = new AudioExtrasSource(new AudioEncoder(), new AudioSourceOptions { AudioSource = isMusic ? AudioSourcesEnum.Music : AudioSourcesEnum.WhiteNoise/*, MusicFile = audioSamplePath*/ });
            MediaStreamTrack audioTrack = new MediaStreamTrack(audioSource.GetAudioSourceFormats(), MediaStreamStatusEnum.SendRecv);
            pc.addTrack(audioTrack);
            audioSource.OnAudioSourceEncodedSample += pc.SendAudio;

            pc.OnAudioFormatsNegotiated += (audioFormats) => audioSource.SetAudioSourceFormat(audioFormats.First());

            pc.onconnectionstatechange += async (state) =>
            {
                Console.WriteLine($"Peer connection state change to {state}.");

                if (state == RTCPeerConnectionState.connected)
                {
                    await audioSource.StartAudio();
                }
                else if (state == RTCPeerConnectionState.failed)
                {
                    pc.Close("ice disconnection");
                }
                else if (state == RTCPeerConnectionState.closed)
                {
                    await audioSource.CloseAudio();
                }
            };

            pc.OnReceiveReport += (re, media, rr) =>
            {
                Console.WriteLine($"RTCP Receive for {media} from {re}\n{rr.GetDebugSummary()}");
            };
            pc.OnSendReport += (media, sr) => Console.WriteLine($"RTCP Send for {media}\n{sr.GetDebugSummary()}");
            pc.GetRtpChannel().OnStunMessageReceived += (msg, ep, isRelay) => Console.WriteLine($"STUN {msg.Header.MessageType} received from {ep}.");
            pc.oniceconnectionstatechange += (state) => Console.WriteLine($"ICE connection state change to {state}.");

            return pc;
        }

        public async Task JoinRoom(string roomId, bool isMusic)
        {
            try
            {
                PeerConnection = await CreateRTCPeerConnection(isMusic);
                Console.WriteLine("PeerConnection" + PeerConnection + "created");//talvez retornar um valor no método acima assim como o server faz?

                //RegisterRTCEventHandlers();

                await HubConnection.InvokeAsync("JoinRoom", roomId);
                Console.WriteLine("Joined in hubConnection ROOM w/ id: " + roomId);

                //HubConnection.inv

                await GetServerOffer();
                Console.WriteLine("getting server offer");
            }
            catch (Exception ex)
            {
                throw new Exception("Error", ex);
            }
        }

        public async Task GetServerOffer()
        {
            try
            {
                var serverOffer = await HubConnection.InvokeAsync<RTCSessionDescriptionInit>("GetServerOffer");
                Console.WriteLine("gettingServerOffer: ");// + serverOffer.sdp);
                await SetAnswer(serverOffer);
            }
            catch (Exception ex)
            {
                throw new Exception("Error", ex);
            }
        }

        public async Task SetAnswer(RTCSessionDescriptionInit offer)
        {
            try
            {
                PeerConnection.setRemoteDescription(offer);
                Console.WriteLine("Setting rtc REMOTE description");

                var answer = PeerConnection.createAnswer();
                Console.WriteLine("Creating a peerconnection answer");

                await PeerConnection.setLocalDescription(answer);

                await HubConnection.InvokeAsync<RTCSessionDescriptionInit>("SetRemoteDescription", answer);
                Console.WriteLine("Setting rtc LOCAL description w/ answer sdp" + answer.sdp);

                //while (true)
                //{
                //    PeerConnection?.SendRtpRaw(SDPMediaTypesEnum.audio, pkt.Payload, pkt.Header.Timestamp, pkt.Header.MarkerBit, pkt.Header.PayloadType);
                //}
            }
            catch (Exception ex)
            {
                throw new Exception("Error", ex);
            }
        }
    }
}