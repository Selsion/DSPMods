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
            var typeSystem = assembly.MainModule.TypeSystem;

            GetType(assembly, "DysonShell")
            ?.AddField("vertsqOffset_lowRes", typeSystem.Int32.MakeArrayType())
            ?.AddField("radius_lowRes", typeSystem.Single)
            ?.AddField("surfaceAreaUnitSphere", typeSystem.Single);

            GetType(assembly, "DysonSphereLayer")
            ?.AddField("radius_lowRes", typeSystem.Single)
            ?.AddField("surfaceAreaUnitSphere", typeSystem.Single)
            ?.AddField("totalNodeSP", typeSystem.Int64)
            ?.AddField("totalFrameSP", typeSystem.Int64)
            ?.AddField("totalCP", typeSystem.Int64);

            GetType(assembly, "DysonSphereSegmentRenderer")
            ?.AddField("layersDirtyMask", typeSystem.Int32.MakeArrayType());

            //GetType(assembly, "StationComponent")
            //?.AddField("factoryIndex", typeSystem.Int32);

            logSource.LogInfo("Successfully ran preloader patch");
        }
    }

    public static class PreloaderExtensions
    {
        public static TypeDefinition AddField(this TypeDefinition type, string name, TypeReference fieldType, FieldAttributes attr = FieldAttributes.Public)
        {
            type.Fields.Add(new FieldDefinition(name, attr, fieldType));
            return type;
        }
    }
}
