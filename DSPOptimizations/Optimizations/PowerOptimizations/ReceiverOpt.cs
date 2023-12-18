using HarmonyLib;
using PowerNetworkStructures;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace DSPOptimizations
{
	// TODO: check compatibility after dark fog update
	//[RunPatches(typeof(Patch))]
	[Optimization("ReceiverOpt", "", false, new Type[] { })]
	class ReceiverOpt : OptimizationSet
	{
		private const int INITIAL_RECEIVER_POOL_SIZE = 8;

		private static void SetReceiverCapacity(PowerSystem powerSystem, int newCapacity)
		{
			int[] oldPool = powerSystem.receiverPool;
			powerSystem.receiverPool = new int[newCapacity];
			if (oldPool != null)
			{
				int oldCapacity = oldPool.Length;
				Array.Copy(oldPool, powerSystem.receiverPool, (newCapacity > oldCapacity) ? oldCapacity : newCapacity);
			}
		}

		private static void AddReceiver(PowerSystem powerSystem, int genId)
		{
			if (powerSystem.receiverPool == null)
			{
				SetReceiverCapacity(powerSystem, INITIAL_RECEIVER_POOL_SIZE);
				powerSystem.receiverCursor = 0;
			}
			else if (powerSystem.receiverCursor == powerSystem.receiverPool.Length)
				SetReceiverCapacity(powerSystem, powerSystem.receiverCursor * 2);

			powerSystem.receiverPool[powerSystem.receiverCursor] = genId;
			powerSystem.receiverCursor++;
		}

		private static void RemoveReceiver(PowerSystem powerSystem, int genId)
		{
			// do a linear scan for the receiver id. should eventually replace with indexing array
			int id = 0;
			while (id < powerSystem.receiverCursor && powerSystem.receiverPool[id] != genId)
				id++;

			// check if we were unable to find it
			if (id == powerSystem.receiverCursor)
				return;

			// swap with last element if needed
			int lastId = powerSystem.receiverCursor - 1;
			if (id < lastId)
				powerSystem.receiverPool[id] = powerSystem.receiverPool[lastId];

			// remove last element
			powerSystem.receiverPool[lastId] = 0;
			powerSystem.receiverCursor--;
		}

		class Patch
		{
			/*[HarmonyPostfix, HarmonyPatch(typeof(PowerSystem), MethodType.Constructor)]
			public static void InitPoolPatch(PowerSystem __instance)
			{
				SetReceiverCapacity(__instance, INITIAL_RECEIVER_POOL_SIZE);
				__instance.receiverCursor = 0;
			}*/

			[HarmonyPostfix, HarmonyPatch(typeof(PowerSystem), "Import")]
			public static void ImportPatch(PowerSystem __instance)
			{
				for (int i = 1; i < __instance.genCursor; i++)
					if (__instance.genPool[i].gamma)
						AddReceiver(__instance, i);
			}

			[HarmonyPostfix, HarmonyPatch(typeof(PowerSystem), "NewGeneratorComponent")]
			public static void AddGenPatch(PowerSystem __instance, int __result)
			{
				if (__instance.genPool[__result].gamma)
					AddReceiver(__instance, __result);
			}

			[HarmonyPrefix, HarmonyPatch(typeof(PowerSystem), "RemoveGeneratorComponent")]
			public static void RemoveGenPatch(PowerSystem __instance, int id)
			{
				if (__instance.genPool[id].gamma)
					RemoveReceiver(__instance, id);
			}

			// TODO: make this a transpiler
			/*[HarmonyPrefix, HarmonyPatch(typeof(PowerSystem), "RequestDysonSpherePower")]
			public static bool RequestDysonSpherePower(PowerSystem __instance)
			{
				__instance.dysonSphere = __instance.factory.gameData.dysonSpheres[__instance.planet.star.index];
				float eta = 1f - GameMain.history.solarEnergyLossRate;
				float increase = (__instance.dysonSphere != null) ? ((float)((double)__instance.dysonSphere.grossRadius / ((double)__instance.planet.sunDistance * 40000.0))) : 0f;
				Vector3 normalized = __instance.planet.runtimeLocalSunDirection.normalized;
				long num = 0L;
				bool flag = false;
				
				
				/*for (int i = 1; i < __instance.genCursor; i++)
				{
					if (__instance.genPool[i].gamma)
					{
						num += __instance.genPool[i].EnergyCap_Gamma_Req(normalized.x, normalized.y, normalized.z, increase, eta);
						flag = true;
					}
				}*//*

				for (int i = 0; i < __instance.receiverCursor; i++)
					num += __instance.genPool[__instance.receiverPool[i]].EnergyCap_Gamma_Req(normalized.x, normalized.y, normalized.z, increase, eta);
				flag = __instance.receiverCursor > 0;


				if (__instance.dysonSphere == null && flag)
				{
					__instance.dysonSphere = __instance.factory.CheckOrCreateDysonSphere();
				}
				if (__instance.dysonSphere != null)
				{
					__instance.dysonSphere.energyReqCurrentTick += num;
				}

				return false;
			}*/
		}
	}
}
