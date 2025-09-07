using System.Collections.Generic;
using Newtonsoft.Json;
using Vintagestory.API.Common;

namespace RottingStorage.Config;

public class ModConfig
{
    public List<JsonItemStack> RottenItemStacks { get; set; } =
    [
        new()
        {
            Type = EnumItemClass.Item,
            Code = new AssetLocation("game", "rot")
        }
    ];
    public float PerishRateIncreasePerItem { get; set; } = 0.01f;
}