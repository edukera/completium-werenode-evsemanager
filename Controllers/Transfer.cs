using Netezos.Forging.Models;
using Netezos.Forging;
using Netezos.Encoding;
using System;
using System.Collections.Generic;
using System.Linq;

public class Transfer {
    public static async void exec (Session session, string from, string to, long value, int counter) {
        var header = await Env.Tezos.Blocks.Head.Hash.GetAsync<string>();
        var content = new ManagerOperationContent[] {
            new TransactionContent {
               Source = Env.PrivateKey.PubKey.Address,
               Counter = counter,
               Amount = 0,
               Destination = Env.Token,
               GasLimit = Env.GasLimit,
               StorageLimit = Env.StorageLimit,
               Fee = Env.Fee,
               Parameters = new Parameters {
                 Entrypoint = "transfer",
                 Value = new MichelinePrim {
                   Prim = PrimType.Pair,
                   Args = new List<IMicheline> {
                     new MichelineString(from),
                     new MichelinePrim {
                         Prim = PrimType.Pair,
                         Args = new List<IMicheline> {
                             new MichelineString(to),
                             new MichelineInt(value)
                         }
                     }
                   }
                 }
               }
             }
        };
        var bytes = await new LocalForge().ForgeOperationGroupAsync(header, content);

        // sign the operation bytes
        byte[] signature = Env.PrivateKey.SignOperation(bytes);

        // inject the operation and get its id (operation hash)
        Console.WriteLine("Injecting transfer operation ...");
        var trOpHash = await Env.Tezos.Inject.Operation.PostAsync(bytes.Concat(signature));
        Console.WriteLine(trOpHash.ToString());
        // Update Indexer
        Indexer.TransferOps.Add(trOpHash.ToString(), new Indexer.Transfer(value));
        session.transferHash = trOpHash.ToString();
    }
}