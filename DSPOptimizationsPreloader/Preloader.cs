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

        private static TypeDefinition GetType(AssemblyDefinition assembly, string type)
        {
            TypeDefinition typeDef = assembly.MainModule.GetType(type);
            if (typeDef == null)
                logSource.LogError("Preloader patch failed: unable to get type " + type);
            return typeDef;
        }

        public static void Patch(AssemblyDefinition assembly)
        {
            var dysonShell = GetType(assembly, "DysonShell");
            dysonShell?.Fields?.Add(new FieldDefinition("vertsqOffset_lowRes", FieldAttributes.Public, assembly.MainModule.TypeSystem.Int32.MakeArrayType()));
            dysonShell?.Fields?.Add(new FieldDefinition("radius_lowRes", FieldAttributes.Public, assembly.MainModule.TypeSystem.Single));
            dysonShell?.Fields?.Add(new FieldDefinition("surfaceAreaUnitSphere", FieldAttributes.Public, assembly.MainModule.TypeSystem.Single));

            var dysonSphereLayer = GetType(assembly, "DysonSphereLayer");
            dysonSphereLayer?.Fields?.Add(new FieldDefinition("radius_lowRes", FieldAttributes.Public, assembly.MainModule.TypeSystem.Single));
            dysonSphereLayer?.Fields?.Add(new FieldDefinition("surfaceAreaUnitSphere", FieldAttributes.Public, assembly.MainModule.TypeSystem.Single));
            dysonSphereLayer?.Fields?.Add(new FieldDefinition("totalNodeSP", FieldAttributes.Public, assembly.MainModule.TypeSystem.Int64));
            dysonSphereLayer?.Fields?.Add(new FieldDefinition("totalFrameSP", FieldAttributes.Public, assembly.MainModule.TypeSystem.Int64));
            dysonSphereLayer?.Fields?.Add(new FieldDefinition("totalCP", FieldAttributes.Public, assembly.MainModule.TypeSystem.Int64));

            logSource.LogInfo("Successfully ran preloader patch");
        }
    }
}
