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
    public partial class Tool_Graphics
    {
        #region TOOLS METHODS

        /// <summary>Creates a Volume GameObject.</summary>
        /// <param name="name">Name to assign to the new GameObject. Default "Volume".</param>
        /// <param name="isGlobal">When true, the volume affects the entire scene globally. Default true.</param>
        /// <param name="weight">Blending weight of the volume, between 0 and 1. Default 1.</param>
        /// <param name="priority">Priority used to resolve conflicts between overlapping volumes. Default 0.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the volume's name, global state, and weight,
        /// or an error if URP or HDRP is not available.
        /// </returns>
        [McpTool("graphics-volume-create", Title = "Graphics / Volume Create")]
        [Description("Creates a Volume GameObject with a VolumeProfile. Requires URP or HDRP.")]
        public ToolResponse VolumeCreate(
            [Description("Name.")] string name = "Volume",
            [Description("Global volume.")] bool isGlobal = true,
            [Description("Weight 0-1.")] float weight = 1f,
            [Description("Priority.")] int priority = 0
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var volType = GetVolumeType();

                if (volType == null)
                {
                    return VolumeNotAvailable();
                }

                var go = new GameObject(name);
                var volume = go.AddComponent(volType);
                volType.GetProperty("isGlobal")?.SetValue(volume, isGlobal);
                volType.GetProperty("weight")?.SetValue(volume, weight);
                volType.GetProperty("priority")?.SetValue(volume, (float)priority);
                var profileType = GetVolumeProfileType();

                if (profileType != null)
                {
                    var profile = ScriptableObject.CreateInstance(profileType);
                    volType.GetProperty("profile")?.SetValue(volume, profile);
                }

                Undo.RegisterCreatedObjectUndo(go, $"Create Volume {name}");
                return ToolResponse.Text($"Created Volume '{name}' (global={isGlobal}, weight={weight}).");
            });
        }

        /// <summary>Gets Volume info.</summary>
        /// <param name="instanceId">Unity instance ID of the Volume's GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the Volume's GameObject. Used when instanceId is 0.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> with global flag, weight, priority, and a list of effect types
        /// in the Volume's profile, or an error if the GameObject or Volume component is not found.
        /// </returns>
        [McpTool("graphics-volume-get-info", Title = "Graphics / Volume Get Info", ReadOnlyHint = true)]
        [Description("Returns Volume info: profile, effects, settings.")]
        public ToolResponse VolumeGetInfo(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var volType = GetVolumeType();

                if (volType == null)
                {
                    return VolumeNotAvailable();
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found.");
                }

                var volume = go.GetComponent(volType);

                if (volume == null)
                {
                    return ToolResponse.Error("No Volume component.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Volume: {go.name}");
                sb.AppendLine($"  Global: {volType.GetProperty("isGlobal")?.GetValue(volume)}");
                sb.AppendLine($"  Weight: {volType.GetProperty("weight")?.GetValue(volume)}");
                sb.AppendLine($"  Priority: {volType.GetProperty("priority")?.GetValue(volume)}");
                var profile = volType.GetProperty("profile")?.GetValue(volume);

                if (profile != null)
                {
                    var compsProp = profile.GetType().GetProperty("components");

                    if (compsProp?.GetValue(profile) is System.Collections.IList comps)
                    {
                        sb.AppendLine($"  Effects ({comps.Count}):");

                        for (int i = 0; i < comps.Count; i++)
                        {
                            sb.AppendLine($"    [{i}] {comps[i]?.GetType().Name}");
                        }
                    }
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>Adds effect to Volume.</summary>
        /// <param name="instanceId">Unity instance ID of the Volume's GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the Volume's GameObject. Used when instanceId is 0.</param>
        /// <param name="effectType">Simple type name of the effect to add (e.g. "Bloom", "Vignette"). Default "Bloom".</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the effect was added,
        /// or an error if the GameObject, Volume, profile, or effect type is not found.
        /// </returns>
        [McpTool("graphics-volume-add-effect", Title = "Graphics / Volume Add Effect")]
        [Description("Adds a post-processing effect to a Volume profile.")]
        public ToolResponse VolumeAddEffect(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("Effect type (e.g. 'Bloom').")] string effectType = "Bloom"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var volType = GetVolumeType();

                if (volType == null)
                {
                    return VolumeNotAvailable();
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found.");
                }

                var volume = go.GetComponent(volType);

                if (volume == null)
                {
                    return ToolResponse.Error("No Volume component.");
                }

                var profile = volType.GetProperty("profile")?.GetValue(volume);

                if (profile == null)
                {
                    return ToolResponse.Error("Volume has no profile.");
                }

                var type = FindVolumeEffectType(effectType);

                if (type == null)
                {
                    return ToolResponse.Error($"Effect '{effectType}' not found.");
                }

                var addMethod = profile.GetType().GetMethod("Add", new[] { typeof(Type) });
                addMethod?.Invoke(profile, new object[] { type });

                if (profile is UnityEngine.Object profileObj)
                {
                    EditorUtility.SetDirty(profileObj);
                }
                else
                {
                    EditorUtility.SetDirty(go);
                }

                return ToolResponse.Text($"Added {effectType} to Volume '{go.name}'.");
            });
        }

        /// <summary>Removes effect from Volume.</summary>
        /// <param name="instanceId">Unity instance ID of the Volume's GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the Volume's GameObject. Used when instanceId is 0.</param>
        /// <param name="effectType">Simple type name of the effect to remove (e.g. "Bloom").</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the effect was removed,
        /// or an error if the GameObject, Volume, profile, or effect type is not found.
        /// </returns>
        [McpTool("graphics-volume-remove-effect", Title = "Graphics / Volume Remove Effect")]
        [Description("Removes a post-processing effect from a Volume.")]
        public ToolResponse VolumeRemoveEffect(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("Effect type.")] string effectType = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var volType = GetVolumeType();

                if (volType == null)
                {
                    return VolumeNotAvailable();
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found.");
                }

                var volume = go.GetComponent(volType);

                if (volume == null)
                {
                    return ToolResponse.Error("No Volume component.");
                }

                var profile = volType.GetProperty("profile")?.GetValue(volume);

                if (profile == null)
                {
                    return ToolResponse.Error("No profile.");
                }

                var type = FindVolumeEffectType(effectType);

                if (type == null)
                {
                    return ToolResponse.Error($"Effect '{effectType}' not found.");
                }

                var removeMethod = profile.GetType().GetMethod("Remove", new[] { typeof(Type) });
                removeMethod?.Invoke(profile, new object[] { type });

                if (profile is UnityEngine.Object profileObj)
                {
                    EditorUtility.SetDirty(profileObj);
                }
                else
                {
                    EditorUtility.SetDirty(go);
                }

                return ToolResponse.Text($"Removed {effectType} from Volume '{go.name}'.");
            });
        }

        /// <summary>Lists available Volume effects.</summary>
        /// <returns>
        /// A <see cref="ToolResponse"/> with the total count and names of all non-abstract types
        /// assignable to VolumeComponent, or an error if URP or HDRP is not available.
        /// </returns>
        [McpTool("graphics-volume-list-effects", Title = "Graphics / Volume List Effects", ReadOnlyHint = true)]
        [Description("Lists all available Volume Component effect types.")]
        public ToolResponse VolumeListEffects()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var baseType = GetVolumeComponentType();

                if (baseType == null)
                {
                    return VolumeNotAvailable();
                }

                var sb = new StringBuilder();
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                int count = 0;

                for (int a = 0; a < assemblies.Length; a++)
                {
                    Type[] types;
                    try
                    {
                        types = assemblies[a].GetTypes();
                    }
                    catch (System.Reflection.ReflectionTypeLoadException)
                    {
                        continue;
                    }

                    for (int t = 0; t < types.Length; t++)
                    {
                        if (types[t] != null && !types[t].IsAbstract && baseType.IsAssignableFrom(types[t]))
                        {
                            sb.AppendLine($"  {types[t].Name}");
                            count++;
                        }
                    }
                }

                sb.Insert(0, $"Available Volume Effects ({count}):\n");
                return ToolResponse.Text(sb.ToString());
            });
        }

        /// <summary>Creates a VolumeProfile asset.</summary>
        /// <param name="path">Asset path for the new profile, must start with "Assets/". Default "Assets/VolumeProfile.asset".</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the path where the asset was saved,
        /// or an error if the path is invalid or URP or HDRP is not available.
        /// </returns>
        [McpTool("graphics-volume-create-profile", Title = "Graphics / Volume Create Profile")]
        [Description("Creates a VolumeProfile asset.")]
        public ToolResponse VolumeCreateProfile(
            [Description("Save path.")] string path = "Assets/VolumeProfile.asset"
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var profileType = GetVolumeProfileType();

                if (profileType == null)
                {
                    return VolumeNotAvailable();
                }

                if (!path.StartsWith("Assets/"))
                {
                    return ToolResponse.Error("path must start with 'Assets/'.");
                }

                var profile = ScriptableObject.CreateInstance(profileType);
                path = AssetDatabase.GenerateUniqueAssetPath(path);
                AssetDatabase.CreateAsset(profile, path);
                AssetDatabase.SaveAssets();
                return ToolResponse.Text($"Created VolumeProfile at '{path}'.");
            });
        }

        /// <summary>Sets Volume properties.</summary>
        /// <param name="instanceId">Unity instance ID of the Volume's GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the Volume's GameObject. Used when instanceId is 0.</param>
        /// <param name="weight">New blending weight (0–1). Pass -1 to leave unchanged.</param>
        /// <param name="priority">New priority. Pass -999 to leave unchanged.</param>
        /// <param name="isGlobal">Global flag: 1 = global, 0 = local, -1 = unchanged.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the updated Volume,
        /// or an error if the GameObject or Volume component is not found.
        /// </returns>
        [McpTool("graphics-volume-set-properties", Title = "Graphics / Volume Set Properties")]
        [Description("Sets Volume properties: weight, priority, isGlobal.")]
        public ToolResponse VolumeSetProperties(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("Weight (-1=unchanged).")] float weight = -1f,
            [Description("Priority (-999=unchanged).")] int priority = -999,
            [Description("IsGlobal (-1=unchanged, 0=false, 1=true).")] int isGlobal = -1
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var volType = GetVolumeType();

                if (volType == null)
                {
                    return VolumeNotAvailable();
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found.");
                }

                var volume = go.GetComponent(volType);

                if (volume == null)
                {
                    return ToolResponse.Error("No Volume component.");
                }

                if (volume is UnityEngine.Object volumeObj)
                {
                    Undo.RecordObject(volumeObj, "Set Volume Properties");
                }
                else
                {
                    Undo.RecordObject(go, "Set Volume Properties");
                }

                if (weight >= 0f)
                {
                    volType.GetProperty("weight")?.SetValue(volume, weight);
                }

                if (priority != -999)
                {
                    volType.GetProperty("priority")?.SetValue(volume, (float)priority);
                }

                if (isGlobal >= 0)
                {
                    volType.GetProperty("isGlobal")?.SetValue(volume, isGlobal == 1);
                }

                return ToolResponse.Text($"Updated Volume '{go.name}'.");
            });
        }

        /// <summary>Sets effect parameters.</summary>
        /// <param name="instanceId">Unity instance ID of the Volume's GameObject. Pass 0 to use objectPath instead.</param>
        /// <param name="objectPath">Hierarchy path of the Volume's GameObject. Used when instanceId is 0.</param>
        /// <param name="effectType">Simple type name of the target effect (e.g. "Bloom").</param>
        /// <param name="parameterName">Public field name of the parameter to set (e.g. "intensity").</param>
        /// <param name="parameterValue">String representation of the new value. Supports float, int, and bool.</param>
        /// <returns>
        /// A <see cref="ToolResponse"/> confirming the assignment,
        /// or an error if the GameObject, Volume, profile, effect, or parameter is not found,
        /// or if the value cannot be parsed to the parameter's type.
        /// </returns>
        [McpTool("graphics-volume-set-effect", Title = "Graphics / Volume Set Effect")]
        [Description("Sets parameters on a Volume effect.")]
        public ToolResponse VolumeSetEffect(
            [Description("Instance ID.")] int instanceId = 0,
            [Description("Object path.")] string objectPath = "",
            [Description("Effect type.")] string effectType = "",
            [Description("Parameter name.")] string parameterName = "",
            [Description("Parameter value.")] string parameterValue = ""
        )
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (string.IsNullOrWhiteSpace(effectType))
                {
                    return ToolResponse.Error("effectType is required.");
                }

                if (string.IsNullOrWhiteSpace(parameterName))
                {
                    return ToolResponse.Error("parameterName is required.");
                }

                var volType = GetVolumeType();

                if (volType == null)
                {
                    return VolumeNotAvailable();
                }

                var go = Tool_Transform.FindGameObject(instanceId, objectPath);

                if (go == null)
                {
                    return ToolResponse.Error("GameObject not found.");
                }

                var volume = go.GetComponent(volType);

                if (volume == null)
                {
                    return ToolResponse.Error("No Volume component.");
                }

                var profile = volType.GetProperty("profile")?.GetValue(volume);

                if (profile == null)
                {
                    return ToolResponse.Error("No profile.");
                }

                var type = FindVolumeEffectType(effectType);

                if (type == null)
                {
                    return ToolResponse.Error($"Effect '{effectType}' not found.");
                }

                var compsProp = profile.GetType().GetProperty("components");

                if (compsProp?.GetValue(profile) is not System.Collections.IList comps)
                {
                    return ToolResponse.Error("Cannot read profile components.");
                }

                object? comp = null;

                for (int i = 0; i < comps.Count; i++)
                {
                    if (comps[i]?.GetType() == type) { comp = comps[i]; break; }
                }

                if (comp == null)
                {
                    return ToolResponse.Error($"Effect '{effectType}' not in profile.");
                }

                var field = type.GetField(parameterName, BindingFlags.Public | BindingFlags.Instance);

                if (field == null)
                {
                    return ToolResponse.Error($"Parameter '{parameterName}' not found.");
                }

                var paramObj = field.GetValue(comp);

                if (paramObj == null)
                {
                    return ToolResponse.Error($"Parameter is null.");
                }

                paramObj.GetType().GetProperty("overrideState")?.SetValue(paramObj, true);
                var valueProp = paramObj.GetType().GetProperty("value");

                if (valueProp != null)
                {
                    var targetType = valueProp.PropertyType;
                    object? parsed = null;

                    if (targetType == typeof(float))
                    {
                        if (!float.TryParse(parameterValue, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float fv))
                        {
                            return ToolResponse.Error($"Cannot parse '{parameterValue}' as float.");
                        }

                        parsed = fv;
                    }
                    else if (targetType == typeof(int))
                    {
                        if (!int.TryParse(parameterValue, out int iv))
                        {
                            return ToolResponse.Error($"Cannot parse '{parameterValue}' as int.");
                        }

                        parsed = iv;
                    }
                    else if (targetType == typeof(bool))
                    {
                        if (!bool.TryParse(parameterValue, out bool bv))
                        {
                            return ToolResponse.Error($"Cannot parse '{parameterValue}' as bool.");
                        }

                        parsed = bv;
                    }
                    if (parsed != null)
                    {
                        valueProp.SetValue(paramObj, parsed);
                    }
                }

                if (profile is UnityEngine.Object profileObj)
                {
                    EditorUtility.SetDirty(profileObj);
                }
                else
                {
                    EditorUtility.SetDirty(go);
                }

                return ToolResponse.Text($"Set {effectType}.{parameterName} = {parameterValue}.");
            });
        }

        #endregion

        #region VOLUME HELPERS

        /// <summary>
        /// Resolves the <c>UnityEngine.Rendering.Volume</c> type at runtime,
        /// searching both the core module and the SRP Core Runtime assembly.
        /// </summary>
        /// <returns>The Volume <see cref="Type"/>, or <c>null</c> if neither assembly is loaded.</returns>
        private static Type? GetVolumeType() => Type.GetType("UnityEngine.Rendering.Volume, UnityEngine.CoreModule") ?? Type.GetType("UnityEngine.Rendering.Volume, Unity.RenderPipelines.Core.Runtime");

        /// <summary>
        /// Resolves the <c>UnityEngine.Rendering.VolumeProfile</c> type at runtime,
        /// searching both the core module and the SRP Core Runtime assembly.
        /// </summary>
        /// <returns>The VolumeProfile <see cref="Type"/>, or <c>null</c> if neither assembly is loaded.</returns>
        private static Type? GetVolumeProfileType() => Type.GetType("UnityEngine.Rendering.VolumeProfile, UnityEngine.CoreModule") ?? Type.GetType("UnityEngine.Rendering.VolumeProfile, Unity.RenderPipelines.Core.Runtime");

        /// <summary>
        /// Resolves the <c>UnityEngine.Rendering.VolumeComponent</c> type at runtime,
        /// searching both the core module and the SRP Core Runtime assembly.
        /// </summary>
        /// <returns>The VolumeComponent <see cref="Type"/>, or <c>null</c> if neither assembly is loaded.</returns>
        private static Type? GetVolumeComponentType() => Type.GetType("UnityEngine.Rendering.VolumeComponent, UnityEngine.CoreModule") ?? Type.GetType("UnityEngine.Rendering.VolumeComponent, Unity.RenderPipelines.Core.Runtime");

        /// <summary>
        /// Returns a standardised error response indicating that Volume tools require URP or HDRP.
        /// </summary>
        /// <returns>A <see cref="ToolResponse"/> error directing the user to install the appropriate SRP package.</returns>
        private static ToolResponse VolumeNotAvailable() => ToolResponse.Error("Volume tools require URP or HDRP render pipeline. Install com.unity.render-pipelines.universal or com.unity.render-pipelines.high-definition.");

        /// <summary>
        /// Searches all loaded assemblies for a concrete <see cref="UnityEngine.Rendering.VolumeComponent"/>
        /// subtype whose name matches <paramref name="typeName"/> (case-insensitive).
        /// </summary>
        /// <param name="typeName">Simple type name of the effect to find (e.g. "Bloom", "Vignette").</param>
        /// <returns>
        /// The matching <see cref="Type"/> assignable to VolumeComponent,
        /// or <c>null</c> if not found or if VolumeComponent itself cannot be resolved.
        /// </returns>
        private static Type? FindVolumeEffectType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            var baseType = GetVolumeComponentType();

            if (baseType == null)
            {
                return null;
            }

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();

            for (int a = 0; a < assemblies.Length; a++)
            {
                Type[] types;
                try
                {
                    types = assemblies[a].GetTypes();
                }
                catch (System.Reflection.ReflectionTypeLoadException)
                {
                    continue;
                }

                for (int t = 0; t < types.Length; t++)
                {
                    if (types[t] != null && !types[t].IsAbstract && baseType.IsAssignableFrom(types[t]) && string.Equals(types[t].Name, typeName, StringComparison.OrdinalIgnoreCase))
                    {
                        return types[t];
                    }
                }
            }
            return null;
        }

        #endregion
    }
}