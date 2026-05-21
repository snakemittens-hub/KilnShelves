using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace KilnShelves;
public class KilnShelfRenderer: IRenderer
{
    /// <summary>
    /// This whole IRenderer is essentially the GroundStorageRenderer with minor changes to make it work with kilnshelf blocks.
    /// </summary>
    private readonly ICoreClientAPI capi;
    private readonly BlockEntityKilnShelf kilnShelf;
    public Matrixf ModelMat = new Matrixf();

    public double RenderOrder => 0.5;

    public int RenderRange => 30;

    private int[] itemTemps;
    private float accumDelta;
    private bool check500;
    private bool check450;
    public KilnShelfRenderer(ICoreClientAPI capi, BlockEntityKilnShelf kilnShelf)
    {
        this.capi = capi;
        this.kilnShelf = kilnShelf;
        capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque);
        itemTemps = new int[kilnShelf.Inventory.Count];
        UpdateTemps();
    }
    public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
    {
        accumDelta += deltaTime;
        var pos = capi.World.Player.Entity.Pos;
        var dist = kilnShelf.Pos.DistanceSqTo(pos.X, pos.Y, pos.Z);
        var outOfRange = RenderRange * RenderRange < dist;

        // update temp only every second
        if (accumDelta > 1)
        {
            UpdateTemps();
        }

        if (!kilnShelf.UseRenderer || kilnShelf.Inventory.Empty || outOfRange) return;

        var rpi = capi.Render;
        var camPos = capi.World.Player.Entity.CameraPos;

        var prog = rpi.PreparedStandardShader(kilnShelf.Pos.X, kilnShelf.Pos.Y, kilnShelf.Pos.Z);

        var offs = new Vec3f[kilnShelf.GetInvSlotCount()];
        kilnShelf.GetLayoutOffset(offs);
        var lightrgbs = capi.World.BlockAccessor.GetLightRGBs(kilnShelf.Pos.X, kilnShelf.Pos.Y, kilnShelf.Pos.Z);
        rpi.GlDisableCullFace();
        rpi.GlToggleBlend(true);

        prog.ViewMatrix = rpi.CameraMatrixOriginf;
        prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

        var meshes = kilnShelf.MeshRefs;
        for (var index = 0; index < meshes.Length; index++)
        {
            var stack = kilnShelf.Inventory[index]?.Itemstack;
            var meshRef = kilnShelf.MeshRefs[index];

            if (stack == null || meshRef == null || meshRef.Disposed) continue;


            var glowColor = ColorUtil.GetIncandescenceColorAsColor4f(itemTemps[index]);
            var gi = GameMath.Clamp((itemTemps[index] - 500) / 3, 0, 255);

            //Modified from GroundStorageRenderer to work with shelf offsets
            ModelMat
                .Identity()
                .Translate(kilnShelf.Pos.X - camPos.X, kilnShelf.Pos.Y - camPos.Y, kilnShelf.Pos.Z - camPos.Z)
                .Translate(0.5f, 0f, 0.5f)
                .RotateYDeg(kilnShelf.Block.Shape.rotateY)
                .Translate(offs[index].X, offs[index].Y, offs[index].Z)
                .Translate(-1f, 0f, -1f)
                ;

            var transform = kilnShelf.ModelTransformsRenderer[index];
            if (transform != null)
            {
                float dx = transform.Translation.X + transform.Origin.X;
                float dy = transform.Translation.Y + transform.Origin.Y;
                float dz = transform.Translation.Z + transform.Origin.Z;

                ModelMat
                    .Translate(dx, dy, dz)
                    .RotateDeg(transform.Rotation.ToVec3f())
                    .Scale(transform.ScaleXYZ.X, transform.ScaleXYZ.Y, transform.ScaleXYZ.Z)
                    .Translate(-transform.Origin.X, -transform.Origin.Y, -transform.Origin.Z)
                    ;
            }

            if (stack.Class == EnumItemClass.Item && (stack.Item.Shape == null || stack.Item.Shape.VoxelizeTexture))
            {
                ModelMat
                    .RotateX(GameMath.PIHALF)
                    .Scale(0.33f, 0.33f, 0.33f)
                    .Translate(0, -7.5f / 16f, 0f)
                    ;
            }

            prog.ModelMatrix = ModelMat.Values;

            prog.TempGlowMode = 1; // stack.ItemAttributes?["tempGlowMode"].AsInt() ?? 0;
            prog.RgbaLightIn = lightrgbs;
            prog.RgbaGlowIn = new Vec4f(glowColor[0], glowColor[1], glowColor[2], gi / 255f);
            prog.ExtraGlow = gi;
            prog.AverageColor = ColorUtil.ToRGBAVec4f(capi.BlockTextureAtlas.GetAverageColor((stack.Item?.FirstTexture ?? stack.Block.FirstTextureInventory).Baked.TextureSubId));

            rpi.RenderMultiTextureMesh(meshRef, "tex");
        }

        //After all shelf contents are rendered, render the shelf itself
        var shelfMesh = capi.TesselatorManager.GetDefaultBlockMeshRef(kilnShelf.Block);
        ModelMat
                .Identity()
                .Translate(kilnShelf.Pos.X - camPos.X, kilnShelf.Pos.Y - camPos.Y, kilnShelf.Pos.Z - camPos.Z)
                .Translate(0.5f, 0f, 0.5f)
                .Translate(-0.5f, 0f, -0.5f)
                ;
        prog.ModelMatrix = ModelMat.Values;

        //make shelf glow same color as hottest item in inventory
        var shelfGlowColor = ColorUtil.GetIncandescenceColorAsColor4f(itemTemps.Max());
        var shelfgi = GameMath.Clamp((itemTemps.Max() - 500) / 3, 0, 255);
        prog.RgbaGlowIn = new Vec4f(shelfGlowColor[0], shelfGlowColor[1], shelfGlowColor[2], shelfgi/255f);
        rpi.RenderMultiTextureMesh(shelfMesh, "tex");

        prog.TempGlowMode = 0;
        prog.Stop();
    }

    public void UpdateTemps()
    {
        accumDelta = 0;
        float maxTemp = 0;
        for (var index = 0; index < kilnShelf.Inventory.Count; index++)
        {
            var itemStack = kilnShelf.Inventory[index].Itemstack;
            itemTemps[index] = (int)(itemStack?.Collectible.GetTemperature(capi.World, itemStack) ?? 0f);
            maxTemp = Math.Max(maxTemp, itemTemps[index]);
        }

        // update to not use the custom renderer on next render
        if (!kilnShelf.NeedsRetesselation)
        {
            if (maxTemp < 500 && !check500)
            {
                check500 = true;
                kilnShelf.NeedsRetesselation = true;
                kilnShelf.MarkDirty(); // Allow the client to redraw itself automatically to avoid flicker
            }
            if (maxTemp < 450 && !check450)
            {
                check450 = true;
                kilnShelf.NeedsRetesselation = true;
                kilnShelf.MarkDirty(true);
            }
        }

        if (maxTemp > 500 && (check500 || check450))
        {
            check500 = false;
            check450 = false;
        }
    }

    public void Dispose()
    {
        capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
    }
}