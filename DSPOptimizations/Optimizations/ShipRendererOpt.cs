using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace DSPOptimizations
{
    // TODO: check compatibility after dark fog update
    //[RunPatches(typeof(Patch))]
    //[Optimization("ShipRendererOpt", "Reduces lag from idle vessels", false, new Type[] { })]
    class ShipRendererOpt : OptimizationSet
    {
        class Patch
        {
            [HarmonyTranspiler, HarmonyPatch(typeof(StationComponent), "ShipRenderersOnTick")]
            static IEnumerable<CodeInstruction> IdleShipPoseFix(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                LocalBuilder planetLoaded = generator.DeclareLocal(typeof(bool));

                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(Assert), nameof(Assert.Zero), new[] { typeof(int) }))
                ).Advance(1).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    Transpilers.EmitDelegate<Func<StationComponent, bool>>((StationComponent station) =>
                    {
                        var localPlanet = GameMain.data.localPlanet;
                        return localPlanet == null ? false : localPlanet.id == station.planetId;
                    }),
                    new CodeInstruction(OpCodes.Stloc_S, planetLoaded)
                );

                matcher.MatchForward(true,
                    new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(ShipRenderingData), nameof(ShipRenderingData.SetPose)))
                ).Advance(1).CreateLabel(out Label end).MatchBack(true,
                    new CodeMatch(OpCodes.Stfld, AccessTools.Field(typeof(ShipRenderingData), nameof(ShipRenderingData.gid)))
                ).Advance(-4);
                OpCode idxOpCode = matcher.Opcode;
                object idxOperand = matcher.Operand;

                matcher.Advance(1+4).CreateLabel(out Label poseCall).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldloc_S, planetLoaded),
                    new CodeInstruction(OpCodes.Brtrue, poseCall),
                    new CodeInstruction(OpCodes.Ldarg_0),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(StationComponent), nameof(StationComponent.shipRenderers))),
                    new CodeInstruction(idxOpCode, idxOperand),
                    new CodeInstruction(OpCodes.Ldelema, typeof(ShipRenderingData)),
                    new CodeInstruction(OpCodes.Ldflda, AccessTools.Field(typeof(ShipRenderingData), nameof(ShipRenderingData.pos))),
                    new CodeInstruction(OpCodes.Ldc_R4, 1e9f),
                    new CodeInstruction(OpCodes.Stfld, AccessTools.Field(typeof(Vector3), nameof(Vector3.x))),
                    new CodeInstruction(OpCodes.Br, end)
                );

                return matcher.InstructionEnumeration();
            }
        }
    }
}
