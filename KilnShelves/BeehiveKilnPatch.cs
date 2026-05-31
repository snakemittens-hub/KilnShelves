using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace KilnShelves;

public static class BeehiveKilnPatch
{
    public static ICoreAPI Api;

    public static void ApplyAll(Harmony harmony)
    {
        //Target line is present as a lambda function passed to WalkMatchingBlocks compiled into inner class BlockEntityBeehiveKiln/'<>c__DisplayClass24_0'
        var constructedType = AccessTools.FirstInner(typeof(BlockEntityBeeHiveKiln), FindInnerClass);
        Apply(harmony, constructedType, "<OnServerTick3s>b__0", transpiler: nameof(KilnHeatDamageTranspiler));
    }

    private static bool FindInnerClass(Type typeToSearch)
    {
        return typeToSearch.Name.Contains("DisplayClass24");
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
        var setBlock = AccessTools.Method(typeof(IBlockAccessor), nameof(IBlockAccessor.SetBlock), parameters: new[] {typeof(int), typeof(BlockPos)});
        //var injectMethod = AccessTools.Method(typeof(BeehiveKilnPatch), nameof(InjectCustomHeatDamageUpdate));
        //Api.Logger.Debug("[KilnShelves] BeehiveKiln Harmony patch started ");

        for (int i = 0; i < codes.Count; i++)
        {
            var code = codes[i];

            if ((code.opcode == OpCodes.Callvirt || code.opcode == OpCodes.Call) && code.operand is MethodInfo mi && mi == setBlock)
            {
                //swap SetBlock for ExchangeBlock to preserve blockentity information
                yield return new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(IBlockAccessor), nameof(IBlockAccessor.ExchangeBlock)));
                Api.Logger.Debug("[KilnShelves] BeehiveKiln Harmony patch applied");
            }
            else yield return code;

        }
    }

    //public static void InjectCustomHeatDamageUpdate(IBlockAccessor blockAccessor, Block block, BlockPos pos, bool StructureComplete)
    //{
    //    if (blockAccessor.GetBlockEntity(pos) is BlockEntityKilnShelf)
    //        blockAccessor.ExchangeBlock(((CollectibleObject)Api.World.GetBlock(((RegistryObject)block).CodeWithVariant("state", "damaged"))).Id, pos);
    //    else
    //    {
    //        blockAccessor.SetBlock(((CollectibleObject)Api.World.GetBlock(((RegistryObject)block).CodeWithVariant("state", "damaged"))).Id, pos);
    //        StructureComplete = false;
    //    }
    //}
}
