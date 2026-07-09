#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.IO;

public static class MIYEONSI_CreateBulletGlassMaterials
{
    private const string Root = "Assets/MIYEONSI/BulletGlass";
    private const string TexturePath = Root + "/Textures/";
    private const string MaterialPath = Root + "/Materials/";

    [MenuItem("Tools/MIYEONSI/Create Bullet Glass Materials")]
    public static void CreateMaterials()
    {
        Directory.CreateDirectory(MaterialPath);

        Shader glassShader = Shader.Find("MIYEONSI/EnergyBullet/GlossyGlass");
        Shader coreShader = Shader.Find("MIYEONSI/EnergyBullet/AdditiveCore");

        if (glassShader == null)
        {
            Debug.LogError("MIYEONSI GlossyGlass shader not found. Check that the shader file imported correctly.");
            return;
        }

        Material glass = new Material(glassShader);
        glass.name = "MAT_EnergyBullet_Outer_GlossyGlass";
        glass.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + "MIYEONSI_BulletGlass_AlbedoAlpha.png"));
        glass.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + "MIYEONSI_BulletGlass_Normal.png"));
        glass.SetTexture("_RoughnessMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + "MIYEONSI_BulletGlass_Roughness.png"));
        glass.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + "MIYEONSI_BulletGlass_Emission.png"));
        glass.SetColor("_Color", new Color(1.0f, 0.07f, 0.14f, 0.36f));
        glass.SetColor("_FresnelColor", new Color(1.0f, 0.18f, 0.38f, 1.0f));
        glass.SetFloat("_EmissionIntensity", 2.7f);
        glass.SetFloat("_Smoothness", 0.94f);
        glass.SetFloat("_AlphaBoost", 1.15f);
        glass.SetFloat("_FresnelPower", 2.1f);
        glass.SetFloat("_FresnelIntensity", 3.4f);
        glass.SetFloat("_NormalStrength", 0.65f);
        glass.renderQueue = 3000;
        AssetDatabase.CreateAsset(glass, MaterialPath + "MAT_EnergyBullet_Outer_GlossyGlass.mat");

        if (coreShader != null)
        {
            Material core = new Material(coreShader);
            core.name = "MAT_EnergyBullet_Inner_AdditiveCore";
            core.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath + "MIYEONSI_BulletGlass_CoreSprite_RGBA.png"));
            core.SetColor("_Color", new Color(1.0f, 0.11f, 0.28f, 1.0f));
            core.SetFloat("_Intensity", 4.0f);
            core.SetFloat("_Alpha", 0.85f);
            core.renderQueue = 3010;
            AssetDatabase.CreateAsset(core, MaterialPath + "MAT_EnergyBullet_Inner_AdditiveCore.mat");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log("MIYEONSI bullet glass materials created at: " + MaterialPath);
    }
}
#endif
