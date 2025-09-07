namespace RottingStorage.Config;

public class ModConfig
{
    public string RottenItemCodes { get; set; } = "game:rot";
    public string RottenBlockCodes { get; set; } = "";
    public float PerishRateIncreasePerItem { get; set; } = 0.005f;
}