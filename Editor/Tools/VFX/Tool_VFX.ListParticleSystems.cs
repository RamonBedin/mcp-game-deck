#nullable enable
using System.ComponentModel;
using System.Text;
using GameDeck.MCP.Attributes;
using GameDeck.MCP.Models;
using GameDeck.MCP.Utils;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    /// <summary>
    /// MCP tool that lists all ParticleSystem components present in the current scene.
    /// </summary>
    [McpToolType]
    public partial class Tool_VFX
    {
        #region TOOL METHODS

        /// <summary>
        /// Finds every ParticleSystem in the active scene and returns a report of each
        /// system's main module and emission settings.
        /// </summary>
        /// <returns>Formatted text with each particle system's name, max particles, duration,
        /// loop, play-on-awake, simulation space, lifetime, speed, size, emission rate,
        /// and current play state.</returns>
        [McpTool("vfx-list-particles", Title = "VFX / List Particle Systems")]
        [Description("Lists all ParticleSystem components in the current scene with key settings " + "including max particles, duration, loop, emission rate, and simulation space.")]
        public ToolResponse ListParticleSystems()
        {
            return MainThreadDispatcher.Execute(() =>
            {
                var particles = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None);

                if (particles.Length == 0)
                {
                    return ToolResponse.Text("No ParticleSystem components found in scene.");
                }

                var sb = new StringBuilder();
                sb.AppendLine($"Particle Systems ({particles.Length}):");

                foreach (var ps in particles)
                {
                    var main = ps.main;
                    var emission = ps.emission;
                    sb.AppendLine($"  {ps.gameObject.name}:");
                    sb.AppendLine($"    Max Particles: {main.maxParticles}");
                    sb.AppendLine($"    Duration: {main.duration}s");
                    sb.AppendLine($"    Looping: {main.loop}");
                    sb.AppendLine($"    Play On Awake: {main.playOnAwake}");
                    sb.AppendLine($"    Simulation Space: {main.simulationSpace}");
                    sb.AppendLine($"    Start Lifetime: {main.startLifetime.constant}");
                    sb.AppendLine($"    Start Speed: {main.startSpeed.constant}");
                    sb.AppendLine($"    Start Size: {main.startSize.constant}");
                    sb.AppendLine($"    Emission Enabled: {emission.enabled}");
                    sb.AppendLine($"    Rate Over Time: {emission.rateOverTime.constant}");
                    sb.AppendLine($"    Is Playing: {ps.isPlaying}");
                    sb.AppendLine();
                }

                return ToolResponse.Text(sb.ToString());
            });
        }

        #endregion
    }
}