using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Threading;

[DebuggerDisplay("{" + nameof(GetDebuggerDisplay) + "(),nq}")]
public class Session {

    public enum ApproveState { TobeApproved, Approved, NotApplied, InvalidAmount, InvalidSpender, TimeElapsed } 
    public enum ChargingState { TobeSet, Off, On, FailedOn, FailedOff }
    public enum SessionState { Created, Charging, Failed, Success }
    public string id { get; set; }
    public DateTime creationDate { get; set; }
    public string approveHash { get; set; } 
    public string transferHash { get; set; }
    public ApproveState approveState { get; set; } = ApproveState.TobeApproved;
    public ChargingState chargeState { get; set; } = ChargingState.TobeSet;
    public SessionState sessionState { get; set; } = SessionState.Created;
    public string userAddress { get; set; }
    public long duration { get; set; }
    public long amount { get; set; }
    public string currency { get; set; }
    public string evseid { get; set; }
    public DateTime startChargingDate { get; set; }
    public DateTime endChargingDate { get; set; }
    public bool userInterrupted { get; set; } = false;
    public bool switchInterrupted { get; set; } = false;
    public override string ToString() {
        return $@"Session {{
  id: {id};
  creation: {creationDate};
  approvehash: {approveHash};
  transferhash: {transferHash};
  approve: {approveState};
  charge: {chargeState};
  duration: {duration};
  amount: {amount};
  currency: {currency};
  evse: {evseid};
  startcharge: {startChargingDate};
  endcharge: {endChargingDate};
  state: {sessionState};
}}";
    }

    public bool IsActive() {
        return sessionState == SessionState.Created || sessionState == SessionState.Charging;
    }
    public bool IsInvalidApprove () {
        return 
            approveState == ApproveState.InvalidAmount  ||
            approveState == ApproveState.InvalidSpender ||
            approveState == ApproveState.NotApplied     ||
            approveState == ApproveState.TimeElapsed;
    }

    private string GetDebuggerDisplay() {
        return ToString();
    }
}

public class SessionProcess {

    public static Dictionary<string,Session> Sessions = new Dictionary<string, Session>();

    private void switchEvseOff (Session session) {
        // TODO : implement real process
        EvseSwitch.ISwitch evse;
        if (EvseSwitch.Switches.TryGetValue(session.evseid,out evse)) {
            Console.WriteLine("Switching EVSE " + session.evseid + " off ...");
            EvseSwitch.SwitchResponse msg = evse.SwitchOff();
            Console.WriteLine(msg);
        } else {
            Console.WriteLine("ANOMALY! switch not found");
        }
    }
    public void Exec(string SessionId) {
        Session session;
        if (Sessions.TryGetValue(SessionId,out session)) {

            // start switch validation thread 
            ThreadPool.QueueUserWorkItem(o => {
                EvseSwitch.ISwitch evse;
                if (EvseSwitch.Switches.TryGetValue(session.evseid, out evse)) {
                    while (session.IsActive()) {
                        Console.WriteLine("Checking " + session.evseid + " status ...");
                        if (!(evse.IsOn())) {
                            session.switchInterrupted = true;
                        }
                        Thread.Sleep(1500);
                    }
                }
            });

            // start approve validation thread
            ThreadPool.QueueUserWorkItem(o => {
                int checkcount = 0;
                bool @continue = true;
                while (@continue) {
                    Indexer.Approve approve;
                    if (Indexer.ApproveOps.TryGetValue(session.approveHash, out approve)) {
                        @continue = false;
                        if (approve.Status == "applied") {
                            if (Int64.Parse(approve.Value) != session.amount) {
                                session.approveState = Session.ApproveState.InvalidAmount;
                                // TODO: should cancel approve
                            } else if (approve.Spender != Env.PrivateKey.PubKey.Address) {
                                session.approveState = Session.ApproveState.InvalidSpender;
                                // Cannot cancel approve ...
                            } else { // Transaction approved
                                session.approveState = Session.ApproveState.Approved;
                            };
                        } else {
                            session.approveState = Session.ApproveState.NotApplied;
                            // TODO: should cancel approve 
                        }
                    }
                    if (checkcount++ > 60) {
                        @continue = false;
                        session.approveState = Session.ApproveState.TimeElapsed;
                    }
                    Thread.Sleep(1000);
                }
            });

            bool @continue = true;
            while (@continue) {
                // compute continue 
                if (session.userInterrupted || session.switchInterrupted) {
                    if (session.chargeState == Session.ChargingState.On) {
                        // Switch evse OFF
                        switchEvseOff(session);
                        session.chargeState = Session.ChargingState.Off;
                        // compute amount to transfer
                        ThreadPool.QueueUserWorkItem(o => { 
                            double duration = (DateTime.Now - session.startChargingDate).TotalSeconds;
                            double ratio = duration / session.duration;
                            long approveAmount = Convert.ToInt64(session.amount);
                            long amount = (long) (ratio * approveAmount);
                            amount = Math.Min(amount, approveAmount);
                            Indexer.Evse evse;
                            if (Indexer.Evses.TryGetValue(session.evseid, out evse)) {
                                Transfer.exec(session, session.userAddress, evse.Owner, amount);
                            } else {
                                Console.WriteLine("ANOMALY! evse not found");
                            }
                        });
                    }
                    session.sessionState = Session.SessionState.Success;
                    @continue = false;
                } else if (session.IsInvalidApprove()) {
                    if (session.chargeState == Session.ChargingState.On) {
                        // Switch evse OFF
                        switchEvseOff(session);
                        session.chargeState = Session.ChargingState.Off;
                    }
                    session.sessionState = Session.SessionState.Failed;
                    @continue = false;
                } else if (session.chargeState == Session.ChargingState.FailedOn) {
                    session.sessionState = Session.SessionState.Failed;
                    @continue = false;
                } else if (session.chargeState == Session.ChargingState.On) {
                    // is charging session over?
                    double duration = (DateTime.Now - session.startChargingDate).TotalSeconds;
                    if (duration >= session.duration) {
                        @continue = false;
                        // Switch evse OFF
                        switchEvseOff(session);
                        session.chargeState = Session.ChargingState.Off;
                        session.sessionState = Session.SessionState.Success;
                        // Originate transfer transaction
                        ThreadPool.QueueUserWorkItem(o => {
                            Indexer.Evse evse;
                            if (Indexer.Evses.TryGetValue(session.evseid, out evse)) {
                                Transfer.exec(session, session.userAddress, evse.Owner, Convert.ToInt64(session.amount));
                            } else {
                                Console.WriteLine("ANOMALY! evse not found");
                            }
                        });
                    }
                }
                Thread.Sleep(2000);
            }
        } else {
            Console.WriteLine("Error : session not found " + SessionId);
        }
    }
}