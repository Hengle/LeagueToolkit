﻿using Fantome.Libraries.League.Helpers.Cryptography;
using Fantome.Libraries.League.Helpers.Extensions;
using Fantome.Libraries.League.IO.AnimationFile;
using Fantome.Libraries.League.IO.SkeletonFile;
using ImageMagick;
using LeagueFileTranslator.FileTranslators.SKL.IO;
using SharpGLTF.Animations;
using SharpGLTF.Geometry;
using SharpGLTF.Geometry.VertexTypes;
using SharpGLTF.Materials;
using SharpGLTF.Scenes;
using SharpGLTF.Schema2;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using GltfAnimation = SharpGLTF.Schema2.Animation;
using LeagueAnimation = Fantome.Libraries.League.IO.AnimationFile.Animation;

namespace Fantome.Libraries.League.IO.SimpleSkin
{
    using VERTEX = VertexBuilder<VertexPositionNormal, VertexTexture1, VertexEmpty>;
    using VERTEX_SKINNED = VertexBuilder<VertexPositionNormal, VertexTexture1, VertexJoints4>;

    public static class SimpleSkinGltfExtensions
    {
        public static ModelRoot ToGltf(this SimpleSkin skn, Dictionary<string, MagickImage> materialTextues = null)
        {
            SceneBuilder sceneBuilder = new SceneBuilder("model");
            NodeBuilder rootNodeBuilder = new NodeBuilder();
            var meshBuilder = VERTEX.CreateCompatibleMesh();

            foreach (SimpleSkinSubmesh submesh in skn.Submeshes)
            {
                MaterialBuilder material = new MaterialBuilder(submesh.Name).WithSpecularGlossinessShader();
                var submeshPrimitive = meshBuilder.UsePrimitive(material);

                // Assign submesh Image
                if(materialTextues is not null && materialTextues.ContainsKey(submesh.Name))
                {
                    MagickImage submeshImage = materialTextues[submesh.Name];
                    AssignMaterialTexture(material, submeshImage);
                }

                // Build vertices
                var vertices = new List<VERTEX>(submesh.Vertices.Count);
                foreach (SimpleSkinVertex vertex in submesh.Vertices)
                {
                    VertexPositionNormal positionNormal = new VertexPositionNormal(vertex.Position, vertex.Normal);
                    VertexTexture1 uv = new VertexTexture1(vertex.UV);

                    vertices.Add(new VERTEX(positionNormal, uv));
                }

                // Add vertices to primitive
                for (int i = 0; i < submesh.Indices.Count; i += 3)
                {
                    VERTEX v1 = vertices[submesh.Indices[i + 0]];
                    VERTEX v2 = vertices[submesh.Indices[i + 1]];
                    VERTEX v3 = vertices[submesh.Indices[i + 2]];

                    submeshPrimitive.AddTriangle(v1, v2, v3);
                }
            }

            sceneBuilder.AddRigidMesh(meshBuilder, rootNodeBuilder);

            return sceneBuilder.ToGltf2();
        }

        public static ModelRoot ToGltf(this SimpleSkin skn, Skeleton skeleton, Dictionary<string, MagickImage> materialTextues = null, List<(string, LeagueAnimation)> leagueAnimations = null)
        {
            SceneBuilder sceneBuilder = new SceneBuilder("model");
            NodeBuilder rootNodeBuilder = new NodeBuilder();
            var meshBuilder = VERTEX_SKINNED.CreateCompatibleMesh();

            List<NodeBuilder> bones = CreateSkeleton(rootNodeBuilder, skeleton);

            foreach (SimpleSkinSubmesh submesh in skn.Submeshes)
            {
                MaterialBuilder material = new MaterialBuilder(submesh.Name).WithSpecularGlossinessShader();
                var submeshPrimitive = meshBuilder.UsePrimitive(material);

                // Assign submesh Image
                if (materialTextues is not null && materialTextues.ContainsKey(submesh.Name))
                {
                    MagickImage submeshImage = materialTextues[submesh.Name];
                    AssignMaterialTexture(material, submeshImage);
                }

                // Build vertices
                var vertices = new List<VERTEX_SKINNED>(submesh.Vertices.Count);
                foreach (SimpleSkinVertex vertex in submesh.Vertices)
                {
                    VertexPositionNormal positionNormal = new VertexPositionNormal(vertex.Position, vertex.Normal);
                    VertexTexture1 uv = new VertexTexture1(vertex.UV);
                    VertexJoints4 joints = new VertexJoints4(new (int, float)[]
                    {
                        (skeleton.Influences[vertex.BoneIndices[0]], vertex.Weights[0]),
                        (skeleton.Influences[vertex.BoneIndices[1]], vertex.Weights[1]),
                        (skeleton.Influences[vertex.BoneIndices[2]], vertex.Weights[2]),
                        (skeleton.Influences[vertex.BoneIndices[3]], vertex.Weights[3])
                    });

                    vertices.Add(new VERTEX_SKINNED(positionNormal, uv, joints));
                }

                // Add vertices to primitive
                for (int i = 0; i < submesh.Indices.Count; i += 3)
                {
                    VERTEX_SKINNED v1 = vertices[submesh.Indices[i + 0]];
                    VERTEX_SKINNED v2 = vertices[submesh.Indices[i + 1]];
                    VERTEX_SKINNED v3 = vertices[submesh.Indices[i + 2]];

                    submeshPrimitive.AddTriangle(v1, v2, v3);
                }
            }

            sceneBuilder.AddSkinnedMesh(meshBuilder, Matrix4x4.Identity, bones.ToArray());

            if (leagueAnimations != null)
            {
                CreateAnimations(bones, leagueAnimations);
            }

            return sceneBuilder.ToGltf2();
        }

        private static void AssignMaterialTexture(MaterialBuilder materialBuilder, MagickImage texture)
        {
            MemoryStream textureStream = new MemoryStream();

            texture.Write(textureStream, MagickFormat.Png);

            materialBuilder
                .UseChannel(KnownChannel.Diffuse)
                .UseTexture()
                .WithPrimaryImage(new SharpGLTF.Memory.MemoryImage(textureStream.GetBuffer()));
        }

        private static List<NodeBuilder> CreateSkeleton(NodeBuilder rootNode, Skeleton skeleton)
        {
            NodeBuilder skeletonRoot = rootNode.CreateNode("skeleton");
            List<NodeBuilder> bones = new List<NodeBuilder>();

            foreach (SkeletonJoint joint in skeleton.Joints)
            {
                // Root
                if (joint.ParentID == -1)
                {
                    NodeBuilder jointNode = skeletonRoot.CreateNode(joint.Name);

                    jointNode.LocalTransform = joint.LocalTransform;

                    bones.Add(jointNode);
                }
                else
                {
                    SkeletonJoint parentJoint = skeleton.Joints.FirstOrDefault(x => x.ID == joint.ParentID);
                    NodeBuilder parentNode = bones.FirstOrDefault(x => x.Name == parentJoint.Name);
                    NodeBuilder jointNode = parentNode.CreateNode(joint.Name);

                    jointNode.LocalTransform = joint.LocalTransform;

                    bones.Add(jointNode);
                }
            }

            return bones;
        }

        private static void CreateAnimations(List<NodeBuilder> joints, List<(string, LeagueAnimation)> leagueAnimations)
        {
            // Check if all animations have names, if not then create them
            for (int i = 0; i < leagueAnimations.Count; i++)
            {
                if (string.IsNullOrEmpty(leagueAnimations[i].Item1))
                {
                    leagueAnimations[i] = ("Animation" + i, leagueAnimations[i].Item2);
                }
            }

            foreach ((string animationName, LeagueAnimation leagueAnimation) in leagueAnimations)
            {
                foreach (AnimationTrack track in leagueAnimation.Tracks)
                {
                    NodeBuilder joint = joints.FirstOrDefault(x => Cryptography.ElfHash(x.Name) == track.JointHash);

                    if (joint is not null)
                    {
                        CurveBuilder<Vector3> translationBuilder = joint.UseTranslation().UseTrackBuilder(animationName);
                        foreach (var translation in track.Translations)
                        {
                            translationBuilder.SetPoint(translation.Key, translation.Value);
                        }

                        CurveBuilder<Quaternion> rotationBuilder = joint.UseRotation().UseTrackBuilder(animationName);
                        foreach (var rotation in track.Rotations)
                        {
                            rotationBuilder.SetPoint(rotation.Key, rotation.Value);
                        }

                        CurveBuilder<Vector3> scaleBuilder = joint.UseScale().UseTrackBuilder(animationName);
                        foreach (var scale in track.Scales)
                        {
                            scaleBuilder.SetPoint(scale.Key, scale.Value);
                        }
                    }
                }
            }
        }
    }
}
