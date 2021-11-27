using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UI;
using DSPOptimizations;

namespace DSPOptimizationsTestMod.Tests
{
    class DysonUITests
    {
        private static UIDysonPanel Panel
        {
            get { return UIRoot.instance.uiGame.dysonmap; }
        }

        private static bool ValidRadius(DysonSphereLayer layer)
        {
            return layer.radius_lowRes >= 1f && layer.radius_lowRes <= layer.orbitRadius;
        }

        private static void SelectLayer(int layerId)
        {
            var dysonPanel = Panel;
            dysonPanel.layerSelected = layerId;
            dysonPanel.UpdateSelectionVisibleChange();
        }

        private static void IterateLayers(Action<DysonSphereLayer> f)
        {
            var dysonPanel = Panel;
            var layers = dysonPanel.viewDysonSphere.layersIdBased;
            for (int i = 1; i < layers.Length; i++)
            {
                var layer = layers[i];
                if (layer != null && layer.id == i)
                    f(layer);
            }
        }

        [Test(TestContext.DysonUI)]
        public static bool GetsCorrectSelectedLayer()
        {
            bool ret = true;
            IterateLayers((DysonSphereLayer layer) =>
            {
                SelectLayer(layer.id);
                if (LowResShellsUI.SelectedLayer != layer)
                    ret = false;
            });
            return ret;
        }

        [Test(TestContext.DysonUI)]
        public static bool CannotSetInvalidRadius()
        {
            bool ret = true;
            
            IterateLayers((DysonSphereLayer layer) =>
            {
                foreach (float val in new float[] { -10f, -1f, 0f, layer.orbitRadius + 1f, layer.orbitRadius * 2f })
                {
                    LowResShellsUI.resSlider.value = val;
                    if (!ValidRadius(layer))
                        ret = false;

                    LowResShellsUI.resValue.text = val.ToString();
                    if (!ValidRadius(layer))
                        ret = false;
                }
            });

            return ret;
        }

        [Test(TestContext.DysonUI)]
        public static bool RadiusMatchesUI()
        {
            bool ret = true;

            IterateLayers((DysonSphereLayer layer) =>
            {
                var vals = new float[] { 1f, layer.orbitRadius * 0.1f, layer.orbitRadius * 0.5f, layer.orbitRadius };

                foreach (float val in vals)
                {
                    LowResShellsUI.resSlider.value = val;
                    if (!ValidRadius(layer) || layer.radius_lowRes != val || layer.radius_lowRes != LowResShellsUI.resSlider.value
                        || layer.radius_lowRes.ToString() != LowResShellsUI.resValue.text)
                        ret = false;
                }

                foreach(float val in vals)
                {
                    LowResShellsUI.resValue.text = val.ToString();
                    if (!ValidRadius(layer) || layer.radius_lowRes != val || layer.radius_lowRes != LowResShellsUI.resSlider.value
                        || layer.radius_lowRes.ToString() != LowResShellsUI.resValue.text)
                        ret = false;
                }
            });

            return true;
        }


        // TODO: add tests for the button?

        /*[Test(TestContext.ExistsShell)]
        public static bool NoDuplicateRegen()
        {
            var shell = Shell;

            RandomRegen(shell);

            return true;
        }*/
    }
}
