namespace FP.DAL.Gateway.Interface
{
  public interface IOutputDatabaseHelper
  {
    string DatabaseType //indicates database type (TEXTFILE,SQL etc.)
    {
      get;
    }
    string DatabasePath //indicates database type (TEXTFILE,SQL etc.)
    {
      get;
    }
    //write final metrics to output
    void WriteAggregatedResult(IInputDatabaseHelper helper, string dbName, double minimumSupport, double totalFrequentItemSets, double totalRunningTimeMs, double epsilon, double delta, double mu);
  }
}
