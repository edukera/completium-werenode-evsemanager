using System;

namespace EvseManager
{
    public class StartRequest
    {
        public string EvseId { get; set; }
        public string Currency { get; set; }
        public string SignedOperation { get; set; }
        public string Branch { get; set; }
        public string ParkingTime { get; set; }
        public string UserAddress { get; set; }
        public string UserPubkey { get; set; }
    }

    public class StartNoSigRequest 
    {
        public string EvseId { get; set; }
        public string Currency { get; set; }
        public string UserPrivatekey { get; set; }
        public string ParkingTime { get; set; }
        public string UserAddress { get; set; }
        public string UserPubkey { get; set; }
        public string NbTokens { get; set; }
    }

    public class StartReply {
        public string Message { get; set; }
        public string Status { get; set; }
    }

    public class StatusRequest {
        public string SessionId { get; set; }
    }

    public class StatusReply {
        public string Message { get; set; }
        public string Status { get; set; }
    }

    public class InterruptRequest {
        public string SessionId { get; set; }
    }

    public class InterruptReply {
        public string Message { get; set; }
        public string Status { get; set; }
    }

}
