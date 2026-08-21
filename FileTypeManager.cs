using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Cloudless
{
    /// <summary>
    /// Manages file type information and utilities for handling different file formats.
    /// Provides centralized access to file type definitions organized in the Cloudless.FileTypes namespace.
    /// </summary>
    public static class FileTypeManager
    {
        private static List<FileType>? _cachedFileTypes;

        /// <summary>
        /// Gets all available file type definitions.
        /// Results are cached after the first call for performance.
        /// </summary>
        public static List<FileType> GetFileTypes()
        {
            if (_cachedFileTypes != null)
                return _cachedFileTypes;


            var fileTypes = new List<FileType>()
            {
                new FileType { Extension = "bmp", IsVideo = false, MaybeAnimatedNonVideo = false },
                new FileType { Extension = "gif", IsVideo = false, MaybeAnimatedNonVideo = true },
                new FileType { Extension = "jfif", IsVideo = false, MaybeAnimatedNonVideo = false },
                new FileType { Extension = "jpeg", IsVideo = false, MaybeAnimatedNonVideo = true },
                new FileType { Extension = "jpg", IsVideo = false, MaybeAnimatedNonVideo = true },
                new FileType { Extension = "mkv", IsVideo = true, MaybeAnimatedNonVideo = false },
                new FileType { Extension = "mp4", IsVideo = true, MaybeAnimatedNonVideo = false },
                new FileType { Extension = "png", IsVideo = false, MaybeAnimatedNonVideo = true },
                new FileType { Extension = "webm", IsVideo = true, MaybeAnimatedNonVideo = false },
                new FileType { Extension = "webp", IsVideo = false, MaybeAnimatedNonVideo = true }
            };

            _cachedFileTypes = fileTypes;
            return fileTypes;
        }


        
        public static FileType? GetFileTypeByExtension(string extension)
        {
            extension = extension.TrimStart('.');
            var fileTypes = GetFileTypes();
            return fileTypes.FirstOrDefault(ft =>
                ft.Extension?.Equals(extension, StringComparison.OrdinalIgnoreCase) ?? false);
        }

        public static bool IsVideoFile(string extension)
        {
            var fileType = GetFileTypeByExtension(extension);
            return fileType?.IsVideo ?? false;
        }

        public static bool IsPotentiallyAnimatednNonVideo(string extension)
        {
            var fileType = GetFileTypeByExtension(extension);
            return (fileType?.IsVideo ?? false) || (fileType?.MaybeAnimatedNonVideo ?? false);
        }
    }

    public class FileType
    {
        public string Extension { get; set; }  // pure, e.g. "webm", "png", "jpg", "gif", etc.
        public bool IsVideo { get; set; }
        public bool MaybeAnimatedNonVideo { get; set; }  // e.g. WEBM, animated GIF, animated PNG, etc.
    }
}