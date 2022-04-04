using SIPSorcery.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Signaler.Models
{
    public class AudioData
    {
        public RTPPacket Packet { get; set; }
        public List<User> Listeners { get; set; }
        public string SenderName { get; set; }
    }
}