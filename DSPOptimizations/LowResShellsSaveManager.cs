using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace DSPOptimizations
{
    public class LowResShellsSaveManager
    {
		private static MemoryStream stream;

		public static void ExportWrapper(BinaryWriter w)
        {
            if (LowResShells.enabled)
			{
				w.Write(1);
				stream = new MemoryStream();
				Export(new BinaryWriter(stream));
			}
            else
				w.Write(0);

			if (stream == null)
				w.Write(0); // stream length of 0
			else
			{
				w.Write(stream.Length);
				stream.WriteTo(w.BaseStream);
			}
		}

		public static void ImportWrapper(BinaryReader r)
        {
			int wasEnabled = r.ReadInt32(); // is this even needed?
			int streamLength = r.ReadInt32();
			if (streamLength > 0)
			{
				stream = new MemoryStream();
				stream.Write(r.ReadBytes(streamLength), 0, streamLength);
			}
			else
				stream = null;

			if(LowResShells.enabled)
				Import(new BinaryReader(stream));
        }
		
		public static void Export(BinaryWriter w)
		{
			int shellSaveVersion = 1;
			w.Write(shellSaveVersion);

			var spheres = GameMain.data.dysonSpheres;
			int numSpheres = spheres.Length;
			w.Write(numSpheres);

			for (int i = 0; i < numSpheres; i++)
			{
				var sphere = spheres[i];
				if (sphere != null)
				{
					w.Write(1);

					int numLayers = sphere.layersIdBased.Length;
					w.Write(numLayers);

					for (int j = 1; j < numLayers; j++)
					{
						var layer = sphere.layersIdBased[j];
						if (layer != null && layer.id == j)
						{
							w.Write(layer.orbitRadius);
							w.Write(layer.id);

							w.Write(layer.radius_lowRes);
							w.Write(layer.shellCursor);

							for (int k = 1; k < layer.shellCursor; k++)
							{
								var shell = layer.shellPool[k];
								if (shell != null && shell.id == k)
								{
									w.Write(shell.id);

									w.Write(shell.radius_lowRes);
									
									w.Write(shell.vertsqOffset.Length);
									for (int l = 0; l < shell.vertsqOffset.Length; l++)
										w.Write(shell.vertsqOffset[l]);

									w.Write(shell.vertsqOffset_lowRes.Length);
									for (int l = 0; l < shell.vertsqOffset_lowRes.Length; l++)
										w.Write(shell.vertsqOffset_lowRes[l]);
								}
								else
									w.Write(0);
							}
						}
						else
						{
							w.Write(-1.0f);
							w.Write(0);
						}
					}
				}
				else
					w.Write(0);
			}
		}

		public static void Import(BinaryReader r)
		{
			int shellSaveVersion = r.ReadInt32();

			var spheres = GameMain.data.dysonSpheres;
			//w.Write(spheres.Length);
			int numSpheres = r.ReadInt32(); // TODO: assume for now that the number of stars will not change
			for (int i = 0; i < numSpheres; i++)
			{
				var sphere = spheres[i];
				int sphereExisted = r.ReadInt32();

				/* sphere, sphereExisted
                 * 0, 0 : no sphere existed last time the mod was loaded, and no sphere exists now. skip
                 * 0, 1 : the sphere was deleted in vanilla. read and ignore the mod data
                 * 1, 0 : the sphere was added in vanilla. generate modded data without reading
                 * 1, 1 : the sphere existed last time the mod was loaded, and still exists now. load as normal
                 */

				if (sphere != null && sphereExisted == 1)
					ImportSphere(r, sphere);
				else if (sphere != null && sphereExisted == 0)
					ImportSphereGenerate(sphere);
				else if (sphere == null && sphereExisted == 1)
					ImportSphereIgnore(r);
			}
		}

		private static void ImportSphere(BinaryReader r, DysonSphere sphere)
		{
			int numLayers = r.ReadInt32(); // TODO: assume for now that the max number of layers will not change
			for (int i = 1; i < numLayers; i++)
			{
				var layer = sphere.layersIdBased[i];
				float layerRadius = r.ReadSingle();
				int oldLayerId = r.ReadInt32();

				// if the radius changed, then read and ignore, then generate
				if (layer != null && layer.id == i && layer.orbitRadius != layerRadius)
				{
					if (oldLayerId != 0)
						ImportLayerIgnore(r);
					ImportLayerGenerate(layer);
				}

				/*
				 * oldLayerID, layer
				 * 0  0 : sphere used to exist and still does. this layer didn't exist and still doesn't. skip
				 * 0  1 : sphere used to exist and still does. this layer didn't exist but does now. generate
				 * >0 0 : sphere used to exist and still does. this layer used to exist but was deleted. read and ignore data
				 * >0 1 : sphere used to exist and still does. this layer used to exist and still does. load as normal
				 */
				if (layer != null && layer.id == i && oldLayerId == i)
					ImportLayer(r, layer);
				else if (layer != null && layer.id == i && oldLayerId != i)
					ImportLayerGenerate(layer);
				else if (oldLayerId == i)
					ImportLayerIgnore(r);
			}
		}

		private static void ImportSphereIgnore(BinaryReader r)
		{
			int numLayers = r.ReadInt32(); // TODO: assume for now that the max number of layers will not change
			for (int j = 1; j < numLayers; j++)
			{
				float layerRadius = r.ReadSingle();
				int oldLayerId = r.ReadInt32();
				/*
				 * oldLayerID
				 * 0  : reading and ignoring mod data. this layer didn't exist. skip
				 * >0 : reading and ignoring mod data. this layer used to exist. read and ignore data
				 */
				if (oldLayerId == j)
					ImportLayerIgnore(r);
			}
		}

		private static void ImportSphereGenerate(DysonSphere sphere)
		{
			int numLayers = sphere.layersIdBased.Length;
			for (int j = 1; j < numLayers; j++)
			{
				var layer = sphere.layersIdBased[j];
				/*
				 * layer
				 * 0 : generating modded data. this layer doesn't exist. skip
				 * 1 : generating modded data. need to generate for this layer
				 */
				if (layer != null && layer.id == j)
					ImportLayerGenerate(layer);
			}
		}

		private static void ImportLayer(BinaryReader r, DysonSphereLayer layer)
		{
			layer.radius_lowRes = r.ReadSingle();
			// if the low res radius is invalid, then reset it
			if (layer.radius_lowRes <= 0f || layer.radius_lowRes > layer.orbitRadius)
				layer.radius_lowRes = layer.orbitRadius;

			int oldShellCursor = r.ReadInt32();
			int shellCursor = layer.shellCursor;
			//w.Write(layer.shellCount);
			//layer.orbitRadius_lowRes = r.ReadSingle();

			// TODO: what about the shell recycle cursor crap?
			// TODO: what if somebody deletes and recreates a layer? add global regen option?

			/*
			 * oldShellCursor < shellCursor : new shells added. load as normal before oldShellCursor, generate after
			 * shellCursor < oldShellCursor : shells deleted. load as normal before shellCursor, ignore after
			 * shellCursor = oldShellCursor : no change. load as normal
			 */

			int minIdx = Math.Min(oldShellCursor, shellCursor);

			for (int i = 1; i < minIdx; i++)
			{
				var shell = layer.shellPool[i];
				int oldShellId = r.ReadInt32();

				/*
				 * shell, oldShellId
				 * 0, 0 : this shell didn't exist and still doesn't. skip
				 * 0, 1 : this shell used to exist but doesn't anymore. read and ignore
				 * 1, 0 : this shell didn't used to exist but does now. generate
				 * 1, 1 : this shell used to exist and still does. load as normal
				 */

				if (shell != null && shell.id == i && oldShellId == i)
					ImportShell(r, shell);
				else if (shell != null && shell.id == i && oldShellId != i)
					ImportShellGenerate(shell);
				else if (oldShellId == i)
					ImportShellIgnore(r);
			}

			if (oldShellCursor < shellCursor)
			{
				for (int i = minIdx; i < shellCursor; i++)
				{
					var shell = layer.shellPool[i];
					if (shell != null && shell.id == i)
						ImportShellGenerate(shell);
				}
			}
			else if (shellCursor < oldShellCursor)
			{
				for (int i = minIdx; i < oldShellCursor; i++)
				{
					int oldShellId = r.ReadInt32();
					if (oldShellId == i)
						ImportShellIgnore(r);
				}
			}
		}

		private static void ImportLayerIgnore(BinaryReader r)
		{
			float layerRadiusLowRes = r.ReadSingle();
			int oldShellCursor = r.ReadInt32();

			for (int i = 1; i < oldShellCursor; i++)
			{
				int oldShellId = r.ReadInt32();
				if (oldShellId == i)
					ImportShellIgnore(r);
			}
		}

		private static void ImportLayerGenerate(DysonSphereLayer layer)
		{
			/*if (layer.radius_lowRes <= 0f)
				layer.radius_lowRes = LowResShells.maxDesiredRadius;*/
			layer.radius_lowRes = layer.orbitRadius;

			int shellCursor = layer.shellCursor;

			for (int i = 1; i < shellCursor; i++)
			{
				var shell = layer.shellPool[i];
				if (shell != null && shell.id == i)
					ImportShellGenerate(shell);
			}
		}

		private static void ImportShell(BinaryReader r, DysonShell shell)
		{
			shell.radius_lowRes = r.ReadSingle();

			int oldNumOffsets = r.ReadInt32();
			int[] oldOffsets = new int[oldNumOffsets];
			for (int i = 0; i < oldNumOffsets; i++)
				oldOffsets[i] = r.ReadInt32();

			int oldNumOffsetsLowRes = r.ReadInt32();
			int[] oldOffsetsLowRes = new int[oldNumOffsetsLowRes];
			for (int i = 0; i < oldNumOffsetsLowRes; i++)
				oldOffsetsLowRes[i] = r.ReadInt32();

			int numOffsets = shell.vertsqOffset.Length;

			/* Note: Guaranteed that layerRadius == oldLayerRadius. other functions would be called otherwise
			 * Note: radius_lowRes <= layerRadius
			 * Let minDesiredRadius = Math.Min(DESIRED_RADIUS, layerRadius)
			 * if regeneration is on:
			 * DESIRED_RADIUS < radius_lowRes <= layerRadius  : regenerate at DESIRED_RADIUS == minDesiredRadius
			 * radius_lowRes = DESIRED_RADIUS <= layerRadius : don't regenerate
			 * radius_lowRes < DESIRED_RADIUS <= layerRadius : regenerate at DESIRED_RADIUS == minDesiredRadius
			 * radius_lowRes < layerRadius < DESIRED_RADIUS  : regenerate at layerRadius == minDesiredRadius
			 * radius_lowRes = layerRadius < DESIRED_RADIUS  : don't regenerate
			 * 
			 * Summary: regenerate (at minDesiredRadius) iff radius_lowRes != minDesiredRadius
			 */

			//float regenRadius = Math.Min(shell.parentLayer.orbitRadius, LowResShells.maxDesiredRadius);//DESIRED_RADIUS);
			//bool regen = regenRadius != shell.radius_lowRes && LowResShells.regenExistingShells; // TODO: add config option // Note: exact fp values are saved, so testing for equality is fine here

			/*
			 * if not regenerating (Note: regeneration is for changing resolution of shells. generation is for entirely new shells):
			 * numOffsets < oldNumOffsets : the shell has fewer nodes? new shell? ignore data and generate
			 * oldNumOffsets < numOffsets : the shell has new nodes? new shell? ignore data and generate
			 * oldNumOffsets = numOffsets : load as normal
			 */

			bool arraysMatch = oldNumOffsets == numOffsets && oldNumOffsetsLowRes == numOffsets;
			if (arraysMatch) {
				for (int i = 0; i < numOffsets; i++) {
					// note: the shell currently has the low res offsets
					if (shell.vertsqOffset[i] != oldOffsetsLowRes[i] || oldOffsetsLowRes[i] > oldOffsets[i]) {
						arraysMatch = false;
						break;
					}
				}
			}

			if (arraysMatch)
			{
				shell.vertsqOffset_lowRes = oldOffsetsLowRes;
				shell.vertsqOffset = oldOffsets;
			}
			else
				ImportShellGenerate(shell);

			/*if (oldNumOffsets != numOffsets || oldNumOffsetsLowRes != numOffsets)
			{
				for (int i = 0; i < oldNumOffsets; i++)
					r.ReadInt32();
				ImportShellGenerate(shell);
			}
			else
			{
				shell.vertsqOffset_lowRes = (int[])shell.vertsqOffset.Clone();
				for (int l = 0; l < shell.vertsqOffset.Length; l++)
					shell.vertsqOffset[l] = r.ReadInt32();
				//if (regen)
					//LowResShells.regenGeoLowRes(shell);
			}*/
		}

		private static void ImportShellIgnore(BinaryReader r)
		{
			r.ReadSingle();
			int oldNumOffsets = r.ReadInt32();
			for (int i = 0; i < oldNumOffsets; i++)
				r.ReadInt32();
			
			int oldNumOffsetsLowRes = r.ReadInt32();
			for (int i = 0; i < oldNumOffsetsLowRes; i++)
				r.ReadInt32();
		}

		private static void ImportShellGenerate(DysonShell shell)
		{
			//LowResShells.regenGeoLowRes(shell);
			shell.radius_lowRes = shell.parentLayer.radius_lowRes;
			shell.vertsqOffset_lowRes = (int[])shell.vertsqOffset.Clone();
		}

		public static void IntoOtherSave()
		{
			var spheres = GameMain.data?.dysonSpheres;
			if (spheres != null)
				foreach (var sphere in spheres)
					if (sphere != null)
						ImportSphereGenerate(sphere);
		}
	}
}
