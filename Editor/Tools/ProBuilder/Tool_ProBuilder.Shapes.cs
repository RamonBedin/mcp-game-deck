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

        /// <summary>Creates a ProBuilder primitive shape.</summary>
        /// <param name="shapeType">Shape type: "Cube", "Cylinder", "Sphere", "Plane", etc. Default "Cube".</param>
        /// <param name="name">Name for the created GameObject. Empty to use ProBuilder's default.</param>
        /// <param name="sizeX">Size on the X axis. Default 1.</param>
        /// <param name="sizeY">Size on the Y axis. Default 1.</param>
        /// <param name="sizeZ">Size on the Z axis. Default 1.</param>
        /// <param name="posX">World-space X position. Default 0.</param>
        /// <param name="posY">World-space Y position. Default 0.</param>
        /// <param name="posZ">World-space Z position. Default 0.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming creation, or an error if ProBuilder is not installed or the shape type is unsupported.</returns>
        [McpTool("probuilder-create-shape", Title = "ProBuilder / Create Shape")]
        [Description("Creates a ProBuilder editable mesh shape (Cube, Cylinder, Sphere, Plane, etc.).")]
        public ToolResponse CreateShape(
            [Description("Shape type: 'Cube','Cylinder','Sphere','Plane','Prism','Stair','Arch','Pipe','Cone','Torus'.")] string shapeType = "Cube",
            [Description("Object name.")] string name = "",
            [Description("Size X.")] float sizeX = 1f,
            [Description("Size Y.")] float sizeY = 1f,
            [Description("Size Z.")] float sizeZ = 1f,
            [Description("Position X.")] float posX = 0f,
            [Description("Position Y.")] float posY = 0f,
            [Description("Position Z.")] float posZ = 0f
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsProBuilderInstalled())
                {
                    return NotInstalled();
                }

                object? mesh = null;
                var size = new Vector3(sizeX, sizeY, sizeZ);

                mesh = shapeType.ToLowerInvariant() switch
                {
                    "cube" => CallShapeGen("GenerateCube", PivotLocation_Center(), size),
                    "cylinder" => CallShapeGen("GenerateCylinder", PivotLocation_Center(), 12, sizeX, sizeY, 1, -1),
                    "sphere" => CallShapeGen("GenerateIcosahedron", PivotLocation_Center(), sizeX, 2, false, false),
                    "plane" => CallShapeGen("GeneratePlane", PivotLocation_Center(), sizeX, sizeZ, 5, 5, Axis_Up()),
                    _ => CallShapeGen("GenerateCube", PivotLocation_Center(), size),
                };

                if (mesh == null)
                {
                    return ToolResponse.Error($"Failed to create shape '{shapeType}'. ShapeGenerator method not found.");
                }

                var meshComp = mesh as UnityEngine.Component;

                if (meshComp == null)
                {
                    return ToolResponse.Error("Shape created but no GameObject returned.");
                }

                var go = meshComp.gameObject;
                go.transform.position = new Vector3(posX, posY, posZ);

                if (!string.IsNullOrWhiteSpace(name))
                {
                    go.name = name;
                }

                Undo.RegisterCreatedObjectUndo(go, $"Create ProBuilder {shapeType}");
                return ToolResponse.Text($"Created ProBuilder {shapeType} '{go.name}' at ({posX},{posY},{posZ}).");
            });
        }

        /// <summary>
        /// Resolves the <c>PivotLocation.Center</c> enum value via reflection.
        /// </summary>
        /// <returns>The parsed enum value, or <c>0</c> if the type is not found.</returns>
        private static object PivotLocation_Center()
        {
            var pivotType = GetPBType("UnityEngine.ProBuilder.PivotLocation");

            if (pivotType != null)
            {
                return Enum.Parse(pivotType, "Center");
            }

            return 0;
        }

        /// <summary>
        /// Resolves the <c>Axis.Up</c> enum value via reflection.
        /// </summary>
        /// <returns>The parsed enum value, or <c>1</c> if the type is not found.</returns>
        private static object Axis_Up()
        {
            var axisType = GetPBType("UnityEngine.ProBuilder.Axis");

            if (axisType != null)
            {
                return Enum.Parse(axisType, "Up");
            }

            return 1;
        }

        /// <summary>Creates a ProBuilder PolyShape from a JSON array of 2D points.</summary>
        /// <param name="pointsJson">JSON array of 2D points (e.g. <c>[[0,0],[1,0],[1,1],[0,1]]</c>). Minimum 3 points.</param>
        /// <param name="extrudeHeight">Height to extrude the shape. Default 1.</param>
        /// <param name="flipNormals">When true, flips the normals on the extruded shape. Default false.</param>
        /// <returns>A <see cref="ToolResponse"/> confirming creation, or an error if ProBuilder is not installed or the points are invalid.</returns>
        [McpTool("probuilder-create-poly-shape", Title = "ProBuilder / Create Poly Shape")]
        [Description("Creates a new GameObject with a PolyShape component from a JSON array of 2D points.")]
        public ToolResponse CreatePolyShape(
            [Description("JSON array of 2D points, e.g. [[0,0],[1,0],[1,1],[0,1]].")] string pointsJson = "[[0,0],[1,0],[1,1],[0,1]]",
            [Description("Extrude height.")] float extrudeHeight = 1f,
            [Description("Flip normals on the extruded shape.")] bool flipNormals = false
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsProBuilderInstalled())
                {
                    return NotInstalled();
                }

                var polyShapeType = GetPBType("UnityEngine.ProBuilder.PolyShape");

                if (polyShapeType == null)
                {
                    return ToolResponse.Error("PolyShape type not found.");
                }

                var points = ParseVector2Array(pointsJson);

                if (points == null || points.Length < 3)
                {
                    return ToolResponse.Error("pointsJson must be a valid JSON array with at least 3 points.");
                }
                try
                {
                    var go = new GameObject("PolyShape");
                    Undo.RegisterCreatedObjectUndo(go, "Create PolyShape");

                    var polyShape = go.AddComponent(polyShapeType);
                    var psType = polyShape.GetType();

                    psType.GetProperty("extrude")?.SetValue(polyShape, extrudeHeight);
                    psType.GetProperty("flipNormals")?.SetValue(polyShape, flipNormals);

                    var controlPointsProp = psType.GetProperty("controlPoints");

                    if (controlPointsProp != null)
                    {
                        var listType = typeof(System.Collections.Generic.List<>).MakeGenericType(typeof(Vector3));
                        var list = System.Activator.CreateInstance(listType);
                        var addMethod = listType.GetMethod("Add");

                        for (int i = 0; i < points.Length; i++)
                        {
                            addMethod?.Invoke(list, new object[] { new Vector3(points[i].x, 0f, points[i].y) });
                        }

                        controlPointsProp.SetValue(polyShape, list);
                    }

                    var pbMeshType = GetPBType("UnityEngine.ProBuilder.ProBuilderMesh");

                    if (pbMeshType != null && go.GetComponent(pbMeshType) == null)
                    {
                        go.AddComponent(pbMeshType);
                    }

                    var refreshMethod = psType.GetMethod("Refresh", BindingFlags.Public | BindingFlags.Instance);
                    refreshMethod?.Invoke(polyShape, null);

                    return ToolResponse.Text($"Created PolyShape '{go.name}' with {points.Length} points, extrude={extrudeHeight}.");
                }
                catch (Exception ex)
                {
                    return ToolResponse.Error($"CreatePolyShape failed: {ex.InnerException?.Message ?? ex.Message}");
                }
            });
        }

        /// <summary>Parses a JSON array of 2D float arrays into Vector2[].</summary>
        /// <param name="json">JSON string in <c>[[x,y],[x,y],...]</c> format.</param>
        /// <returns>The parsed Vector2 array, or <c>null</c> if the input is empty or malformed.</returns>
        private static Vector2[]? ParseVector2Array(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            json = json.Trim();

            if (!json.StartsWith("[") || !json.EndsWith("]"))
            {
                return null;
            }

            json = json[1..^1].Trim();

            var result = new System.Collections.Generic.List<Vector2>();
            int depth = 0;
            int start = -1;

            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];

                if (c == '[') 
                { 
                    depth++;

                    if (depth == 1)
                    {
                        start = i + 1;
                    }
                }
                else if (c == ']')
                {
                    depth--;

                    if (depth == 0 && start >= 0)
                    {
                        string inner = json[start..i].Trim();
                        string[] parts = inner.Split(',');

                        if (parts.Length >= 2 && float.TryParse(parts[0].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float x) && float.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float y))
                        {
                            result.Add(new Vector2(x, y));
                        }

                        start = -1;
                    }
                }
            }

            return result.Count > 0 ? result.ToArray() : null;
        }

        #endregion
    }
}