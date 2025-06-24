using System;
using System.Collections.Generic;

namespace Unispect.Core.Models
{
    /// <summary>
    /// Result of a Unispect type dumping operation
    /// </summary>
    public class UnispectResult
    {
        /// <summary>
        /// Whether the operation was successful
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Error message if operation failed
        /// </summary>
        public string ErrorMessage { get; set; }

        /// <summary>
        /// Exception details if operation failed
        /// </summary>
        public Exception Exception { get; set; }

        /// <summary>
        /// Parsed type definitions from the target process
        /// </summary>
        public List<Unispect.TypeDefWrapper> TypeDefinitions { get; set; } = new List<Unispect.TypeDefWrapper>();

        /// <summary>
        /// Process name that was dumped
        /// </summary>
        public string ProcessName { get; set; }

        /// <summary>
        /// Module name that was dumped
        /// </summary>
        public string ModuleName { get; set; }

        /// <summary>
        /// Memory proxy type that was used
        /// </summary>
        public string MemoryProxyType { get; set; }

        /// <summary>
        /// When the dump was performed
        /// </summary>
        public DateTime DumpTime { get; set; } = DateTime.Now;

        /// <summary>
        /// Duration of the dump operation
        /// </summary>
        public TimeSpan Duration { get; set; }

        /// <summary>
        /// Number of types discovered
        /// </summary>
        public int TypeCount => TypeDefinitions?.Count ?? 0;

        /// <summary>
        /// Unity target version used (if any)
        /// </summary>
        public string UnityTarget { get; set; }

        /// <summary>
        /// Create a successful result
        /// </summary>
        public static UnispectResult CreateSuccess(List<Unispect.TypeDefWrapper> typeDefinitions, string processName, string moduleName, string memoryProxyType, TimeSpan duration)
        {
            return new UnispectResult
            {
                Success = true,
                TypeDefinitions = typeDefinitions ?? new List<Unispect.TypeDefWrapper>(),
                ProcessName = processName,
                ModuleName = moduleName,
                MemoryProxyType = memoryProxyType,
                Duration = duration
            };
        }

        /// <summary>
        /// Create a failed result
        /// </summary>
        public static UnispectResult CreateFailure(string errorMessage, Exception exception = null)
        {
            return new UnispectResult
            {
                Success = false,
                ErrorMessage = errorMessage,
                Exception = exception
            };
        }
    }
} 