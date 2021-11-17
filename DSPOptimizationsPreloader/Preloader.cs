using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Rocks;
using BepInEx.Logging;

using FieldAttributes = Mono.Cecil.FieldAttributes;

namespace DSPOptimizations
{
    public class Preloader
    {
        public static ManualLogSource logSource;
        public static void Initialize()
        {
            logSource = Logger.CreateLogSource("DSPOptimizations Preloader");
        }

        public static IEnumerable<string> TargetDLLs { get; } = new[] { "Assembly-CSharp.dll" };

        public static void Patch(AssemblyDefinition assembly)
        {
            TypeDefinition dysonShell = assembly.MainModule.GetType("DysonShell");

            if(dysonShell == null)
            {
                logSource.LogError("Preloader patch failed: unable to get DysonShell type");
                return;
            }

            dysonShell.Fields.Add(new FieldDefinition("vertsqOffset_lowRes", FieldAttributes.Public, assembly.MainModule.TypeSystem.Int32.MakeArrayType()));
            dysonShell.Fields.Add(new FieldDefinition("radius_lowRes", FieldAttributes.Public, assembly.MainModule.TypeSystem.Single));

            logSource.LogInfo("Successfully ran preloader patch");
        }
    }
}
