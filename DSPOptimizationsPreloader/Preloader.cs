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

            // TODO: these are obsolete. look into removing them
            GetType(assembly, "DysonShell")
            ?.AddField("vertsqOffset_lowRes", typeSystem.Int32.MakeArrayType())
            ?.AddField("radius_lowRes", typeSystem.Single)
            ?.AddField("surfaceAreaUnitSphere", typeSystem.Single);

            GetType(assembly, "DysonSphereLayer")
            ?.AddField("radius_lowRes", typeSystem.Single) // also remove this
            ?.AddField("surfaceAreaUnitSphere", typeSystem.Single) // also remove this
            ?.AddField("totalNodeSP", typeSystem.Int64)
            ?.AddField("totalFrameSP", typeSystem.Int64)
            ?.AddField("totalCP", typeSystem.Int64);

            GetType(assembly, "DysonSphereSegmentRenderer")
            ?.AddField("layersDirtyMask", typeSystem.Int32.MakeArrayType());

            GetType(assembly, "PowerSystem")
            ?.AddField("receiverCursor", typeSystem.Int32)
            ?.AddField("receiverPool", typeSystem.Int32.MakeArrayType()) // stores the gen pool ID
            ?.AddField("chargerCursor", typeSystem.Int32)
            ?.AddField("chargerPool", typeSystem.Int32.MakeArrayType()) // doesn't include satellite substations
            ?.AddField("poleCursor", typeSystem.Int32)
            ?.AddField("polePool", typeSystem.Int32.MakeArrayType()) // includes tesla towers, wireless power towers, and satellite substations
            ?.AddField("substationEnergyDemand", typeSystem.Int64);

            GetType(assembly, "AssemblerComponent")
            ?.AddField("needsDirty", typeSystem.Boolean); // whether "needs" needs to be updated

            GetType(assembly, "MonitorComponent")
            ?.AddField("startIdx", typeSystem.Int32);


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

        /*public static TypeReference GetReference(this AssemblyDefinition assembly, string typeName)
        {
            var type = assembly.MainModule.GetType(typeName);
            return assembly.MainModule.ImportReference(type);
        }*/
    }
}
