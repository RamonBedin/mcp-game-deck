#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.UIElements;

namespace GameDeck.Editor.ChatUI
{
    /// <summary>
    /// Handles file attachments in the Chat UI: file picker, base64 encoding,
    /// preview pills, and serialization into the WebSocket prompt payload.
    /// Supports images (png, jpg, gif, webp) and documents (pdf).
    /// </summary>
    public partial class ChatWindow
    {
        #region CONSTANTS

        private const long ATTACH_MAX_BYTES = 5 * 1024 * 1024;
        private const int ATTACH_MAX_COUNT = 10;

        private static readonly HashSet<string> IMAGE_EXTENSIONS = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg", ".gif", ".webp"
        };

        #endregion

        #region FIELDS

        private List<AttachmentInfo> _pendingAttachments = new();
        private VisualElement? _attachPreviewArea;

        #endregion

        #region ATTACHMENT METHODS

        /// <summary>
        /// Opens a file picker dialog and adds the selected file to the pending attachments.
        /// Validates file size, extension, and count limit before encoding.
        /// </summary>
        private void OpenAttachFilePicker()
        {
            if (_pendingAttachments.Count >= ATTACH_MAX_COUNT)
            {
                EditorUtility.DisplayDialog("Limit reached", $"Maximum {ATTACH_MAX_COUNT} attachments per message.", "OK");
                return;
            }

            string path = EditorUtility.OpenFilePanel("Attach file", "", "png,jpg,jpeg,gif,webp,pdf");

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            var fileInfo = new FileInfo(path);

            if (fileInfo.Length > ATTACH_MAX_BYTES)
            {
                EditorUtility.DisplayDialog("File too large", $"Maximum file size is {ATTACH_MAX_BYTES / (1024 * 1024)}MB.", "OK");
                return;
            }

            string ext = fileInfo.Extension.ToLowerInvariant();
            bool isImage = IMAGE_EXTENSIONS.Contains(ext);
            string mediaType = GetMediaType(ext);

            if (string.IsNullOrEmpty(mediaType))
            {
                EditorUtility.DisplayDialog("Unsupported file", "Supported formats: PNG, JPG, GIF, WebP, PDF.", "OK");
                return;
            }

            byte[] bytes = File.ReadAllBytes(path);
            string base64 = Convert.ToBase64String(bytes);

            _pendingAttachments.Add(new AttachmentInfo
            {
                FileName = fileInfo.Name,
                MediaType = mediaType,
                Base64Data = base64,
                IsImage = isImage
            });

            RefreshAttachPreview();
        }

        /// <summary>
        /// Removes an attachment by index and refreshes the preview area.
        /// </summary>
        /// <param name="index">Zero-based index of the attachment to remove.</param>
        private void RemoveAttachment(int index)
        {
            if (index < 0 || index >= _pendingAttachments.Count)
            {
                return;
            }

            _pendingAttachments.RemoveAt(index);
            RefreshAttachPreview();
        }

        /// <summary>
        /// Rebuilds the visual preview area showing all pending attachments.
        /// Each attachment renders as a compact pill with an emoji icon, filename, and remove button.
        /// </summary>
        private void RefreshAttachPreview()
        {
            if (_attachPreviewArea == null)
            {
                return;
            }

            _attachPreviewArea.Clear();

            if (_pendingAttachments.Count == 0)
            {
                _attachPreviewArea.RemoveFromClassList("attach-preview-area--visible");
                return;
            }

            _attachPreviewArea.AddToClassList("attach-preview-area--visible");

            for (int i = 0; i < _pendingAttachments.Count; i++)
            {
                int capturedIndex = i;
                var attachment = _pendingAttachments[i];

                var chip = new VisualElement();
                chip.AddToClassList("attach-chip");

                var label = new Label(attachment.FileName);
                label.AddToClassList("attach-name");
                chip.Add(label);

                var removeBtn = new Button(() => RemoveAttachment(capturedIndex)) { text = "\u00d7" };
                removeBtn.AddToClassList("attach-remove");
                chip.Add(removeBtn);

                _attachPreviewArea.Add(chip);
            }
        }

        /// <summary>
        /// Serializes all pending attachments into a JSON array fragment for the WebSocket prompt payload.
        /// Clears the pending list after serialization.
        /// </summary>
        /// <returns>
        /// A JSON array string (e.g. <c>[{"type":"image","mediaType":"image/png","name":"file.png","data":"base64..."}]</c>),
        /// or empty string if no attachments are pending.
        /// </returns>
        private string ConsumeAttachmentsJson()
        {
            if (_pendingAttachments.Count == 0)
            {
                return "";
            }

            var sb = new System.Text.StringBuilder();
            sb.Append('[');

            for (int i = 0; i < _pendingAttachments.Count; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                var a = _pendingAttachments[i];
                string type = a.IsImage ? "image" : "document";
                sb.Append($"{{\"type\":{EscapeJson(type)},\"mediaType\":{EscapeJson(a.MediaType)},\"name\":{EscapeJson(a.FileName)},\"data\":{EscapeJson(a.Base64Data)}}}");
            }

            sb.Append(']');

            _pendingAttachments.Clear();
            RefreshAttachPreview();

            return sb.ToString();
        }

        /// <summary>
        /// Returns the MIME type for a file extension.
        /// </summary>
        /// <param name="ext">Lowercase file extension including the dot (e.g. ".png").</param>
        /// <returns>MIME type string, or empty string if unsupported.</returns>
        private static string GetMediaType(string ext)
        {
            return ext switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".pdf" => "application/pdf",
                _ => ""
            };
        }

        #endregion

        #region ATTACHMENT TYPES

        /// <summary>
        /// Represents a file attached to a chat message, ready to send as base64.
        /// </summary>
        private sealed class AttachmentInfo
        {
            /// <summary>Original file name (e.g. "screenshot.png").</summary>
            public string FileName = "";

            /// <summary>MIME type (e.g. "image/png", "application/pdf").</summary>
            public string MediaType = "";

            /// <summary>Base64-encoded file content.</summary>
            public string Base64Data = "";

            /// <summary>Whether this is an image (true) or document (false).</summary>
            public bool IsImage;
        }

        #endregion
    }
}
