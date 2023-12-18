using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace DSPOptimizations
{
    [RunPatches(typeof(Patch))]
    [Optimization("ShellShaderVarOpt", "Improves fps when rendering dyson shells", false, new Type[] { })]
    class ShellShaderVarOpt : OptimizationSet
    {
        class Patch
        {
            //[HarmonyPostfix]
            [HarmonyPatch(typeof(DysonShell), nameof(DysonShell.GenerateModelObjects))]
            public static void ShellMatInitPostfix(DysonShell __instance)
            {
                __instance.material.SetFloat("_State", __instance.state);
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(DysonSphere), nameof(DysonSphere.UpdateStates), new[] { typeof(DysonShell), typeof(uint), typeof(bool), typeof(bool) })]
            public static void UpdateStatePostfix(DysonShell shell)
            {
                shell.material?.SetFloat("_State", shell.state);
            }

            [HarmonyPostfix]
            [HarmonyPatch(typeof(DysonShell), nameof(DysonShell.Construct))]
            public static void UpdateCPProgressPostfix(DysonShell __instance)
            {
                __instance.SetMaterialDynamicVars();
            }

            [HarmonyTranspiler, HarmonyPatch(typeof(DysonSphereSegmentRenderer), "DrawModels")]
            static IEnumerable<CodeInstruction> RemoveShaderVarCall(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(DysonShell),  nameof(DysonShell.SetMaterialDynamicVars)))
                ).Advance(-1)
                .SetOpcodeAndAdvance(OpCodes.Nop)
                .SetOpcodeAndAdvance(OpCodes.Nop);

                return matcher.InstructionEnumeration();
            }

            [HarmonyTranspiler, HarmonyPatch(typeof(DysonShell), nameof(DysonShell.SetMaterialDynamicVars))]
            static IEnumerable<CodeInstruction> RemoveSetFloatCall(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(UnityEngine.Material), nameof(UnityEngine.Material.SetFloat), new[] { typeof(string), typeof(float) }))
                ).Advance(-7)
                .SetOpcodeAndAdvance(OpCodes.Nop)
                .RemoveInstructions(7);

                return matcher.InstructionEnumeration();
            }

            [HarmonyTranspiler]
            [HarmonyPatch(typeof(UIDEToolbox), nameof(UIDEToolbox.OnColorChange))]
            static IEnumerable<CodeInstruction> UpdateShellColourPatch1(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                // add a call to SetMaterialDynamicVars() after the colour is updated
                matcher.MatchForward(true,
                    new CodeMatch(OpCodes.Stfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.color)))
                ).Advance(-1).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Dup)
                ).Advance(2).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DysonShell), nameof(DysonShell.SetMaterialDynamicVars)))
                );

                return matcher.InstructionEnumeration();
            }

            [HarmonyTranspiler]
            [HarmonyPatch(typeof(UIDysonBrush_Paint), nameof(UIDysonBrush_Paint._OnUpdate))]
            static IEnumerable<CodeInstruction> UpdateShellColourPatch2(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                // add a call to SetMaterialDynamicVars() after the colour is updated
                matcher.MatchForward(true,
                    new CodeMatch(OpCodes.Stfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.color)))
                ).Advance(-12);

                var shellVarOpCode = matcher.Opcode;
                var shellVarOperand = matcher.Operand;

                matcher.Advance(13).InsertAndAdvance(
                    new CodeInstruction(shellVarOpCode, shellVarOperand),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(DysonShell), nameof(DysonShell.SetMaterialDynamicVars)))
                );

                return matcher.InstructionEnumeration();
            }

            // skips drawing a shell if it has no CPs. normally you'd see green in the editor for empty shells. maybe do this when not in the editor? TODO
            /*[HarmonyTranspiler, HarmonyPatch(typeof(DysonSphereSegmentRenderer), nameof(DysonSphereSegmentRenderer.DrawModels))]
            static IEnumerable<CodeInstruction> DysonSphereSegmentRenderer_DrawModels_Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                matcher.MatchForward(false, new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(DysonSphereLayer), nameof(DysonSphereLayer.shellPool))));
                matcher.Advance(4);
                OpCode shellOpCode = matcher.Opcode;
                var shellOperand = matcher.Operand;
                matcher.Advance(5);
                var label = matcher.Operand;

                matcher.Advance(1).InsertAndAdvance(
                    new CodeInstruction(shellOpCode, shellOperand),
                    new CodeInstruction(OpCodes.Ldfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.nodecps))),
                    new CodeInstruction(OpCodes.Dup),
                    new CodeInstruction(OpCodes.Ldlen),
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Sub),
                    new CodeInstruction(OpCodes.Ldelem_I4),
                    new CodeInstruction(OpCodes.Brfalse, label)
                );

                return matcher.InstructionEnumeration();
            }*/
        }
    }
}
