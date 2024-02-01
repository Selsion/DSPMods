using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using HarmonyLib;

namespace DSPOptimizations
{
    [RunPatches(typeof(Patch))]
    class LowResShellsLegacySupport// : OptimizationSet
    {
        private static List<DysonShell> shellsToUpdate;

        public static void UpdateShells()
        {
            if (shellsToUpdate == null)
                return;

            foreach(DysonShell shell in shellsToUpdate) {
                double[] tempCP = new double[shell.nodes.Count];
                for (int i = 0; i < shell.nodes.Count; i++)
                    tempCP[i] = (double)shell.nodecps[i] / (double)((shell.vertsqOffset[i + 1] - shell.vertsqOffset[i]) * 2);
                shell.vertsqOffset = null;
                shell.nodecps = null;
                shell.GenerateGeometry();
                int totalCP = 0;
                shell.nodecps = new int[shell.nodes.Count + 1];
                for (int i = 0; i < shell.nodes.Count; i++)
                {
                    shell.nodecps[i] = (int)(tempCP[i] * (double)((shell.vertsqOffset[i + 1] - shell.vertsqOffset[i]) * shell.cpPerVertex) + 0.1);
                    totalCP += shell.nodecps[i];
                }
                shell.nodecps[shell.nodes.Count] = totalCP;

                shell.GenerateModelObjects();
            }

            shellsToUpdate = null;
        }

        class Patch
        {
            [HarmonyPrefix]
            [HarmonyPatch(typeof(GameData), "Import")]
            public static void ResetShellUpdateListPatch()
            {
                shellsToUpdate = new List<DysonShell>();
            }

            [HarmonyTranspiler, HarmonyPatch(typeof(DysonShell), "Import")]
            static IEnumerable<CodeInstruction> ShellUpdateTranspiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
            {
                CodeMatcher matcher = new CodeMatcher(instructions, generator);

                /*matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(DysonShell), nameof(DysonShell.GenerateModelObjects)))
                ).Advance(1);*/
                /*matcher.End();
                Label methodEnd = (Label)matcher.Operand;*/

                matcher.Start();
                matcher.MatchForward(false,
                    new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(DysonShell), nameof(DysonShell.GenerateGeometry)))
                ).MatchForward(false,
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Ldnull),
                    new CodeMatch(OpCodes.Stfld, AccessTools.Field(typeof(DysonShell), nameof(DysonShell.vertsqOffset)))
                ).RemoveInstructions(6)
                .MatchForward(false,
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(DysonShell), nameof(DysonShell.GenerateGeometry)))
                ).InsertAndAdvance(
                    new CodeInstruction(OpCodes.Ldarg_0),
                    Transpilers.EmitDelegate<Action<DysonShell>>((DysonShell shell) =>
                    {
                        shellsToUpdate.Add(shell);
                    }),
                    //new CodeInstruction(OpCodes.Br, methodEnd)
                    new CodeInstruction(OpCodes.Ret)
                );

                return matcher.InstructionEnumeration();
            }
        }
    }
}
