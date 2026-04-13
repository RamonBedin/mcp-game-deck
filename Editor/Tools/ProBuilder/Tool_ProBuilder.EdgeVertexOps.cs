#nullable enable
using System;
using System.ComponentModel;
using System.Reflection;
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

        /// <summary>Extrudes edges on a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <param name="distance">Distance to extrude each edge outward. Default 0.25.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the extrusion, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-extrude-edges", Title = "ProBuilder / Extrude Edges")]
        [Description("Extrudes all edges of a ProBuilder mesh by a given distance.")]
        public ToolResponse ExtrudeEdges(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("Extrude distance.")] float distance = 0.25f
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
                var edges = pbType.GetProperty("edges")?.GetValue(pb);

                if (edges == null)
                {
                    return ToolResponse.Error("Could not get edges.");
                }

                var extrudeType = GetPBType("UnityEngine.ProBuilder.MeshOperations.ExtrudeElements");

                if (extrudeType == null)
                {
                    return ToolResponse.Error("ExtrudeElements not found.");
                }

                var method = extrudeType.GetMethod("Extrude", BindingFlags.Public | BindingFlags.Static, null, new[] { pbType, edges.GetType(), typeof(float) }, null);

                if (method == null)
                {
                    MethodInfo[]? methods = extrudeType.GetMethods(BindingFlags.Public | BindingFlags.Static);

                    for (int i = 0; i < methods.Length; i++)
                    {
                        if (methods[i].Name == "Extrude")
                        {
                            var p = methods[i].GetParameters();

                            if (p.Length >= 2 && p[1].ParameterType.Name.Contains("Edge"))
                            {
                                method = methods[i];
                                break;
                            }
                        }
                    }
                }

                if (method == null)
                {
                    return ToolResponse.Error("Extrude(edges) overload not found.");
                }
                try
                {
                    Undo.RecordObject(pb, "Extrude Edges");
                    var methodParams = method.GetParameters();
                    object[] args = methodParams.Length == 3 ? new object[] { pb, edges, distance } : new object[] { pb, edges };
                    method.Invoke(null, args);

                    pbType.GetMethod("ToMesh")?.Invoke(pb, null);
                    pbType.GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Extruded edges by {distance} on '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"ExtrudeEdges failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Bevels edges on a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <param name="amount">Bevel amount controlling the chamfer size. Default 0.1.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the bevel, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-bevel-edges", Title = "ProBuilder / Bevel Edges")]
        [Description("Bevels all edges of a ProBuilder mesh by a given amount.")]
        public ToolResponse BevelEdges(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("Bevel amount.")] float amount = 0.1f
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
                var edges = pbType.GetProperty("edges")?.GetValue(pb);

                if (edges == null)
                {
                    return ToolResponse.Error("Could not get edges.");
                }

                var bevelType = GetPBType("UnityEngine.ProBuilder.MeshOperations.Bevel");

                if (bevelType == null)
                {
                    return ToolResponse.Error("Bevel type not found.");
                }

                var method = FindStaticMethod(bevelType, "BevelEdges") ?? FindStaticMethod(bevelType, "Bevel");

                if (method == null)
                {
                    return ToolResponse.Error("Bevel method not found.");
                }
                try
                {
                    Undo.RecordObject(pb, "Bevel Edges");
                    method.Invoke(null, new object[] { pb, edges, amount });

                    pbType.GetMethod("ToMesh")?.Invoke(pb, null);
                    pbType.GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Beveled edges by {amount} on '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"BevelEdges failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Bridges open edges on a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the bridge, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-bridge-edges", Title = "ProBuilder / Bridge Edges")]
        [Description("Bridges open edges on a ProBuilder mesh to close gaps. Operates on all open edge pairs found.")]
        public ToolResponse BridgeEdges(
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

                var bridgeType = GetPBType("UnityEngine.ProBuilder.MeshOperations.Bridge") ?? GetPBType("UnityEngine.ProBuilder.MeshOperations.AppendElements");

                if (bridgeType == null)
                {
                    return ToolResponse.Error("Bridge type not found.");
                }

                var method = FindStaticMethod(bridgeType, "Bridge") ?? FindStaticMethod(bridgeType, "BridgeEdges");

                if (method == null)
                {
                    return ToolResponse.Error("Bridge method not found — this operation requires two selected edges.");
                }
                try
                {
                    Undo.RecordObject(pb, "Bridge Edges");
                    var pbType = pb.GetType();
                    var edges = pbType.GetProperty("edges")?.GetValue(pb);
                    method.Invoke(null, new object[] { pb, edges! });

                    pbType.GetMethod("ToMesh")?.Invoke(pb, null);
                    pbType.GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Bridged edges on '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"BridgeEdges failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Merges/collapses all vertices of a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <param name="collapseToFirst">When true, collapses to the first vertex position; otherwise to the centroid.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the merge, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-merge-vertices", Title = "ProBuilder / Merge Vertices")]
        [Description("Merges (collapses) all selected vertices of a ProBuilder mesh to a single point.")]
        public ToolResponse MergeVertices(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("Collapse to first vertex position (true) or centroid (false).")] bool collapseToFirst = false
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

                var mergeType = GetPBType("UnityEngine.ProBuilder.MeshOperations.MergeElements");

                if (mergeType == null)
                {
                    return ToolResponse.Error("MergeElements type not found.");
                }

                var method = FindStaticMethod(mergeType, "MergeVertices") ?? FindStaticMethod(mergeType, "CollapseVertices");

                if (method == null)
                {
                    return ToolResponse.Error("MergeVertices method not found.");
                }
                try
                {
                    Undo.RecordObject(pb, "Merge Vertices");
                    var pbType = pb.GetType();

                    if (pbType.GetProperty("positions")?.GetValue(pb) is not Vector3[] positions || positions.Length == 0)
                    {
                        return ToolResponse.Error("Mesh has no vertices.");
                    }

                    int[] indices = new int[positions.Length];

                    for (int i = 0; i < positions.Length; i++)
                    {
                        indices[i] = i;
                    }

                    var methodParams = method.GetParameters();
                    object[] args = methodParams.Length == 3 ? new object[] { pb, indices, collapseToFirst } : new object[] { pb, indices };
                    method.Invoke(null, args);

                    pbType.GetMethod("ToMesh")?.Invoke(pb, null);
                    pbType.GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Merged {positions.Length} vertices on '{go.name}' (collapseToFirst={collapseToFirst}).");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"MergeVertices failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Welds nearby vertices within a radius on a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <param name="radius">Vertices closer than this distance will be merged together. Default 0.01.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the weld, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-weld-vertices", Title = "ProBuilder / Weld Vertices")]
        [Description("Welds vertices that are within a given radius of each other on a ProBuilder mesh.")]
        public ToolResponse WeldVertices(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("Weld radius — vertices closer than this will be merged.")] float radius = 0.01f
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

                var weldType = GetPBType("UnityEngine.ProBuilder.MeshOperations.WeldVertices") ?? GetPBType("UnityEngine.ProBuilder.MeshOperations.MergeElements");

                if (weldType == null)
                {
                    return ToolResponse.Error("WeldVertices type not found.");
                }

                var method = FindStaticMethod(weldType, "WeldVertices") ?? FindStaticMethod(weldType, "Weld");

                if (method == null)
                {
                    return ToolResponse.Error("WeldVertices method not found.");
                }
                try
                {
                    Undo.RecordObject(pb, "Weld Vertices");
                    var pbType = pb.GetType();

                    if (pbType.GetProperty("positions")?.GetValue(pb) is not Vector3[] positions || positions.Length == 0)
                    {
                        return ToolResponse.Error("Mesh has no vertices.");
                    }

                    int[] indices = new int[positions.Length];

                    for (int i = 0; i < positions.Length; i++)
                    {
                        indices[i] = i;
                    }

                    method.Invoke(null, new object[] { pb, indices, radius });

                    pbType.GetMethod("ToMesh")?.Invoke(pb, null);
                    pbType.GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Welded vertices within radius {radius} on '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"WeldVertices failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Splits shared vertices on a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the split, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-split-vertices", Title = "ProBuilder / Split Vertices")]
        [Description("Splits shared vertices on a ProBuilder mesh so each face has its own unique vertices.")]
        public ToolResponse SplitVertices(
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

                var splitType = GetPBType("UnityEngine.ProBuilder.MeshOperations.SplitVertices") ?? GetPBType("UnityEngine.ProBuilder.MeshOperations.MergeElements");

                if (splitType == null)
                {
                    return ToolResponse.Error("SplitVertices type not found.");
                }

                var method = FindStaticMethod(splitType, "SplitVertices") ?? FindStaticMethod(splitType, "Split");

                if (method == null)
                {
                    return ToolResponse.Error("SplitVertices method not found.");
                }
                try
                {
                    Undo.RecordObject(pb, "Split Vertices");
                    var pbType = pb.GetType();

                    if (pbType.GetProperty("positions")?.GetValue(pb) is not Vector3[] positions || positions.Length == 0)
                    {
                        return ToolResponse.Error("Mesh has no vertices.");
                    }

                    int[] indices = new int[positions.Length];

                    for (int i = 0; i < positions.Length; i++)
                    {
                        indices[i] = i;
                    }

                    method.Invoke(null, new object[] { pb, indices });

                    pbType.GetMethod("ToMesh")?.Invoke(pb, null);
                    pbType.GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Split vertices on '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"SplitVertices failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Moves all vertices of a ProBuilder mesh by a world-space offset.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <param name="offsetX">X component of the local-space offset.</param>
        /// <param name="offsetY">Y component of the local-space offset.</param>
        /// <param name="offsetZ">Z component of the local-space offset.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the move, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-move-vertices", Title = "ProBuilder / Move Vertices")]
        [Description("Translates all vertices of a ProBuilder mesh by a local-space offset vector.")]
        public ToolResponse MoveVertices(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("X offset.")] float offsetX = 0f,
            [Description("Y offset.")] float offsetY = 0f,
            [Description("Z offset.")] float offsetZ = 0f
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
                if (pb == null) return ToolResponse.Error("No ProBuilderMesh component.");

                var pbType = pb.GetType();

                if (pbType.GetProperty("positions")?.GetValue(pb) is not Vector3[] positions || positions.Length == 0)
                {
                    return ToolResponse.Error("Mesh has no vertices.");
                }
                try
                {
                    Undo.RecordObject(pb, "Move Vertices");
                    var offset = new Vector3(offsetX, offsetY, offsetZ);

                    for (int i = 0; i < positions.Length; i++)
                    {
                        positions[i] += offset;
                    }

                    var setPosProp = pbType.GetProperty("positions");

                    if (setPosProp != null && setPosProp.CanWrite)
                    {
                        setPosProp.SetValue(pb, positions);
                    }
                    else
                    {
                        var setPosMethod = pbType.GetMethod("SetPositions", new[] { typeof(Vector3[]) });
                        setPosMethod?.Invoke(pb, new object[] { positions });
                    }

                    pbType.GetMethod("ToMesh")?.Invoke(pb, null);
                    pbType.GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Moved {positions.Length} vertices by ({offsetX},{offsetY},{offsetZ}) on '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"MoveVertices failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Inserts a vertex at a world-space point on the nearest face of a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <param name="pointX">X coordinate in local space.</param>
        /// <param name="pointY">Y coordinate in local space.</param>
        /// <param name="pointZ">Z coordinate in local space.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the insertion, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-insert-vertex", Title = "ProBuilder / Insert Vertex")]
        [Description("Inserts a new vertex at a specified local-space point on a ProBuilder mesh by appending it.")]
        public ToolResponse InsertVertex(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("X coordinate (local space).")] float pointX = 0f,
            [Description("Y coordinate (local space).")] float pointY = 0f,
            [Description("Z coordinate (local space).")] float pointZ = 0f
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

                var method = FindStaticMethod(appendType, "InsertVertexInFace") ?? FindStaticMethod(appendType, "AddVertex") ?? FindStaticMethod(appendType, "AppendVerticesToFace");

                if (method == null)
                {
                    return ToolResponse.Error("InsertVertex method not found.");
                }
                try
                {
                    Undo.RecordObject(pb, "Insert Vertex");
                    var pbType = pb.GetType();
                    var point = new Vector3(pointX, pointY, pointZ);
                    var faces = pbType.GetProperty("faces")?.GetValue(pb);
                    object? firstFace = null;

                    if (faces is System.Collections.IEnumerable enumerable)
                    {
                        var enumerator = enumerable.GetEnumerator();

                        if (enumerator.MoveNext())
                        {
                            firstFace = enumerator.Current;
                        }
                    }

                    if (firstFace == null)
                    {
                        return ToolResponse.Error("No faces on mesh.");
                    }

                    method.Invoke(null, new object[] { pb, firstFace, point });

                    pbType.GetMethod("ToMesh")?.Invoke(pb, null);
                    pbType.GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Inserted vertex at ({pointX},{pointY},{pointZ}) on '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"InsertVertex failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Appends new vertices to a ProBuilder mesh.</summary>
        /// <param name="instanceId">Instance ID of the target GameObject. Use 0 to find by objectPath.</param>
        /// <param name="objectPath">Hierarchy path of the target GameObject. Used when instanceId is 0.</param>
        /// <param name="count">Number of vertices to append (1–1000). Default 1.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming the append, or an error if ProBuilder is not installed or the operation fails.</returns>
        [McpTool("probuilder-append-vertices", Title = "ProBuilder / Append Vertices")]
        [Description("Appends a number of duplicate vertices (copies of vertex 0) to a ProBuilder mesh.")]
        public ToolResponse AppendVertices(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("Number of vertices to append.")] int count = 1
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

                if (count < 1 || count > 1000)
                {
                    return ToolResponse.Error("count must be between 1 and 1000.");
                }

                var pbType = pb.GetType();

                if (pbType.GetProperty("positions")?.GetValue(pb) is not Vector3[] positions || positions.Length == 0)
                {
                    return ToolResponse.Error("Mesh has no vertices.");
                }

                var appendType = GetPBType("UnityEngine.ProBuilder.MeshOperations.AppendElements");

                if (appendType == null)
                {
                    return ToolResponse.Error("AppendElements type not found.");
                }

                var method = FindStaticMethod(appendType, "AppendVertices") ?? FindStaticMethod(appendType, "AddVertices");

                if (method == null)
                {
                    return ToolResponse.Error("AppendVertices method not found.");
                }
                try
                {
                    Undo.RecordObject(pb, "Append Vertices");
                    var vertexType = GetPBType("UnityEngine.ProBuilder.Vertex");

                    if (vertexType == null)
                    {
                        return ToolResponse.Error("Vertex type not found.");
                    }

                    var newVerts = System.Array.CreateInstance(vertexType, count);
                    var defaultVert = System.Activator.CreateInstance(vertexType);
                    vertexType.GetProperty("position")?.SetValue(defaultVert, positions[0]);

                    for (int i = 0; i < count; i++)
                    {
                        newVerts.SetValue(defaultVert, i);
                    }

                    method.Invoke(null, new object[] { pb, newVerts });

                    pbType.GetMethod("ToMesh")?.Invoke(pb, null);
                    pbType.GetMethod("Refresh")?.Invoke(pb, null);

                    return ToolResponse.Text($"Appended {count} vertex/vertices to '{go.name}'.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"AppendVertices failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        #endregion
    }
}