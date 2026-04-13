#nullable enable
using System;
using System.ComponentModel;
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

        /// <summary>Deletes faces by index on a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <param name="faceIndicesJson">JSON int array of face indices to delete (e.g. "[0,1,2]").</param>
        /// <returns>A <see cref="ToolResponse"/> confirming deletion, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-delete-faces", Title = "ProBuilder / Delete Faces")]
        [Description("Deletes faces at the given indices from a ProBuilder mesh.")]
        public ToolResponse DeleteFaces(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("JSON int array of face indices to delete, e.g. [0,1,2].")] string faceIndicesJson = "[0]"
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

                int[]? indices = ParseIntArray(faceIndicesJson);

                if (indices == null || indices.Length == 0)
                {
                    return ToolResponse.Error("faceIndicesJson must be a non-empty JSON int array.");
                }

                var pbType = pb.GetType();
                var facesRaw = pbType.GetProperty("faces")?.GetValue(pb);

                if (facesRaw == null)
                {
                    return ToolResponse.Error("Could not get faces.");
                }

                var faceList = new System.Collections.Generic.List<object>();
                int idx = 0;

                foreach (var face in (System.Collections.IEnumerable)facesRaw)
                {
                    for (int i = 0; i < indices.Length; i++)
                    {
                        if (indices[i] == idx) { faceList.Add(face); break; }
                    }

                    idx++;
                }

                if (faceList.Count == 0)
                {
                    return ToolResponse.Error("No faces found at specified indices.");
                }

                var deleteType = GetPBType("UnityEngine.ProBuilder.MeshOperations.DeleteElements");

                if (deleteType == null)
                {
                    return ToolResponse.Error("DeleteElements type not found.");
                }

                var method = FindStaticMethod(deleteType, "DeleteFaces");

                if (method == null)
                {
                    return ToolResponse.Error("DeleteFaces method not found.");
                }
                try
                {
                    Undo.RecordObject(pb, "Delete Faces");
                    var faceType = GetPBType("UnityEngine.ProBuilder.Face");

                    if (faceType == null)
                    {
                        return ToolResponse.Error("Face type not found.");
                    }

                    var typedArray = System.Array.CreateInstance(faceType, faceList.Count);

                    for (int i = 0; i < faceList.Count; i++)
                    {
                        typedArray.SetValue(faceList[i], i);
                    }

                    method.Invoke(null, new object[] { pb, typedArray });

                    pbType.GetMethod("ToMesh")?.Invoke(pb, null);
                    pbType.GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Deleted {faceList.Count} face(s) on '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"DeleteFaces failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Detaches faces from a ProBuilder mesh into a new GameObject.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
       /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <param name="deleteSource">When true, deletes the source faces after detaching. Default true.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the detach, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-detach-faces", Title = "ProBuilder / Detach Faces")]
        [Description("Detaches all faces of a ProBuilder mesh into a new child GameObject.")]
        public ToolResponse DetachFaces(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("Delete source faces after detaching.")] bool deleteSource = true
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

                var detachType = GetPBType("UnityEngine.ProBuilder.MeshOperations.DetachFaces") ?? GetPBType("UnityEngine.ProBuilder.MeshOperations.SurfaceTopology");

                if (detachType == null)
                {
                    return ToolResponse.Error("DetachFaces type not found.");
                }

                var method = FindStaticMethod(detachType, "DetachFaces") ?? FindStaticMethod(detachType, "Detach");

                if (method == null)
                {
                    return ToolResponse.Error("DetachFaces method not found.");
                }
                try
                {
                    Undo.RecordObject(pb, "Detach Faces");
                    var methodParams = method.GetParameters();
                    object[] args = methodParams.Length == 3 ? new object[] { pb, faces, deleteSource } : new object[] { pb, faces };
                    method.Invoke(null, args);

                    pbType.GetMethod("ToMesh")?.Invoke(pb, null);
                    pbType.GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Detached faces on '{go.name}' (deleteSource={deleteSource}).");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"DetachFaces failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Merges all faces of a ProBuilder mesh into one.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the merge, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-merge-faces", Title = "ProBuilder / Merge Faces")]
        [Description("Merges all coplanar or selected faces of a ProBuilder mesh into a single face.")]
        public ToolResponse MergeFaces(
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
                var faces = pbType.GetProperty("faces")?.GetValue(pb);

                if (faces == null)
                {
                    return ToolResponse.Error("Could not get faces.");
                }

                var mergeType = GetPBType("UnityEngine.ProBuilder.MeshOperations.MergeElements") ?? GetPBType("UnityEngine.ProBuilder.MeshOperations.SurfaceTopology");

                if (mergeType == null)
                {
                    return ToolResponse.Error("MergeElements type not found.");
                }

                var method = FindStaticMethod(mergeType, "MergeFaces") ?? FindStaticMethod(mergeType, "Merge");

                if (method == null)
                {
                    return ToolResponse.Error("MergeFaces method not found.");
                }
                try
                {
                    Undo.RecordObject(pb, "Merge Faces");
                    method.Invoke(null, new object[] { pb, faces });

                    pbType.GetMethod("ToMesh")?.Invoke(pb, null);
                    pbType.GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Merged faces on '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"MergeFaces failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Connects elements (edges/vertices) on a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the connection, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-connect-elements", Title = "ProBuilder / Connect Elements")]
        [Description("Connects edges or vertices on a ProBuilder mesh, inserting new geometry.")]
        public ToolResponse ConnectElements(
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

                var connectType = GetPBType("UnityEngine.ProBuilder.MeshOperations.ConnectElements");

                if (connectType == null)
                {
                    return ToolResponse.Error("ConnectElements type not found.");
                }

                var pbType = pb.GetType();
                var edges = pbType.GetProperty("edges")?.GetValue(pb);

                if (edges == null)
                {
                    return ToolResponse.Error("Could not get edges.");
                }

                var method = FindStaticMethod(connectType, "Connect");

                if (method == null)
                {
                    return ToolResponse.Error("Connect method not found.");
                }
                try
                {
                    Undo.RecordObject(pb, "Connect Elements");
                    method.Invoke(null, new object[] { pb, edges });

                    pbType.GetMethod("ToMesh")?.Invoke(pb, null);
                    pbType.GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Connected elements on '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"ConnectElements failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Sets per-face color on all faces of a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <param name="r">Red channel (0–1). Default 1.</param>
        /// <param name="g">Green channel (0–1). Default 1.</param>
        /// <param name="b">Blue channel (0–1). Default 1.</param>
        /// <param name="a">Alpha channel (0–1). Default 1.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the color was set, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-set-face-color", Title = "ProBuilder / Set Face Color")]
        [Description("Sets a vertex color on all faces of a ProBuilder mesh.")]
        public ToolResponse SetFaceColor(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("Red channel (0-1).")] float r = 1f,
            [Description("Green channel (0-1).")] float g = 1f,
            [Description("Blue channel (0-1).")] float b = 1f,
            [Description("Alpha channel (0-1).")] float a = 1f
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

                var colorUtilType = GetPBType("UnityEngine.ProBuilder.MeshOperations.VertexColors") ?? GetPBType("UnityEngine.ProBuilder.MeshOperations.MeshPaint");

                if (colorUtilType == null)
                {
                    return ToolResponse.Error("VertexColors type not found.");
                }

                var method = FindStaticMethod(colorUtilType, "SetColor") ?? FindStaticMethod(colorUtilType, "PaintVertexColors");

                if (method == null)
                {
                    return ToolResponse.Error("SetColor method not found.");
                }
                try
                {
                    Undo.RecordObject(pb, "Set Face Color");
                    var color = new Color(r, g, b, a);
                    method.Invoke(null, new object[] { pb, faces, color });

                    pbType.GetMethod("ToMesh")?.Invoke(pb, null);
                    pbType.GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Set face color ({r:F2},{g:F2},{b:F2},{a:F2}) on '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"SetFaceColor failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Sets UV transform on all faces of a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <param name="scaleX">UV scale on the X axis. Default 1.</param>
        /// <param name="scaleY">UV scale on the Y axis. Default 1.</param>
        /// <param name="offsetX">UV offset on the X axis. Default 0.</param>
        /// <param name="offsetY">UV offset on the Y axis. Default 0.</param>
        /// <param name="rotation">UV rotation in degrees. Default 0.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the UV settings, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-set-face-uvs", Title = "ProBuilder / Set Face UVs")]
        [Description("Sets UV scale, offset, and rotation on all faces of a ProBuilder mesh.")]
        public ToolResponse SetFaceUVs(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("UV scale X.")] float scaleX = 1f,
            [Description("UV scale Y.")] float scaleY = 1f,
            [Description("UV offset X.")] float offsetX = 0f,
            [Description("UV offset Y.")] float offsetY = 0f,
            [Description("UV rotation in degrees.")] float rotation = 0f
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

                var uvSettingsType = GetPBType("UnityEngine.ProBuilder.AutoUnwrapSettings");

                if (uvSettingsType == null)
                {
                    return ToolResponse.Error("AutoUnwrapSettings type not found.");
                }
                try
                {
                    Undo.RecordObject(pb, "Set Face UVs");

                    var settings = System.Activator.CreateInstance(uvSettingsType);
                    uvSettingsType.GetField("scale")?.SetValue(settings, new Vector2(scaleX, scaleY));
                    uvSettingsType.GetField("offset")?.SetValue(settings, new Vector2(offsetX, offsetY));
                    uvSettingsType.GetField("rotation")?.SetValue(settings, rotation);

                    var faceType = GetPBType("UnityEngine.ProBuilder.Face");

                    if (faceType != null && faces is System.Collections.IEnumerable enumerable)
                    {
                        var uvProp = faceType.GetProperty("uv");

                        foreach (var face in enumerable)
                        {
                            uvProp?.SetValue(face, settings);
                        }
                    }

                    var uvGenType = GetPBType("UnityEngine.ProBuilder.MeshOperations.UVEditing");
                    var uvMethod = uvGenType != null ? FindStaticMethod(uvGenType, "ProjectFaceAutoUVs") ?? FindStaticMethod(uvGenType, "SplitUVs") : null;
                    uvMethod?.Invoke(null, new object[] { pb, faces });

                    pbType.GetMethod("ToMesh")?.Invoke(pb, null);
                    pbType.GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Set UVs (scale={scaleX}x{scaleY}, offset={offsetX},{offsetY}, rot={rotation}) on '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"SetFaceUVs failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Sets the smoothing group on all faces of a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <param name="smoothingGroup">Smoothing group index (0 = no smoothing, 1–24 = smoothed groups). Default 1.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the smoothing group was set, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-set-smoothing", Title = "ProBuilder / Set Smoothing")]
        [Description("Assigns a smoothing group index to all faces of a ProBuilder mesh.")]
        public ToolResponse SetSmoothing(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("Smoothing group index (0 = no smoothing, 1-24 = smoothed groups).")] int smoothingGroup = 1
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

                var faceType = GetPBType("UnityEngine.ProBuilder.Face");

                if (faceType == null)
                {
                    return ToolResponse.Error("Face type not found.");
                }
                try
                {
                    Undo.RecordObject(pb, "Set Smoothing");
                    var smoothProp = faceType.GetProperty("smoothingGroup");

                    if (smoothProp == null)
                    {
                        return ToolResponse.Error("smoothingGroup property not found on Face.");
                    }

                    foreach (var face in (System.Collections.IEnumerable)faces)
                    {
                        smoothProp.SetValue(face, smoothingGroup);
                    }

                    pbType.GetMethod("ToMesh")?.Invoke(pb, null);
                    pbType.GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Set smoothing group {smoothingGroup} on all faces of '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"SetSmoothing failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Auto-smooths a ProBuilder mesh by angle threshold.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <param name="angleThreshold">Faces within this angle (degrees) share a smoothing group. Default 15.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming auto-smoothing, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-auto-smooth", Title = "ProBuilder / Auto Smooth")]
        [Description("Automatically assigns smoothing groups to a ProBuilder mesh based on an angle threshold.")]
        public ToolResponse AutoSmooth(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("Angle threshold in degrees — faces within this angle share a smoothing group.")] float angleThreshold = 15f
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

                var smoothingType = GetPBType("UnityEngine.ProBuilder.Smoothing") ?? GetPBType("UnityEngine.ProBuilder.MeshOperations.Smoothing");

                if (smoothingType == null)
                {
                    return ToolResponse.Error("Smoothing type not found.");
                }

                var method = FindStaticMethod(smoothingType, "ApplySmoothingGroups") ?? FindStaticMethod(smoothingType, "AutoSmooth");

                if (method == null)
                {
                    return ToolResponse.Error("ApplySmoothingGroups method not found.");
                }
                try
                {
                    Undo.RecordObject(pb, "Auto Smooth");
                    var pbType = pb.GetType();
                    var faces = pbType.GetProperty("faces")?.GetValue(pb);
                    var methodParams = method.GetParameters();
                    object[] args = methodParams.Length == 3 ? new object[] { pb, faces!, angleThreshold } : new object[] { pb, angleThreshold };
                    method.Invoke(null, args);

                    pbType.GetMethod("ToMesh")?.Invoke(pb, null);
                    pbType.GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Auto-smoothed '{go.name}' with angle threshold {angleThreshold}.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"AutoSmooth failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Returns face indices whose normals match a given direction.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <param name="direction">Direction to match: "up", "down", "left", "right", "forward", "back". Default "up".</param>
        /// <param name="tolerance">Dot-product tolerance (0–1). 1 = exact match. Default 0.9.</param>
        /// <returns>A <see cref="ToolResponse"/> listing matched face indices, or an error if ProBuilder is not installed.</returns>
        [McpTool("probuilder-select-faces", Title = "ProBuilder / Select Faces", ReadOnlyHint = true)]
        [Description("Returns the indices of faces whose normals match a given world direction within a tolerance.")]
        public ToolResponse SelectFaces(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("Direction to match: 'up','down','left','right','forward','back'.")] string direction = "up",
            [Description("Dot-product tolerance (0-1). 1 = exact match.")] float tolerance = 0.9f
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

                var targetDir = direction.ToLowerInvariant() switch
                {
                    "down" => Vector3.down,
                    "left" => Vector3.left,
                    "right" => Vector3.right,
                    "forward" => Vector3.forward,
                    "back" => Vector3.back,
                    _ => Vector3.up,
                };

                var pbType = pb.GetType();
                var faces = pbType.GetProperty("faces")?.GetValue(pb);

                if (faces == null)
                {
                    return ToolResponse.Error("Could not get faces.");
                }

                var faceType = GetPBType("UnityEngine.ProBuilder.Face");

                if (faceType == null)
                {
                    return ToolResponse.Error("Face type not found.");
                }

                var matchedIndices = new System.Collections.Generic.List<int>();
                int faceIdx = 0;

                if (pbType.GetProperty("positions")?.GetValue(pb) is not Vector3[] positions)
                {
                    return ToolResponse.Error("Could not get mesh positions.");
                }

                foreach (var face in (System.Collections.IEnumerable)faces)
                {
                    var indexesProp = faceType.GetProperty("indexes") ?? faceType.GetProperty("indices");
                    var rawIndexes = indexesProp?.GetValue(face);

                    if (rawIndexes != null)
                    {
                        int[] idxArray = ToIntArray(rawIndexes);

                        if (idxArray.Length >= 3)
                        {
                            Vector3 a = positions[idxArray[0]];
                            Vector3 b = positions[idxArray[1]];
                            Vector3 c = positions[idxArray[2]];
                            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
                            Vector3 worldNormal = go.transform.TransformDirection(normal);

                            if (Vector3.Dot(worldNormal, targetDir) >= tolerance)
                            {
                                matchedIndices.Add(faceIdx);
                            }
                        }
                    }

                    faceIdx++;
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Faces matching direction '{direction}' (tolerance={tolerance}) on '{go.name}':");
                sb.Append("  Indices: [");

                for (int i = 0; i < matchedIndices.Count; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }

                    sb.Append(matchedIndices[i]);
                }
                sb.AppendLine("]");
                sb.AppendLine($"  Count: {matchedIndices.Count}");
                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}