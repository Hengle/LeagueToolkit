﻿using System;
using System.IO;
using System.Linq;
using System.Numerics;
using CommunityToolkit.Diagnostics;
using LeagueToolkit.Core.Environment;
using LeagueToolkit.Core.Renderer;
using LeagueToolkit.IO.MapGeometryFile;
using LeagueToolkit.Meta;
using LeagueToolkit.Meta.Classes;
using SharpGLTF.Schema2;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using GltfImage = SharpGLTF.Schema2.Image;
using TextureRegistry = System.Collections.Generic.Dictionary<string, SharpGLTF.Schema2.Image>;

namespace LeagueToolkit.IO.Extensions.MapGeometry.Shaders;

internal sealed class SrxBlendInfernalIsland : IMaterialAdapter
{
    private const string DEFAULT_DIFFUSE_TEXTURE = "ASSETS/Maps/KitPieces/SRX/textures/Earth_RockSpike_A.dds";

    public void InitializeMaterial(
        Material gltfMaterial,
        StaticMaterialDef materialDef,
        EnvironmentAssetMesh mesh,
        TextureRegistry textureRegistry,
        ModelRoot root,
        MapGeometryGltfConversionContext context
    )
    {
        gltfMaterial.WithUnlit();

        InitializeMaterialRenderTechnique(gltfMaterial, materialDef);
        InitializeMaterialBaseColorChannel(gltfMaterial, materialDef, mesh, root, textureRegistry, context);
    }

    private static void InitializeMaterialRenderTechnique(Material gltfMaterial, StaticMaterialDef materialDef)
    {
        StaticMaterialShaderParamDef alphaTestDef = materialDef.ParamValues.FirstOrDefault(x =>
            x.Value.Name is "Alpha_Test_Value"
        );
        alphaTestDef ??= new() { Value = Vector4.Zero with { X = 0.3f } };

        StaticMaterialTechniqueDef techniqueDef = materialDef.Techniques.FirstOrDefault() ?? new(new());
        StaticMaterialPassDef passDef = techniqueDef.Passes.FirstOrDefault() ?? new(new());

        if (passDef.BlendEnable)
        {
            gltfMaterial.Alpha = AlphaMode.MASK;
            gltfMaterial.AlphaCutoff = alphaTestDef.Value.X;
        }
    }

    private static void InitializeMaterialBaseColorChannel(
        Material gltfMaterial,
        StaticMaterialDef materialDef,
        EnvironmentAssetMesh mesh,
        ModelRoot root,
        TextureRegistry textureRegistry,
        MapGeometryGltfConversionContext context
    )
    {
        // Resolve diffuse sampler definition, return if not found
        StaticMaterialShaderSamplerDef samplerDef = materialDef.SamplerValues.FirstOrDefault(x =>
            x.Value.TextureName is "DiffuseTexture"
        );
        samplerDef ??= new() { TexturePath = DEFAULT_DIFFUSE_TEXTURE };

        gltfMaterial.WithChannelTexture(
            "BaseColor",
            0,
            TextureUtils.CreateGltfImage(samplerDef.TexturePath, root, textureRegistry, context)
        );
    }
}
