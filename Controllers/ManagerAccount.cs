using System;
using System.Threading;
using Priority_Queue;

public class ManagerAccount
{

  public enum Status { Created, Waiting, Failed }

  public class TransferItem
  {
    private Session _session;
    public Session Session
    {
      get => _session;
      set => _session = value;
    }

    private string _from;
    public string From
    {
      get => _from;
      set => _from = value;
    }

    private string _to;
    public string To
    {
      get => _to;
      set => _to = value;
    }
    private long _value;
    public long Value
    {
      get => _value;
      set => _value = value;
    }

    public TransferItem(Session session, string from, string to, long value)
    {
      this._session = session;
      this._from = from;
      this._to = to;
      this._value = value;
    }
  }

  public class Item
  {
    private Status _status = Status.Created;
    public Status Status
    {
      get => _status;
      set => _status = value;
    }

    private int _counter;
    public int Counter
    {
      get => _counter;
      set => _counter = value;
    }

    private TransferItem _transferItem;
    public TransferItem TransferItem
    {
      get => _transferItem;
      set => _transferItem = value;
    }

    public Item(int counter, TransferItem TransferItem)
    {
      this._counter = counter;
      this._transferItem = TransferItem;
    }
  }

  private static int counter = 0;
  private static SimplePriorityQueue<Item> pQueue = new SimplePriorityQueue<Item>();

  public static async void Run(Object StateInfo)
  {
    Console.WriteLine("ManagerAccount: running ...");
    counter = await Env.Tezos.Blocks.Head.Context.Contracts[Env.PrivateKey.PubKey.Address].Counter.GetAsync<int>();
    Console.WriteLine("ManagerAccount: initialized");
    while (true)
    {
      // Console.WriteLine("ManagerAccount: Queue length {0}", pQueue.Count);
      foreach (Item item in pQueue)
      {
        var session = item.TransferItem.Session;
        switch (item.Status)
        {
          case Status.Created:
            Exec(item.TransferItem, item.Counter);
            item.Status = Status.Waiting;
            break;
          case Status.Waiting:
            Indexer.Transfer transfer;
            if (Indexer.TransferOps.TryGetValue(session.transferHash, out transfer))
            {
              var status = transfer.status;
              // Console.WriteLine("ManagerAccount: Status {0}", status);
              if (status == "applied")
              {
                Console.WriteLine("ManagerAccount: {0} transfered", session.evseid);
                pQueue.Remove(item);
              }
              else if (status == "failed")
              {
                Console.WriteLine("ManagerAccount: {0} failed", session.evseid);
                item.Status = Status.Failed;
              }
            }
            break;
          case Status.Failed:
            Console.WriteLine("ManagerAccount: {0} retry", session.evseid);
            AddInternal(item.TransferItem);
            pQueue.Remove(item);
            break;
        }
      }
      Thread.Sleep(1000);
    }
  }

  private static void Exec(TransferItem transferItem, int counter)
  {
    Transfer.exec(transferItem.Session, transferItem.From, transferItem.To, transferItem.Value, counter);
  }

  private static void AddInternal(TransferItem transferItem)
  {
    var c = ++counter;
    Item lItem = new Item(c, transferItem);
    pQueue.Enqueue(lItem, c);
  }

  public static void Add(Session session, string from, string to, long value)
  {
    TransferItem transferItem = new TransferItem(session, from, to, value);
    AddInternal(transferItem);
  }

}