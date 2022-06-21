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

        /// <summary>
        /// Converts a file extension to mime type (returns null if no match is found or null has been passed into function)
        /// </summary>
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

        /// <summary>
        /// Converts a mime type to default file extension (returns null if no match is found or null has been passed into function)
        /// </summary>
        public static string? MimeTypeToExtension(string? mime)
        {
            mimeTypetoExtension.TryGetValue(mime ?? string.Empty, out var ext);
            return ext;
        }

        /// <summary>
        /// Returns true, if the extension is a valid recognized file extension
        /// </summary>
        public static bool IsValidExt(string? ext)
        {
            return extensionToMimeType.ContainsKey(ext ?? string.Empty);
        }

        /// <summary>
        /// Returns true, if the extension is a valid recognized mime type
        /// </summary>
        public static bool IsValidMimeType(string? mime)
        {
            return mimeTypetoExtension.ContainsKey(mime ?? string.Empty);
        }

        /// <summary>
        /// Returns true, if the extension is a valid image mime.
        /// </summary>
        public static bool IsImageMime(string? mime)
        {
            return IsValidMimeType(mime) && (mime?.StartsWith("image") ?? false);
        }

        /// <summary>
        /// Returns true, if the extension is a valid image extension.
        /// </summary>
        public static bool IsImageExt(string? ext)
        {
            string? mime = ExtensionToMimeType(ext);
            return IsImageMime(mime);
        }

        /// <summary>
        /// Generate file name from id and extension
        /// </summary>
        public static string? MakeFileNameExt(ItemId fileId, string? ext)
        {
            if (fileId.IsZero || !IsValidExt(ext))
            {
                return null;
            }
            return $"{fileId}.{ext}";
        }

        /// <summary>
        /// Generate file name from id and mime type (which is converted to extension)
        /// </summary>
        public static string? MakeFileNameMime(ItemId fileId, string? mime)
        {
            if (!IsValidMimeType(mime))
            {
                return null;
            }
            return MakeFileNameExt(fileId, MimeTypeToExtension(mime));
        }

        /// <summary>
        /// Helper method for printing a double value with consistent precision of 3 digits
        /// </summary>
        private static string ToString3Digits(double value, string suffix)
        {
            if (value > 100.0)
            {
                return $"{value:0} {suffix}";
            }
            else if (value > 10.0)
            {
                return $"{value: 0.0} {suffix}";
            }
            else
            {
                return $"{value:0.00} {suffix}";
            }
        }

        /// <summary>
        /// Returns a human readable string for a file size. Does not respect culture (and should not have to)
        /// </summary>
        /// <param name="size">File size in bytes</param>
        public static string MakeHumanReadableFileSize(long size)
        {
            string fileSize = "";
            const long oneKB = 1000L;
            const long oneMB = oneKB * 1000L;
            const long oneGB = oneMB * 1000L;
            const long oneTB = (long)oneGB * 1000L;
            if (size < oneKB)
            {
                fileSize = ToString3Digits((double)size, "B");
            }
            else if (size >= oneKB &&size < oneMB)
            {
                fileSize = ToString3Digits((double)size / oneKB, "KB");
            }
            else if (size >= oneMB &&size < oneGB)
            {
                fileSize = ToString3Digits((double)size / oneMB, "MB");
            }
            else if (size >= oneGB &&size < oneTB)
            {
                fileSize = ToString3Digits((double)size / oneGB, "GB");
            }
            else
            {
                fileSize = ToString3Digits((double)size / oneTB, "TB");
            }
            return fileSize;
        }
    }

    /// <summary>
    /// Class for transferring attachment data API <-> Client. File attachments and blob storage links have no user controlled names by design.
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
            return FileHelper.MakeFileNameMime(Id, MimeType) ?? "invalid";
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
