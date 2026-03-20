namespace Conspectare.Services.Models;
public class MultiModelSettings
{
    public bool Enabled { get; set; }
    public List<string> Providers { get; set; } = new();
    public string ConsensusStrategy { get; set; } = "highest_confidence";
    public int TimeoutSeconds { get; set; } = 120;
}
