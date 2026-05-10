using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace KilnShelves
{
    public class KilnShelvesModSystem : ModSystem
    {

        // Called on server and client
        // Useful for registering block/entity classes on both sides
        public override void Start(ICoreAPI api)
        {
            api.RegisterBlockClass("BlockKilnShelf", typeof(BlockKilnShelf));
            api.RegisterBlockEntityClass("BEKilnShelf", typeof(BlockEntityKilnShelf));
            api.RegisterCollectibleBehaviorClass("kilnshelves.Offhand", typeof(CollectibleBehaviorOffhand));
            api.RegisterCollectibleBehaviorClass("kilnshelves.StackShelf", typeof(CollectibleBehaviorStackShelf));
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            Mod.Logger.Notification("Hello from template mod server side: " + Lang.Get("kilnshelves:hello"));
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            Mod.Logger.Notification("Hello from template mod client side: " + Lang.Get("kilnshelves:hello"));
        }

    }
}
