using Netezos.Rpc;
using Netezos.Keys;

public class Env {

    public static TezosRpc Tezos = new TezosRpc("https://delphinet-tezos.giganode.io/");
    public static readonly string Protocol = "PsDELPH1Kxsxt8f9eWbxQeRxkjfbxoqM52jvs5Y5fBxWWh4ifpo";
    public static readonly string Token = "KT1Cg3HRquNxN2ZyQmaw8CJLR4Fae1GRDwmJ";
    public static readonly string AddressBook = "KT1X88DyBqCkU1P9tMJBHgYcZDFepFc1d5ub";
    public static readonly Key PrivateKey = Key.FromBase58("edsk3BksmijaVkBoi485CHA7X9pDfexAwSWiQum6WAHNaLot2SXfyW");
    // public key : JG
    public static readonly int Fee = 50000; // TODO: a bit too high?
    public static readonly int GasLimit = 400000;
    public static readonly int StorageLimit = 400;
}