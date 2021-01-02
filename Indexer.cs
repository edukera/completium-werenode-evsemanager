using System;
using System.Threading;
using System.Collections.Generic;
using Dynamic.Json;

public class Indexer {

    public class Evse {
        public string Id { get; }
        public string Address { get; }   
        public string Manager { get; }
        public string Owner { get; }
        // TODO : add technical & pricing fields from smart contract at Address
        public Evse (string i, string a, string m, string o) {
            Id = i;
            Address = a;
            Manager = m;
            Owner = o;
        }
        public override string ToString()
        {
            return $@"Evse {{
    id: {Id};
    address: {Address};
    manager: {Manager};
    owner: {Owner};
}}";
        }
    }

    public static Dictionary<string,Evse> Evses = new Dictionary<string,Evse>();

    private static async void indexEvses () {
        var storage = await Env.Tezos.Blocks.Head.Context.Contracts[Env.AddressBook].Storage.GetAsync();
        foreach(dynamic evsedata in storage.args[1].args[1].args[0]) {
            var id      = evsedata.args[0]["string"].ToString();
            var address = evsedata.args[1].args[0]["string"].ToString();
            var manager = evsedata.args[1].args[1].args[0]["string"].ToString();
            var owner   = evsedata.args[1].args[1].args[1]["string"].ToString();
            Evse evse = new Evse(id,address,manager,owner);
            Evses.Add(id,evse);
            Console.WriteLine(evse);
            Console.WriteLine("---------------------");
        }
    }

    public class Approve {
        public DateTime Time { get; }
        public string Caller { get; }
        public string Spender { get; }
        public string Value { get; }
        public string Status { get; }

        public Approve (DateTime iTime, string iCaller, string iValue, string iSpender, string iStatus) {
            Time = iTime;
            Caller = iCaller;
            Value = iValue;
            Spender = iSpender;
            Status = iStatus;
        }
        public override string ToString () {
            return $@"Approve {{
    time: {Time};
    caller: {Caller};
    spender: {Spender};
    value: {Value};
    status: {Status}
}}";
        }
    }

    public static Dictionary<string,Approve> ApproveOps = new Dictionary<string,Approve>();

    public class Transfer {
        public string status;
        public long amount;
        public Transfer (long a) {
            amount = a;
        }
        public override string ToString() {
            return "Transfer {\n  status: " + status + ";\n  amount: " + amount + ";\n}";
        }
    }

    public static Dictionary<string,Transfer> TransferOps = new Dictionary<string, Transfer>();

    public static string Curhash { get; set; }

    private static async void index () {
        DJsonArray operations = await Env.Tezos.Blocks.Head.Operations.GetAsync();
        var enum1 = operations.GetEnumerator();
        while (enum1.MoveNext()) {
           DJsonArray tok = (DJsonArray)enum1.Current;
            var enum2 = tok.GetEnumerator();
            while (enum2.MoveNext()) {
                dynamic op = enum2.Current;
                string kind = op.contents[0].kind.ToString();
                if (kind == "transaction") {
                    string destination = op.contents[0].destination.ToString();
                    if (destination == Env.Token) {
                        string hash        = op.hash.ToString();
                        string entrypoint  = op.contents[0].parameters.entrypoint.ToString();
                        string status      = op.contents[0].metadata.operation_result.status.ToString();
                        if (entrypoint == "approve") {
                            string caller      = op.contents[0].source.ToString();
                            string spender     = op.contents[0].parameters.value.args[0]["string"].ToString();
                            string amount      = op.contents[0].parameters.value.args[1]["int"].ToString();
                            Approve approve = new Approve(DateTime.Now,caller,amount,spender,status);
                            ApproveOps.Add(hash,approve);
                            Console.WriteLine(approve);
                        } else if (entrypoint == "transfer") {
                            Transfer transfer;
                            if (TransferOps.TryGetValue(hash, out transfer)) {
                                transfer.status = status;
                                Console.WriteLine("op hash: " + hash);
                                Console.WriteLine(transfer);
                            } else {
                                Console.WriteLine("WARNING! transfer tx " + hash + " not indexed by this server");
                            }
                        }
                        Console.WriteLine("===========================================================");
                    }
                    // TODO : scan Address Book add & remove of evse to update Evses
                }
            } 
        }
    }

    public static async void ProcessIndex(Object StateInfo) {
        string hash = "";
        indexEvses();
        while (true) {
            var HashToken = await Env.Tezos.Blocks.Head.Hash.GetAsync();
            string currenthash = HashToken.ToString();
            if (currenthash != hash) {
                Console.WriteLine("New hash is {0}.", currenthash);
                index();
                // TODO : clean old approve base on approve' Time field
                hash = currenthash;
                Curhash = currenthash;
            } else {
                Console.WriteLine("Same hash.");
            }
            Thread.Sleep(5000);
        }
    } 
}
