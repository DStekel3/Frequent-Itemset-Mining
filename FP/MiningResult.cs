namespace FP
{
  public struct MiningResult
  {
    public int Size { get; set; }
    public int FoundFrequentItemsets { get; set; }
    public int DBound { get; set; }
    public double TotalRunningTime { get; set; }
    public double Theta { get; set; }
  }
}