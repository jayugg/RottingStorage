using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using RottingStorage.Behavior;
using Vintagestory.API.Common;

namespace RottingStorage;

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public class RottingStorageCore : ModSystem
{
    public static ILogger? Logger { get; private set; }
    public static string? ModId { get; private set; }
    public static ICoreAPI? Api { get; private set; }
    private static Harmony? HarmonyInstance { get; set; }

    public override void StartPre(ICoreAPI api)
    {
        base.StartPre(api);
        Api = api;
        Logger = Mod.Logger;
        ModId = Mod.Info.ModID;
        HarmonyInstance = new Harmony(ModId);
        HarmonyInstance.PatchAll();
    }

    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterBlockEntityBehaviorClass(BlockEntityBehaviorRottingStorage.BehaviorTypeName,
            typeof(BlockEntityBehaviorRottingStorage));
    }

    public override void AssetsFinalize(ICoreAPI api)
    {
        base.AssetsFinalize(api);
        string behName = BlockEntityBehaviorRottingStorage.BehaviorTypeName;
        foreach (var block in api.World.Blocks.Where(b=> HasIBlockEntityContainer(b, api)) )
        {
            Logger?.Warning("Found container block: " + block.Code);
            if (block == null) continue;
            var beh = block.BlockEntityBehaviors;
            if (beh == null || beh.Length == 0)
            {
                Logger?.Warning("Adding behavior to block: " + block.Code);
                block.BlockEntityBehaviors =
                [
                    new BlockEntityBehaviorType { Name = behName }
                ];
                continue;
            }
            if (beh.Any(b => b?.Name == behName)) continue;
            Logger?.Warning("Adding behavior to block: " + block.Code);
            var list = beh.ToList();
            list.Add(new BlockEntityBehaviorType { Name = behName });
            block.BlockEntityBehaviors = list.ToArray();
        }
    }

    public override void Dispose()
    {
        HarmonyInstance?.UnpatchAll(ModId);
        HarmonyInstance = null;
        Logger = null;
        ModId = null;
        Api = null;
        base.Dispose();
    }
    
    private static bool HasIBlockEntityContainer(Block block, ICoreAPI api)
    {
        if (block?.EntityClass == null) return false;
        var beType = api.ClassRegistry.GetBlockEntity(block.EntityClass);
        return beType != null && typeof(IBlockEntityContainer).IsAssignableFrom(beType);
    }
}