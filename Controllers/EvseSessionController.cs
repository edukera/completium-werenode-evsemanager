using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.AspNetCore.Http;

namespace EvseManager.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class EvseSessionController : ControllerBase
    {
        private readonly ILogger<StartRequest> _logger;

         public EvseSessionController(ILogger<StartRequest> logger)
        {
            _logger = logger;
        }

        [HttpPost("start")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public StartReply Start(StartRequest request)
        {
            StartCharge start = new StartCharge(request);
            return start.exec();
        }

        [HttpPost("startnosig")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public StartReply StartNoSig(StartNoSigRequest request)
        {
            StartCharge start = new StartCharge(request);
            return start.exec();
        }

        [HttpGet("getstatus")]
        public StatusReply GetStatus(StatusRequest request) {
            Session session;
            if (SessionProcess.Sessions.TryGetValue(request.SessionId, out session)) {
                return new StatusReply {
                    Message = session.approveState.ToString() + "_" + session.chargeState.ToString() + "_" + session.userInterrupted + "_" + session.switchInterrupted,
                    Status = session.sessionState.ToString()
                };

            } else {
                return new StatusReply {
                    Message = "Session id not found",
                    Status = "1"
                };
            }
        }

        [HttpPost("interrupt")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public InterruptReply Interrupt(InterruptRequest request) {
            Session session;
            if (SessionProcess.Sessions.TryGetValue(request.SessionId, out session)) {
                session.userInterrupted = true;
                return new InterruptReply {
                    Message = "",
                    Status = "O"
                };

            } else {
                return new InterruptReply {
                    Message = "Session id not found",
                    Status = "1"
                };
            }
        }

    }
}
