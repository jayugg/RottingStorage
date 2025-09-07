using System.Linq;
using HarmonyLib;
using JetBrains.Annotations;
using RottingStorage.Behavior;
using RottingStorage.Config;
using Vintagestory.API.Server;
using Vintagestory.API.Common;

namespace RottingStorage;

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public class RottingStorageCore : ModSystem
{
    public static ILogger? Logger { get; private set; }
    public static string? ModId { get; private set; }
    public static ICoreAPI? Api { get; private set; }
    private static Harmony? HarmonyInstance { get; set; }
    public override double ExecuteOrder() =>
        ConfigLoader.FixedExecuteOrder + 0.01;
    
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
        var addedCount = 0;
        var presentCount = 0;
        foreach (var block in api.World.Blocks.Where(b=> HasIBlockEntityContainer(b, api)) )
        {
            if (block == null) continue;
            var beh = block.BlockEntityBehaviors;
            if (beh == null || beh.Length == 0)
            {
                block.BlockEntityBehaviors =
                [
                    new BlockEntityBehaviorType { Name = behName }
                ];
                addedCount++;
                continue;
            }

            if (beh.Any(b => b?.Name == behName))
            {
                presentCount++;
                continue;
            };
            var list = beh.ToList();
            list.Add(new BlockEntityBehaviorType { Name = behName });
            block.BlockEntityBehaviors = list.ToArray();
            addedCount++;
        }
        Logger?.Notification($"Added {behName} behavior to {addedCount} blocks with IBlockEntityContainer, " +
                                    $"while {presentCount} blocks already had it.");
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