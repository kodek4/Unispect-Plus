using System;

namespace Unispect.SDK.Models
{
    /// <summary>
    /// Information about a cached type definition file
    /// </summary>
    public class CacheInfo
    {
        /// <summary>
        /// The process name this cache was created for
        /// </summary>
        public string ProcessName { get; set; }

        /// <summary>
        /// The module name (typically Assembly-CSharp)
        /// </summary>
        public string ModuleName { get; set; }

        /// <summary>
        /// Full path to the cache file
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// File size in bytes
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// When the cache file was last modified
        /// </summary>
        public DateTime LastModified { get; set; }

        /// <summary>
        /// Age of the cache file in hours
        /// </summary>
        public double Age { get; set; }

        /// <summary>
        /// Human-readable file size
        /// </summary>
        public string FormattedSize
        {
            get
            {
                if (Size < 1024) return $"{Size} B";
                if (Size < 1024 * 1024) return $"{Size / 1024.0:F1} KB";
                return $"{Size / (1024.0 * 1024.0):F1} MB";
            }
        }

        /// <summary>
        /// Human-readable age
        /// </summary>
        public string FormattedAge
        {
            get
            {
                if (Age < 1) return $"{Age * 60:F0} minutes ago";
                if (Age < 24) return $"{Age:F1} hours ago";
                return $"{Age / 24:F1} days ago";
            }
        }
    }
} 