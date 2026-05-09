using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;
using Vintagestory.Client.NoObf;
using Vintagestory.GameContent;

namespace KilnShelves;

public interface IShelvable
{
    public EnumShelvableLayout? GetShelvableType(ItemStack stack) => EnumShelvableLayout.Quadrants;
    public ModelTransform? GetOnShelfTransform(ItemStack stack) => null;
}
public class BlockEntityKilnShelf : BlockEntityGroundStorage
{
    protected override int invSlotCount => 8;
    public virtual int GetInvSlotCount()
    {
        return invSlotCount;
    }
    public override InventoryBase Inventory => inventory;
    public override string InventoryClassName => "kilnshelf";
    public override string AttributeTransformCode => "onshelfTransform";
    protected string GetSlotType(int slotid) => "shelf";
    private KilnShelfRenderer renderer;
    public new MultiTextureMeshRef[] MeshRefs = new MultiTextureMeshRef[8];
    public new ModelTransform[] ModelTransformsRenderer = new ModelTransform[8];
    public BlockEntityKilnShelf()
    {
        inventory = new InventoryGeneric(invSlotCount, "shelf-0", null, (id, inv) => new ItemSlotDisplay(inv, GetSlotType(id)));
    }
    public override void Initialize(ICoreAPI api)
    {
        base.Initialize(api);
        if (capi != null)
        {
            float temp = 0;
            if (!Inventory.Empty)
            {
                int index = 0;
                foreach (var slot in Inventory)
                {
                    if (slot.Itemstack is { } stack)
                    {
                        temp = Math.Max(temp, stack.Collectible.GetTemperature(capi.World, stack));

                        if (stack.Class == EnumItemClass.Block && stack.Block is IBlockMealContainer be and not BlockCrock) // Crocks don't render the actual meal mesh
                        {
                            GetOrCreateMealMesh(be, stack, index);   // pre-generate any meal meshes during initialization, as these might have to be uploaded to the GPU
                        }
                    }

                    index++;
                }

                if (temp >= 450)
                {
                    renderer = new KilnShelfRenderer(capi, this);
                }
            }
        }
    }

    //////
    /// Code modified from BlockEntityGroundStorage
    //////
    public override void CheckInventoryClearedMidTick() { return; } //BEGroundStorage destroys entity if inventory is cleared. We do not want this behavior.
    public new bool OnTryCreateKiln() { return false; } //Do not create kilns
    protected override void UpdateLegacyStorageLayouts() { return; } //Disable weird behavior with slots getting shoved around
    public new void OnTransformed(IWorldAccessor worldAccessor, ITreeAttribute tree, int degreeRotation, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping, EnumAxis? flipAxis)
    { return; }
    public override void DetermineStorageProperties(ItemSlot sourceSlot) 
    {
        ItemStack sourceStack = inventory.FirstNonEmptySlot?.Itemstack ?? sourceSlot?.Itemstack;

        var StorageProps = this.StorageProps;

        //this section being disabled breaks inventory rendering even though we're not using StorageProps.
        if (!forceStorageProps)
        {
            if (StorageProps == null)
            {
                if (sourceStack == null) return;

                StorageProps = this.StorageProps = sourceStack.Collectible?.GetBehavior<CollectibleBehaviorGroundStorable>()?.StorageProps;
            }
        }

        if (StorageProps == null) return;  // Seems necessary to avoid crash with certain items placed in game version 1.15-pre.1?
    }
    public override int DisplayedItems
    {
        get
        {
            return inventory.Count;
        }
    }
    public new void GetLayoutOffset(Vec3f[] offs)
    {
        for (int index = 0; index < invSlotCount; index++)
        {
            var shelvableType = GetShelvableLayout(inventory[index].Itemstack);

            float x = ((index % 4) >= 2) ? 12 / 16f : 4 / 16f;
            float y = index >= 4 ? 0.5f : 0f;
            float z = (index % 2 == 0) ? 4 / 16f : 12 / 16f;

            if (index is 0 or 4 && shelvableType is EnumShelvableLayout.SingleCenter) x = 0.5f;
            if (index is 0 or 2 or 4 or 6 && shelvableType is EnumShelvableLayout.Halves or EnumShelvableLayout.SingleCenter) z = 0.5f;

            offs[index] = new Vec3f(x, y, z);
        }
    }
    public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
    {
        float temp = 0;
        if (!Inventory.Empty)
        {
            foreach (var slot in Inventory)
            {
                temp = Math.Max(temp, slot.Itemstack?.Collectible.GetTemperature(capi.World, slot.Itemstack) ?? 0);
            }
        }

        UseRenderer = temp >= 500;
        // enable the renderer before we need it to avoid a frame where there is no mesh rendered (flicker)
        if (renderer == null && temp >= 450)
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                if (renderer == null)
                {
                    renderer = new KilnShelfRenderer(capi, this);
                }
            }, "kilnShelfRendererE");
        }
        if (UseRenderer)
        {
            updateMeshes();
            return true;
        }
        // once the items cools down again we can remove the renderer
        if (renderer != null && temp < 450)
        {
            capi.Event.EnqueueMainThreadTask(() =>
            {
                if (renderer != null)
                {
                    renderer.Dispose();
                    renderer = null;
                }
            }, "kilnShelfRendererD");
        }
        NeedsRetesselation = false;
        lock (inventoryLock)
        {
            return base.OnTesselation(mesher, tesselator);
        }
    }
    public override bool OnPlayerInteractStart(IPlayer player, BlockSelection bs)
    {
        ItemSlot slot = player.InventoryManager.ActiveHotbarSlot;

        if (TryUse(player, bs)) return true;
        else if (slot.Empty) return TryTake(player, bs);
        else if (GetShelvableLayout(slot.Itemstack) != null) return TryPut(player, bs);

        return false;
    }
    protected override void Dispose()
    {
        base.Dispose();
        renderer?.Dispose();
        //ambientSound?.Stop();
    }
    //////
    ///End code modified from BEGroundStorage
    //////

    //////
    ///Code imported/modified from BlockEntityShelf
    //////
    public static EnumShelvableLayout? GetShelvableLayout(ItemStack? stack)
    {
        if (stack == null) return null;

        var attr = stack.Collectible?.Attributes;
        var layout = stack.Collectible?.GetCollectibleInterface<IShelvable>()?.GetShelvableType(stack);

        layout ??= attr?["shelvable"].AsString() switch
        {
            "Quadrants" => EnumShelvableLayout.Quadrants,
            "Halves" => EnumShelvableLayout.Halves,
            "SingleCenter" => EnumShelvableLayout.SingleCenter,
            _ => null
        };

        layout ??= attr?["shelvable"].AsBool() == true ? EnumShelvableLayout.Quadrants : null;

        return layout;
    }
    public bool CanUse(ItemStack? stack, BlockSelection blockSel)
    {
        if (stack == null) return false;

        var obj = stack.Collectible;
        if (blockSel.SelectionBoxIndex == 4){
            return false;
        }

        bool up = blockSel.SelectionBoxIndex > 1;
        bool left = (blockSel.SelectionBoxIndex % 2) == 0;
        var shelvableLayout = GetShelvableLayout(inventory[up ? 4 : 0].Itemstack);
        if (shelvableLayout is not EnumShelvableLayout.SingleCenter)
        {
            if (!left) shelvableLayout = GetShelvableLayout(inventory[up ? 6 : 2].Itemstack);
        }

        int start = (up ? 4 : 0) + (shelvableLayout is EnumShelvableLayout.SingleCenter ? 0 : (left ? 0 : 2));
        int end = start + (shelvableLayout is EnumShelvableLayout.Halves or EnumShelvableLayout.SingleCenter ? 1 : 2);

        CollectibleObject invColl;
        for (int i = end - 1; i >= start; i--)
        {
            if (inventory[i].Empty) continue;

            invColl = inventory[i].Itemstack.Collectible;

            if (obj?.Attributes?["mealContainer"]?.AsBool() == true || obj is IContainedInteractable or IBlockMealContainer)
            {
                return invColl is BlockCookedContainerBase;
            }

            if (obj?.Attributes?["canSealCrock"]?.AsBool() == true)
            {
                return invColl is BlockCrock;
            }
        }

        return false;
    }

    public bool CanPlace(ItemStack? stack, BlockSelection blockSel, out bool canTake)
    {
        if (blockSel.SelectionBoxIndex == 4)
        {
            canTake = false;
            return false;
        }

        bool up = blockSel.SelectionBoxIndex > 1;
        bool left = (blockSel.SelectionBoxIndex % 2) == 0;

        if (GetShelvableLayout(inventory[up ? 4 : 0].Itemstack) is EnumShelvableLayout shelvableLayoutFullSlot &&
            (shelvableLayoutFullSlot is EnumShelvableLayout.SingleCenter || (shelvableLayoutFullSlot is EnumShelvableLayout.Halves && left)) ||
            (GetShelvableLayout(inventory[up ? 6 : 2].Itemstack) is EnumShelvableLayout.Halves && !left))
        {
            canTake = true;
            return false;
        }

        var shelvableLayout = GetShelvableLayout(stack);

        int start = (up ? 4 : 0) + (shelvableLayout is EnumShelvableLayout.SingleCenter ? 0 : (left ? 0 : 2));
        int end = start + (shelvableLayout is EnumShelvableLayout.Halves or EnumShelvableLayout.SingleCenter ? 1 : 2);

        canTake = false;
        bool canPlace = false;
        for (int i = end - 1; i >= start; i--)
        {
            if (inventory[i].Empty) canPlace = true;
            else canTake = true;
        }

        return canPlace;
    }

    private bool TryUse(IPlayer player, BlockSelection blockSel)
    {
        if (blockSel.SelectionBoxIndex == 4)
        {
            return false;
        }

        bool up = blockSel.SelectionBoxIndex > 1;
        bool left = (blockSel.SelectionBoxIndex % 2) == 0;
        var shelvableLayout = GetShelvableLayout(inventory[up ? 4 : 0].Itemstack);
        if (shelvableLayout is not EnumShelvableLayout.SingleCenter)
        {
            if (!left) shelvableLayout = GetShelvableLayout(inventory[up ? 6 : 2].Itemstack);
        }

        int start = (up ? 4 : 0) + (shelvableLayout is EnumShelvableLayout.SingleCenter ? 0 : (left ? 0 : 2));
        int end = start + (shelvableLayout is EnumShelvableLayout.Halves or EnumShelvableLayout.SingleCenter ? 1 : 2);

        if (player.Entity.Controls.ShiftKey) return false;

        for (int i = end - 1; i >= start; i--)
        {
            var collIci = inventory[i].Itemstack?.Collectible.GetCollectibleInterface<IContainedInteractable>();
            if (collIci != null)
            {
                if (collIci.OnContainedInteractStart(this, inventory[i], player, blockSel))
                {
                    MarkDirty();
                    return true;
                }
            }
        }

        return false;
    }

    private bool TryPut(IPlayer byPlayer, BlockSelection blockSel)
    {
        if (blockSel.SelectionBoxIndex == 4)
        {
            return false;
        }

        var heldSlot = byPlayer.InventoryManager.ActiveHotbarSlot;

        bool up = blockSel.SelectionBoxIndex > 1;
        bool left = (blockSel.SelectionBoxIndex % 2) == 0;

        int filledSlots = 0;
        var shelvableLayout = GetShelvableLayout(heldSlot.Itemstack);

        int start = (up ? 4 : 0) + (shelvableLayout is EnumShelvableLayout.SingleCenter ? 0 : (left ? 0 : 2));
        int end = start + (shelvableLayout is EnumShelvableLayout.SingleCenter ? 4 : 2);

        if (shelvableLayout is EnumShelvableLayout.Halves or EnumShelvableLayout.SingleCenter)
        {
            for (int i = start; i < end; i++)
            {
                if (!inventory[i].Empty)
                {
                    var layout = GetShelvableLayout(inventory[i].Itemstack);
                    filledSlots += layout is EnumShelvableLayout.SingleCenter ? 4 : layout is EnumShelvableLayout.Halves ? 2 : 1;
                }
            }
        }

        if (filledSlots > 0 && filledSlots < (shelvableLayout is EnumShelvableLayout.SingleCenter ? 4 : 2))
        {
            (Api as ICoreClientAPI)?.TriggerIngameError(this, "needsmorespace", Lang.Get("shelfhelp-needsmorespace-error"));
            return false;
        }

        if (shelvableLayout is not EnumShelvableLayout.SingleCenter) shelvableLayout = GetShelvableLayout(inventory[up ? 4 : 0].Itemstack);
        if (shelvableLayout is not EnumShelvableLayout.SingleCenter && !left) shelvableLayout = GetShelvableLayout(inventory[up ? 6 : 2].Itemstack);

        start = (up ? 4 : 0) + (shelvableLayout is EnumShelvableLayout.SingleCenter ? 0 : (left ? 0 : 2));
        end = start + (shelvableLayout is EnumShelvableLayout.Halves or EnumShelvableLayout.SingleCenter ? 1 : 2);

        for (int i = start; i < end; i++)
        {
            if (!inventory[i].Empty) continue;

            int moved = heldSlot.TryPutInto(Api.World, inventory[i]);
            MarkDirty();
            (Api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);

            if (moved > 0)
            {
                Api.World.PlaySoundAt(inventory[i].Itemstack?.Block?.Sounds?.Place ?? GlobalConstants.DefaultBuildSound, byPlayer.Entity, byPlayer);
                Api.World.Logger.Audit("{0} Put 1x{1} into Shelf index {3} at {2}.",
                    byPlayer.PlayerName,
                    inventory[i].Itemstack?.Collectible.Code,
                    Pos,
                    i
                );
                return true;
            }

            return false;
        }

        (Api as ICoreClientAPI)?.TriggerIngameError(this, "shelffull", Lang.Get("shelfhelp-shelffull-error"));
        return false;
    }

    private bool TryTake(IPlayer byPlayer, BlockSelection blockSel)
    {
        if (blockSel.SelectionBoxIndex == 4)
        {
            return false;
        }

        bool up = blockSel.SelectionBoxIndex > 1;
        bool left = (blockSel.SelectionBoxIndex % 2) == 0;
        var shelvableLayout = GetShelvableLayout(inventory[up ? 4 : 0].Itemstack);
        if (shelvableLayout is not EnumShelvableLayout.SingleCenter)
        {
            if (!left) shelvableLayout = GetShelvableLayout(inventory[up ? 6 : 2].Itemstack);
        }

        int start = (up ? 4 : 0) + (shelvableLayout is EnumShelvableLayout.SingleCenter ? 0 : (left ? 0 : 2));
        int end = start + (shelvableLayout is EnumShelvableLayout.SingleCenter ? 4 : 2);

        for (int i = end - 1; i >= start; i--)
        {
            if (inventory[i].Empty) continue;

            ItemStack? stack = inventory[i].TakeOut(1);
            if (byPlayer.InventoryManager.TryGiveItemstack(stack))
            {
                SoundAttributes? sound = stack?.Block?.Sounds?.Place;
                Api.World.PlaySoundAt(sound ?? GlobalConstants.DefaultBuildSound, byPlayer.Entity, byPlayer);
            }

            if (stack?.StackSize > 0)
            {
                Api.World.SpawnItemEntity(stack, Pos);
            }
            Api.World.Logger.Audit("{0} Took 1x{1} from Shelf index {3} at {2}.",
                byPlayer.PlayerName,
                stack?.Collectible.Code,
                Pos,
                i
            );

            (Api as ICoreClientAPI)?.World.Player.TriggerFpAnimation(EnumHandInteract.HeldItemInteract);
            MarkDirty();

            return true;
        }

        return false;
    }
    protected override float[][] genTransformationMatrices()
    {
        float[][] tfMatrices = new float[invSlotCount][];

        Vec3f[] offs = new Vec3f[DisplayedItems];
        lock (inventoryLock)
        {
            GetLayoutOffset(offs);
        }

        for (int i = 0; i < invSlotCount; i++)
        {
            Vec3f off = offs[i];
            tfMatrices[i] =
                new Matrixf()
                .Translate(0.5f, 0, 0.5f)
                .RotateYDeg(Block.Shape.rotateY)
                .Translate(off.X - 0.5f, off.Y, off.Z - 0.5f)
                .Translate(-0.5f, 0, -0.5f)
                .Values
            ;
        }

        return tfMatrices;
    }
    #region Block info

    public override void GetBlockInfo(IPlayer forPlayer, StringBuilder sb)
    {
        base.GetBlockInfo(forPlayer, sb);


        float ripenRate = GameMath.Clamp(((1 - container.GetPerishRate()) - 0.5f) * 3, 0, 1);
        if (ripenRate > 0)
        {
            sb.Append(Lang.Get("Suitable spot for food ripening."));
        }

        sb.AppendLine();

        bool up = forPlayer.CurrentBlockSelection != null && forPlayer.CurrentBlockSelection.SelectionBoxIndex > 1;

        for (int j = 3; j >= 0; j--)
        {
            int i = j + (up ? 4 : 0);
            i ^= 2;   //Display shelf contents text for items from left-to-right, not right-to-left

            if (inventory[i].Empty) continue;

            ItemStack? stack = inventory[i].Itemstack;

            var transitionableProps = stack?.Collectible?.GetTransitionableProperties(Api.World, stack, forPlayer.Entity);
            if (transitionableProps != null && transitionableProps.Length > 0)
            {
                sb.Append(PerishableInfoCompact(Api, inventory[i], ripenRate));
            }
            else
            {
                sb.AppendLine(stack?.Collectible.GetCollectibleInterface<IContainedCustomName>()?.GetContainedInfo(inventory[i]) ?? stack?.GetName() ?? Lang.Get("unknown"));
            }
        }
    }

    public static string PerishableInfoCompact(ICoreAPI Api, ItemSlot contentSlot, float ripenRate, bool withStackName = true)
    {
        if (contentSlot.Empty) return "";

        StringBuilder dsc = new StringBuilder();

        if (withStackName)
        {
            dsc.Append(contentSlot.Itemstack.GetName());
        }

        TransitionState[]? transitionStates = contentSlot.Itemstack.Collectible.UpdateAndGetTransitionStates(Api.World, contentSlot);
        if (transitionStates == null) return dsc.ToString();

        bool nowSpoiling = false;
        bool appendLine = false;
        for (int i = 0; i < transitionStates.Length; i++)
        {
            TransitionState state = transitionStates[i];

            TransitionableProperties prop = state.Props;
            float perishRate = contentSlot.Itemstack.Collectible.GetTransitionRateMul(Api.World, contentSlot, prop.Type);

            if (perishRate <= 0) continue;

            float transitionLevel = state.TransitionLevel;
            float freshHoursLeft = state.FreshHoursLeft / perishRate;

            switch (prop.Type)
            {
                case EnumTransitionType.Perish:

                    appendLine = true;

                    if (transitionLevel > 0)
                    {
                        nowSpoiling = true;
                        dsc.Append(", " + Lang.Get("{0}% spoiled", (int)Math.Round(transitionLevel * 100)));
                    }
                    else
                    {
                        double hoursPerday = Api.World.Calendar.HoursPerDay;

                        if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                        {
                            dsc.Append(", " + Lang.Get("fresh for {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                        }
                        else if (freshHoursLeft > hoursPerday)
                        {
                            dsc.Append(", " + Lang.Get("fresh for {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                        }
                        else
                        {
                            dsc.Append(", " + Lang.Get("fresh for {0} hours", Math.Round(freshHoursLeft, 1)));
                        }
                    }
                    break;

                case EnumTransitionType.Ripen:
                    if (nowSpoiling) break;

                    appendLine = true;

                    if (transitionLevel > 0)
                    {
                        dsc.Append(", " + Lang.Get("{1:0.#} days left to ripen ({0}%)", (int)Math.Round(transitionLevel * 100), (state.TransitionHours - state.TransitionedHours) / Api.World.Calendar.HoursPerDay / ripenRate));
                    }
                    else
                    {
                        double hoursPerday = Api.World.Calendar.HoursPerDay;

                        if (freshHoursLeft / hoursPerday >= Api.World.Calendar.DaysPerYear)
                        {
                            dsc.Append(", " + Lang.Get("will ripen in {0} years", Math.Round(freshHoursLeft / hoursPerday / Api.World.Calendar.DaysPerYear, 1)));
                        }
                        else if (freshHoursLeft > hoursPerday)
                        {
                            dsc.Append(", " + Lang.Get("will ripen in {0} days", Math.Round(freshHoursLeft / hoursPerday, 1)));
                        }
                        else
                        {
                            dsc.Append(", " + Lang.Get("will ripen in {0} hours", Math.Round(freshHoursLeft, 1)));
                        }
                    }
                    break;
            }
        }

        if (appendLine) dsc.AppendLine();

        return dsc.ToString();
    }

    #endregion
    //////
    ///End code from BEShelf
    //////

    //////
    ///Code imported/modified from BEContainerDisplay to bypass  BEGroundStorage override
    //////
    protected override MeshData getOrCreateMesh(ItemSlot slot, int index)
    {
        var stack = slot.Itemstack;
        if (stack.Class == EnumItemClass.Block)
        {
            if (stack.Block is IBlockMealContainer be and not BlockCrock) // Crocks don't render the actual meal mesh
            {
                GetOrCreateMealMesh(be, stack, index);
            }
            else
            {
                MeshRefs[index] = capi.TesselatorManager.GetDefaultBlockMeshRef(stack.Block);
            }
        }

        MeshData mesh = getMesh(slot);
        if (mesh != null) return mesh;
        CompositeShape customShape = stack.Collectible.Attributes?["displayedShape"].AsObject<CompositeShape>(null, stack.Collectible.Code.Domain);
        if (customShape != null)
        {
            string customkey = "displayedShape-" + customShape.ToString();
            mesh = ObjectCacheUtil.GetOrCreate(capi, customkey, () =>
                capi.TesselatorManager.CreateMesh(
                    "displayed item shape",
                    customShape,
                    (shape, name) => new ContainedTextureSource(capi, capi.BlockTextureAtlas, shape.Textures, string.Format("For displayed item {0}", stack.Collectible.Code)),
                    null
            ));
        }
        else
        {
            IContainedMeshSource meshSource = stack.Collectible?.GetCollectibleInterface<IContainedMeshSource>();

            if (meshSource != null)
            {
                mesh = meshSource.GenMesh(slot, capi.BlockTextureAtlas, Pos);
            }
        }

        if (mesh == null)
        {
            mesh = getDefaultMesh(stack);
        }

        applyDefaultTranforms(stack, mesh);

        string key = getMeshCacheKey(slot);
        MeshCache[key] = mesh;

        if (stack.Collectible.Attributes?[AttributeTransformCode].Exists == true)
        {
            var transform = stack.Collectible.Attributes?[AttributeTransformCode].AsObject<ModelTransform>();
            ModelTransformsRenderer[index] = transform;
        }
        else
        {
            ModelTransformsRenderer[index] = null;
        }

        return mesh;
    }
    //////
    ///End code from BEContainerDisplay
    //////
}
