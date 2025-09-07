using System;
using System.Collections.Generic;
using System.Linq;
using RottingStorage.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace RottingStorage.Behavior;

public class BlockEntityBehaviorRottingStorage(BlockEntity blockEntity) : BlockEntityBehavior(blockEntity)
{
    public static string BehaviorTypeName => $"{RottingStorageCore.ModId}.{nameof(BlockEntityBehaviorRottingStorage)}";
    private IBlockEntityContainer? _container;
    private InventoryBase? _inventory;
    private bool ShouldRun => _container != null && _inventory != null;

    public override void Initialize(ICoreAPI api, JsonObject jsonProperties)
    {
        base.Initialize(api, jsonProperties);
        if (Block.GetInterface<IBlockEntityContainer>(blockEntity.Api.World, blockEntity.Pos)
            is not { Inventory: InventoryBase } blockEntityContainer)
            return;
        _container = blockEntityContainer;
        _inventory = _container.Inventory as InventoryBase;
        var resolved = 0;
        foreach (var ingredient in ConfigLoader.Config.RottenItemStacks)
        {
            var didResolve = ingredient.Resolve(api.World, nameof(Initialize));
            if (didResolve) resolved++;
        }
        if (resolved == 0)
        {
            RottingStorageCore.Logger?.Error($"RottingStorage behavior added to block {Block.Code} " +
                                             $"but none of the rotten items could be resolved. " +
                                             $"Check previous logs for details.");
            return;
        }
        if (resolved != ConfigLoader.Config.RottenItemStacks.Count)
        {
            var count = ConfigLoader.Config.RottenItemStacks.Count;
            RottingStorageCore.Logger?.Warning($"RottingStorage behavior added to block {Block.Code} " +
                                                $"but {count - resolved}/{count} items could not be resolved. " +
                                                $"Check previous logs for details.");
        }
        if (!ShouldRun)
        {
            RottingStorageCore.Logger?.Error($"RottingStorage behavior added to block {Block.Code} " +
                                             $"which is not a container or has no rotten items defined.");
            return;
        }
        if (_inventory != null) _inventory.OnAcquireTransitionSpeed += Inventory_OnAcquireTransitionSpeed;
    }

    private float Inventory_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float baseMul)
    {
        if (transType != EnumTransitionType.Perish || _container == null)
            return baseMul;
        var rottenCount = CountRottenStuff(_container, ConfigLoader.Config.RottenItemStacks);
        var baseRate = 1 + 0.1f;// ConfigLoader.Config.PerishRateIncreasePerItem;
        var rateIncrease = MathF.Pow(baseRate, rottenCount);
        RottingStorageCore.Logger?.Warning($"Perish rate increase: {{0}}x (base {{1}}x, {{2}} rotten items) at position {Pos} for {Block.Code}", rateIncrease, baseRate, rottenCount);
        return baseMul * rateIncrease;
    }

    private static int CountRottenStuff(IBlockEntityContainer blockEntityContainer, IReadOnlyList<JsonItemStack> rottenStacks)
    {
        if (blockEntityContainer.Inventory == null || rottenStacks.Count == 0) return 0;
        var total = 0;
        foreach (var slot in blockEntityContainer.Inventory)
        {
            var itemStack = slot?.Itemstack;
            if (itemStack?.Collectible == null) continue;
            if (rottenStacks.Any(ing => ing.ResolvedItemstack.Satisfies(itemStack)))
                total += itemStack.StackSize;
        }
        return total;
    }
}