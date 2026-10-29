using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Converts Kevin Iglesias's Human Character Dummy materials from Built-in
// "Unlit/Texture" (flat, ignores lights) to URP Lit so the dummy is lit and
// shadowed like the rest of the forest.
//
// The seven colour materials all use the same palette texture; they differ only
// by the texture's Y offset (Blue 0.375, Green 0.625, ...). That scale/offset is
// carried across to the Lit shader's base map, otherwise every colour would
// collapse to the same one. The six coloured materials start out as Material
// Variants of HumanDummy.mat; they are detached from it so each stands alone.
//
// Safe to run more than once: materials already on URP Lit are skipped. Original
// copies were saved outside Assets in _material_backups/ before this was added.
public static class DummyMaterialConverter
{
    const string MaterialFolder = "Assets/Kevin Iglesias/Human Character Dummy/Materials";

    // The dummy is a matte mannequin, so keep it non-metallic and low-gloss.
    // URP Lit's defaults (0.5 smoothness) would make it look like shiny plastic.
    const float Smoothness = 0.2f;
    const float Metallic = 0f;

    [MenuItem("Tools/Treadmill/Player Model/Convert Iglesias Dummy Materials To Lit (URP)")]
    static void ConvertFromMenu()
    {
        int converted = Convert(out string problem);
        if (problem != null)
        {
            EditorUtility.DisplayDialog("Couldn't convert the dummy materials", problem, "OK");
            return;
        }

        EditorUtility.DisplayDialog("Dummy materials",
            converted > 0
                ? $"Converted {converted} dummy material(s) to URP Lit. The dummy is now lit and shadowed."
                : "Nothing to do: the dummy materials are already on URP Lit.",
            "OK");
    }

    // Returns how many materials were converted. 'problem' is set (and nothing changed) if it can't run.
    public static int Convert(out string problem)
    {
        problem = null;

        Shader lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null)
        {
            problem = "The URP Lit shader wasn't found. Is the Universal Render Pipeline package installed?";
            return 0;
        }

        string[] guids = AssetDatabase.FindAssets("t:Material HumanDummy", new[] { MaterialFolder });
        if (guids.Length == 0)
        {
            problem = $"No HumanDummy materials found in:\n{MaterialFolder}";
            return 0;
        }

        // Pass 1: read every material's values while they are all still untouched.
        // The six colour materials are Material Variants of HumanDummy.mat: a variant
        // can't have its own shader (it follows its parent), and its only data is the
        // palette offset. Reading first means the order they're converted in can't matter.
        var pending = new List<Pending>();
        foreach (string guid in guids)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
            if (material == null) continue;
            if (material.shader == lit && !material.isVariant) continue; // already converted

            string textureProperty = material.HasProperty("_MainTex") ? "_MainTex" : "_BaseMap";
            pending.Add(new Pending
            {
                Material = material,
                Texture = material.GetTexture(textureProperty),
                Scale = material.GetTextureScale(textureProperty),
                Offset = material.GetTextureOffset(textureProperty),
                Tint = material.HasProperty("_Color") ? material.GetColor("_Color") : Color.white,
            });
        }

        // Pass 2: make each one a normal standalone material, then set it up for Lit.
        foreach (Pending p in pending)
        {
            Material material = p.Material;

            if (material.isVariant) material.parent = null;
            material.shader = lit;

            material.SetTexture("_BaseMap", p.Texture);
            material.SetTextureScale("_BaseMap", p.Scale);
            material.SetTextureOffset("_BaseMap", p.Offset);
            material.SetColor("_BaseColor", p.Tint);
            material.SetFloat("_Metallic", Metallic);
            material.SetFloat("_Smoothness", Smoothness);

            // Unlit never used the emission texture left over on the white material; Lit would.
            material.SetColor("_EmissionColor", Color.black);
            material.DisableKeyword("_EMISSION");

            EditorUtility.SetDirty(material);
        }

        if (pending.Count > 0) AssetDatabase.SaveAssets();
        return pending.Count;
    }

    struct Pending
    {
        public Material Material;
        public Texture Texture;
        public Vector2 Scale;
        public Vector2 Offset;
        public Color Tint;
    }
}
