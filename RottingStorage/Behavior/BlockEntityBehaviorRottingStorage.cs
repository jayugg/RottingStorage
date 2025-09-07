using System;
using System.Collections.Generic;
using System.Linq;
using RottingStorage.Config;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace RottingStorage.Behavior;

public class BlockEntityBehaviorRottingStorage(BlockEntity blockEntity) : BlockEntityBehavior(blockEntity)
{
    public static string BehaviorTypeName => $"{RottingStorageCore.ModId}.{nameof(BlockEntityBehaviorRottingStorage)}";
    private const string RottenStacksCacheKey = "rottingstorage:rottenstacks";
    private const string RottenStacksPropertyKey = "rottenStacks";
    private JsonItemStack[] RottenStacks { get; set; } = [];
    private IBlockEntityContainer? _container;
    private InventoryBase? _inventory;
    private bool ShouldRun => _container != null && _inventory != null;

    public override void Initialize(ICoreAPI api, JsonObject jsonProperties)
    {
        base.Initialize(api, jsonProperties);
        if (Block.GetInterface<IBlockEntityContainer>(Blockentity.Api.World, Blockentity.Pos)
            is not { Inventory: InventoryBase } blockEntityContainer)
            return;
        RottenStacks = ObjectCacheUtil.GetOrCreate(api, RottenStacksCacheKey,
            () => PopulateRottenStacks(api, jsonProperties));
        if (RottenStacks.Length == 0 ||
            RottenStacks.All(s => s.ResolvedItemstack == null)) return;
        _container = blockEntityContainer;
        _inventory = _container.Inventory as InventoryBase;
        if (ShouldRun)
        {
            if (_inventory != null) _inventory.OnAcquireTransitionSpeed += Inventory_OnAcquireTransitionSpeed;
            return;
        }
        RottingStorageCore.Logger?.Warning($"RottingStorage behavior added to block {Block.Code} at {Pos} " +
                                           $"which is not a container or has no valid inventory.");
    }
    
    private float Inventory_OnAcquireTransitionSpeed(EnumTransitionType transType, ItemStack stack, float baseMul)
    {
        if (transType != EnumTransitionType.Perish || _container == null)
            return baseMul;
        var (rottenCount, rottingCount) = CountRottenStuff(Blockentity.Api.World, _container, RottenStacks);
        var baseRottenRate = 1 + ConfigLoader.Config.PerishRateIncreasePerItem;
        var baseRottingRate = 1 + ConfigLoader.Config.PerishRateIncreasePerItem / 5;
        var rateIncrease = MathF.Pow(baseRottenRate, rottenCount) * MathF.Pow(baseRottingRate, rottingCount);
        RottingStorageCore.Logger?.Warning($"Perish rate increase: " +
                                           $"{rateIncrease}x (base {baseRottenRate}x, {rottenCount} rotten items, {rottingCount} rotting items) " +
                                           $"at position {Pos} for {Block.Code}");
        return baseMul * rateIncrease;
    }
    
    private static (int, int) CountRottenStuff(IWorldAccessor world, IBlockEntityContainer blockEntityContainer, IReadOnlyList<JsonItemStack> rottenStacks)
    {
        if (blockEntityContainer.Inventory == null || rottenStacks.Count == 0) return (0, 0);
        var totalRotten = 0;
        var totalRotting = 0;
        foreach (var slot in blockEntityContainer.Inventory)
        {
            var itemStack = slot?.Itemstack;
            if (itemStack?.Collectible == null) continue;
            // Handle containers so we can check their contents
            if (TryGetContents(world, itemStack, out var contents) && contents?.Length > 0)
                totalRotten += CountRottenStuff(contents, rottenStacks);
            else if (rottenStacks.Any(stack => stack.ResolvedItemstack.Satisfies(itemStack)))
                totalRotten += itemStack.StackSize;
            else if (!itemStack.Collectible.IsReasonablyFresh(world, slot?.Itemstack))
            {
                totalRotting += itemStack.StackSize;
            }
        }
        return (totalRotten, totalRotting);
    }
    
    private static int CountRottenStuff(ItemStack[] itemStacks, IReadOnlyList<JsonItemStack> rottenStacks)
    {
        var total = 0;
        foreach (var itemStack in itemStacks)
        {
            if (itemStack.Collectible == null) continue;
            if (rottenStacks.Any(stack => stack.ResolvedItemstack.Satisfies(itemStack)))
                total += itemStack.StackSize;
        }
        return total;
    }

    private static bool TryGetContents(IWorldAccessor world, ItemStack containerStack, out ItemStack[]? contents)
    {
        contents = [];
        switch (containerStack.Collectible)
        {
            case BlockContainer container:
                contents = container.GetContents(world, containerStack);
                return contents != null;
            case IBlockMealContainer blockMealContainer:
                contents = blockMealContainer.GetContents(world, containerStack);
                // ReSharper disable once ConditionIsAlwaysTrueOrFalseAccordingToNullableAPIContract
                return contents != null;
        }
        return false;
    }
    
    private static JsonItemStack[] PopulateRottenStacks(ICoreAPI api, JsonObject? jsonProperties)
    {
        var itemList = ConfigLoader.Config.RottenItemCodes.Split(',')
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(code => new JsonItemStack() { Type = EnumItemClass.Item, Code = code })
            .ToList();
        var blockList = ConfigLoader.Config.RottenBlockCodes.Split(',')
            .Where(s => !string.IsNullOrEmpty(s))
            .Select(code => new JsonItemStack() { Type = EnumItemClass.Block, Code = code })
            .ToList();
        var fromProperties = jsonProperties?[RottenStacksPropertyKey]?.AsArray<JsonItemStack>() ?? [];
        var rottenStacks = itemList.Concat(blockList).Concat(fromProperties).ToArray();
        if (rottenStacks.Length > 0)
        {
            ResolveRottenStacks(api, ref rottenStacks);
        }
        return rottenStacks;
    }

    private static void ResolveRottenStacks(ICoreAPI api, ref JsonItemStack[] itemStacks)
    {
        var resolved = 0;
        foreach (var jsonItemStack in itemStacks)
        {
            var didResolve = jsonItemStack.Resolve(api.World, nameof(Initialize));
            if (didResolve) resolved++;
        }
        if (resolved == 0)
        {
            RottingStorageCore.Logger?.Error($"[{nameof(BlockEntityBehaviorRottingStorage)}] Tried initialising behavior " +
                                             $"but none of the rotten items could be resolved. " +
                                             $"Check previous logs for details.");
            return;
        }
        if (resolved != itemStacks.Length)
        {
            var count = itemStacks.Length;
            RottingStorageCore.Logger?.Warning($"[{nameof(BlockEntityBehaviorRottingStorage)}] Tried initialising behavior " +
                                               $"but {count - resolved}/{count} items could not be resolved. " +
                                               $"Check previous logs for details.");
        }
        itemStacks = itemStacks.Where(stack => stack.ResolvedItemstack != null).ToArray();
        RottingStorageCore.Logger?.Notification($"[{nameof(BlockEntityBehaviorRottingStorage)}] Finished resolving {resolved} rotten stacks.");
    }
}