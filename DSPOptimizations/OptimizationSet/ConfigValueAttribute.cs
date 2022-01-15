using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DSPOptimizations
{
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    class ConfigValueAttribute : Attribute
    {
        private string description;
        private object defaultValue;

        public ConfigValueAttribute(object defaultValue, string description)
        {
            this.description = description;
            this.defaultValue = defaultValue;
        }

        public string Description
        {
            get { return description; }
        }

        public object DefaultValue
        {
            get { return defaultValue; }
        }
    }
}
