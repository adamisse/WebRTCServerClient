using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Signaler.Hubs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WebServer.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class RtcController : ControllerBase
    {
        private readonly ILogger<RtcController> _logger;
        private readonly Hub<WebRTCHub> hubContext;

        public RtcController(ILogger<RtcController> logger, Hub<WebRTCHub> hubContext)
        {
            _logger = logger;
            this.hubContext = hubContext;
        }
    }
}
