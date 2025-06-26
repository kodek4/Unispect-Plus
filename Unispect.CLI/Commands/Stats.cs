using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Unispect.CLI.Helpers;
using Unispect.SDK;

namespace Unispect.CLI.Commands
{
    public static class Stats
    {
        public static void HandleStatsCommand(string? process, string? outputPath, string? format, bool detailed)
        {
            try
            {
                var inspector = LoadInspectorFromCache(process);
                if (inspector == null) return;

                ConsoleFormatting.WriteHeader($"Statistics for '{process}'");
                
                var stats = GenerateStatistics(inspector, detailed);
                
                DisplayStatistics(stats, detailed);
                
                if (!string.IsNullOrEmpty(outputPath))
                {
                    ExportStatistics(stats, outputPath, format, process);
                }
            }
            catch (Exception ex)
            {
                ConsoleFormatting.WriteError($"Stats generation failed: {ex.Message}");
            }
        }

        private static TypeStatistics GenerateStatistics(Inspector inspector, bool detailed)
        {
            var stats = new TypeStatistics();
            var types = inspector.TypeDefinitions;
            
            stats.TotalTypes = types.Count;
            stats.RawClassCount = inspector.RawClassCount;
            
            // Basic counts
            stats.ClassCount = types.Count(t => t.ClassType == "Class");
            stats.StructCount = types.Count(t => t.ClassType == "Struct");
            stats.InterfaceCount = types.Count(t => t.ClassType == "Interface");
            stats.EnumCount = types.Count(t => t.ClassType == "Enum");
            
            // Field statistics
            var allFields = types.SelectMany(t => t.Fields ?? new List<FieldDefWrapper>()).ToList();
            stats.TotalFields = allFields.Count;
            stats.StaticFields = 0; // Static field detection not available in current FieldDefWrapper
            stats.ConstantFields = allFields.Count(f => f.HasValue);
            
            // Type with most fields
            var typeWithMostFields = types.OrderByDescending(t => t.Fields?.Count ?? 0).FirstOrDefault();
            if (typeWithMostFields != null)
            {
                stats.LargestTypeName = typeWithMostFields.FullName;
                stats.LargestTypeFieldCount = typeWithMostFields.Fields?.Count ?? 0;
            }
            
            // Average fields per type
            stats.AverageFieldsPerType = types.Average(t => t.Fields?.Count ?? 0);
            
            if (detailed)
            {
                GenerateDetailedStatistics(stats, types, allFields);
            }
            
            return stats;
        }

        private static void GenerateDetailedStatistics(TypeStatistics stats, List<TypeDefWrapper> types, List<FieldDefWrapper> allFields)
        {
            // Namespace analysis
            stats.Namespaces = types
                .Select(t => t.FullName.Contains('.') ? t.FullName.Substring(0, t.FullName.LastIndexOf('.')) : "<global>")
                .GroupBy(ns => ns)
                .ToDictionary(g => g.Key, g => g.Count())
                .OrderByDescending(kvp => kvp.Value)
                .Take(20)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            // Field type analysis
            stats.CommonFieldTypes = allFields
                .GroupBy(f => f.FieldType)
                .ToDictionary(g => g.Key, g => g.Count())
                .OrderByDescending(kvp => kvp.Value)
                .Take(20)
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            // Types with no fields
            stats.EmptyTypes = types.Where(t => t.Fields?.Count == 0).Select(t => t.FullName).ToList();
            
            // Types with many fields (>50)
            // Some Unity games (e.g., EFT) contain duplicated type definitions with identical FullName
            // which causes ToDictionary to throw "An item with the same key has already been added".
            // Group by FullName first and keep the *largest* field-count variant to ensure keys are unique.
            stats.ComplexTypes = types
                .Where(t => (t.Fields?.Count ?? 0) > 50)
                .Select(t => new { Name = t.FullName, FieldCount = t.Fields?.Count ?? 0 })
                .GroupBy(t => t.Name)
                .Select(g => g.OrderByDescending(x => x.FieldCount).First())
                .OrderByDescending(t => t.FieldCount)
                .ToDictionary(t => t.Name, t => t.FieldCount);
            
            // System vs User types
            stats.SystemTypes = types.Count(t => 
                t.FullName.StartsWith("System.") || 
                t.FullName.StartsWith("UnityEngine.") ||
                t.FullName.StartsWith("Microsoft.") ||
                t.FullName.StartsWith("Mono."));
            stats.UserTypes = stats.TotalTypes - stats.SystemTypes;
        }

        private static void DisplayStatistics(TypeStatistics stats, bool detailed)
        {
            // Basic statistics
            ConsoleFormatting.WriteSubHeader("Overview");
            ConsoleFormatting.WriteProperty("Total Types", stats.TotalTypes.ToString());
            ConsoleFormatting.WriteProperty("Raw Class Count", stats.RawClassCount.ToString());
            ConsoleFormatting.WriteProperty("Total Fields", stats.TotalFields.ToString());
            
            Console.WriteLine();
            ConsoleFormatting.WriteSubHeader("Type Breakdown");
            ConsoleFormatting.WriteProperty("Classes", stats.ClassCount.ToString());
            ConsoleFormatting.WriteProperty("Structs", stats.StructCount.ToString());
            ConsoleFormatting.WriteProperty("Interfaces", stats.InterfaceCount.ToString());
            ConsoleFormatting.WriteProperty("Enums", stats.EnumCount.ToString());
            
            Console.WriteLine();
            ConsoleFormatting.WriteSubHeader("Field Statistics");
            ConsoleFormatting.WriteProperty("Total Fields", stats.TotalFields.ToString());
            ConsoleFormatting.WriteProperty("Static Fields", stats.StaticFields.ToString());
            ConsoleFormatting.WriteProperty("Constant Fields", stats.ConstantFields.ToString());
            ConsoleFormatting.WriteProperty("Average Fields/Type", $"{stats.AverageFieldsPerType:F1}");
            
            if (!string.IsNullOrEmpty(stats.LargestTypeName))
            {
                ConsoleFormatting.WriteProperty("Largest Type", $"{stats.LargestTypeName} ({stats.LargestTypeFieldCount} fields)");
            }
            
            if (detailed)
            {
                DisplayDetailedStatistics(stats);
            }
        }

        private static void DisplayDetailedStatistics(TypeStatistics stats)
        {
            Console.WriteLine();
            ConsoleFormatting.WriteSubHeader("System vs User Types");
            ConsoleFormatting.WriteProperty("System Types", stats.SystemTypes.ToString());
            ConsoleFormatting.WriteProperty("User Types", stats.UserTypes.ToString());
            ConsoleFormatting.WriteProperty("System %", $"{(double)stats.SystemTypes / stats.TotalTypes * 100:F1}%");
            
            // Top namespaces
            if (stats.Namespaces?.Any() == true)
            {
                Console.WriteLine();
                ConsoleFormatting.WriteSubHeader("Top Namespaces");
                foreach (var ns in stats.Namespaces.Take(10))
                {
                    ConsoleFormatting.WriteProperty(ns.Key, ns.Value.ToString());
                }
            }
            
            // Common field types
            if (stats.CommonFieldTypes?.Any() == true)
            {
                Console.WriteLine();
                ConsoleFormatting.WriteSubHeader("Common Field Types");
                foreach (var fieldType in stats.CommonFieldTypes.Take(10))
                {
                    ConsoleFormatting.WriteProperty(fieldType.Key, fieldType.Value.ToString());
                }
            }
            
            // Complex types
            if (stats.ComplexTypes?.Any() == true)
            {
                Console.WriteLine();
                ConsoleFormatting.WriteSubHeader("Complex Types (>50 fields)");
                foreach (var complexType in stats.ComplexTypes.Take(10))
                {
                    ConsoleFormatting.WriteProperty(complexType.Key, $"{complexType.Value} fields");
                }
            }
            
            // Empty types
            if (stats.EmptyTypes?.Any() == true)
            {
                Console.WriteLine();
                ConsoleFormatting.WriteSubHeader($"Empty Types ({stats.EmptyTypes.Count} total)");
                foreach (var emptyType in stats.EmptyTypes.Take(10))
                {
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine($"  {emptyType}");
                }
                Console.ResetColor();
                
                if (stats.EmptyTypes.Count > 10)
                {
                    ConsoleFormatting.WriteInfo($"  ... and {stats.EmptyTypes.Count - 10} more");
                }
            }
        }

        private static void ExportStatistics(TypeStatistics stats, string outputPath, string? format, string? process)
        {
            ConsoleFormatting.WriteInfo($"Exporting statistics to {outputPath}...");
            
            try
            {
                switch (format?.ToLower())
                {
                    case "json":
                        ExportStatisticsJson(stats, outputPath, process);
                        break;
                    case "text":
                    default:
                        ExportStatisticsText(stats, outputPath, process);
                        break;
                }
                
                ConsoleFormatting.WriteSuccess($"Statistics exported to {outputPath}");
            }
            catch (Exception ex)
            {
                ConsoleFormatting.WriteError($"Export failed: {ex.Message}");
            }
        }

        private static void ExportStatisticsText(TypeStatistics stats, string outputPath, string? process)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Unispect CLI Statistics Report");
            sb.AppendLine($"Generated: {DateTime.Now}");
            sb.AppendLine($"Process: {process}");
            sb.AppendLine();
            
            sb.AppendLine("OVERVIEW");
            sb.AppendLine("========");
            sb.AppendLine($"Total Types: {stats.TotalTypes}");
            sb.AppendLine($"Raw Class Count: {stats.RawClassCount}");
            sb.AppendLine($"Total Fields: {stats.TotalFields}");
            sb.AppendLine();
            
            sb.AppendLine("TYPE BREAKDOWN");
            sb.AppendLine("==============");
            sb.AppendLine($"Classes: {stats.ClassCount}");
            sb.AppendLine($"Structs: {stats.StructCount}");
            sb.AppendLine($"Interfaces: {stats.InterfaceCount}");
            sb.AppendLine($"Enums: {stats.EnumCount}");
            sb.AppendLine();
            
            sb.AppendLine("FIELD STATISTICS");
            sb.AppendLine("================");
            sb.AppendLine($"Total Fields: {stats.TotalFields}");
            sb.AppendLine($"Static Fields: {stats.StaticFields}");
            sb.AppendLine($"Constant Fields: {stats.ConstantFields}");
            sb.AppendLine($"Average Fields/Type: {stats.AverageFieldsPerType:F1}");
            sb.AppendLine($"Largest Type: {stats.LargestTypeName} ({stats.LargestTypeFieldCount} fields)");
            sb.AppendLine();
            
            AppendDetailedStatisticsText(sb, stats);
            
            File.WriteAllText(outputPath, sb.ToString());
        }

        private static void AppendDetailedStatisticsText(StringBuilder sb, TypeStatistics stats)
        {
            if (stats.SystemTypes > 0 || stats.UserTypes > 0)
            {
                sb.AppendLine("SYSTEM VS USER TYPES");
                sb.AppendLine("====================");
                sb.AppendLine($"System Types: {stats.SystemTypes}");
                sb.AppendLine($"User Types: {stats.UserTypes}");
                sb.AppendLine($"System %: {(double)stats.SystemTypes / stats.TotalTypes * 100:F1}%");
                sb.AppendLine();
            }
            
            if (stats.Namespaces?.Any() == true)
            {
                sb.AppendLine("TOP NAMESPACES");
                sb.AppendLine("==============");
                foreach (var ns in stats.Namespaces)
                {
                    sb.AppendLine($"{ns.Key}: {ns.Value}");
                }
                sb.AppendLine();
            }
            
            if (stats.CommonFieldTypes?.Any() == true)
            {
                sb.AppendLine("COMMON FIELD TYPES");
                sb.AppendLine("==================");
                foreach (var fieldType in stats.CommonFieldTypes)
                {
                    sb.AppendLine($"{fieldType.Key}: {fieldType.Value}");
                }
                sb.AppendLine();
            }
        }

        private static void ExportStatisticsJson(TypeStatistics stats, string outputPath, string? process)
        {
            var exportData = new
            {
                GeneratedBy = $"Unispect CLI v{ConsoleFormatting.GetVersion()}",
                GeneratedOn = DateTime.Now,
                Process = process,
                Statistics = stats
            };
            
            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            
            File.WriteAllText(outputPath, json);
        }

        private static Inspector? LoadInspectorFromCache(string? processName)
        {
            if (string.IsNullOrEmpty(processName))
            {
                ConsoleFormatting.WriteError("Process name cannot be null or empty.");
                return null;
            }
            
            var inspector = new Inspector();
            if (!inspector.LoadFromCache(processName))
            {
                ConsoleFormatting.WriteError($"No cache found for {processName}. Run 'unispect dump --process {processName}' first.");
                // Print last 10 log lines for debugging
                var lastLogs = Unispect.SDK.Log.GetLast(10);
                foreach (var log in lastLogs)
                {
                    ConsoleFormatting.WriteError($"[SDK Log] {log}");
                }
                return null;
            }
            return inspector;
        }

        private class TypeStatistics
        {
            public int TotalTypes { get; set; }
            public int TotalFields { get; set; }
            public int ClassCount { get; set; }
            public int StructCount { get; set; }
            public int InterfaceCount { get; set; }
            public int EnumCount { get; set; }
            public int StaticFields { get; set; }
            public int ConstantFields { get; set; }
            public double AverageFieldsPerType { get; set; }
            public string? LargestTypeName { get; set; }
            public int LargestTypeFieldCount { get; set; }
            public int SystemTypes { get; set; }
            public int UserTypes { get; set; }
            public Dictionary<string, int>? Namespaces { get; set; }
            public Dictionary<string, int>? CommonFieldTypes { get; set; }
            public Dictionary<string, int>? ComplexTypes { get; set; }
            public List<string>? EmptyTypes { get; set; }
            public int RawClassCount { get; set; }
        }
    }
} 