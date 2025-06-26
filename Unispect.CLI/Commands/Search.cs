using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Unispect.CLI.Helpers;
using Unispect.SDK;
using Unispect.SDK.Models;

namespace Unispect.CLI.Commands
{
    public static class Search
    {
        public static void HandleSearchCommand(string process, string pattern, bool regex, string? type, string? offsetRange, int limit, 
            bool includeParent, bool includeInterfaces, bool excludeSystem, int minFields, int maxFields)
        {
            try
            {
                var inspector = LoadInspectorFromCache(process);
                if (inspector == null) return;
                
                ConsoleFormatting.WriteHeader($"Searching for '{pattern}' in {inspector.TypeDefinitions.Count} types");
                
                // Handle offset range search
                if (!string.IsNullOrEmpty(offsetRange))
                {
                    HandleOffsetRangeSearch(inspector, offsetRange, limit);
                    return;
                }
                
                // Handle different search types
                switch (type?.ToLower())
                {
                    case "types":
                        HandleTypeSearch(inspector, pattern, regex, includeParent, includeInterfaces, excludeSystem, minFields, maxFields, limit);
                        break;
                        
                    case "fields":
                        HandleFieldSearch(inspector, pattern, regex, excludeSystem, limit);
                        break;
                        
                    case "all":
                    default:
                        HandleAllSearch(inspector, pattern, regex, includeParent, includeInterfaces, excludeSystem, minFields, maxFields, limit);
                        break;
                }
            }
            catch (Exception ex)
            {
                ConsoleFormatting.WriteError($"Search failed: {ex.Message}");
            }
        }

        private static void HandleOffsetRangeSearch(Inspector inspector, string offsetRange, int limit)
        {
            var parts = offsetRange.Split('-');
            if (parts.Length == 2 && 
                uint.TryParse(parts[0].Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var min) &&
                uint.TryParse(parts[1].Replace("0x", ""), System.Globalization.NumberStyles.HexNumber, null, out var max))
            {
                var results = inspector.SearchFieldsByOffset(min, max);
                DisplayFieldResults(results.Take(limit).ToList());
            }
            else
            {
                ConsoleFormatting.WriteError("Invalid offset range format. Use: 0x10-0x50");
            }
        }

        private static void HandleTypeSearch(Inspector inspector, string pattern, bool regex, bool includeParent, bool includeInterfaces, bool excludeSystem, int minFields, int maxFields, int limit)
        {
            var typeResults = SearchTypesWithFilters(inspector, pattern, regex, includeParent, includeInterfaces, excludeSystem, minFields, maxFields);
            ConsoleFormatting.WriteInfo($"Found {typeResults.Count} types");
            DisplayTypeResults(typeResults.Take(limit).ToList());
        }

        private static void HandleFieldSearch(Inspector inspector, string pattern, bool regex, bool excludeSystem, int limit)
        {
            var fieldResults = inspector.SearchFields(pattern, regex);
            
            if (excludeSystem)
            {
                fieldResults = fieldResults.Where(f => 
                    !f.TypeName.StartsWith("System.") && 
                    !f.TypeName.StartsWith("UnityEngine.") &&
                    !f.TypeName.StartsWith("Microsoft.") &&
                    !f.TypeName.StartsWith("Mono.")).ToList();
            }
            
            ConsoleFormatting.WriteInfo($"Found {fieldResults.Count} fields");
            DisplayFieldResults(fieldResults.Take(limit).ToList());
        }

        private static void HandleAllSearch(Inspector inspector, string pattern, bool regex, bool includeParent, bool includeInterfaces, bool excludeSystem, int minFields, int maxFields, int limit)
        {
            var allResults = inspector.SearchAll(pattern, regex);
            
            var filteredTypes = SearchTypesWithFilters(inspector, pattern, regex, includeParent, includeInterfaces, excludeSystem, minFields, maxFields);
            var filteredFields = allResults.Fields;
            
            if (excludeSystem)
            {
                filteredFields = filteredFields.Where(f => 
                    !f.TypeName.StartsWith("System.") && 
                    !f.TypeName.StartsWith("UnityEngine.") &&
                    !f.TypeName.StartsWith("Microsoft.") &&
                    !f.TypeName.StartsWith("Mono.")).ToList();
            }
            
            ConsoleFormatting.WriteInfo($"Found {filteredTypes.Count} types and {filteredFields.Count} fields");
            
            if (filteredTypes.Any())
            {
                ConsoleFormatting.WriteSubHeader("Types");
                DisplayTypeResults(filteredTypes.Take(limit / 2).ToList());
            }
            
            if (filteredFields.Any())
            {
                ConsoleFormatting.WriteSubHeader("Fields");
                DisplayFieldResults(filteredFields.Take(limit / 2).ToList());
            }
        }

        private static Inspector? LoadInspectorFromCache(string processName)
        {
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
        
        private static List<TypeDefWrapper> SearchTypesWithFilters(Inspector inspector, string? pattern, bool useRegex, 
            bool includeParent, bool includeInterfaces, bool excludeSystem, int minFields, int maxFields)
        {
            var results = new List<TypeDefWrapper>();
            
            // Start with basic search
            if (!string.IsNullOrEmpty(pattern))
            {
                results = inspector.SearchTypes(pattern, useRegex);
            }
            else
            {
                results = inspector.TypeDefinitions.ToList();
            }
            
            // Apply additional filters
            if (includeParent || includeInterfaces)
            {
                results = ExpandSearchWithParentAndInterfaces(inspector, pattern, useRegex, includeParent, includeInterfaces, results);
            }
            
            // Apply system type filter
            if (excludeSystem)
            {
                results = results.Where(t => 
                    !t.FullName.StartsWith("System.") && 
                    !t.FullName.StartsWith("UnityEngine.") &&
                    !t.FullName.StartsWith("Microsoft.") &&
                    !t.FullName.StartsWith("Mono.")).ToList();
            }
            
            // Apply field count filters
            if (minFields > 0)
            {
                results = results.Where(t => t.Fields?.Count >= minFields).ToList();
            }
            
            if (maxFields > 0)
            {
                results = results.Where(t => t.Fields?.Count <= maxFields).ToList();
            }
            
            return results.OrderBy(t => t.FullName).ToList();
        }

        private static List<TypeDefWrapper> ExpandSearchWithParentAndInterfaces(Inspector inspector, string? pattern, bool useRegex, bool includeParent, bool includeInterfaces, List<TypeDefWrapper> baseResults)
        {
            var expandedResults = new List<TypeDefWrapper>(baseResults);
            
            foreach (var type in inspector.TypeDefinitions)
            {
                bool matches = false;
                
                if (includeParent && type.Parent != null)
                {
                    matches = MatchesPattern(type.Parent.Name, pattern, useRegex);
                }
                
                if (!matches && includeInterfaces && type.Interfaces?.Any() == true)
                {
                    matches = type.Interfaces.Any(iface => MatchesPattern(iface.Name, pattern, useRegex));
                }
                
                if (matches && !expandedResults.Contains(type))
                {
                    expandedResults.Add(type);
                }
            }
            
            return expandedResults;
        }

        private static bool MatchesPattern(string text, string? pattern, bool useRegex)
        {
            if (pattern is null)
                return false;

            try
            {
                if (useRegex)
                {
                    var regex = new Regex(pattern, RegexOptions.IgnoreCase);
                    return regex.IsMatch(text);
                }
                else
                {
                    var regexPattern = "^" + Regex.Escape(pattern)
                        .Replace(@"\*", ".*")
                        .Replace(@"\?", ".") + "$";
                    var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
                    return regex.IsMatch(text);
                }
            }
            catch
            {
                return false;
            }
        }

        private static void DisplayTypeResults(List<TypeDefWrapper> types)
        {
            if (!types.Any())
            {
                ConsoleFormatting.WriteInfo("No types found matching the criteria.");
                return;
            }

            foreach (var type in types)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"🏷️  {type.FullName}");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($" ({type.Fields?.Count ?? 0} fields)");
                
                if (type.Parent != null)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"   ↳ extends {type.Parent.Name}");
                }
                Console.ResetColor();
            }
        }

        private static void DisplayFieldResults(List<FieldSearchResult> fields)
        {
            if (!fields.Any())
            {
                ConsoleFormatting.WriteInfo("No fields found matching the criteria.");
                return;
            }

            foreach (var result in fields)
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write($"🔧 {result.TypeName}.");
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{result.Field.Name}");
                Console.ForegroundColor = ConsoleColor.Gray;
                Console.WriteLine($" : {result.Field.FieldType} @ 0x{result.Field.Offset:X}");
                Console.ResetColor();
            }
        }
    }
} 