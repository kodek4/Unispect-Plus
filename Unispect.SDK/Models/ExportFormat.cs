namespace Unispect.SDK.Models
{
    /// <summary>
    /// Available export formats for Unispect results
    /// </summary>
    public enum ExportFormat
    {
        /// <summary>
        /// Human-readable text format (same as GUI default)
        /// </summary>
        Text,
        
        /// <summary>
        /// JSON format for programmatic consumption
        /// </summary>
        Json,
        
        /// <summary>
        /// Unispect Type Database format (.utd) for loading back into GUI
        /// </summary>
        TypeDatabase
    }
} 