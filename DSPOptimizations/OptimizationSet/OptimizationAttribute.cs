using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSPOptimizations
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    class OptimizationAttribute : Attribute
    {
        private string name;
        private string description;
        private bool hasSaveData;
        private Type[] subModules;

        public OptimizationAttribute(string name, string description, bool hasSaveData, Type[] subModules = null)
        {
            this.name = name;
            this.description = description;
            this.hasSaveData = hasSaveData;
            this.subModules = subModules;
        }

        public string Name
        {
            get { return name; }
        }

        public string Description
        {
            get { return description; }
        }

        public Type[] SubModules
        {
            get { return subModules; }
        }

        public bool HasSaveData
        {
            get { return hasSaveData; }
        }
    }
}
