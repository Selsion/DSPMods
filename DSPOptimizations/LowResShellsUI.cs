using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using BepInEx;
using HarmonyLib;

namespace DSPOptimizations
{
    public class LowResShellsUI
    {
        internal static GameObject resPanelObj, resSliderObj, resValueObj, regenButtonObj, resLabelObj, expVertsLabelObj;
        public /*internal*/ static Slider resSlider;
        public /*internal*/ static InputField resValue;
        public /*internal*/ static UIButton regenButton;
        internal static Text expVertsText;

        private static bool initializedUI = false;

        public static void Init(BaseUnityPlugin plugin, Harmony harmony)
        {
            harmony.PatchAll(typeof(Patch));
        }

        public static bool InitializedUI
        {
            get { return initializedUI; }
        }

        public static void OnDestroy()
        {
            UnityEngine.Object.Destroy(resPanelObj);
            UnityEngine.Object.Destroy(resSliderObj);
            UnityEngine.Object.Destroy(resValueObj);
            UnityEngine.Object.Destroy(regenButtonObj);
        }

        private static void CreateUI()
        {
            var shellGroup = GameObject.Find("UI Root/Always on Top/Overlay Canvas - Top/Dyson Editor Top/info-group/shell");

            var srcPanel = GameObject.Find("UI Root/Always on Top/Overlay Canvas - Top/Dyson Editor Top/info-group/screen-group/add-panel");
            resPanelObj = UnityEngine.Object.Instantiate(srcPanel, shellGroup.transform.position, Quaternion.identity, shellGroup.transform);
            resPanelObj.transform.localPosition = new Vector3(180f, -360f, 0f);
            resPanelObj.name = "shell-resolution-panel";
            foreach (Transform child in resPanelObj.transform)
                UnityEngine.Object.Destroy(child.gameObject);
            RectTransform rect = (resPanelObj.GetComponentInChildren<UIBlockZone>().transform as RectTransform);
            rect.sizeDelta = new Vector2(360f, 100f);

            var srcSlider = GameObject.Find("UI Root/Always on Top/Overlay Canvas - Top/Dyson Editor Top/info-group/screen-group/add-panel/bar-slider-0");
            resSliderObj = UnityEngine.Object.Instantiate(srcSlider, resPanelObj.transform.position, Quaternion.identity, resPanelObj.transform);
            resSliderObj.transform.localPosition = new Vector3(-170f, 10f, 0f);
            resSliderObj.name = "shell-resolution-slider";
            resSlider = resSliderObj.GetComponentInChildren<Slider>();
            resSlider.onValueChanged.AddListener(new UnityAction<float>(OnResSliderChange));
            resSlider.minValue = 1.0f;

            var srcValue = GameObject.Find("UI Root/Always on Top/Overlay Canvas - Top/Dyson Editor Top/info-group/screen-group/add-panel/bar-value-0");
            resValueObj = UnityEngine.Object.Instantiate(srcValue, resPanelObj.transform.position, Quaternion.identity, resPanelObj.transform);
            resValueObj.transform.localPosition = new Vector3(170f, 10f, 0f);
            resValueObj.name = "shell-resolution-value";
            resValue = resValueObj.GetComponentInChildren<InputField>();
            resValue.onEndEdit.AddListener(new UnityAction<string>(OnValueTextBoxChange));

            var srcLabel = GameObject.Find("UI Root/Always on Top/Overlay Canvas - Top/Dyson Editor Top/info-group/screen-group/add-panel/bar-label");
            resLabelObj = UnityEngine.Object.Instantiate(srcLabel, resPanelObj.transform.position, Quaternion.identity, resPanelObj.transform);
            resLabelObj.transform.localPosition = new Vector3(-72f, 48f, 0f);
            resLabelObj.name = "shell-resolution-label";
            resLabelObj.GetComponentInChildren<Text>().text = "Shell Resolution Radius (m)";
            UnityEngine.Object.Destroy(resLabelObj.GetComponentInChildren<Localizer>()); // TODO: add translation

            var srcButton = GameObject.Find("UI Root/Always on Top/Overlay Canvas - Top/Dyson Editor Top/info-group/screen-group/add-panel/add-button");
            regenButtonObj = UnityEngine.Object.Instantiate(srcButton, resPanelObj.transform.position, Quaternion.identity, resPanelObj.transform);
            regenButtonObj.transform.localPosition = new Vector3(-100f, -35f, 0f);
            regenButtonObj.name = "shell-resolution-button";
            regenButton = regenButtonObj.GetComponentInChildren<UIButton>();
            regenButtonObj.GetComponentInChildren<Text>().text = "Regenerate Shells";
            UnityEngine.Object.Destroy(regenButtonObj.GetComponentInChildren<Localizer>()); // TODO: add translation
            regenButton.onClick += Regen;

            expVertsLabelObj = UnityEngine.Object.Instantiate(srcLabel, resPanelObj.transform.position, Quaternion.identity, resPanelObj.transform);
            expVertsLabelObj.transform.localPosition = new Vector3(-25f, -7f, 0f);
            expVertsLabelObj.name = "shell-expected-verts-label";
            expVertsText = expVertsLabelObj.GetComponentInChildren<Text>();
            expVertsText.text = "";
            UnityEngine.Object.Destroy(expVertsLabelObj.GetComponentInChildren<Localizer>()); // TODO: add translation

            //resPanelObj.SetActive(true);

            initializedUI = true;
        }

        public static DysonSphereLayer SelectedLayer {
            get {
                UIDysonPanel panel = UIRoot.instance.uiGame.dysonmap;
                if (panel.layerSelected > 0)
                    return panel.viewDysonSphere.layersIdBased[panel.layerSelected];
                else
                    return null;
            }
        }

        private static void UpdateLayer(float newRadius = -1.0f)
        {
            DysonSphereLayer layer = SelectedLayer;
            if (newRadius > 0)
                layer.radius_lowRes = newRadius;
            expVertsText.text = "Expected Vertices: ~" + ExpectedVertexCount(layer).ToString();
        }

        public static void OnResSliderChange(float val)
        {
            val = Mathf.Round(val);
            resSlider.value = val;
            resValue.text = val.ToString("0");
            //GetSelectedLayer().radius_lowRes = val;
            UpdateLayer(val);
        }

        public static void OnValueTextBoxChange(string val)
        {
            float value = 0f;
            if (float.TryParse(val, out value))
                resSlider.value = Mathf.Clamp(value, resSlider.minValue, resSlider.maxValue);
            else
                value = resSlider.value;
            resValue.text = resSlider.value.ToString();
            //GetSelectedLayer().radius_lowRes = resSlider.value;
            UpdateLayer(resSlider.value);
        }

        public static void Regen(int obj)
        {
            //UnityEngine.Debug.Log("clicked on regen button");

            var layer = SelectedLayer;
            if (layer == null)
                return;

            if (layer.radius_lowRes <= 0f || layer.radius_lowRes > layer.orbitRadius)
                layer.radius_lowRes = layer.orbitRadius;

            for (int i = 1; i < layer.shellCursor; i++)
            {
                DysonShell shell = layer.shellPool[i];
                if (shell != null && shell.id == i)
                    if(shell.radius_lowRes != layer.radius_lowRes)
                        LowResShells.RegenGeoLowRes(shell);
            }
        }

        private static long ExpectedVertexCount(DysonSphereLayer layer)
        {
            long ret = (long)(layer.surfaceAreaUnitSphere * layer.radius_lowRes * layer.radius_lowRes / 5540f);
            if (ret < 0) // in case of floating point imprecision when adding and deleting shells
                ret = 0;
            return ret;
        }

        private static void ComputeSurfaceArea(DysonShell shell)
        {
            if (shell.polygon.Count < 3) {
                shell.surfaceAreaUnitSphere = 0f;
                return;
            }

            Vector3 p1 = shell.polygon[0].normalized;
            Vector3 sum = new Vector3(0f, 0f, 0f);

            Vector3 pCurr = shell.polygon[1].normalized;

            for (int i = 1; i < shell.polygon.Count - 1; i++)
            {
                Vector3 pNext = shell.polygon[i + 1].normalized;
                sum += Vector3.Cross(pCurr - p1, pNext - p1);
                pCurr = pNext;
            }

            shell.surfaceAreaUnitSphere = sum.magnitude * 0.5f;
        }

        public class Patch
        {
            [HarmonyPostfix, HarmonyPatch(typeof(UIDysonPanel), "_OnOpen")]
            public static void UIDysonPanel_OnOpen_Postfix()
            {
                if (!InitializedUI)
                    CreateUI();
            }

            [HarmonyPostfix, HarmonyPatch(typeof(UIDysonPanel), "UpdateSelectionVisibleChange")]
            public static void LayerVisibilityPostfix(UIDysonPanel __instance)
            {
                if (InitializedUI)
                    resPanelObj.SetActive(__instance.layerSelected > 0);

                var layer = SelectedLayer;
                if (layer != null && resSlider != null)
                {
                    float temp = layer.radius_lowRes;
                    resSlider.maxValue = layer.orbitRadius;
                    resSlider.value = temp;
                }
            }

            [HarmonyPostfix, HarmonyPatch(typeof(DysonShell), "Import")]
            public static void ShellSurfaceAreaPatch_Import(DysonShell __instance)
            {
                ComputeSurfaceArea(__instance);
            }

            [HarmonyPostfix, HarmonyPatch(typeof(DysonSphereLayer), "Import")]
            public static void ShellSurfaceAreaPatch_LayerImport(DysonSphereLayer __instance)
            {
                float sa = 0f;
                for (int i = 1; i < __instance.shellCursor; i++)
                {
                    DysonShell shell = __instance.shellPool[i];
                    if (shell != null && shell.id == i)
                        sa += shell.surfaceAreaUnitSphere;
                }
                __instance.surfaceAreaUnitSphere = sa;
            }

            [HarmonyPostfix, HarmonyPatch(typeof(DysonSphereLayer), "NewDysonShell")]
            public static void ShellSurfaceAreaPatch_NewDysonShell(DysonSphereLayer __instance, int __result)
            {
                DysonShell shell = __instance.shellPool[__result];
                ComputeSurfaceArea(shell);
                __instance.surfaceAreaUnitSphere += shell.surfaceAreaUnitSphere;
                UpdateLayer();
            }

            [HarmonyPrefix, HarmonyPatch(typeof(DysonSphereLayer), "RemoveDysonShell")]
            public static void ShellSurfaceAreaPatch_RemoveDysonShell(DysonSphereLayer __instance, int shellId)
            {
                DysonShell shell = __instance.shellPool[shellId];
                __instance.surfaceAreaUnitSphere -= shell.surfaceAreaUnitSphere;
                UpdateLayer();
            }
        }
    }
}
