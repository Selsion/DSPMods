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
    //[RunPatches(typeof(Patch))]
    [Optimization("PowerConsumerOpt", "Optimizes power consumer logic by always demanding 100% power", false, new Type[] { })]
    class PowerConsumerOpt : OptimizationSet
    {
		private static long GetRequiredPower(PowerNetwork powerNetwork)
        {
			return 0L;
        }

        class Patch
        {
			
		}
    }
}
