using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace DSPOptimizations
{
    // TODO: check compatibility after dark fog update
    //[RunPatches(typeof(Patch))] // TODO: check all this code again
    [Optimization("EntityNeedsOpt", "", false, new Type[] { })]
    class EntityNeedsOpt : OptimizationSet
    {
        class Patch
        {
            [HarmonyTranspiler, HarmonyPatch(typeof(AssemblerComponent), "InternalUpdate")] // correct
            static IEnumerable<CodeInstruction> NeedsDirtyPatch1(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                matcher.MatchForward(true,
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldc_I4_1),
                    new CodeMatch(OpCodes.Stfld, AccessTools.Field(typeof(AssemblerComponent), nameof(AssemblerComponent.replicating)))
                ).Advance(1).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(AssemblerComponent), nameof(AssemblerComponent.needsDirty)))
                );

                return matcher.InstructionEnumeration();
            }

            [HarmonyTranspiler, HarmonyPatch(typeof(UIAssemblerWindow), "OnManualServingContentChange")] // probably correct
            static IEnumerable<CodeInstruction> NeedsDirtyPatch2(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                matcher.End().MatchBack(true,
                    new CodeMatch(OpCodes.Ret)
                ).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(UIAssemblerWindow), nameof(UIAssemblerWindow.factorySystem))),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(FactorySystem), nameof(FactorySystem.assemblerPool))),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(UIAssemblerWindow), "get_assemblerId")),
                    new CodeInstruction(OpCodes.Ldelema, typeof(AssemblerComponent)),
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(AssemblerComponent), nameof(AssemblerComponent.needsDirty)))
                );

                return matcher.InstructionEnumeration();
            }

            [HarmonyPostfix, HarmonyPatch(typeof(PlanetFactory), "InsertInto")] // probably correct
            public static void NeedsDirtyPatch3(PlanetFactory __instance, int __result, int entityId)
            {
                if (__result == 0)
                    return;
                int assemblerId = __instance.entityPool[entityId].assemblerId;
                if (assemblerId > 0)
                    __instance.factorySystem.assemblerPool[assemblerId].needsDirty = true;
            }

            [HarmonyTranspiler, HarmonyPatch(typeof(PlanetFactory), "EntityFastFillIn")] // probably correct
            static IEnumerable<CodeInstruction> NeedsDirtyPatch4(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                matcher.MatchForward(true,
                    new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(AssemblerComponent), nameof(AssemblerComponent.requires)))
                ).Advance(-3).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlanetFactory), nameof(PlanetFactory.factorySystem))),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(FactorySystem), nameof(FactorySystem.assemblerPool))),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(PlanetFactory), nameof(PlanetFactory.entityPool))),
                    new CodeInstruction(OpCodes.Ldarg_1),
                    new CodeInstruction(OpCodes.Ldelema, typeof(EntityData)),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(EntityData), nameof(EntityData.assemblerId))),
                    new CodeInstruction(OpCodes.Ldelema, typeof(AssemblerComponent)),
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(AssemblerComponent), nameof(AssemblerComponent.needsDirty)))
                );
                //.InsertAndAdvance(
                //    new CodeInstruction(OpCodes.Dup),
                //    new CodeInstruction(OpCodes.Ldc_I4_1),
                //    new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(AssemblerComponent), nameof(AssemblerComponent.needsDirty)))
                //);

                return matcher.InstructionEnumeration();
            }

            [HarmonyPostfix] // correct
            [HarmonyPatch(typeof(AssemblerComponent), "Import")]
            [HarmonyPatch(typeof(AssemblerComponent), "SetEmpty")]
            [HarmonyPatch(typeof(AssemblerComponent), "SetRecipe")]
            public static void NeedsDirtyPatch5(ref AssemblerComponent __instance)
            {
                __instance.needsDirty = true;
            }
        }
    }
}
