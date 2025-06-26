using System.Collections.Generic;

namespace Unispect.SDK.Models
{
    /// <summary>
    /// Container for search results across multiple categories
    /// </summary>
    public class SearchResults
    {
        /// <summary>
        /// Types that matched the search
        /// </summary>
        public List<TypeDefWrapper> Types { get; set; } = new List<TypeDefWrapper>();

        /// <summary>
        /// Fields that matched the search
        /// </summary>
        public List<FieldSearchResult> Fields { get; set; } = new List<FieldSearchResult>();

        /// <summary>
        /// Total number of results across all categories
        /// </summary>
        public int TotalCount => Types.Count + Fields.Count;
    }

    /// <summary>
    /// Represents a field found in search results
    /// </summary>
    public class FieldSearchResult
    {
        /// <summary>
        /// The full name of the type containing this field
        /// </summary>
        public string TypeName { get; set; }

        /// <summary>
        /// The field definition
        /// </summary>
        public FieldDefWrapper Field { get; set; }

        /// <summary>
        /// Formatted string for display purposes
        /// </summary>
        public string DisplayText => $"{TypeName}.{Field.Name} (0x{Field.Offset:X2}) : {Field.FieldType}";
    }
} 