using Vintagestory.API.Common;
using HarmonyLib;

namespace KilnShelves
{
    public class KilnShelvesModSystem : ModSystem
    {
        private Harmony harmony;

        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass("BlockKilnShelf", typeof(BlockKilnShelf));
            api.RegisterBlockEntityClass("BEKilnShelf", typeof(BlockEntityKilnShelf));
            api.RegisterCollectibleBehaviorClass("kilnshelves.Offhand", typeof(CollectibleBehaviorOffhand));
            api.RegisterCollectibleBehaviorClass("kilnshelves.StackShelf", typeof(CollectibleBehaviorStackShelf));
            BeehiveKilnPatch.Api = api;
            harmony = new Harmony(Mod.Info.ModID);
            BeehiveKilnPatch.ApplyAll(harmony);
            api.Logger.Notification("Stackable Kiln Shelves Mod: Started.");
        }

        public override void Dispose()
        {
            harmony?.UnpatchAll(Mod.Info.ModID);
            base.Dispose();
        }

    }
}
