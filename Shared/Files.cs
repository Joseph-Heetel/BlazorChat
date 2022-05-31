using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace BlazorChat.Shared
{
    /// <summary>
    /// Static helper class for dealing with file extensions and mime types
    /// </summary>
    public static class FileHelper
    {
        public const long MAX_FILE_SIZE = 16 * 1024 * 1024;

        private static readonly Dictionary<string, string> mimeTypetoExtension = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> extensionToMimeType = new Dictionary<string, string>();

        static FileHelper()
        {
            mimeTypetoExtension.Add("image/jpeg", "jpg");
            mimeTypetoExtension.Add("image/png", "png");
            mimeTypetoExtension.Add("image/webm", "webm");
            mimeTypetoExtension.Add("image/tiff", "tiff");
            mimeTypetoExtension.Add("image/bmp", "bmp");
            mimeTypetoExtension.Add("application/pdf", "pdf");

            extensionToMimeType.Add("jpg", "image/jpeg");
            extensionToMimeType.Add("jpeg", "image/jpeg");
            extensionToMimeType.Add("png", "image/png");
            extensionToMimeType.Add("webm", "image/webm");
            extensionToMimeType.Add("tiff", "image/tiff");
            extensionToMimeType.Add("bmp", "image/bmp");
            extensionToMimeType.Add("gif", "image/gif");
            extensionToMimeType.Add("pdf", "application/pdf");
        }

        public static string? ExtensionToMimeType(string? ext)
        {
            if (ext == null)
            {
                return null;
            }
            if (ext.StartsWith('.'))
            {
                ext = ext.Substring(1);
            }
            extensionToMimeType.TryGetValue(ext ?? string.Empty, out var mimeType);
            return mimeType;
        }

        public static string? MimeTypeToExtension(string? mime)
        {
            mimeTypetoExtension.TryGetValue(mime ?? string.Empty, out var ext);
            return ext;
        }

        public static bool IsValidExt(string? ext)
        {
            return extensionToMimeType.ContainsKey(ext ?? string.Empty);
        }

        public static bool IsValidMimeType(string? mime)
        {
            return mimeTypetoExtension.ContainsKey(mime ?? string.Empty);
        }

        public static bool IsImageMime(string? mime)
        {
            return mime?.StartsWith("image") ?? false;
        }

        public static bool IsImageExt(string? ext)
        {
            string? mime = ExtensionToMimeType(ext);
            return IsImageMime(mime);
        }

        public static string? MakeFileNameExt(ItemId fileId, string? ext)
        {
            if (fileId.IsZero || !IsValidExt(ext))
            {
                return null;
            }
            return $"{fileId}.{ext}";
        }

        public static string? MakeFileNameMime(ItemId fileId, string? mime)
        {
            if (!IsValidMimeType(mime))
            {
                return null;
            }
            return MakeFileNameExt(fileId, MimeTypeToExtension(mime));
        }
    }

    /// <summary>
    /// Class for transferring attachment data API <-> Client
    /// </summary>
    public class FileAttachment : ItemBase
    {
        /// <summary>
        /// Mimetype associated with the file
        /// </summary>
        public string MimeType { get; set; } = string.Empty;
        /// <summary>
        /// Size of the file in bytes
        /// </summary>
        public long Size { get; set; } = 0;
        [JsonIgnore]
        public bool IsImage { get => FileHelper.IsImageMime(MimeType); }
        public string TemporaryFileRequestURL(ItemId channelId)
        {
            return $"api/storage/{channelId}/{FileName()}";
        }

        public string FileName()
        {
            string? ext = FileHelper.MimeTypeToExtension(MimeType);
            if (ext == null)
            {
                return $"invalid";
            }
            return $"{Id}.{ext}";
        }
    }

    /// <summary>
    /// A temporary URL for accessing an attachment
    /// </summary>
    public class TemporaryURL
    {
        public string Url { get; set; } = string.Empty;
        public long ExpiresTS { get; set; }
        [JsonIgnore]
        public DateTimeOffset Expires
        {
            get => DateTimeOffset.FromUnixTimeMilliseconds(ExpiresTS);
            set => ExpiresTS = value.ToUnixTimeMilliseconds();
        }
    }

    /// <summary>
    /// File upload information
    /// </summary>
    public class FileUploadInfo
    {
        /// <summary>
        /// Extensions string indicating file type, without leading dot.
        /// </summary>
        public string MimeType { get; set; } = string.Empty;
        /// <summary>
        /// Full data
        /// </summary>
        public byte[] Data { get; set; } = Array.Empty<byte>();
    }
}
