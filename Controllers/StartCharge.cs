using System;
using System.Threading.Tasks;
using System.Security.Cryptography;
using Netezos.Encoding;
using System.Threading;


namespace EvseManager
{

    public class StartCharge {

        private string evse_id;
        private string currency;
        private string signed_operation = null;
        private string user_privatekey = null;
        private string branch;
        private string parking_time;
        private string user_address;
        private string user_pubkey;
        private string nb_tokens;

        private static RNGCryptoServiceProvider rngCsp = new RNGCryptoServiceProvider();

        private static string getNextSessionId () {
            byte[] randomNumber = new byte[128];
            rngCsp.GetBytes(randomNumber);
            return ""+BitConverter.ToUInt64(randomNumber,0);
        }
        private bool ExistsActiveSessionFor(string evseid) {
            foreach(Session s in SessionProcess.Sessions.Values) {
                if (s.evseid == evseid && s.IsActive()) {
                    return true;
                }
            }
            return false;
        }
        private bool checkPricing(
            long amount,
            string duration,
            string currency,
            string evseid) {
            //////////////////////////////////////////////
            // TODO : implement pricing policy validation
            //////////////////////////////////////////////
            return true;
        }

        public StartCharge(StartRequest request) {
            evse_id = request.EvseId;
            currency = request.Currency;
            signed_operation = request.SignedOperation;
            branch = request.Branch;
            parking_time = request.ParkingTime;
            user_address = request.UserAddress;
            user_pubkey = request.UserPubkey;
        }
        public StartCharge(StartNoSigRequest request) {
            evse_id = request.EvseId;
            currency = request.Currency;
            user_privatekey = request.UserPrivatekey;
            parking_time = request.ParkingTime;
            user_address = request.UserAddress;
            user_pubkey = request.UserPubkey;
            nb_tokens = request.NbTokens;
        }

        public StartReply exec() {
            if (!(Indexer.Evses.ContainsKey(evse_id))) {
                return new StartReply {
                    Message = "EvseId not found",
                    Status = "1"
                };
            }
            EvseSwitch.ISwitch evse;
            if (!(EvseSwitch.Switches.TryGetValue(evse_id, out evse))) {
                return new StartReply {
                    Message = "Switch not found",
                    Status = "2"
                };
            };
            if (ExistsActiveSessionFor(evse_id)) {
                return new StartReply {
                    Message = "Evse is busy",
                    Status = "3"
                };
            };
            ApproveOperation approve = new ApproveOperation();
            if (signed_operation != null) {
                // Prevalidating approve parameters: manager address & amount
                approve.setSignedOp(signed_operation, branch);
                if (!(approve.signedOperation.IsSignedBy(user_pubkey))) {
                    return new StartReply {
                        Message = "Approve transaction not signed by public key",
                        Status = "4"
                    };
                }
                if (!(approve.IsValid())) {
                    return new StartReply {
                        Message = "Invalid approve parameters",
                        Status = "5"
                    };
                };
            } else {
                // Forge apply operation
                signed_operation = approve.forge(nb_tokens, user_privatekey);
            }
            SignedOperation.PreApplyResponse response = approve.PreApply();
            if (!(response.IsValid())) {
                return new StartReply {
                    Message = response.GetMsg(),
                    Status = "6"
                };
            };
            if (!(checkPricing(approve.GetValue(), parking_time, currency, evse_id))) {
                return new StartReply {
                    Message = "Invalid price",
                    Status = "7"
                };
            };
            EvseSwitch.SwitchResponse switchResponse = evse.SwitchOn();
            if (switchResponse.status == EvseSwitch.SwitchStatus.SwtichFAIL) {
                return new StartReply {
                    Message = "Could not switch on EVSE " + evse_id + " (" + switchResponse.msg + ")",
                    Status  = "8"
                };
            }
            // Inject approve operation
            var hash = "";
            try {
                Console.WriteLine("Injecting signed op: {0}", signed_operation);
                hash = Task.Run(async () => await Env.Tezos.Inject.Operation.PostAsync(Hex.Parse(signed_operation))).Result.ToString();
                Console.WriteLine("Approve op hash: ",hash.ToString());
            } catch (Exception e) {
                return new StartReply {
                    Message = "Could not inject approve operation: " + e.ToString(),
                    Status  = "9"
                };
            }
            // Create session
            string sessionId = getNextSessionId();
            SessionProcess.Sessions.Add(sessionId, new Session () {
                id                = sessionId,
                creationDate      = DateTime.Now,
                approveHash       = hash,
                chargeState       = Session.ChargingState.On,
                sessionState      = Session.SessionState.Charging,
                userAddress       = user_address,
                duration          = Int64.Parse(parking_time),
                amount            = approve.GetValue(),
                currency          = currency.ToString(),
                evseid            = evse_id,
                startChargingDate = DateTime.Now,
            });
            // Start session Thread
            SessionProcess process = new SessionProcess();
            ThreadPool.QueueUserWorkItem(o => process.Exec(sessionId));
            return new StartReply {
                Message = sessionId,
                Status = "0"
            };
        }
    }
}