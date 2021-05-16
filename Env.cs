using Netezos.Rpc;
using Netezos.Keys;

public class Env {

    public static TezosRpc Tezos = new TezosRpc("https://testnet-tezos.giganode.io");
    public static readonly string Protocol = "PsFLorenaUUuikDWvMDr6fGBRG8kt3e3D3fHoXK1j1BFRxeSH4i";
    public static readonly string Token = "KT1Lg7wwCw44XnAzGYuBtmGtHfPWtRdaeMxm";
    public static readonly string AddressBook = "KT1KoWRa47eziJVe2GuYXNHS6cztzmxFomoM";
    public static readonly Key PrivateKey = Key.FromBase58("edsk3LQXpM9RkUXUarHrxQqfw6oozDCjdxYpPjsZFyGxubyrwxg9bh");
    // public key : JG
    public static readonly int Fee = 50000; // TODO: a bit too high?
    public static readonly int GasLimit = 400000;
    public static readonly int StorageLimit = 400;
}
