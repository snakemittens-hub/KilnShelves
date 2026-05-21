using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;

namespace KilnShelves
{
    public class KilnShelvesModSystem : ModSystem
    {
        // Called on server and client
        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass("BlockKilnShelf", typeof(BlockKilnShelf));
            api.RegisterBlockEntityClass("BEKilnShelf", typeof(BlockEntityKilnShelf));
            api.RegisterCollectibleBehaviorClass("kilnshelves.Offhand", typeof(CollectibleBehaviorOffhand));
            api.RegisterCollectibleBehaviorClass("kilnshelves.StackShelf", typeof(CollectibleBehaviorStackShelf));
            api.Logger.Notification("Stackable Kiln Shelves Mod: Started.");
        }
    }
}
