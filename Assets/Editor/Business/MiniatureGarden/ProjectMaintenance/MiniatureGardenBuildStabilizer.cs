using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class MiniatureGardenBuildStabilizer
{
    private const string GraphicsSettingsAssetPath = "ProjectSettings/GraphicsSettings.asset";
    private const string CartoonShaderPath = "Assets/Shader/Toon/Generic/Cartoon.shader";
    private const string XRayShaderPath = "Assets/Shader/Toon/Generic/XRay.shader";
    private const string DefaultCursorTexturePath = "Assets/Sprites/Common/Cursors/9.png";
    private const string UniversalRenderPipelineLitShaderName = "Universal Render Pipeline/Lit";

    [MenuItem("MiniatureGarden/Project Maintenance/Apply Build Stabilizer")]
    private static void ApplyBuildStabilizerFromMenu()
    {
        ApplyBuildStabilizer();
    }

    public static void ApplyBuildStabilizer()
    {
        RemoveProblematicAlwaysIncludedShaders();
        FixDefaultCursorTextureImporter();
    }

    private static void RemoveProblematicAlwaysIncludedShaders()
    {
        Object[] graphicsSettingsAssets = AssetDatabase.LoadAllAssetsAtPath(GraphicsSettingsAssetPath);
        if (graphicsSettingsAssets == null || graphicsSettingsAssets.Length == 0)
        {
            Debug.LogWarning("Could not load GraphicsSettings.asset. Skipped Always Included Shaders cleanup.");
            return;
        }

        Shader cartoonShader = AssetDatabase.LoadAssetAtPath<Shader>(CartoonShaderPath);
        Shader xRayShader = AssetDatabase.LoadAssetAtPath<Shader>(XRayShaderPath);
        SerializedObject graphicsSettingsObject = new SerializedObject(graphicsSettingsAssets[0]);
        SerializedProperty alwaysIncludedShadersProperty =
            graphicsSettingsObject.FindProperty("m_AlwaysIncludedShaders");
        if (alwaysIncludedShadersProperty == null || !alwaysIncludedShadersProperty.isArray)
        {
            Debug.LogWarning("Could not find m_AlwaysIncludedShaders on GraphicsSettings.asset.");
            return;
        }

        List<string> removedShaderNames = new List<string>(4);
        bool changed = false;

        for (int i = alwaysIncludedShadersProperty.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty shaderProperty = alwaysIncludedShadersProperty.GetArrayElementAtIndex(i);
            Shader shader = shaderProperty.objectReferenceValue as Shader;
            if (!ShouldRemoveAlwaysIncludedShader(shader, cartoonShader, xRayShader))
                continue;

            removedShaderNames.Add(shader.name);
            shaderProperty.objectReferenceValue = null;
            alwaysIncludedShadersProperty.DeleteArrayElementAtIndex(i);
            changed = true;
        }

        if (!changed)
            return;

        graphicsSettingsObject.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(graphicsSettingsAssets[0]);
        AssetDatabase.SaveAssets();
        Debug.Log($"Removed Always Included Shaders before build: {string.Join(", ", removedShaderNames)}");
    }

    private static bool ShouldRemoveAlwaysIncludedShader(Shader shader, Shader cartoonShader, Shader xRayShader)
    {
        if (shader == null)
            return false;

        return shader == cartoonShader ||
               shader == xRayShader ||
               shader.name == UniversalRenderPipelineLitShaderName;
    }

    private static void FixDefaultCursorTextureImporter()
    {
        TextureImporter textureImporter = AssetImporter.GetAtPath(DefaultCursorTexturePath) as TextureImporter;
        if (textureImporter == null)
            return;

        bool changed = false;

        if (textureImporter.textureType != TextureImporterType.Cursor)
        {
            textureImporter.textureType = TextureImporterType.Cursor;
            changed = true;
        }

        if (textureImporter.mipmapEnabled)
        {
            textureImporter.mipmapEnabled = false;
            changed = true;
        }

        if (!textureImporter.isReadable)
        {
            textureImporter.isReadable = true;
            changed = true;
        }

        if (!textureImporter.alphaIsTransparency)
        {
            textureImporter.alphaIsTransparency = true;
            changed = true;
        }

        if (textureImporter.textureCompression != TextureImporterCompression.Uncompressed)
        {
            textureImporter.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }

        TextureImporterPlatformSettings defaultSettings =
            textureImporter.GetPlatformTextureSettings("DefaultTexturePlatform");
        TextureImporterPlatformSettings standaloneSettings =
            textureImporter.GetPlatformTextureSettings("Standalone");

        changed |= ApplyCursorPlatformSettings(ref defaultSettings);
        changed |= ApplyCursorPlatformSettings(ref standaloneSettings);

        textureImporter.SetPlatformTextureSettings(defaultSettings);
        textureImporter.SetPlatformTextureSettings(standaloneSettings);

        if (!changed)
            return;

        textureImporter.SaveAndReimport();
        Debug.Log("Fixed default cursor texture importer settings for player build.");
    }

    private static bool ApplyCursorPlatformSettings(ref TextureImporterPlatformSettings settings)
    {
        bool changed = false;

        if (!settings.overridden)
        {
            settings.overridden = true;
            changed = true;
        }

        if (settings.maxTextureSize != 128)
        {
            settings.maxTextureSize = 128;
            changed = true;
        }

        if (settings.textureCompression != TextureImporterCompression.Uncompressed)
        {
            settings.textureCompression = TextureImporterCompression.Uncompressed;
            changed = true;
        }

        if (settings.format != TextureImporterFormat.RGBA32)
        {
            settings.format = TextureImporterFormat.RGBA32;
            changed = true;
        }

        return changed;
    }
}

internal sealed class MiniatureGardenBuildStabilizerBuildProcessor : IPreprocessBuildWithReport
{
    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        MiniatureGardenBuildStabilizer.ApplyBuildStabilizer();
    }
}
