using System;
using System.Collections.Generic;
using FP.DAL.Gateway.Interface;
using System.IO;
using FP.DAL.DAO;

namespace FP.DAL.Gateway
{
  public class FileOutputDatabaseHelper : IOutputDatabaseHelper
  {
    public string DatabasePath { get; }

    public string DatabaseType { get; }

    public FileOutputDatabaseHelper(string outputFilePath)
    {
      if(outputFilePath == null) throw new ArgumentNullException(nameof(outputFilePath));
      DatabaseType = "File";
      DatabasePath = outputFilePath;
    }


    public void WriteAggregatedResult(IInputDatabaseHelper input, string dbName, double minimumSupport, double totalFrequentItemSets, double totalRunningTimeMs, double epsilon, double delta, double mu)
    {
      try
      {
        var baseName = dbName.Split('_')[0];
        string fileName = $"{DatabasePath}\\{baseName}\\{dbName}_{epsilon}_{delta}_{mu}.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(fileName));

        var file = new StreamWriter(fileName);
        file.WriteLine("Parameters:");
        file.WriteLine($"{delta} \t(delta)");
        file.WriteLine($"{epsilon} \t(epsilon)");
        file.WriteLine($"{mu} \t(mu)");
        file.WriteLine($"Size of dataset: {input.TotalTransactionNumber}");
        file.WriteLine($"{Math.Round(minimumSupport, 3)}\t(minimum support)");
        file.WriteLine($"{totalFrequentItemSets}\t\t(# FI's)");
        file.WriteLine($"{ (totalRunningTimeMs / 1000f)}\t(running time)");
        
        file.Close();
        Console.WriteLine($"Written results to {fileName}");
      }
      catch(Exception e)
      {
        Console.WriteLine(e.Message);
        Console.WriteLine(e.StackTrace);
      }
    }
  }
}
