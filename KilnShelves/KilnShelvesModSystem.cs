using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using HarmonyLib;
using Newtonsoft.Json.Linq;

namespace KilnShelves
{
    public class KilnShelvesModSystem : ModSystem
    {
        public static KilnShelvesConfig Config = new();
        private Harmony harmony;

        public override void Start(ICoreAPI api)
        {
            Config = api.LoadModConfig<KilnShelvesConfig>("kilnshelves.json") ?? new KilnShelvesConfig();
            api.StoreModConfig(Config, "kilnshelves.json");

            api.RegisterBlockClass("BlockKilnShelf", typeof(BlockKilnShelf));
            api.RegisterBlockEntityClass("BEKilnShelf", typeof(BlockEntityKilnShelf));
            api.RegisterCollectibleBehaviorClass("kilnshelves.Offhand", typeof(CollectibleBehaviorOffhand));
            api.RegisterCollectibleBehaviorClass("kilnshelves.StackShelf", typeof(CollectibleBehaviorStackShelf));
            BeehiveKilnPatch.Api = api;
            harmony = new Harmony(Mod.Info.ModID);
            BeehiveKilnPatch.ApplyAll(harmony);
            api.Logger.Notification("Stackable Kiln Shelves Mod: Started.");
        }

        public override void AssetsFinalize(ICoreAPI api)
        {
            if (Config.EnableShelfDamage) return;

            foreach (var block in api.World.Blocks)
            {
                if (block?.Code == null) continue;
                if (block.Code.Domain != "kilnshelves") continue;
                if (!block.Code.Path.Contains("kilnshelf")) continue;

                if (block.Attributes == null)
                {
                    block.Attributes = new JsonObject(JObject.Parse("{\"heatResistance\":1.0}"));
                }
                else
                {
                    block.Attributes.Token["heatResistance"] = 1.0;
                }
            }
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(Mod.Info.ModID);
            base.Dispose();
        }
    }
}
