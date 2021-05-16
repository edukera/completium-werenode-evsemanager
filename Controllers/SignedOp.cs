using System;
using Netezos.Keys;
using Netezos.Forging;
using Netezos.Forging.Models;
using Netezos.Encoding;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Linq;


public class SignedOperation {
    private byte[] sigprefix = new byte[] { 9 , 245, 205, 134, 18 };
    private string branch;
    private string data;
    private string signature;
    private string unforged = "";
    public string signed_operation;
    public SignedOperation (string b, string so) {
        branch = b;
        var datalength = so.Length - 128;
        data   = so.Substring(0,datalength);
        signature = so.Substring(datalength);
        signed_operation = so;
    }
    public bool IsSignedBy(string user) {
        var pubkey = PubKey.FromBase58(user);
        /* Console.WriteLine(pubkey.Address);
        Console.WriteLine("data length: {0}",data.Length);
        Console.WriteLine("signature length: {0}", signed.Length); */
        return pubkey.Verify(Hex.Parse("03" + data),Hex.Parse(signature));
    }
    public string GetSignature () {
        var bytes = Hex.Parse(signature);
        return (new Signature(bytes,sigprefix)).ToString();
    }
    public string Get () { return data + signature; }
    public string GetUnForged () {
        var request = new {
            operations = new List<object> {
                new {
                    @branch = branch,
                    @data = (data + signature).Substring(64)
                }
            }
        };
        var result = Task.Run(async() => await Env.Tezos.Blocks.Head.Helpers.Parse.Operations.PostAsync<object>(request)).Result;
        if (unforged == "") { unforged = result.ToString(); }
        return result.ToString();
    }

    public class PreApplyResponse {
        public bool valid;
        public string message;
        public bool IsValid() { return valid; }
        public string GetMsg () { return message; }
    }
    public PreApplyResponse PreApply(string entrypoint, Func<object, object> GetParameters) {
        unforged = (unforged == "") ? GetUnForged() : unforged;
        dynamic operations = JsonConvert.DeserializeObject<dynamic>(unforged);
        /* Console.WriteLine("Unforged operation:");
        Console.WriteLine(operations); */
        var request = new List<object> {
            new {
                protocol = Env.Protocol,
                @branch = branch,
                contents = new List<object> {
                    new  {
                        kind          = "transaction",
                        source        = operations[0].contents[0].source.ToString(),
                        fee           = operations[0].contents[0].fee.ToString(),
                        counter       = operations[0].contents[0].counter.ToString(),
                        gas_limit     = operations[0].contents[0].gas_limit.ToString(),
                        storage_limit = operations[0].contents[0].storage_limit.ToString(),
                        amount        = operations[0].contents[0].amount.ToString(),
                        destination   = operations[0].contents[0].destination.ToString(),
                        parameters    = new {
                            @entrypoint = entrypoint,
                            value       = GetParameters(operations)
                        }
                    }
                },
                signature = GetSignature(),
            }
        };
        /* Console.WriteLine("Pre apply request");
        Console.WriteLine(request[0]); */
        var result = Task.Run(async() => await Env.Tezos.Blocks.Head.Helpers.Preapply.Operations.PostAsync<object>(request)).Result;
        var resultoperation = Newtonsoft.Json.Linq.JToken.Parse(result.ToString());
        var status = resultoperation.SelectToken("$[0].contents[0].metadata.operation_result.status").ToString();
        if (status == "failed") {
            string msg = "";
            var errors = resultoperation.SelectToken("$[0].contents[0].metadata.operation_result.errors").Children().GetEnumerator();
            while (errors.MoveNext()) {
                var err = errors.Current;
                try {
                    var with = err.SelectToken("$.with");
                    try {
                        msg += with.SelectToken("$.args[0].string");
                    } catch (Exception) {
                        msg += with.ToString();
                    }
                } catch (Exception) {}
            }
            return new PreApplyResponse () { valid = false, message = msg };
        } else {
            return new PreApplyResponse () { valid = true, message = "" };
        }
    }
}

public class Forger {
    private string destination;
    private string privatekey;
    private dynamic parameters;
    public Forger(string d, string pk, dynamic pr) {
        destination = d;
        privatekey = pk;
        parameters = pr;
    }
    public async Task<SignedOperation> exec() {
        Console.WriteLine("private key: {0}",privatekey);
        var key = Key.FromBase58(privatekey);
        var counter = await Env.Tezos.Blocks.Head.Context.Contracts[key.PubKey.Address].Counter.GetAsync<int>();
        Console.WriteLine("counter: {0}",counter);
        var head = await Env.Tezos.Blocks.Head.Hash.GetAsync<string>();
        Console.WriteLine("head: {0}",head);
        var content = new ManagerOperationContent[] {
          new TransactionContent {
            Source = key.PubKey.Address,
            Counter = ++counter,
            Amount = 0,
            Destination = destination,
            GasLimit = Env.GasLimit,
            StorageLimit = Env.StorageLimit,
            Fee = Env.Fee,
            Parameters = parameters,
          }
        };
        Console.WriteLine(content[0]);
        var bytes = await new LocalForge().ForgeOperationGroupAsync(head, content);

        byte[] signature = key.SignOperation(bytes);
        Console.WriteLine("signed op: {0}",Hex.Convert(bytes.Concat(signature)));
        return new SignedOperation(head,Hex.Convert(bytes.Concat(signature)));
    }
}

public class ApproveOperation {
    public SignedOperation signedOperation;
    public string Destination;
    public string Entrypoint;
    public string Spender;
    public long Value;

    public override string ToString() {
        return $@"ApproveOperation {{
    destination: {Destination};
    entrypoint: {Entrypoint};
    spender: {Spender};
    value: {Value};
}}";;
    }

    public void setSignedOp(string op, string b) {
        signedOperation = new SignedOperation(b, op);
        ExtractParameters();
    }
    public string forge(string nbt, string pk) {
        Value = (long)Convert.ToInt64(nbt);
        var parameters = new Parameters {
            Entrypoint = "approve",
            Value = new MichelinePrim {
                Prim = PrimType.Pair,
                Args = new List<IMicheline> {
                  new MichelineString(Env.PrivateKey.PubKey.Address),
                  new MichelineInt(Value)
                }
            }
        };
        Forger forger = new Forger(Env.Token,pk,parameters);
        signedOperation = forger.exec().Result;
        ExtractParameters();
        return signedOperation.signed_operation;
    }

    private void ExtractParameters () {
        string unforged = signedOperation.GetUnForged();
        dynamic operation = JsonConvert.DeserializeObject<dynamic>(unforged);
        try {
            Destination =             operation[0].contents[0].destination.ToString();
            Entrypoint  =             operation[0].contents[0].parameters.entrypoint.ToString();
            Spender     =             operation[0].contents[0].parameters.value.args[0]["string"].ToString();
            Value       = Int64.Parse(operation[0].contents[0].parameters.value.args[1]["int"].ToString());
        } catch (System.Exception) {
            Console.WriteLine("Invalid Approve data");
        }
        /* Console.WriteLine("Destination: {0}", Destination);
        Console.WriteLine("Entrypoint: {0}", Entrypoint);
        Console.WriteLine("Spender: {0}", Spender);
        Console.WriteLine("Value: {0}", Value); */
    }
    public bool IsValid() {
        Console.WriteLine("Env pubkey addr: {0}",Env.PrivateKey.PubKey.Address);
        return
            Entrypoint  == "approve" &&
            Destination == Env.Token &&
            Spender     == Env.PrivateKey.PubKey.Address;
    }

    public long GetValue() { return Value; }

    public SignedOperation.PreApplyResponse PreApply () {
        return signedOperation.PreApply("approve", operation => {
            return new {
                prim = "Pair",
                args = new List<object>() {
                    new { @string = Spender },
                    new { @int = (Value).ToString() }
                }
            };
        });
    }

}