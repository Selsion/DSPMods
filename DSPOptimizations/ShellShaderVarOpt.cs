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
            [HarmonyPostfix]
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
        }
    }
}
