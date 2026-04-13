#nullable enable
using System;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEditor;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_ProBuilder
    {
        #region TOOL METHODS

        /// <summary>Extrudes faces on a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <param name="distance">Distance to extrude faces outward. Default 0.5.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the extrusion, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-extrude-faces", Title = "ProBuilder / Extrude Faces")]
        [Description("Extrudes selected faces on a ProBuilder mesh by a given distance.")]
        public ToolResponse ExtrudeFaces(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("Extrude distance.")] float distance = 0.5f
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsProBuilderInstalled())
                {
                    return NotInstalled();
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found.");
                }

                var pb = GetPBMesh(go);

                if (pb == null)
                {
                    return ToolResponse.Error("No ProBuilderMesh component.");
                }

                var pbType = pb.GetType();
                var faces = pbType.GetProperty("faces")?.GetValue(pb);

                if (faces == null)
                {
                    return ToolResponse.Error("Could not get faces.");
                }

                var extrudeType = GetPBType("UnityEngine.ProBuilder.MeshOperations.ExtrudeElements");

                if (extrudeType == null)
                {
                    return ToolResponse.Error("ExtrudeElements not found.");
                }

                var extrudeMethod = extrudeType.GetMethod("Extrude", BindingFlags.Public | BindingFlags.Static);

                if (extrudeMethod == null)
                {
                    return ToolResponse.Error("Extrude method not found.");
                }
                try
                {
                    Undo.RecordObject(pb, "Extrude Faces");
                    extrudeMethod.Invoke(null, new object[] { pb, faces, 0, distance });

                    var toMeshMethod = pbType.GetMethod("ToMesh", BindingFlags.Public | BindingFlags.Instance);
                    var refreshMethod = pbType.GetMethod("Refresh", BindingFlags.Public | BindingFlags.Instance);
                    toMeshMethod?.Invoke(pb, null);
                    refreshMethod?.Invoke(pb, null);

                    return ToolResponse.Text($"Extruded all faces by {distance} on '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"Extrude failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Subdivides a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the subdivision, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-subdivide", Title = "ProBuilder / Subdivide")]
        [Description("Subdivides all faces of a ProBuilder mesh, increasing polygon density.")]
        public ToolResponse Subdivide(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsProBuilderInstalled())
                {
                    return NotInstalled();
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found.");
                }

                var pb = GetPBMesh(go);

                if (pb == null)
                {
                    return ToolResponse.Error("No ProBuilderMesh component.");
                }

                var subdivideType = GetPBType("UnityEngine.ProBuilder.MeshOperations.ConnectElements");

                if (subdivideType == null)
                {
                    return ToolResponse.Error("ConnectElements not found.");
                }

                var faces = pb.GetType().GetProperty("faces")?.GetValue(pb);

                if (faces == null)
                {
                    return ToolResponse.Error("Could not get faces.");
                }
                try
                {
                    Undo.RecordObject(pb, "Subdivide");
                    var method = subdivideType.GetMethod("Connect", BindingFlags.Public | BindingFlags.Static);
                    method?.Invoke(null, new object[] { pb, faces });

                    pb.GetType().GetMethod("ToMesh")?.Invoke(pb, null);
                    pb.GetType().GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Subdivided mesh on '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"Subdivide failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Flips face normals on a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the flip, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-flip-normals", Title = "ProBuilder / Flip Normals")]
        [Description("Flips all face normals on a ProBuilder mesh.")]
        public ToolResponse FlipNormals(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsProBuilderInstalled())
                {
                    return NotInstalled();
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found.");
                }

                var pb = GetPBMesh(go);

                if (pb == null)
                {
                    return ToolResponse.Error("No ProBuilderMesh component.");
                }

                var faces = pb.GetType().GetProperty("faces")?.GetValue(pb);

                if (faces == null)
                {
                    return ToolResponse.Error("Could not get faces.");
                }
                try
                {
                    Undo.RecordObject(pb, "Flip Normals");
                    var faceUtilType = GetPBType("UnityEngine.ProBuilder.FaceRebuildData") ?? GetPBType("UnityEngine.ProBuilder.MeshOperations.SurfaceTopology");
                    var faceType = GetPBType("UnityEngine.ProBuilder.Face");

                    if (faceType != null && faces is System.Collections.IEnumerable enumerable)
                    {
                        var reverseMethod = faceType.GetMethod("Reverse");

                        foreach (var face in enumerable)
                        {
                            reverseMethod?.Invoke(face, null);
                        }
                    }

                    pb.GetType().GetMethod("ToMesh")?.Invoke(pb, null);
                    pb.GetType().GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Flipped normals on '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"Flip normals failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Centers the pivot of a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
       /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the pivot was centered, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-center-pivot", Title = "ProBuilder / Center Pivot")]
        [Description("Centers the pivot point of a ProBuilder mesh to its geometric center.")]
        public ToolResponse CenterPivot(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsProBuilderInstalled())
                {
                    return NotInstalled();
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found.");
                }

                var pb = GetPBMesh(go);

                if (pb == null)
                {
                    return ToolResponse.Error("No ProBuilderMesh component.");
                }
                try
                {
                    Undo.RecordObject(pb, "Center Pivot");
                    Undo.RecordObject(go.transform, "Center Pivot");

                    var centerMethod = pb.GetType().GetMethod("CenterPivot", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
                    centerMethod?.Invoke(pb, null);

                    pb.GetType().GetMethod("ToMesh")?.Invoke(pb, null);
                    pb.GetType().GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Centered pivot on '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"Center pivot failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Sets material on ProBuilder faces.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <param name="materialPath">Project-relative path to the Material asset to assign.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the material was set, or an error if the material or mesh is not found.</returns>
        [McpTool("probuilder-set-face-material", Title = "ProBuilder / Set Face Material")]
        [Description("Assigns a material to all faces of a ProBuilder mesh.")]
        public ToolResponse SetFaceMaterial(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("Material asset path.")] string materialPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsProBuilderInstalled())
                {
                    return NotInstalled();
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found.");
                }

                if (string.IsNullOrWhiteSpace(materialPath))
                {
                    return ToolResponse.Error("materialPath is required.");
                }

                var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);

                if (material == null)
                {
                    return ToolResponse.Error($"Material not found at '{materialPath}'.");
                }

                
                if (!go.TryGetComponent<Renderer>(out var renderer))
                {
                    return ToolResponse.Error("No Renderer found.");
                }

                Undo.RecordObject(renderer, "Set ProBuilder Material");
                renderer.sharedMaterial = material;

                return ToolResponse.Text($"Set material '{material.name}' on '{go.name}'.");
            });
        }

        /// <summary>Validates a ProBuilder mesh for errors.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <returns>A <see cref="ToolResponse"/> with mesh validation info (vertex/face counts, component presence), or an error if not found.</returns>
        [McpTool("probuilder-validate-mesh", Title = "ProBuilder / Validate Mesh", ReadOnlyHint = true)]
        [Description("Validates a ProBuilder mesh for common issues.")]
        public ToolResponse ValidateMesh(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsProBuilderInstalled())
                {
                    return NotInstalled();
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found.");
                }

                var pb = GetPBMesh(go);

                if (pb == null)
                {
                    return ToolResponse.Error("No ProBuilderMesh component.");
                }

                var pbType = pb.GetType();
                var vertCount = (int)(pbType.GetProperty("vertexCount")?.GetValue(pb) ?? 0);
                var faceCount = (int)(pbType.GetProperty("faceCount")?.GetValue(pb) ?? 0);

                var sb = new StringBuilder();
                sb.AppendLine($"Mesh Validation: {go.name}");
                sb.AppendLine($"  Vertices: {vertCount}");
                sb.AppendLine($"  Faces: {faceCount}");
                sb.AppendLine($"  Has MeshFilter: {go.GetComponent<MeshFilter>() != null}");
                sb.AppendLine($"  Has MeshRenderer: {go.GetComponent<MeshRenderer>() != null}");
                sb.AppendLine($"  Has MeshCollider: {go.GetComponent<MeshCollider>() != null}");
                sb.AppendLine($"  Status: {(vertCount > 0 && faceCount > 0 ? "OK" : "WARNING: empty mesh")}");

                return ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>Repairs a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the repair, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-repair-mesh", Title = "ProBuilder / Repair Mesh")]
        [Description("Attempts to repair a ProBuilder mesh by rebuilding it.")]
        public ToolResponse RepairMesh(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsProBuilderInstalled())
                {
                    return NotInstalled();
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found.");
                }

                var pb = GetPBMesh(go);

                if (pb == null)
                {
                    return ToolResponse.Error("No ProBuilderMesh component.");
                }
                try
                {
                    Undo.RecordObject(pb, "Repair Mesh");
                    pb.GetType().GetMethod("ToMesh")?.Invoke(pb, null);
                    pb.GetType().GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Repaired mesh on '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"Repair failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Combines multiple ProBuilder meshes into one.</summary>
        /// <param name="targetsJson">JSON array of target descriptors with "id" and/or "path" fields (e.g. <c>[{"id":100},{"id":200}]</c>).</param>
       /// <returns>A <see cref="ToolResponse"/> confirming the combine, or an error if ProBuilder is not installed or targets are invalid.</returns>
        [McpTool("probuilder-combine-meshes", Title = "ProBuilder / Combine Meshes")]
        [Description("Combines multiple ProBuilder meshes (by instance ID or path) into a single mesh. Provide a JSON array of objects with 'id' and/or 'path'.")]
        public ToolResponse CombineMeshes(
            [Description("JSON array of target descriptors, e.g. [{\"id\":100},{\"id\":200}].")] string targetsJson = "[]"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsProBuilderInstalled())
                {
                    return NotInstalled();
                }

                var targets = ResolveTargets(targetsJson);

                if (targets.Count == 0)
                {
                    return ToolResponse.Error("No valid GameObjects found in targetsJson.");
                }

                var combineType = GetPBType("UnityEngine.ProBuilder.MeshOperations.CombineMeshes");

                if (combineType == null)
                {
                    return ToolResponse.Error("CombineMeshes type not found.");
                }

                var method = FindStaticMethod(combineType, "Combine");

                if (method == null)
                {
                    return ToolResponse.Error("Combine method not found.");
                }

                var pbType = GetPBType("UnityEngine.ProBuilder.ProBuilderMesh");

                if (pbType == null)
                {
                    return ToolResponse.Error("ProBuilderMesh type not found.");
                }

                var typedList = System.Array.CreateInstance(pbType, targets.Count);

                for (int i = 0; i < targets.Count; i++)
                {
                    var pbComp = targets[i].GetComponent(pbType);

                    if (pbComp == null)
                    {
                        return ToolResponse.Error($"'{targets[i].name}' has no ProBuilderMesh.");
                    }

                    typedList.SetValue(pbComp, i);
                }

                try
                {
                    Undo.RecordObjects(targets.ToArray(), "Combine Meshes");
                    method.Invoke(null, new object[] { typedList });
                    return ToolResponse.Text($"Combined {targets.Count} meshes.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"CombineMeshes failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Merges multiple ProBuilder objects into one new GameObject.</summary>
        /// <param name="targetsJson">JSON array of target descriptors with "id" and/or "path" fields (e.g. <c>[{"id":100},{"id":200}]</c>).</param>
        /// <param name="newName">Name for the merged GameObject. Default "MergedMesh".</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the merge, or an error if fewer than 2 targets or ProBuilder is not installed.</returns>
        [McpTool("probuilder-merge-objects", Title = "ProBuilder / Merge Objects")]
        [Description("Merges multiple ProBuilder GameObjects into a single new GameObject.")]
        public ToolResponse MergeObjects(
            [Description("JSON array of target descriptors, e.g. [{\"id\":100},{\"id\":200}].")] string targetsJson = "[]",
            [Description("Name for the merged GameObject.")] string newName = "MergedMesh"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsProBuilderInstalled())
                {
                    return NotInstalled();
                }

                var targets = ResolveTargets(targetsJson);

                if (targets.Count < 2)
                {
                    return ToolResponse.Error("At least 2 valid GameObjects required.");
                }

                var combineType = GetPBType("UnityEngine.ProBuilder.MeshOperations.CombineMeshes");

                if (combineType == null)
                {
                    return ToolResponse.Error("CombineMeshes type not found.");
                }

                var method = FindStaticMethod(combineType, "Combine");

                if (method == null)
                {
                    return ToolResponse.Error("Combine method not found.");
                }

                var pbType = GetPBType("UnityEngine.ProBuilder.ProBuilderMesh");

                if (pbType == null)
                {
                    return ToolResponse.Error("ProBuilderMesh type not found.");
                }

                var typedList = System.Array.CreateInstance(pbType, targets.Count);

                for (int i = 0; i < targets.Count; i++)
                {
                    var pbComp = targets[i].GetComponent(pbType);

                    if (pbComp == null)
                    {
                        return ToolResponse.Error($"'{targets[i].name}' has no ProBuilderMesh.");
                    }

                    typedList.SetValue(pbComp, i);
                }

                try
                {
                    Undo.RecordObjects(targets.ToArray(), "Merge Objects");
                    var result = method.Invoke(null, new object[] { typedList });
                    var resultComp = result as UnityEngine.Component;

                    if (resultComp != null)
                    {
                        resultComp.gameObject.name = string.IsNullOrWhiteSpace(newName) ? "MergedMesh" : newName;
                        Undo.RegisterCreatedObjectUndo(resultComp.gameObject, "Merge Objects");
                    }

                    return ToolResponse.Text($"Merged {targets.Count} objects into '{newName}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"MergeObjects failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Duplicates a ProBuilder mesh and flips its normals.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the duplicate was created with flipped normals, or an error if the operation fails.</returns>
        [McpTool("probuilder-duplicate-and-flip", Title = "ProBuilder / Duplicate And Flip")]
        [Description("Duplicates a ProBuilder GameObject and flips the normals of the duplicate, useful for double-sided meshes.")]
        public ToolResponse DuplicateAndFlip(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsProBuilderInstalled())
                {
                    return NotInstalled();
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found.");
                }

                var pb = GetPBMesh(go);

                if (pb == null)
                {
                    return ToolResponse.Error("No ProBuilderMesh component.");
                }
                try
                {
                    var duplicate = UnityEngine.Object.Instantiate(go, go.transform.position, go.transform.rotation);
                    duplicate.name = go.name + "_Flipped";
                    Undo.RegisterCreatedObjectUndo(duplicate, "Duplicate And Flip");

                    var dupPb = GetPBMesh(duplicate);

                    if (dupPb == null)
                    {
                        return ToolResponse.Error("Duplicate has no ProBuilderMesh.");
                    }

                    var dupPbType = dupPb.GetType();
                    var faces = dupPbType.GetProperty("faces")?.GetValue(dupPb);
                    var faceType = GetPBType("UnityEngine.ProBuilder.Face");

                    if (faceType != null && faces is System.Collections.IEnumerable enumerable)
                    {
                        var reverseMethod = faceType.GetMethod("Reverse");

                        foreach (var face in enumerable)
                        {
                            reverseMethod?.Invoke(face, null);
                        }
                    }

                    dupPbType.GetMethod("ToMesh")?.Invoke(dupPb, null);
                    dupPbType.GetMethod("Refresh")?.Invoke(dupPb, null);

                    return ToolResponse.Text($"Duplicated '{go.name}' and flipped normals on '{duplicate.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"DuplicateAndFlip failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Creates a polygon face on a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the polygon was appended, or an error if ProBuilder is not installed or the mesh has too few vertices.</returns>
        [McpTool("probuilder-create-polygon", Title = "ProBuilder / Create Polygon")]
        [Description("Appends a new polygon face to an existing ProBuilder mesh using its existing vertices.")]
        public ToolResponse CreatePolygon(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsProBuilderInstalled())
                {
                    return NotInstalled();
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found.");
                }

                var pb = GetPBMesh(go);

                if (pb == null)
                {
                    return ToolResponse.Error("No ProBuilderMesh component.");
                }

                var appendType = GetPBType("UnityEngine.ProBuilder.MeshOperations.AppendElements");

                if (appendType == null)
                {
                    return ToolResponse.Error("AppendElements type not found.");
                }

                var method = FindStaticMethod(appendType, "AppendPolygon") ?? FindStaticMethod(appendType, "CreatePolygon");

                if (method == null)
                {
                    return ToolResponse.Error("AppendPolygon method not found.");
                }
                try
                {
                    Undo.RecordObject(pb, "Create Polygon");
                    var pbType = pb.GetType();

                    if (pbType.GetProperty("positions")?.GetValue(pb) is not Vector3[] positions || positions.Length < 3)
                    {
                        return ToolResponse.Error("Mesh has fewer than 3 vertices.");
                    }

                    int[] indices = new int[positions.Length];

                    for (int i = 0; i < positions.Length; i++)
                    {
                        indices[i] = i;
                    }

                    method.Invoke(null, new object[] { pb, indices, false });

                    pbType.GetMethod("ToMesh")?.Invoke(pb, null);
                    pbType.GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Appended polygon face to '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"CreatePolygon failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Freezes the transform of a ProBuilder mesh (bakes transform into vertex positions).</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the transform was frozen, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-freeze-transform", Title = "ProBuilder / Freeze Transform")]
        [Description("Freezes (bakes) the current transform into a ProBuilder mesh's vertex positions and resets the transform to identity.")]
        public ToolResponse FreezeTransform(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsProBuilderInstalled())
                {
                    return NotInstalled();
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found.");
                }

                var pb = GetPBMesh(go);

                if (pb == null)
                {
                    return ToolResponse.Error("No ProBuilderMesh component.");
                }
                try
                {
                    Undo.RecordObject(pb, "Freeze Transform");
                    Undo.RecordObject(go.transform, "Freeze Transform");
                    var pbType = pb.GetType();
                    var freezeMethod = pbType.GetMethod("FreezeScaleTransform", BindingFlags.Public | BindingFlags.Instance) ?? pbType.GetMethod("FreezeTransform", BindingFlags.Public | BindingFlags.Instance);

                    if (freezeMethod != null)
                    {
                        freezeMethod.Invoke(pb, null);
                    }
                    else
                    {
                        if (pbType.GetProperty("positions")?.GetValue(pb) is not Vector3[] positions)
                        {
                            return ToolResponse.Error("Could not read vertex positions.");
                        }

                        var trs = go.transform.localToWorldMatrix;

                        for (int i = 0; i < positions.Length; i++)
                        {
                            positions[i] = trs.MultiplyPoint3x4(positions[i]);
                        }

                        var setPosProp = pbType.GetProperty("positions");

                        if (setPosProp != null && setPosProp.CanWrite)
                        {
                            setPosProp.SetValue(pb, positions);
                        }
                        else
                        {
                            pbType.GetMethod("SetPositions", new[] { typeof(Vector3[]) })?.Invoke(pb, new object[] { positions });
                        }

                        go.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                        go.transform.localScale = Vector3.one;
                    }

                    pbType.GetMethod("ToMesh")?.Invoke(pb, null);
                    pbType.GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Froze transform on '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"FreezeTransform failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        #endregion
    }
}