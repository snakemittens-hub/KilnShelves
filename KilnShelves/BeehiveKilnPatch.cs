using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace KilnShelves;

public static class BeehiveKilnPatch
{
    public static ICoreAPI Api;

    public static void ApplyAll(Harmony harmony)
    {
        Apply(harmony, typeof(BlockEntityBeeHiveKiln), "OnServerTick3s", transpiler: nameof(KilnHeatDamageTranspiler));
    }
    private static void Apply(Harmony harmony, System.Type target, string function, string? prefix = null, string? postfix = null, string? transpiler = null)
    {
        MethodInfo h_target = AccessTools.Method(target, function);

        MethodInfo? h_prefix = prefix != null ? AccessTools.Method(typeof(BeehiveKilnPatch), prefix) : null;
        MethodInfo? h_postfix = postfix != null ? AccessTools.Method(typeof(BeehiveKilnPatch), postfix) : null;
        MethodInfo? h_transpiler = transpiler != null ? AccessTools.Method(typeof(BeehiveKilnPatch), transpiler) : null;

        harmony.Patch(h_target,
            prefix: h_prefix != null ? new HarmonyMethod(h_prefix) : null,
            postfix: h_postfix != null ? new HarmonyMethod(h_postfix) : null,
            transpiler: h_transpiler != null ? new HarmonyMethod(h_transpiler) : null);
    }

    private static IEnumerable<CodeInstruction> KilnHeatDamageTranspiler(IEnumerable<CodeInstruction> instructions)
    {
        var codes = new List<CodeInstruction>(instructions);
        var setBlock = AccessTools.Method(typeof(IBlockAccessor), "SetBlock", new[] {typeof(int), typeof(BlockPos)});
        var injectMethod = AccessTools.Method(typeof(BeehiveKilnPatch), nameof(InjectCustomHeatDamageUpdate));
        Api.Logger.Debug("[KilnShelves] BeehiveKiln Harmony patch started ");

        for (int i = 0; i < codes.Count; i++)
        {
            var code = codes[i];

            if (code.opcode == OpCodes.Callvirt && code.operand is MethodInfo mi && mi == setBlock)
            {
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(IBlockAccessor), "ExchangeBlock")); //swap SetBlock for ExchangeBlock to preserve blockentity information
                Api.Logger.Debug("[KilnShelves] BeehiveKiln Harmony patch applied");
            }
            else yield return code;

        }
    }

    public static void InjectCustomHeatDamageUpdate(IBlockAccessor blockAccessor, Block block, BlockPos pos)
    {
        if(blockAccessor.GetBlockEntity(pos) is BlockEntityKilnShelf)
            blockAccessor.ExchangeBlock(((CollectibleObject) Api.World.GetBlock(((RegistryObject)block).CodeWithVariant("state", "damaged"))).Id, pos);
        else blockAccessor.SetBlock(((CollectibleObject) Api.World.GetBlock(((RegistryObject)block).CodeWithVariant("state", "damaged"))).Id, pos);
    }
}
