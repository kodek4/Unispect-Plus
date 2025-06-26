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
    public static class Compare
    {
        public static void HandleCompareCommand(string process1, string process2, string? outputPath, string? format)
        {
            try
            {
                ConsoleFormatting.WriteHeader($"Comparing '{process1}' vs '{process2}'");
                
                // Load both caches
                var inspector1 = new Inspector();
                var inspector2 = new Inspector();
                
                if (!inspector1.LoadFromCache(process1))
                {
                    ConsoleFormatting.WriteError($"No cache found for '{process1}'. Run dump command first.");
                    // Print last 10 log lines for debugging
                    var lastLogs = Unispect.SDK.Log.GetLast(10);
                    foreach (var log in lastLogs)
                    {
                        ConsoleFormatting.WriteError($"[SDK Log] {log}");
                    }
                    return;
                }
                
                if (!inspector2.LoadFromCache(process2))
                {
                    ConsoleFormatting.WriteError($"No cache found for '{process2}'. Run dump command first.");
                    // Print last 10 log lines for debugging
                    var lastLogs = Unispect.SDK.Log.GetLast(10);
                    foreach (var log in lastLogs)
                    {
                        ConsoleFormatting.WriteError($"[SDK Log] {log}");
                    }
                    return;
                }
                
                ConsoleFormatting.WriteInfo($"Loaded {inspector1.TypeDefinitions.Count} types from '{process1}'");
                ConsoleFormatting.WriteInfo($"Loaded {inspector2.TypeDefinitions.Count} types from '{process2}'");
                
                // Perform comparison
                var comparison = CompareTypeDefinitions(inspector1.TypeDefinitions, inspector2.TypeDefinitions);
                
                // Display results
                DisplayComparisonResults(comparison);
                
                // Export if requested
                if (!string.IsNullOrEmpty(outputPath))
                {
                    ExportComparison(comparison, outputPath, format, process1, process2);
                }
            }
            catch (Exception ex)
            {
                ConsoleFormatting.WriteError($"Comparison failed: {ex.Message}");
            }
        }

        private static ComparisonResult CompareTypeDefinitions(List<TypeDefWrapper> types1, List<TypeDefWrapper> types2)
        {
            var result = new ComparisonResult();
            
            // The same FullName can appear multiple times (e.g., Unity anonymous types). Keep the variant with most fields to avoid key collisions.
            var dict1 = types1
                .GroupBy(t => t.FullName)
                .Select(g => g.OrderByDescending(x => x.Fields?.Count ?? 0).First())
                .ToDictionary(t => t.FullName, t => t);

            var dict2 = types2
                .GroupBy(t => t.FullName)
                .Select(g => g.OrderByDescending(x => x.Fields?.Count ?? 0).First())
                .ToDictionary(t => t.FullName, t => t);
            
            // Find types only in first set
            result.OnlyInFirst = dict1.Keys.Except(dict2.Keys).ToList();
            
            // Find types only in second set
            result.OnlyInSecond = dict2.Keys.Except(dict1.Keys).ToList();
            
            // Find common types and compare them
            var commonTypes = dict1.Keys.Intersect(dict2.Keys);
            
            foreach (var typeName in commonTypes)
            {
                var type1 = dict1[typeName];
                var type2 = dict2[typeName];
                
                var diff = CompareTypes(type1, type2);
                if (diff.HasDifferences)
                {
                    result.ModifiedTypes.Add(diff);
                }
            }
            
            return result;
        }

        private static TypeDifference CompareTypes(TypeDefWrapper type1, TypeDefWrapper type2)
        {
            var diff = new TypeDifference
            {
                TypeName = type1.FullName,
                Type1 = type1,
                Type2 = type2
            };
            
            // Compare field counts
            var fields1 = type1.Fields ?? new List<FieldDefWrapper>();
            var fields2 = type2.Fields ?? new List<FieldDefWrapper>();
            
            if (fields1.Count != fields2.Count)
            {
                diff.FieldCountChanged = true;
                diff.FieldCount1 = fields1.Count;
                diff.FieldCount2 = fields2.Count;
            }
            
            // Compare individual fields
            // Some obfuscated assemblies contain duplicate field names (e.g., single_0x00).
            // Deduplicate by keeping the variant with the **lowest offset** so comparisons are stable.
            var fieldDict1 = fields1
                .GroupBy(f => f.Name)
                .Select(g => g.OrderBy(x => x.Offset).First())
                .ToDictionary(f => f.Name, f => f);

            var fieldDict2 = fields2
                .GroupBy(f => f.Name)
                .Select(g => g.OrderBy(x => x.Offset).First())
                .ToDictionary(f => f.Name, f => f);
            
            // Fields only in first type
            diff.FieldsOnlyInFirst = fieldDict1.Keys.Except(fieldDict2.Keys).ToList();
            
            // Fields only in second type
            diff.FieldsOnlyInSecond = fieldDict2.Keys.Except(fieldDict1.Keys).ToList();
            
            // Compare common fields
            var commonFields = fieldDict1.Keys.Intersect(fieldDict2.Keys);
            
            foreach (var fieldName in commonFields)
            {
                var field1 = fieldDict1[fieldName];
                var field2 = fieldDict2[fieldName];
                
                if (field1.Offset != field2.Offset || field1.FieldType != field2.FieldType)
                {
                    diff.ModifiedFields.Add(new FieldDifference
                    {
                        FieldName = fieldName,
                        Field1 = field1,
                        Field2 = field2,
                        OffsetChanged = field1.Offset != field2.Offset,
                        TypeChanged = field1.FieldType != field2.FieldType
                    });
                }
            }
            
            return diff;
        }

        private static void DisplayComparisonResults(ComparisonResult comparison)
        {
            Console.WriteLine();
            
            // Summary
            ConsoleFormatting.WriteSubHeader("Comparison Summary");
            ConsoleFormatting.WriteProperty("Types only in first", comparison.OnlyInFirst.Count.ToString());
            ConsoleFormatting.WriteProperty("Types only in second", comparison.OnlyInSecond.Count.ToString());
            ConsoleFormatting.WriteProperty("Modified types", comparison.ModifiedTypes.Count.ToString());
            
            // Types only in first
            if (comparison.OnlyInFirst.Any())
            {
                ConsoleFormatting.WriteSubHeader("Types only in first process");
                foreach (var typeName in comparison.OnlyInFirst.Take(10))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"  - {typeName}");
                }
                if (comparison.OnlyInFirst.Count > 10)
                {
                    ConsoleFormatting.WriteInfo($"  ... and {comparison.OnlyInFirst.Count - 10} more");
                }
                Console.ResetColor();
            }
            
            // Types only in second
            if (comparison.OnlyInSecond.Any())
            {
                ConsoleFormatting.WriteSubHeader("Types only in second process");
                foreach (var typeName in comparison.OnlyInSecond.Take(10))
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  + {typeName}");
                }
                if (comparison.OnlyInSecond.Count > 10)
                {
                    ConsoleFormatting.WriteInfo($"  ... and {comparison.OnlyInSecond.Count - 10} more");
                }
                Console.ResetColor();
            }
            
            // Modified types
            if (comparison.ModifiedTypes.Any())
            {
                ConsoleFormatting.WriteSubHeader("Modified types");
                foreach (var modifiedType in comparison.ModifiedTypes.Take(5))
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"  ~ {modifiedType.TypeName}");
                    Console.ResetColor();
                    
                    if (modifiedType.FieldCountChanged)
                    {
                        ConsoleFormatting.WriteProperty("    Field count", $"{modifiedType.FieldCount1} → {modifiedType.FieldCount2}");
                    }
                    
                    if (modifiedType.ModifiedFields.Any())
                    {
                        ConsoleFormatting.WriteProperty("    Modified fields", modifiedType.ModifiedFields.Count.ToString());
                    }
                }
                if (comparison.ModifiedTypes.Count > 5)
                {
                    ConsoleFormatting.WriteInfo($"  ... and {comparison.ModifiedTypes.Count - 5} more modified types");
                }
            }
        }

        private static void ExportComparison(ComparisonResult comparison, string outputPath, string? format, string process1, string process2)
        {
            ConsoleFormatting.WriteInfo($"Exporting comparison to {outputPath}...");
            
            try
            {
                switch (format?.ToLower())
                {
                    case "json":
                        ExportComparisonJson(comparison, outputPath, process1, process2);
                        break;
                    case "text":
                    default:
                        ExportComparisonText(comparison, outputPath, process1, process2);
                        break;
                }
                
                ConsoleFormatting.WriteSuccess($"Comparison exported to {outputPath}");
            }
            catch (Exception ex)
            {
                ConsoleFormatting.WriteError($"Export failed: {ex.Message}");
            }
        }

        private static void ExportComparisonText(ComparisonResult comparison, string outputPath, string process1, string process2)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Unispect CLI Comparison Report");
            sb.AppendLine($"Generated: {DateTime.Now}");
            sb.AppendLine($"Process 1: {process1}");
            sb.AppendLine($"Process 2: {process2}");
            sb.AppendLine();
            
            sb.AppendLine("SUMMARY");
            sb.AppendLine("=======");
            sb.AppendLine($"Types only in {process1}: {comparison.OnlyInFirst.Count}");
            sb.AppendLine($"Types only in {process2}: {comparison.OnlyInSecond.Count}");
            sb.AppendLine($"Modified types: {comparison.ModifiedTypes.Count}");
            sb.AppendLine();
            
            if (comparison.OnlyInFirst.Any())
            {
                sb.AppendLine($"TYPES ONLY IN {process1.ToUpper()}");
                sb.AppendLine(new string('=', process1.Length + 14));
                foreach (var typeName in comparison.OnlyInFirst)
                {
                    sb.AppendLine($"- {typeName}");
                }
                sb.AppendLine();
            }
            
            if (comparison.OnlyInSecond.Any())
            {
                sb.AppendLine($"TYPES ONLY IN {process2.ToUpper()}");
                sb.AppendLine(new string('=', process2.Length + 14));
                foreach (var typeName in comparison.OnlyInSecond)
                {
                    sb.AppendLine($"+ {typeName}");
                }
                sb.AppendLine();
            }
            
            if (comparison.ModifiedTypes.Any())
            {
                sb.AppendLine("MODIFIED TYPES");
                sb.AppendLine("==============");
                foreach (var modifiedType in comparison.ModifiedTypes)
                {
                    sb.AppendLine($"~ {modifiedType.TypeName}");
                    
                    if (modifiedType.FieldCountChanged)
                    {
                        sb.AppendLine($"  Field count: {modifiedType.FieldCount1} → {modifiedType.FieldCount2}");
                    }
                    
                    foreach (var field in modifiedType.FieldsOnlyInFirst)
                    {
                        sb.AppendLine($"  - Field: {field}");
                    }
                    
                    foreach (var field in modifiedType.FieldsOnlyInSecond)
                    {
                        sb.AppendLine($"  + Field: {field}");
                    }
                    
                    foreach (var field in modifiedType.ModifiedFields)
                    {
                        sb.AppendLine($"  ~ Field: {field.FieldName}");
                        if (field.OffsetChanged)
                        {
                            sb.AppendLine($"    Offset: 0x{field.Field1.Offset:X} → 0x{field.Field2.Offset:X}");
                        }
                        if (field.TypeChanged)
                        {
                            sb.AppendLine($"    Type: {field.Field1.FieldType} → {field.Field2.FieldType}");
                        }
                    }
                    
                    sb.AppendLine();
                }
            }
            
            File.WriteAllText(outputPath, sb.ToString());
        }

        private static void ExportComparisonJson(ComparisonResult comparison, string outputPath, string process1, string process2)
        {
            var exportData = new
            {
                GeneratedBy = "Unispect CLI v" + ConsoleFormatting.GetVersion(),
                GeneratedOn = DateTime.Now,
                Process1 = process1,
                Process2 = process2,
                Summary = new
                {
                    OnlyInFirst = comparison.OnlyInFirst.Count,
                    OnlyInSecond = comparison.OnlyInSecond.Count,
                    ModifiedTypes = comparison.ModifiedTypes.Count
                },
                Comparison = comparison
            };
            
            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            
            File.WriteAllText(outputPath, json);
        }

        private class ComparisonResult
        {
            public List<string> OnlyInFirst { get; set; } = new List<string>();
            public List<string> OnlyInSecond { get; set; } = new List<string>();
            public List<TypeDifference> ModifiedTypes { get; set; } = new List<TypeDifference>();
        }

        private class TypeDifference
        {
            public string TypeName { get; set; } = "";
            public TypeDefWrapper Type1 { get; set; } = null!;
            public TypeDefWrapper Type2 { get; set; } = null!;
            public bool FieldCountChanged { get; set; }
            public int FieldCount1 { get; set; }
            public int FieldCount2 { get; set; }
            public List<string> FieldsOnlyInFirst { get; set; } = new List<string>();
            public List<string> FieldsOnlyInSecond { get; set; } = new List<string>();
            public List<FieldDifference> ModifiedFields { get; set; } = new List<FieldDifference>();
            
            public bool HasDifferences => FieldCountChanged || FieldsOnlyInFirst.Any() || FieldsOnlyInSecond.Any() || ModifiedFields.Any();
        }

        private class FieldDifference
        {
            public string FieldName { get; set; } = "";
            public FieldDefWrapper Field1 { get; set; } = null!;
            public FieldDefWrapper Field2 { get; set; } = null!;
            public bool OffsetChanged { get; set; }
            public bool TypeChanged { get; set; }
        }
    }
} 