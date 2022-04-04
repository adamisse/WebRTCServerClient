using Newtonsoft.Json;
using SIPSorcery.Net;

namespace Signaler.Models
{
    /// <summary>
    ///     Usuário
    /// </summary>
    public class User
    {
        /// <summary>
        ///     ctor
        /// </summary>
        public User()
        {
        }

        /// <summary>
        ///     Id do usuário
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        ///     Nome do usuário
        /// </summary>
        public string Username { get; set; }

        /// <summary>
        ///     Id da conexão do usuario
        /// </summary>
        public string ConnectionId { get; set; }

        /// <summary>
        ///     Indica se o usuario está em uma chamada
        /// </summary>
        /// 
        public bool IsInCall { get; set; }

        //public bool SendingPkt { get; set; }

        /// <summary>
        ///     A sala que o usuário está participando
        /// </summary>
        public virtual Room? Room { get; set; }

        /// <summary>
        ///     Peer connection vinculada ao usuario
        /// </summary>
        [JsonIgnore]
        public RTCPeerConnection? PeerConnection { get; set; }
    }
}
