using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using FP.DAL.Gateway.Interface;
using FP.DAL.Gateway;
using FP.Algorithm;

namespace FP
{

  class Program
  {
    private static double _exampleSupport = 0.05;
    private static double _epsilon = 0.01;
    private static double _delta = 0.01;
    private static double _mu = 0.01;
    private static string _database = "kosarak";
    private static string _originalDbName = _database;
    private static int _randomSeed = 1;
    private static bool _isDuplicated = false;
    private static int DIndex = 1;
    private static int VCBound = 0;
    private static int ToivonenBound;

    private static MiningResult _BaseMiningResult;
    private static List<MiningResult> _VcMiningResults = new List<MiningResult>();
    private static List<MiningResult> _ToivonenMiningResults = new List<MiningResult>();

    private static int VcIterations = 0;
    private static int ToivonenIterations = 30;

    static void Main(string[] args)
    {
      args = new[] {"retail", "0,01", "0,01", "0,02", "0,9"};
      double minSup = 0.0;

      if(args.Length == 0)
      {
        minSup = _exampleSupport;
      }
      else
      {
        try
        {
          _database = args[0];
          minSup = double.Parse(args[1]);
          _epsilon = double.Parse(args[2]);
          _delta = double.Parse(args[3]);
          _mu = double.Parse(args[4]);
        }
        catch(Exception e)
        {
          Console.WriteLine(e.Message);
          Console.ReadLine();
          return;
        }
      }
      _originalDbName = _database;

      List<string[]> allTransactions = ReadTransactions();
      Console.WriteLine($"Database contains {allTransactions.Count} transactions.");

      ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["FileDB"];


      // No sampling
      Console.WriteLine("Busy with the full dataset.");
      IInputDatabaseHelper normalSampler = new FileInputDatabaseHelper($"{_database}_original");
      IOutputDatabaseHelper normalHelper = new FileOutputDatabaseHelper(@"D:\temp\");
      FPGrowth fpGrowth = new FPGrowth();
      var normalThreshold = minSup;
      var realFIs = fpGrowth.CreateFpTreeAndGenerateFrequentItemsets(normalSampler, normalHelper, normalThreshold);
      _BaseMiningResult = new MiningResult()
      {
        FoundFrequentItemsets = realFIs.Count,
        TotalRunningTime = fpGrowth._elapsedTime,
        Size = allTransactions.Count,
        Theta = normalThreshold
      };

      Console.WriteLine("Calculating the D-index.");
      DIndex = CalcDIndex(allTransactions);
      Console.WriteLine($"D-index: {DIndex}");

      var minSizeNeeded = GetDBoundSize(DIndex);
      while(allTransactions.Count < minSizeNeeded)
      {
        var multiplyFactor = minSizeNeeded / allTransactions.Count();
        var originalVersion = Clone(allTransactions);
        for(int x = 0; x < multiplyFactor; x++)
        {
          allTransactions.AddRange(originalVersion);
        }
        _isDuplicated = true;
      }

      if(_isDuplicated)
      {
        Console.WriteLine($"Database increased to {allTransactions.Count} transactions.");
        WriteTransactions(allTransactions);
      }

      Console.WriteLine("Calculating the D-Bound.");
      VCBound = GetVcBound(allTransactions, DIndex);
      var vcThreshold = minSup - (_epsilon / 2);
      Console.WriteLine($"D-bound: {VCBound}");
      Console.WriteLine($"VC threshold: {vcThreshold}");

      for(int n = 0; n < VcIterations; n++)
      {
        // VcBound sampling
        Console.WriteLine($"VC: n = {n}");
        var vcSample = CalcSample(allTransactions, VCBound);
        List<string> writeVcSample = new List<string>();
        foreach(string[] transaction in vcSample)
        {
          writeVcSample.Add(string.Join(" ", transaction));
        }
        File.WriteAllLines($"{settings.ConnectionString}{_database}_VcSample.dat", writeVcSample);
        IInputDatabaseHelper vcSampler = new FileInputDatabaseHelper($"{_database}_VcSample");
        IOutputDatabaseHelper outVcHelper = new FileOutputDatabaseHelper(@"D:\temp\");
        fpGrowth = new FPGrowth();
        var foundFIs = fpGrowth.CreateFpTreeAndGenerateFrequentItemsets(vcSampler, outVcHelper, vcThreshold);
        _VcMiningResults.Add(new MiningResult() { Size = vcSample.Count, FoundFrequentItemsets = foundFIs.Count, DBound = DIndex, TotalRunningTime = fpGrowth._elapsedTime, Theta = vcThreshold });
        File.Delete($"{settings.ConnectionString}{_database}_VcSample.dat");
      }

      ToivonenBound = GetToivonenBound();
      Console.WriteLine($"Toivonen bound: {ToivonenBound}");
      for(int n = 0; n < ToivonenIterations; n++)
      {
        // Toivonen sampling
        Console.WriteLine($"Toivonen: n = {n}");
        var toivonenSample = CalcSample(allTransactions, ToivonenBound);
        List<string> writeToivonenSample = toivonenSample.Select(transaction => string.Join(" ", transaction)).ToList();
        File.WriteAllLines($"{settings.ConnectionString}{_database}_ToivonenSample.dat", writeToivonenSample);
        IInputDatabaseHelper toivonenSampler = new FileInputDatabaseHelper($"{_database}_ToivonenSample");
        IOutputDatabaseHelper outToivonenHelper = new FileOutputDatabaseHelper(@"D:\temp\");
        fpGrowth = new FPGrowth();
        var toivonenThreshold = minSup - Math.Sqrt((double)(1.0 / (2 * toivonenSample.Count)) * (Math.Log((1.0 / _mu), Math.E)));
        var foundFIs = fpGrowth.CreateFpTreeAndGenerateFrequentItemsets(toivonenSampler, outToivonenHelper,
          toivonenThreshold);
        
        _ToivonenMiningResults.Add(new MiningResult() { Size = toivonenSample.Count, FoundFrequentItemsets = foundFIs.Count, TotalRunningTime = fpGrowth._elapsedTime, Theta = toivonenThreshold });
        File.Delete($"{settings.ConnectionString}{_database}_ToivonenSample.dat");
      }

      // WRITE OUTPUT
      WriteOutput(normalSampler, normalHelper, _originalDbName, allTransactions);
    }

    public static IList<T> Clone<T>(IList<T> listToClone) where T : ICloneable
    {
      return listToClone.Select(item => (T)item.Clone()).ToList();
    }

    public static void WriteOutput(IInputDatabaseHelper inputHelper, IOutputDatabaseHelper outHelper, string dbName, List<string[]> transactions)
    {
      try
      {
        var dbPath = @"D:\temp\";
        string fileName = $"{dbPath}\\{dbName}\\{dbName}_{_epsilon}_{_delta}_{_mu}.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(fileName));

        // Print general parameters
        var file = new StreamWriter(fileName);
        file.WriteLine($"Dataset: {dbName}");
        file.WriteLine("Parameters:");
        file.WriteLine($"{_delta} \t(delta)");
        file.WriteLine($"{_epsilon} \t(epsilon)");
        file.WriteLine($"{_mu} \t(mu)");
        file.WriteLine($"D-index: {DIndex}");
        file.WriteLine($"VC-bound: {VCBound}");
        file.WriteLine($"Toivonen-bound: {ToivonenBound}");
        file.WriteLine($"VC Samples drawn # of times: {VcIterations}");
        file.WriteLine($"Toivonen Samples drawn # of times: {ToivonenIterations}");

        file.WriteLine("");

        // Print results of complete dataset
        file.WriteLine("Complete dataset mining:");
        file.WriteLine($"Original size of dataset: {_BaseMiningResult.Size}");
        file.WriteLine($"Increased to: {transactions.Count}");
        file.WriteLine($"Minimum frequency threshold: {_BaseMiningResult.Theta}");
        file.WriteLine($"FIs found: {_BaseMiningResult.FoundFrequentItemsets}");
        file.WriteLine($"total running time (Ms): {_BaseMiningResult.TotalRunningTime}");

        file.WriteLine("");

        // Print results of VC
        //file.WriteLine("VC mining:");
        //file.WriteLine($"Size of sample (avg): {_VcMiningResults.Average(item => item.Size)}");
        //var stdDevOfSizeVc = CalcStdDeviation(_VcMiningResults.Select(x => (double)x.Size));
        //file.WriteLine($"Deviation of dataset: {stdDevOfSizeVc}");
        //file.WriteLine($"Minimum frequency threshold: {_VcMiningResults.Average(item => item.Theta)}");
        //file.WriteLine($"FIs found: {_VcMiningResults.Average(item => item.FoundFrequentItemsets)}");
        //var stdDevOfFIsVc = CalcStdDeviation(_VcMiningResults.Select(x => (double)x.FoundFrequentItemsets));
        //file.WriteLine($"Deviation of FIs found: {stdDevOfFIsVc}");
        //file.WriteLine($"total running time (Ms) (avg): {_VcMiningResults.Average(item => item.TotalRunningTime)}");

        //file.WriteLine("");

        // Print results of Toivonen
        file.WriteLine("Toivonen mining:");
        file.WriteLine($"Size of sample (avg): {_ToivonenMiningResults.Average(item => item.Size)}");
        var stdDevOfSizeToivonen = CalcStdDeviation(_ToivonenMiningResults.Select(x => (double)x.Size));
        file.WriteLine($"Deviation of dataset: {stdDevOfSizeToivonen}");
        file.WriteLine($"Minimum frequency threshold: {_ToivonenMiningResults.Average(item => item.Theta)}");
        file.WriteLine($"FIs found: {_ToivonenMiningResults.Average(item => item.FoundFrequentItemsets)}");
        var stdDevOfFIsToivonen = CalcStdDeviation(_ToivonenMiningResults.Select(x => (double)x.FoundFrequentItemsets));
        file.WriteLine($"Deviation of FIs found: {stdDevOfFIsToivonen}");
        file.WriteLine($"total running time (Ms) (avg): {_ToivonenMiningResults.Average(item => item.TotalRunningTime)}");

        file.Close();
        Console.WriteLine($"Written results to {fileName}");
      }
      catch(Exception e)
      {
        Console.WriteLine(e.Message);
        Console.WriteLine(e.StackTrace);
      }
    }

    public static double CalcStdDeviation(IEnumerable<double> results)
    {
      var enumerable = results as double[] ?? results.ToArray();
      double avg = enumerable.Average();
      return Math.Sqrt(enumerable.Average(v => Math.Pow(v - avg, 2)));
    }

    private static int GetToivonenBound()
    {
      return (int)Math.Ceiling((1.0 / (2.0 * Math.Pow(_epsilon, 2))) * Math.Log(2.0 / _delta));
    }

    private static List<string[]> ReadTransactions()
    {
      List<string[]> transactions = new List<string[]>();
      ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["FileDB"];
      // get connection settings for File Type Database
      if(settings != null)
      {
        try
        {
          var file = new StreamReader($"{settings.ConnectionString}{_database}_original.dat");
          string line;
          while((line = file.ReadLine()) != null)
          {
            string[] tempItems = line.Split(' ');
            transactions.Add(tempItems);
          }

          file.Close(); // close file
        }
        catch(Exception e)
        {
          Console.WriteLine(e.Message);
          Console.WriteLine(e.StackTrace);
        }
      }
      return transactions;
    }

    private static void WriteTransactions(List<string[]> transactions)
    {
      var newDatabase = new List<string>();
      foreach(string[] transaction in transactions)
      {
        newDatabase.Add(string.Join(" ", transaction));
      }

      ConnectionStringSettings settings = ConfigurationManager.ConnectionStrings["FileDB"];
      // get connection settings for File Type Database
      if(settings != null)
      {
        try
        {
          File.WriteAllLines($"{settings.ConnectionString}{_database}.dat", newDatabase);
        }
        catch(Exception e)
        {
          Console.WriteLine(e.Message);
          Console.WriteLine(e.StackTrace);
        }
      }
    }

    private static List<string[]> CalcSample(List<string[]> transactions, int bound)
    {
      Random r = new Random(_randomSeed);
      List<string[]> sample = new List<string[]>();

      while(sample.Count < bound)
      {
        var chosenTransaction = transactions[r.Next(0, transactions.Count)];
        sample.Add(chosenTransaction);
      }

      return sample;
    }

    public static int CalcDIndex(List<string[]> transactions)
    {
      int dIndex = 1;
      var dictionary = new Dictionary<int, int>();
      foreach(string[] transaction in transactions)
      {
        string[] tempItems = transaction;
        //Console.WriteLine($"Length of transaction: {tempItems.Length}");
        int transactionLength = tempItems.Length;
        if(!dictionary.ContainsKey(transactionLength))
        {
          dictionary.Add(transactionLength, 1);
        }
        else
        {
          dictionary[transactionLength]++;
        }
        for(int y = transactionLength-1; y > 0; y--)
        {
          if (!dictionary.ContainsKey(y))
          {
            dictionary.Add(y, 0);
          }
          dictionary[y]++;
          if (y > dIndex)
          {
            if (dictionary[y] >= y)
            {
              dIndex = y;
              // Console.WriteLine($"D-index updated to: {y}");
            }
          }
        }

        if(dictionary[transactionLength] >= transactionLength)
        {
          if(transactionLength > dIndex)
          {
            dIndex = transactionLength;
            Console.WriteLine($"D-index updated to: {transactionLength}");
          }
        }
      }
      
      return dIndex;
    }


    public static int GetVcBound(List<string[]> transactions, int dIndex)
    {
      int a = transactions.Count;
      int b = (int)Math.Ceiling(((4*0.5) / Math.Pow(_epsilon, 2)) * (dIndex + Math.Log(1 / _delta)));

      if(a < b)
      {
        return a;
      }
      return b;
    }

    public static int GetDBoundSize(int dIndex)
    {
      return (int)Math.Ceiling(((4 * 0.5) / Math.Pow(_epsilon, 2)) * (dIndex + Math.Log(1 / _delta)));
    }
  }
}
