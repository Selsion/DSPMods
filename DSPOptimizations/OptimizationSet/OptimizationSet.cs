using BepInEx;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSPOptimizations
{
    public abstract class OptimizationSet
    {
        protected bool enabled = false; // TODO: what about mod saves when this is disabled?

        public virtual void Init(BaseUnityPlugin plugin)
        {

        }

        public virtual void OnDestroy()
        {

        }

        public bool Enabled
        {
            get { return enabled; }
            set { enabled = value; }
        }
    }
}
