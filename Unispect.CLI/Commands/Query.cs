using System;
using System.Linq;
using System.Threading.Tasks;
using Unispect.CLI.Helpers;
using Unispect.SDK;
using System.IO;

namespace Unispect.CLI.Commands
{
    public static class Query
    {
        public static void HandleQueryCommand(string? process, string? query, string? format)
        {
            try
            {
                var inspector = LoadInspectorFromCache(process);
                if (inspector == null) return;

                if (query == null)
                {
                    ConsoleFormatting.WriteError("Query cannot be null.");
                    return;
                }

                ConsoleFormatting.WriteHeader($"Querying '{query}' in {inspector.TypeDefinitions.Count} types");

                // Handle wildcard queries
                if (query.Contains("*"))
                {
                    HandleWildcardQuery(inspector, query, format);
                    return;
                }

                // Handle multiple field queries (comma-separated)
                if (query.Contains(","))
                {
                    HandleMultipleFieldQuery(inspector, query, format);
                    return;
                }

                // Handle single type or field query
                if (query.Contains("."))
                {
                    HandleFieldQuery(inspector, query, format);
                }
                else
                {
                    HandleTypeQuery(inspector, query, format);
                }
            }
            catch (Exception ex)
            {
                ConsoleFormatting.WriteError($"Query failed: {ex.Message}");
            }
        }

        private static void HandleWildcardQuery(Inspector inspector, string query, string? format)
        {
            if (query.EndsWith(".*"))
            {
                // Show all fields of a type
                var typeName = query.Substring(0, query.Length - 2);
                var type = inspector.GetType(typeName);
                
                if (type == null)
                {
                    ConsoleFormatting.WriteError($"Type '{typeName}' not found");
                    return;
                }

                ConsoleFormatting.WriteSuccess($"Found type: {type.FullName}");
                DisplayTypeWithAllFields(type, format);
            }
            else
            {
                // Wildcard search
                var searchResults = inspector.SearchTypes(query, false);
                ConsoleFormatting.WriteInfo($"Found {searchResults.Count} types matching '{query}'");
                
                foreach (var type in searchResults.Take(20))
                {
                    DisplayTypeInfo(type, format);
                }
                
                if (searchResults.Count > 20)
                {
                    ConsoleFormatting.WriteInfo($"... and {searchResults.Count - 20} more results");
                }
            }
        }

        private static void HandleMultipleFieldQuery(Inspector inspector, string query, string? format)
        {
            var queries = query.Split(',', StringSplitOptions.RemoveEmptyEntries);
            
            foreach (var singleQuery in queries)
            {
                var trimmedQuery = singleQuery.Trim();
                ConsoleFormatting.WriteSubHeader($"Query: {trimmedQuery}");
                
                if (trimmedQuery.Contains("."))
                {
                    HandleFieldQuery(inspector, trimmedQuery, format);
                }
                else
                {
                    HandleTypeQuery(inspector, trimmedQuery, format);
                }
                
                Console.WriteLine();
            }
        }

        private static void HandleFieldQuery(Inspector inspector, string query, string? format)
        {
            var parts = query.Split('.');
            if (parts.Length != 2)
            {
                ConsoleFormatting.WriteError("Invalid field query format. Use: TypeName.FieldName");
                return;
            }

            var typeName = parts[0];
            var fieldName = parts[1];

            var field = inspector.GetField(typeName, fieldName);
            if (field == null)
            {
                ConsoleFormatting.WriteError($"Field '{query}' not found");
                return;
            }

            var type = inspector.GetType(typeName);
            ConsoleFormatting.WriteSuccess($"Found field: {typeName}.{field.Name}");
            DisplayFieldInfo(type, field, format);
        }

        private static void HandleTypeQuery(Inspector inspector, string query, string? format)
        {
            var type = inspector.GetType(query);
            if (type == null)
            {
                ConsoleFormatting.WriteError($"Type '{query}' not found");
                return;
            }

            ConsoleFormatting.WriteSuccess($"Found type: {type.FullName}");
            DisplayTypeInfo(type, format);
        }

        private static void DisplayTypeWithAllFields(TypeDefWrapper type, string? format)
        {
            switch (format?.ToLower())
            {
                case "full":
                    DisplayFullTypeInfo(type);
                    break;
                case "offset-only":
                    DisplayOffsetOnlyInfo(type);
                    break;
                case "type-only":
                    DisplayTypeOnlyInfo(type);
                    break;
                default:
                    DisplayFullTypeInfo(type);
                    break;
            }
        }

        private static void DisplayTypeInfo(TypeDefWrapper type, string? format)
        {
            switch (format?.ToLower())
            {
                case "full":
                    DisplayBasicTypeInfo(type);
                    break;
                case "offset-only":
                    ConsoleFormatting.WriteInfo($"Type: {type.FullName} ({type.Fields?.Count ?? 0} fields)");
                    break;
                case "type-only":
                    Console.WriteLine(type.FullName);
                    break;
                default:
                    DisplayBasicTypeInfo(type);
                    break;
            }
        }

        private static void DisplayFieldInfo(TypeDefWrapper? type, FieldDefWrapper field, string? format)
        {
            switch (format?.ToLower())
            {
                case "full":
                    DisplayFullFieldInfo(type, field);
                    break;
                case "offset-only":
                    Console.WriteLine($"0x{field.Offset:X}");
                    break;
                case "type-only":
                    Console.WriteLine(field.FieldType);
                    break;
                default:
                    DisplayFullFieldInfo(type, field);
                    break;
            }
        }

        private static void DisplayFullTypeInfo(TypeDefWrapper type)
        {
            ConsoleFormatting.WriteProperty("Type", type.FullName);
            ConsoleFormatting.WriteProperty("Kind", type.ClassType);
            
            if (type.Parent != null)
            {
                ConsoleFormatting.WriteProperty("Parent", type.Parent.Name);
            }
            
            if (type.Interfaces?.Any() == true)
            {
                ConsoleFormatting.WriteProperty("Interfaces", string.Join(", ", type.Interfaces.Select(i => i.Name)));
            }
            
            ConsoleFormatting.WriteProperty("Field Count", (type.Fields?.Count ?? 0).ToString());
            
            if (type.Fields?.Any() == true)
            {
                Console.WriteLine();
                ConsoleFormatting.WriteSubHeader("Fields");
                
                foreach (var field in type.Fields)
                {
                    var fieldInfo = field.HasValue 
                        ? $"[0x{field.Offset:X2}][{field.ConstantValueTypeShort}] {field.Name} : {field.FieldType}"
                        : $"[0x{field.Offset:X2}] {field.Name} : {field.FieldType}";
                    
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine($"  {fieldInfo}");
                    Console.ResetColor();
                }
            }
        }

        private static void DisplayBasicTypeInfo(TypeDefWrapper type)
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

        private static void DisplayOffsetOnlyInfo(TypeDefWrapper type)
        {
            ConsoleFormatting.WriteInfo($"Type: {type.FullName}");
            if (type.Fields?.Any() == true)
            {
                foreach (var field in type.Fields)
                {
                    Console.WriteLine($"  {field.Name}: 0x{field.Offset:X}");
                }
            }
        }

        private static void DisplayTypeOnlyInfo(TypeDefWrapper type)
        {
            if (type.Fields?.Any() == true)
            {
                foreach (var field in type.Fields)
                {
                    Console.WriteLine($"{field.Name}: {field.FieldType}");
                }
            }
        }

        private static void DisplayFullFieldInfo(TypeDefWrapper? type, FieldDefWrapper field)
        {
            if (type != null)
            {
                ConsoleFormatting.WriteProperty("Container Type", type.FullName);
            }
            ConsoleFormatting.WriteProperty("Field Name", field.Name);
            ConsoleFormatting.WriteProperty("Field Type", field.FieldType);
            ConsoleFormatting.WriteProperty("Offset", $"0x{field.Offset:X}");
            ConsoleFormatting.WriteProperty("Is Pointer", field.IsPointer.ToString());
            ConsoleFormatting.WriteProperty("Is Value Type", field.IsValueType.ToString());

            if (field.HasValue)
            {
                ConsoleFormatting.WriteProperty("Constant Value", field.ConstantValueType.Trim());
            }
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
                var lastLogs = Unispect.SDK.Log.GetLast(10);
                foreach (var log in lastLogs)
                {
                    ConsoleFormatting.WriteError($"[SDK Log] {log}");
                }
                return null;
            }

            return inspector;
        }
    }
} 