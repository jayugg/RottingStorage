using System;
using JetBrains.Annotations;
using Vintagestory.API.Common;

namespace RottingStorage.Config;

[UsedImplicitly(ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
public class ConfigLoader : ModSystem
{
    public const double FixedExecuteOrder = 0.1;
    private const string ConfigName = "RottingStorage.json";
    private static ModConfig? _config;
    public static ModConfig Config => _config ??= new ModConfig();
    public override double ExecuteOrder() => FixedExecuteOrder;
    public override void StartPre(ICoreAPI api)
    {
        try
        {
            _config = api.LoadModConfig<ModConfig>(ConfigName);
            if (_config == null)
            {
                _config = new ModConfig();
                Mod.Logger.VerboseDebug("Config file not found, creating a new one...");
            }

            api.StoreModConfig(_config, ConfigName);
        }
        catch (Exception e)
        {
            Mod.Logger.Error("Failed to load config, you probably made a typo: {0}", e);
            _config = new ModConfig();
        }
    }

    public override void Dispose()
    {
        _config = null;
        base.Dispose();
    }
}