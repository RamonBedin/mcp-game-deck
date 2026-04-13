#nullable enable
using System.Text;
using UnityEngine;

namespace GameDeck.Editor.Tools
{
    public partial class Tool_Physics
    {
        #region HELPERS

        /// <summary>
        /// Formats a <see cref="RaycastHit"/> into a human-readable text block and appends it
        /// to <paramref name="sb"/>. When <paramref name="index"/> is non-negative the entry is
        /// prefixed with a bracketed index for multi-hit listings.
        /// </summary>
        /// <param name="sb">The <see cref="StringBuilder"/> to append into.</param>
        /// <param name="hit">The hit to format.</param>
        /// <param name="index">Optional zero-based index. Pass -1 (default) to omit the index prefix.</param>
        private static void AppendHitInfo(StringBuilder sb, RaycastHit hit, int index = -1)
        {
            string prefix = index >= 0 ? $"  [{index}] " : "  ";
            string indent = index >= 0 ? "       "      : "  ";

            sb.AppendLine($"{prefix}GameObject: {hit.collider.gameObject.name}");
            sb.AppendLine($"{indent}Collider: {hit.collider.name}");
            sb.AppendLine($"{indent}Layer: {LayerMask.LayerToName(hit.collider.gameObject.layer)} ({hit.collider.gameObject.layer})");
            sb.AppendLine($"{indent}Point: {hit.point}");
            sb.AppendLine($"{indent}Normal: {hit.normal}");
            sb.AppendLine($"{indent}Distance: {hit.distance}");
        }

        #endregion
    }
}
