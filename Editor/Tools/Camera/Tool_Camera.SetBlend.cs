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
    public partial class Tool_Camera
    {
        #region TOOL METHODS

        /// <summary>
        /// Sets the default camera blend style and duration on the CinemachineBrain component.
        /// This controls how the brain transitions between live virtual cameras.
        /// </summary>
        /// <param name="style">
        /// Blend style keyword: EaseInOut, EaseIn, EaseOut, HardIn, HardOut, Linear, Cut.
        /// </param>
        /// <param name="duration">Blend duration in seconds.</param>
        /// <returns>Confirmation text with the applied blend style and duration.</returns>
        [McpTool("camera-set-blend", Title = "Camera / Set Blend")]
        [Description("Configures the default blend style and duration on the CinemachineBrain. " + "Blend styles: EaseInOut, EaseIn, EaseOut, HardIn, HardOut, Linear, Cut. " + "Requires a CinemachineBrain component in the scene.")]
        public ToolResponse SetBlend(
            [Description("Blend style: EaseInOut, EaseIn, EaseOut, HardIn, HardOut, Linear, Cut. Default EaseInOut.")] string style = "EaseInOut",
            [Description("Blend duration in seconds. Default 2.")] float duration = 2f)
        {
            return MainThreadDispatcher.Execute(() =>
            {
                if (!IsCinemachineInstalled())
                {
                    return ToolResponse.Error("Cinemachine is not installed in this project.");
                }

                UnityEngine.Component? brain = FindCinemachineBrain();

                if (brain == null)
                {
                    return ToolResponse.Error("No CinemachineBrain found in the scene. " + "Use camera-ensure-brain to add one.");
                }

                Undo.RecordObject(brain, "Set Cinemachine Blend");

                var sb = new StringBuilder();
                sb.AppendLine("Set CinemachineBrain blend:");
                bool applied = ApplyBlendDefinition(brain, "DefaultBlend", style, duration, sb);

                if (!applied)
                {
                    applied = ApplyBlendDefinition(brain, "m_DefaultBlend", style, duration, sb);
                }

                if (!applied)
                {
                    return ToolResponse.Error("Could not set blend settings — CinemachineBrain property not found.");
                }

                EditorUtility.SetDirty(brain);
                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion

        #region PRIVATE BLEND HELPERS

        /// <summary>
        /// Resolves a blend style keyword to the integer enum value used by Cinemachine's
        /// CinemachineBlendDefinition.Style enum. Works across v2 and v3 by checking both
        /// known enum type names.
        /// </summary>
        /// <param name="styleName">The blend style keyword (case-insensitive).</param>
        /// <param name="enumValue">The resolved enum value when successful.</param>
        /// <returns>True when the style was resolved; otherwise false.</returns>
        private static bool TryResolveBlendStyle(string styleName, out int enumValue)
        {
            enumValue = 0;

            string canonical = styleName.ToLowerInvariant() switch
            {
                "easeinout" or "ease_in_out" or "ease"    => "EaseInOut",
                "easein"    or "ease_in"                   => "EaseIn",
                "easeout"   or "ease_out"                  => "EaseOut",
                "hardin"    or "hard_in"                   => "HardIn",
                "hardout"   or "hard_out"                  => "HardOut",
                "linear"                                   => "Linear",
                "cut"                                      => "Cut",
                _                                          => styleName
            };

            string[] enumTypeNames =
            {
                "Unity.Cinemachine.CinemachineBlendDefinition+Style, Unity.Cinemachine",
                "Cinemachine.CinemachineBlendDefinition+Style, Cinemachine"
            };

            for (int i = 0; i < enumTypeNames.Length; i++)
            {
                Type? enumType = Type.GetType(enumTypeNames[i]);

                if (enumType == null)
                {
                    continue;
                }
                try
                {
                    object parsed = Enum.Parse(enumType, canonical, true);
                    enumValue = (int)parsed;
                    return true;
                }
                catch(ArgumentException ex)
                {
                    Debug.LogWarning($"[Camera] Blend style '{canonical}' not found in {enumType.Name}: {ex.Message}");
                }
            }

            return false;
        }

        /// <summary>
        /// Constructs a CinemachineBlendDefinition value via reflection and assigns it to the
        /// named property or field on the brain component.
        /// </summary>
        /// <param name="brain">The CinemachineBrain component.</param>
        /// <param name="memberName">Property or field name on the brain.</param>
        /// <param name="style">Blend style keyword.</param>
        /// <param name="duration">Blend duration in seconds.</param>
        /// <param name="sb">Log builder for reporting changes.</param>
        /// <returns>True when the blend definition was applied; otherwise false.</returns>
        private static bool ApplyBlendDefinition(UnityEngine.Component brain, string memberName, string style, float duration, StringBuilder sb)
        {
            Type? blendDefType = Type.GetType("Unity.Cinemachine.CinemachineBlendDefinition, Unity.Cinemachine");
            blendDefType ??= Type.GetType("Cinemachine.CinemachineBlendDefinition, Cinemachine");

            if (blendDefType == null)
            {
                return false;
            }

            Type brainType = brain.GetType();

            var prop  = brainType.GetProperty(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            var field = brainType.GetField(memberName, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (prop == null && field == null)
            {
                return false;
            }

            object blendDef = Activator.CreateInstance(blendDefType)!;

            if (TryResolveBlendStyle(style, out int styleInt))
            {
                var styleProp = blendDefType.GetProperty("Style", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (styleProp != null && styleProp.CanWrite)
                {
                    Type? enumType = styleProp.PropertyType;
                    styleProp.SetValue(blendDef, Enum.ToObject(enumType, styleInt));
                    sb.AppendLine($"  Style: {style}");
                }
                else
                {
                    var styleField = blendDefType.GetField("m_Style", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                    if (styleField != null)
                    {
                        styleField.SetValue(blendDef, Enum.ToObject(styleField.FieldType, styleInt));
                        sb.AppendLine($"  Style: {style}");
                    }
                }
            }
            else
            {
                sb.AppendLine($"  Style '{style}' not recognized — using default.");
            }

            var timeProp = blendDefType.GetProperty("Time", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

            if (timeProp != null && timeProp.CanWrite)
            {
                timeProp.SetValue(blendDef, duration);
                sb.AppendLine($"  Duration: {duration}s");
            }
            else
            {
                var timeField = blendDefType.GetField("m_Time", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

                if (timeField != null)
                {
                    timeField.SetValue(blendDef, duration);
                    sb.AppendLine($"  Duration: {duration}s");
                }
            }

            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(brain, blendDef);
                return true;
            }
            if (field != null)
            {
                field.SetValue(brain, blendDef);
                return true;
            }

            return false;
        }

        #endregion
    }
}