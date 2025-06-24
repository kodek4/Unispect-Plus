using System;

namespace Unispect.Core.Models
{
    /// <summary>
    /// Configuration options for Unispect type dumping operations.
    /// Exposes all GUI options programmatically.
    /// </summary>
    public class UnispectOptions
    {
        /// <summary>
        /// Process name or handle to attach to (without .exe extension for BasicMemory)
        /// </summary>
        public string ProcessName { get; set; }

        /// <summary>
        /// Target module to dump (default: "Assembly-CSharp")
        /// </summary>
        public string ModuleName { get; set; } = "Assembly-CSharp";

        /// <summary>
        /// Memory proxy type to use (BasicMemory, or custom DMA implementation)
        /// </summary>
        public Type MemoryProxyType { get; set; } = typeof(Unispect.BasicMemory);

        /// <summary>
        /// Path to Unity version offset JSON file (e.g., targets/v2022.json)
        /// </summary>
        public string UnityTargetPath { get; set; }

        /// <summary>
        /// Enable verbose output (includes class types, inheritance, etc.)
        /// </summary>
        public bool Verbose { get; set; } = true;

        /// <summary>
        /// Output type for formatting (matches GUI dropdown)
        /// </summary>
        public OutputType OutputType { get; set; } = OutputType.Standard;

        /// <summary>
        /// Timeout in milliseconds for operations (default: 30 seconds)
        /// </summary>
        public int TimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Validate options before processing
        /// </summary>
        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(ProcessName))
                throw new ArgumentException("ProcessName is required", nameof(ProcessName));
            
            if (string.IsNullOrWhiteSpace(ModuleName))
                throw new ArgumentException("ModuleName is required", nameof(ModuleName));
            
            if (MemoryProxyType == null)
                throw new ArgumentException("MemoryProxyType is required", nameof(MemoryProxyType));

            if (!typeof(Unispect.MemoryProxy).IsAssignableFrom(MemoryProxyType))
                throw new ArgumentException("MemoryProxyType must inherit from MemoryProxy", nameof(MemoryProxyType));
        }
    }
} 