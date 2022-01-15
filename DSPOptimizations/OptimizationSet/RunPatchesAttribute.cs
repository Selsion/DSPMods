using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSPOptimizations
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = true, Inherited = false)]
    class RunPatchesAttribute : Attribute
    {
        private Type patches;

        public RunPatchesAttribute(Type patches)
        {
            this.patches = patches;
        }

        public Type Patches
        {
            get { return patches; }
        }
    }
}
