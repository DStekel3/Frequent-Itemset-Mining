using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FP.DAL.Gateway.Interface;
using FP.DAL.DAO;
namespace FP.Algorithm
{
  public class FPTree
  {
    readonly Node _root;
    public IDictionary<string, Node> _headerTable;
    public double _minimumSupport;
    private int _minimumSupportCount;
    public readonly IInputDatabaseHelper _inputDatabaseHelper;
    public List<Item> FrequentItems { get; set; }
    public int _dIndex = 1;
    public List<ItemSet> _DIndexSample = new List<ItemSet>();

    private FPTree()
    {
      _root = new Node("");
      _headerTable = new Dictionary<string, Node>();
      _minimumSupport = 0f;
      FrequentItems = new List<Item>();
    }

    public FPTree(IInputDatabaseHelper inDatabaseHelper, double minSup)
        : this()
    {
      _minimumSupport = minSup;
      _inputDatabaseHelper = inDatabaseHelper;

      _minimumSupportCount = (int)(_minimumSupport * _inputDatabaseHelper.TotalTransactionNumber);

      CalculateFrequentItems();
      FrequentItems = FrequentItems.OrderByDescending(x => x.SupportCount).ToList();

      _inputDatabaseHelper.OpenDatabaseConnection();
      List<string> aTransaction = new List<string>();
      do
      {
        aTransaction = _inputDatabaseHelper.GetNextTransaction();
        InsertTransaction(aTransaction);
      }
      while(aTransaction.Count > 0);
      _inputDatabaseHelper.CloseDatabaseConnection();
    }

    private void InsertTransaction(List<string> aTransaction)
    {
      //filter transactions to get frequent items in sorted order of frequentItems
      List<Item> items = FrequentItems.FindAll
          (
              delegate (Item anItem)
              {
                return aTransaction.Exists(x => x == anItem.Symbol);
              }
          );
      Node tempRoot = _root;
      foreach(Item anItem in items)
      {
        Node aNode = new Node(anItem.Symbol) {FpCount = 1};
        Node tempNode;
        if((tempNode = tempRoot.Children.Find(c => c.Symbol == aNode.Symbol)) != null)
        {
          tempNode.FpCount++;
          tempRoot = tempNode;
        }
        else
        {
          tempRoot.AddChild(aNode);
          tempRoot = aNode;
          if(_headerTable.ContainsKey(aNode.Symbol))
          {
            aNode.NextHeader = _headerTable[aNode.Symbol];
            _headerTable[aNode.Symbol] = aNode;
          }
          else
          {
            _headerTable[aNode.Symbol] = aNode;
          }
        }
      }
    }

    private void CalculateFrequentItems()
    {
      List<Item> items = _inputDatabaseHelper.CalculateFrequencyAllItems();

      foreach(Item anItem in items)
      {
        if(anItem.SupportCount >= _minimumSupportCount)
        {
          FrequentItems.Add(anItem.Clone());
        }
      }
    }

    private void InsertBranch(List<Node> branch)
    {
      Node tempRoot = _root;
      for(int i = 0; i < branch.Count; ++i)
      {
        Node aNode = branch[i];
        Node tempNode = tempRoot.Children.Find(x => x.Symbol == aNode.Symbol);
        if(null != tempNode)
        {
          tempNode.FpCount += aNode.FpCount;
          tempRoot = tempNode;
        }
        else
        {
          while(i < branch.Count)
          {
            aNode = branch[i];
            aNode.Parent = tempRoot;
            tempRoot.AddChild(aNode);
            if(_headerTable.ContainsKey(aNode.Symbol))
            {
              aNode.NextHeader = _headerTable[aNode.Symbol];
            }

            _headerTable[aNode.Symbol] = aNode;

            tempRoot = aNode;
            ++i;

          }
          break;
        }
      }
    }

    public int GetTotalSupportCount(string itemSymbol)
    {
      int sCount = 0;
      Node node = _headerTable[itemSymbol];
      while(null != node)
      {
        sCount += node.FpCount;
        node = node.NextHeader;
      }
      return sCount;
    }

    public FPTree Project(Item anItem)
    {
      FPTree tree = new FPTree
      {
        _minimumSupport = _minimumSupport,
        _minimumSupportCount = _minimumSupportCount
      };

      Node startNode = _headerTable[anItem.Symbol];

      while(startNode != null)
      {
        int projectedFpCount = startNode.FpCount;
        Node tempNode = startNode;
        List<Node> aBranch = new List<Node>();
        while(null != tempNode.Parent)
        {
          Node parentNode = tempNode.Parent;
          if(parentNode.IsNull())
          {
            break;
          }
          Node newNode = new Node(parentNode.Symbol) {FpCount = projectedFpCount};
          aBranch.Add(newNode);
          tempNode = tempNode.Parent;
        }
        aBranch.Reverse();
        tree.InsertBranch(aBranch);
        startNode = startNode.NextHeader;
      }

      IDictionary<string, Node> inFrequentHeaderTable = tree._headerTable.
          Where(x => tree.GetTotalSupportCount(x.Value.Symbol) < _minimumSupportCount).
          ToDictionary(p => p.Key, p => p.Value);
      tree._headerTable = tree._headerTable.
          Where(x => tree.GetTotalSupportCount(x.Value.Symbol) >= _minimumSupportCount).
          ToDictionary(p => p.Key, p => p.Value);

      foreach(KeyValuePair<string, Node> hEntry in inFrequentHeaderTable)
      {
        Node temp = hEntry.Value;
        while(null != temp)
        {
          Node tempNext = temp.NextHeader;
          Node tempParent = temp.Parent;
          tempParent.Children.Remove(temp);
          temp = tempNext;
        }
      }

      tree.FrequentItems = FrequentItems.FindAll
      (
        item => tree._headerTable.ContainsKey(item.Symbol)
      );
      return tree;
    }
  }
}
