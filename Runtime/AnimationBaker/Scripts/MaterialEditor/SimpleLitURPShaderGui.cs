#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor.Rendering.Universal;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor;
using System;

public class SimpleLitURPShaderGui : BaseShaderGUI
{
    // Properties
    private SimpleLitGUI.SimpleLitProperties shadingModelProperties;
    /*
        _PosTex("position texture", 2D) = "black"{}
        _NmlTex("normal texture", 2D) = "white"{}
        _DT("delta time", float) = 0
        _Length("animation length", Float) = 1
        [Toggle(ANIM_LOOP)] _Loop("loop", Float) = 0
    */
    protected MaterialProperty _PosTexProp { get; set; }

    protected MaterialProperty _NmlTexProp { get; set; }

    protected MaterialProperty _DTProp { get; set; }

    protected MaterialProperty _LengthProp { get; set; }

    protected MaterialProperty _LoopProp { get; set; }

    // collect properties from the material properties
    public override void FindProperties(MaterialProperty[] properties)
    {
        base.FindProperties(properties);
        shadingModelProperties = new SimpleLitGUI.SimpleLitProperties(properties);
        _PosTexProp = FindProperty("_PosTex", properties, false);
        _NmlTexProp = FindProperty("_NmlTex", properties, false);
        _DTProp = FindProperty("_DT", properties, false);
        _LengthProp = FindProperty("_Length", properties, false);
        _LoopProp = FindProperty("_Loop", properties, false);
    }

    // material changed check
    public override void MaterialChanged(Material material)
    {
        if (material == null)
            throw new ArgumentNullException("material");

        SetMaterialKeywords(material, SimpleLitGUI.SetMaterialKeywords);
    }

    // material main surface options
    public override void DrawSurfaceOptions(Material material)
    {
        if (material == null)
            throw new ArgumentNullException("material");

        // Use default labelWidth
        EditorGUIUtility.labelWidth = 0f;

        // Detect any changes to the material
        EditorGUI.BeginChangeCheck();
        {
            base.DrawSurfaceOptions(material);
        }
        if (EditorGUI.EndChangeCheck())
        {
            foreach (var obj in blendModeProp.targets)
                MaterialChanged((Material)obj);
        }
    }

    // material main surface inputs
    public override void DrawSurfaceInputs(Material material)
    {
        base.DrawSurfaceInputs(material);
        SimpleLitGUI.Inputs(shadingModelProperties, materialEditor, material);
        DrawEmissionProperties(material, true);
        DrawTileOffset(materialEditor, baseMapProp);
    }

    public override void DrawAdvancedOptions(Material material)
    {
        SimpleLitGUI.Advanced(shadingModelProperties);
        base.DrawAdvancedOptions(material);
        // Draw 
        materialEditor.ShaderProperty(_PosTexProp, "Positions Animations", 1);
        materialEditor.ShaderProperty(_NmlTexProp, "Normal Animations", 1);
        materialEditor.ShaderProperty(_DTProp, "Delta Time", 1);
        materialEditor.ShaderProperty(_LengthProp, "Length", 1);
        materialEditor.ShaderProperty(_LoopProp, "Loop", 1);

        if (_LoopProp.floatValue > 0)
        {
            material.EnableKeyword("_ANIM_LOOP");
        }
        else
        {
            material.DisableKeyword("_ANIM_LOOP");
        }
    }

    public override void AssignNewShaderToMaterial(Material material, Shader oldShader, Shader newShader)
    {
        if (material == null)
            throw new ArgumentNullException("material");

        // _Emission property is lost after assigning Standard shader to the material
        // thus transfer it before assigning the new shader
        if (material.HasProperty("_Emission"))
        {
            material.SetColor("_EmissionColor", material.GetColor("_Emission"));
        }

        base.AssignNewShaderToMaterial(material, oldShader, newShader);

        if (oldShader == null || !oldShader.name.Contains("Legacy Shaders/"))
        {
            SetupMaterialBlendMode(material);
            return;
        }

        SurfaceType surfaceType = SurfaceType.Opaque;
        BlendMode blendMode = BlendMode.Alpha;
        if (oldShader.name.Contains("/Transparent/Cutout/"))
        {
            surfaceType = SurfaceType.Opaque;
            material.SetFloat("_AlphaClip", 1);
        }
        else if (oldShader.name.Contains("/Transparent/"))
        {
            // NOTE: legacy shaders did not provide physically based transparency
            // therefore Fade mode
            surfaceType = SurfaceType.Transparent;
            blendMode = BlendMode.Alpha;
        }
        material.SetFloat("_Surface", (float)surfaceType);
        material.SetFloat("_Blend", (float)blendMode);

        MaterialChanged(material);
    }
}
#endif