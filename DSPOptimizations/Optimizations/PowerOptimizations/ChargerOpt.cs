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
	[Optimization("ChargerOpt", "", false, new Type[] { })]
	class ChargerOpt
	{
		private const int INITIAL_CHARGER_POOL_SIZE = 8;

		private static void SetChargerCapacity(PowerSystem powerSystem, int newCapacity)
		{
			int[] oldPool = powerSystem.chargerPool;
			powerSystem.chargerPool = new int[newCapacity];
			if (oldPool != null)
			{
				int oldCapacity = oldPool.Length;
				Array.Copy(oldPool, powerSystem.chargerPool, (newCapacity > oldCapacity) ? oldCapacity : newCapacity);
			}
		}

		private static void AddCharger(PowerSystem powerSystem, int nodeId)
		{
			if (powerSystem.chargerPool == null)
			{
				SetChargerCapacity(powerSystem, INITIAL_CHARGER_POOL_SIZE);
				powerSystem.chargerCursor = 0;
			}
			else if (powerSystem.chargerCursor == powerSystem.chargerPool.Length)
				SetChargerCapacity(powerSystem, powerSystem.chargerCursor * 2);

			powerSystem.chargerPool[powerSystem.chargerCursor] = nodeId;
			powerSystem.chargerCursor++;
		}

		private static void RemoveCharger(PowerSystem powerSystem, int nodeId)
		{
			// do a linear scan for the charger id. there will likely never be many wireless power towers, so __instance shouldn't be slow
			// could eventually replace __instance with an indexing array
			int id = 0;
			while (id < powerSystem.chargerCursor && powerSystem.chargerPool[id] != nodeId)
				id++;

			// check if we were unable to find it
			if (id == powerSystem.chargerCursor)
				return;

			// swap with last element if needed
			int lastId = powerSystem.chargerCursor - 1;
			if (id < lastId)
				powerSystem.chargerPool[id] = powerSystem.chargerPool[lastId];

			// remove last element
			powerSystem.chargerPool[lastId] = 0;
			powerSystem.chargerCursor--;
		}

		private static bool IsSubstation(PowerSystem powerSystem, int nodeId)
        {
			return powerSystem.nodePool[nodeId].coverRadius >= 15f;
		}

		class Patch
        {
			[HarmonyPostfix, HarmonyPatch(typeof(PowerSystem), "Import")]
			public static void ImportPatch(PowerSystem __instance)
			{
				__instance.substationEnergyDemand = 0L;

				for (int i = 1; i < __instance.netCursor; i++)
				{
					PowerNetwork powerNetwork = __instance.netPool[i];
					if (powerNetwork != null && powerNetwork.id == i)
					{
						foreach (Node node in powerNetwork.nodes)
						{
							int id = node.id;
							if (__instance.nodePool[id].id == id && __instance.nodePool[id].isCharger)
                            {
								if (IsSubstation(__instance, id))
									__instance.substationEnergyDemand += __instance.nodePool[id].idleEnergyPerTick;
								else
									AddCharger(__instance, id);
							}
						}
					}
				}
			}

			[HarmonyPostfix, HarmonyPatch(typeof(PowerSystem), "NewNodeComponent")]
			public static void AddNodePatch(PowerSystem __instance, int __result)
			{
				if (__instance.nodePool[__result].isCharger)
				{
					if (IsSubstation(__instance, __result))
						__instance.substationEnergyDemand += __instance.nodePool[__result].idleEnergyPerTick;
					else
						AddCharger(__instance, __result);
				}
			}

			[HarmonyPrefix, HarmonyPatch(typeof(PowerSystem), "RemoveNodeComponent")]
			public static void RemoveNodePatch(PowerSystem __instance, int id)
			{
				if (__instance.nodePool[id].isCharger)
				{
					if (IsSubstation(__instance, id))
						__instance.substationEnergyDemand -= __instance.nodePool[id].idleEnergyPerTick;
					else
						RemoveCharger(__instance, id);
				}
			}

			// ...

		}
	}
}
