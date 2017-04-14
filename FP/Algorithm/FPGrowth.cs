using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FP.DAL.Gateway.Interface;
using FP.DAL.DAO;
namespace FP.Algorithm
{
  public class FPGrowth
  {
    public FPTree _fpTree;
    private IOutputDatabaseHelper _outputDatabaseHelper;
    public double _elapsedTime;

    public FPGrowth()
    {
      _fpTree = null;
      _outputDatabaseHelper = null;
    }
    public FPGrowth(FPTree tree, IOutputDatabaseHelper outDatabaseHelper)
        : this()
    {
      _fpTree = tree;
      _outputDatabaseHelper = outDatabaseHelper;
    }
    public List<ItemSet> GenerateFrequentItemSets()
    {
      List<Item> frequentItems = _fpTree.FrequentItems;
      List<ItemSet> frequentItemSets = new List<ItemSet>();
      foreach (Item frequentItem in frequentItems)
      {
        var newItemSet = new ItemSet();
        newItemSet.AddItem(frequentItem);
        frequentItemSets.Add(newItemSet);
      }
      foreach(Item anItem in frequentItems)
      {
        ItemSet anItemSet = new ItemSet();
        anItemSet.AddItem(anItem);
        frequentItemSets.AddRange(Mine(_fpTree, anItemSet));
        // Console.WriteLine(frequentItemSets.Count + " itemsets for " + anItem.Symbol);
      }
      // Console.WriteLine(frequentItemSets);
      return frequentItemSets;
    }

    private static List<ItemSet> Mine(FPTree fpTree, ItemSet anItemSet)
    {
      List<ItemSet> minedItemSets = new List<ItemSet>();
      var projectedTree = fpTree.Project(anItemSet.GetLastItem());
      foreach (Item fi in projectedTree.FrequentItems)
      {
        var newItemSet = new ItemSet();
        newItemSet.AddItem(fi);
        minedItemSets.Add(newItemSet);
      }
      foreach(Item anItem in projectedTree.FrequentItems)
      {
        ItemSet nextItemSet = anItemSet.Clone();
        nextItemSet.AddItem(anItem);
        minedItemSets.AddRange(Mine(projectedTree, nextItemSet));
      }
      return minedItemSets;
    }
    public List<ItemSet> CreateFpTreeAndGenerateFrequentItemsets(IInputDatabaseHelper inputHelper, IOutputDatabaseHelper outHelper, double minSup)
    {
      _outputDatabaseHelper = outHelper;
      var watch = System.Diagnostics.Stopwatch.StartNew();
      FPTree fpTree = new FPTree(inputHelper, minSup);
      this._fpTree = fpTree;
      var totalFrequentItemSets = GenerateFrequentItemSets();
      watch.Stop();
      _elapsedTime = watch.ElapsedMilliseconds;
      return totalFrequentItemSets;
    }
  }
}