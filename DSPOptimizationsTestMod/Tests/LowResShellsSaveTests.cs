using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DSPOptimizations;

namespace DSPOptimizationsTestMod.Tests
{
    class LowResShellsSaveTests
    {
        /**
         * -exports low res rather than high res
         * -can call vanilla construct on vanilla imported shell from 0% cp to 100% cp
         * -can load save in vanilla
         * -can load save when .moddsv is deleted
         * -can load save when mod save data is corrupted
         */

        private static void SwapAllOffsetArrays()
        {
            LowResShellsTest.IterShells((DysonShell shell) =>
            {
                LowResShellsSaveManager.SwapVertOffsetArrays(shell);
            });
        }

        // doesn't include wrapper (i.e. no enabled flag and stream size)
        private static MemoryStream GetExport()
        {
            MemoryStream stream = new MemoryStream();
            var w = new BinaryWriter(stream);

            LowResShellsSaveManager.Export(w);
            w.Flush();

            stream.Position = 0;
            return stream;
        }

        // doesn't include wrapper
        private static void RunImport(MemoryStream stream)
        {
            // we need to swap beforehand since the low res offsets are read into the high rest offsets.
            // this is the vanilla behaviour, so we correct it when loading. but when running tests,
            // the offsets won't be swapped, so we need to do it manually
            SwapAllOffsetArrays();

            LowResShellsSaveManager.Import(new BinaryReader(stream), (int)stream.Length);
        }

        private static bool AllShellsValid(bool logInvalid = false)
        {
            bool ret = true;
            LowResShellsTest.IterShells((DysonShell shell) =>
            {
                if (!LowResShellsTest.ValidShell(shell))
                {
                    ret = false;
                    if(logInvalid)
                        Mod.logger.LogDebug(string.Format("Shell {0} is invalid", shell.id));
                }
            });
            return ret;
        }

        // must be unwrapped
        public static void DumpStream(MemoryStream stream, bool formatted = true)
        {
            stream.Position = 0;
            Mod.logger.LogDebug(string.Format("Dumping stream of size {0}:", stream.Length));
			var r = new BinaryReader(stream);

            if (!formatted)
            {
                for(int i = 0; i < stream.Length; i++)
                    UnityEngine.Debug.Log(i + ": " + r.PeekChar() + " " + r.ReadSingle());
                return;
            }

			UnityEngine.Debug.Log("Version: " + r.ReadInt32());

			int numSpheres = r.ReadInt32();
			UnityEngine.Debug.Log(numSpheres + " spheres");

			for (int i = 0; i < numSpheres; i++)
			{
				int existsSphere = r.ReadInt32();
				UnityEngine.Debug.Log(i + ": " + (existsSphere == 1 ? "exists" : "doesn't exist"));
				if (existsSphere == 1)
				{
					int numLayers = r.ReadInt32();
					UnityEngine.Debug.Log("  " + numSpheres + " layers");

					for (int j = 1; j < numLayers; j++)
					{
						float orbitRadius = r.ReadSingle();
						int layerId = r.ReadInt32();
						UnityEngine.Debug.Log("  " + j + ": r=" + orbitRadius + " id=" + layerId);

						if (layerId == j)
						{
							float radius_lowRes = r.ReadSingle();
							int shellCursor = r.ReadInt32();
							UnityEngine.Debug.Log("  r_low=" + radius_lowRes + " cursor=" + shellCursor);

							for (int k = 1; k < shellCursor; k++)
							{
								int shellId = r.ReadInt32();
								UnityEngine.Debug.Log("    shellId=" + shellId);

								if (shellId == k)
								{
                                    float shell_radius_lowRes = r.ReadSingle();
                                    UnityEngine.Debug.Log("    r=" + shell_radius_lowRes);

                                    int numOffsetsV = r.ReadInt32();
                                    UnityEngine.Debug.Log("    offV=" + numOffsetsV);

                                    int[] offsetsV = new int[numOffsetsV];
                                    for (int l = 0; l < numOffsetsV; l++)
                                        offsetsV[l] = r.ReadInt32();
                                    UnityEngine.Debug.Log("    " + string.Join(" ", offsetsV));

                                    int numOffsetsM = r.ReadInt32();
                                    UnityEngine.Debug.Log("    offM=" + numOffsetsM);

                                    int[] offsetsM = new int[numOffsetsM];
                                    for (int l = 0; l < numOffsetsM; l++)
                                        offsetsM[l] = r.ReadInt32();
                                    UnityEngine.Debug.Log("    " + string.Join(" ", offsetsM));
                                }
							}
						}
					}
				}
			}
		}

        [Test(TestContext.ExistsShell)]
        public static bool ValidReimport()
        {
            var shell = LowResShellsTest.Shell;
            LowResShellsTest.RandomRegen(shell);

            var export = GetExport();
            //RunImport(GetExport());
            //DumpStream(export);
            RunImport(export);
            return AllShellsValid();
        }

        [Test(TestContext.ExistsShell)]
        public static bool ShellUnchanged()
        {
            var shell = LowResShellsTest.Shell;
            LowResShellsTest.RandomRegen(shell);

            int[] offsets = (int[])shell.vertsqOffset.Clone();
            int[] offsetsLowRes = (int[])shell.vertsqOffset_lowRes.Clone();
            float radius_lowRes = shell.radius_lowRes;

            RunImport(GetExport());

            return offsets.SequenceEqual(shell.vertsqOffset)
                && offsetsLowRes.SequenceEqual(shell.vertsqOffset_lowRes)
                && radius_lowRes == shell.radius_lowRes;
        }

        //[Test(TestContext.ExistsShell)]
        public static bool RecoversFromGarbageData()
        {
            var export = GetExport();
            byte[] newData = new byte[export.Length];

            // assume that the save file is not at least 2 GB
            for (int j = (int)export.Length; j > 0; j--)
            {
                for (int i = 0; i < 1; i++)
                {
                    TestManager.rng.NextBytes(newData);

                    MemoryStream newStream = new MemoryStream();
                    newStream.Position = newData.Length - j;
                    newStream.Write(newData, 0, j);
                    newStream.Position = 0;


                    try
                    {
                        RunImport(newStream);
                        if (!AllShellsValid())
                        {
                            newStream.Position = 0;
                            DumpStream(newStream);
                            return false;
                        }
                    }
                    catch
                    {
                        newStream.Position = 0;
                        DumpStream(newStream, false);
                        return false;
                    }
                }
            }

            return true;
        }

        // TODO: CPs were cleared when regenerating vanilla cp counts. add a new unit test to make sure the fix works
        [Test(TestContext.ExistsShell)] // messes up the shell, giving it too many cps. this ruins regen. maybe regen vanilla counts if the cps are too high
        public static bool RecoversFromMissingSave()
        {
            SwapAllOffsetArrays();
            LowResShellsSaveManager.IntoOtherSave();
            return AllShellsValid();
        }

        // TODO: running the tests messed up the shell verts? cleared vert cps after running regen?
        // TODO: when should i actually wipe cps? need to be VERY careful with this


        // TODO: make a test to detect that we swapped the arrays properly. didn't detect that only one swap was done
    }
}
