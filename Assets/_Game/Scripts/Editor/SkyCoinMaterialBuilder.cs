using UnityEditor;
using UnityEngine;

namespace SkyCoin.EditorTools
{
    /// <summary>
    /// EDITOR-ONLY helper. Creates the project's shared material palette with the exact
    /// colours from the art direction, so the whole game stays visually consistent and
    /// you do not have to hand build seven materials.
    ///
    /// Safe to run more than once: existing materials are updated in place, so any
    /// material already assigned to prefabs and scene objects keeps its reference.
    ///
    /// Menu: Sky Coin Adventure > Setup > 1. Create Materials
    /// </summary>
    public static class SkyCoinMaterialBuilder
    {
        private const string MaterialsFolder = "Assets/_Game/Materials";

        // Menu entry disabled: the materials already exist in Assets/_Game/Materials.
        // [MenuItem("Sky Coin Adventure/Setup/1. Create Materials", false, 10)]
        public static void CreateMaterials()
        {
            EnsureFolder();

            int created = 0;
            created += CreateOrUpdate("M_Robot_Body",
                new Color(0.86f, 0.90f, 0.94f), 0.10f, 0.35f, null, 0f) ? 1 : 0;

            created += CreateOrUpdate("M_Robot_Dark",
                new Color(0.15f, 0.18f, 0.24f), 0.30f, 0.30f, null, 0f) ? 1 : 0;

            created += CreateOrUpdate("M_Robot_Accent",
                new Color(1.00f, 0.45f, 0.10f), 0.20f, 0.40f, new Color(1.00f, 0.45f, 0.10f), 1.5f) ? 1 : 0;

            created += CreateOrUpdate("M_Visor",
                new Color(0.04f, 0.07f, 0.10f), 0.10f, 0.60f, new Color(0.30f, 0.95f, 1.00f), 2.5f) ? 1 : 0;

            created += CreateOrUpdate("M_Ground_Grass",
                new Color(0.30f, 0.62f, 0.38f), 0.00f, 0.15f, null, 0f) ? 1 : 0;

            created += CreateOrUpdate("M_Ground_Rock",
                new Color(0.42f, 0.44f, 0.50f), 0.05f, 0.25f, null, 0f) ? 1 : 0;

            created += CreateOrUpdate("M_Bridge_Wood",
                new Color(0.55f, 0.38f, 0.24f), 0.00f, 0.20f, null, 0f) ? 1 : 0;

            created += CreateOrUpdate("M_Coin",
                new Color(1.00f, 0.78f, 0.20f), 0.65f, 0.70f, new Color(1.00f, 0.72f, 0.15f), 1.2f) ? 1 : 0;

            created += CreateOrUpdate("M_Enemy_Body",
                new Color(0.72f, 0.18f, 0.22f), 0.20f, 0.35f, null, 0f) ? 1 : 0;

            created += CreateOrUpdate("M_Enemy_Eye",
                new Color(0.05f, 0.05f, 0.06f), 0.00f, 0.70f, new Color(1.00f, 0.15f, 0.15f), 2.5f) ? 1 : 0;

            created += CreateOrUpdate("M_Hazard_Safe",
                new Color(0.35f, 0.55f, 0.65f), 0.10f, 0.35f, null, 0f) ? 1 : 0;

            created += CreateOrUpdate("M_Hazard_Warning",
                new Color(0.95f, 0.35f, 0.10f), 0.10f, 0.40f, new Color(1.00f, 0.35f, 0.05f), 2.0f) ? 1 : 0;

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[SkyCoin] Material palette ready in {MaterialsFolder} ({created} created, rest updated).");
        }

        private static void EnsureFolder()
        {
            if (AssetDatabase.IsValidFolder(MaterialsFolder)) return;

            if (!AssetDatabase.IsValidFolder("Assets/_Game"))
            {
                AssetDatabase.CreateFolder("Assets", "_Game");
            }

            AssetDatabase.CreateFolder("Assets/_Game", "Materials");
        }

        /// <summary>Creates the material if missing, otherwise updates it. Returns true when newly created.</summary>
        private static bool CreateOrUpdate(string materialName, Color baseColor, float metallic,
            float smoothness, Color? emissionColor, float emissionIntensity)
        {
            Shader urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null)
            {
                Debug.LogError("[SkyCoin] Could not find the shader 'Universal Render Pipeline/Lit'. " +
                               "Is this project using URP? (see the setup guide, section 2.1)");
                return false;
            }

            string path = $"{MaterialsFolder}/{materialName}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool isNew = material == null;

            if (isNew)
            {
                material = new Material(urpLit);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = urpLit;
            }

            // URP Lit uses _BaseColor, not the Built-in pipeline's _Color.
            material.SetColor("_BaseColor", baseColor);
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Smoothness", smoothness);

            if (emissionColor.HasValue && emissionIntensity > 0f)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emissionColor.Value * emissionIntensity);
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(material);
            return isNew;
        }
    }
}
