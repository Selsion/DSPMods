using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DSPOptimizations
{
	[RunPatches(typeof(Patch))]
	[Optimization("SailBulletOpt", "Optimizes logic for ejected sails", false, new Type[] { })]
	class SailBulletOpt : OptimizationSet
    {

		private static ConfigEntry<bool> ejectedSailsVisible;

		public override void Init(BaseUnityPlugin plugin)
		{
			ejectedSailsVisible = plugin.Config.Bind<bool>("Spheres and Swarms", "EjectedSailsVisible", true, "Set to false to hide sails being ejected, which can improve UPS");
		}

		private static bool SailBulletsVisible(DysonSwarm swarm)
        {
			if (swarm == null)
				return false;

			if (ejectedSailsVisible.Value == false)
				return false;

			var data = GameMain.data;
			var uiGame = UIRoot.instance.uiGame;
			var dysonEditor = uiGame.dysonEditor;

            switch (DysonSphere.renderPlace)
            {
				case ERenderPlace.Universe:
					return data.localStar == swarm.starData;
				case ERenderPlace.Starmap:
					return !UIStarmap.isChangingToMilkyWay && uiGame.starmap.viewStarSystem == swarm.starData;
				case ERenderPlace.Dysonmap:
					return dysonEditor.selection.viewDysonSphere == swarm.dysonSphere;
				default:
					return true;
            }
        }

		class Patch
        {
			[HarmonyTranspiler, HarmonyPatch(typeof(DysonSwarm), "GameTick")]
			static IEnumerable<CodeInstruction> SailBulletBufferPatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
			{
				CodeMatcher matcher = new CodeMatcher(instructions, generator);

				LocalBuilder sailsVisibleVar = generator.DeclareLocal(typeof(bool));

				// set sailsVisibleVar before the for loop
				matcher.MatchForward(true,
					new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(GameHistoryData), nameof(GameHistoryData.solarSailLife))),
					new CodeMatch(OpCodes.Ldc_R4),
					new CodeMatch(OpCodes.Mul),
					new CodeMatch(OpCodes.Ldc_R4),
					new CodeMatch(OpCodes.Add)
				).Advance(11).InsertAndAdvance(
					new CodeInstruction(OpCodes.Ldarg_0),
					new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(SailBulletOpt), nameof(SailBulletOpt.SailBulletsVisible))),
					new CodeInstruction(OpCodes.Stloc_S, sailsVisibleVar)
				);

				// get the label for the end of the for loop
				matcher.MatchForward(false,
					new CodeMatch(OpCodes.Ldloc_S),
					new CodeMatch(OpCodes.Ldc_I4_1),
					new CodeMatch(OpCodes.Add),
					new CodeMatch(OpCodes.Stloc_S),
					new CodeMatch(OpCodes.Ldloc_S),
					new CodeMatch(OpCodes.Ldarg_0),
					new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(DysonSwarm), nameof(DysonSwarm.bulletCursor))),
					new CodeMatch(OpCodes.Blt)
				).CreateLabel(out Label loopEnd);

				// add a jump to the end of the foor loop before the buffer position stuff, if the sails aren't visible
				matcher.Start().MatchForward(true,
					new CodeMatch(OpCodes.Ldelema),
					new CodeMatch(OpCodes.Ldc_I4_0),
					new CodeMatch(OpCodes.Stfld),
					new CodeMatch(OpCodes.Ldsfld, AccessTools.Field(typeof(DysonSphere), nameof(DysonSphere.renderPlace)))
				).SetOpcodeAndAdvance(OpCodes.Nop) // disable this instruction and add it later, since it has a label attached to it
				.InsertAndAdvance(
					new CodeInstruction(OpCodes.Ldloc_S, sailsVisibleVar),
					new CodeInstruction(OpCodes.Brfalse_S, loopEnd),
					new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(DysonSphere), nameof(DysonSphere.renderPlace)))
				);

				matcher.MatchForward(true,
					new CodeMatch(OpCodes.Ldarg_0),
					new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(DysonSwarm), nameof(DysonSwarm.bulletBuffer))),
					new CodeMatch(OpCodes.Ldarg_0),
					new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(DysonSwarm), nameof(DysonSwarm.bulletPool))),
					new CodeMatch(OpCodes.Callvirt, AccessTools.Method(typeof(ComputeBuffer), nameof(ComputeBuffer.SetData), new[] { typeof(System.Array) }))
				).Advance(1).CreateLabel(out Label methodEnd).Advance(-5)
				.SetOpcodeAndAdvance(OpCodes.Nop) // there's a label here, so move the instruction
				.InsertAndAdvance(
					new CodeInstruction(OpCodes.Ldloc_S, sailsVisibleVar),
					new CodeInstruction(OpCodes.Brfalse_S, methodEnd),
					new CodeInstruction(OpCodes.Ldarg_0)
				);

				return matcher.InstructionEnumeration();
			}

			[HarmonyTranspiler, HarmonyPatch(typeof(DysonSwarm), "DrawPost")]
			static IEnumerable<CodeInstruction> SailBulletHidePatch(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
			{
				CodeMatcher matcher = new CodeMatcher(instructions, generator);

				matcher.MatchForward(false,
					new CodeMatch(OpCodes.Ldarg_0),
					new CodeMatch(OpCodes.Call, AccessTools.Method(typeof(DysonSwarm), nameof(DysonSwarm.GpuAnalysisPost)))
				).CreateLabel(out Label end)
				.Start().MatchForward(false,
					new CodeMatch(OpCodes.Ldarg_0),
					new CodeMatch(OpCodes.Ldfld, AccessTools.Field(typeof(DysonSwarm), nameof(DysonSwarm.bulletMaterial)))
				).SetOpcodeAndAdvance(OpCodes.Nop) // there's a label here, so add the instruction again later
				.InsertAndAdvance(
					new CodeInstruction(OpCodes.Ldsfld, AccessTools.Field(typeof(SailBulletOpt), nameof(SailBulletOpt.ejectedSailsVisible))),
					new CodeInstruction(OpCodes.Callvirt, AccessTools.Method(typeof(ConfigEntry<bool>), "get_Value")),
					new CodeInstruction(OpCodes.Brfalse_S, end),
					new CodeInstruction(OpCodes.Ldarg_0)
				);

				return matcher.InstructionEnumeration();
			}
		}
    }
}
