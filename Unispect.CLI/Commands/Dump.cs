using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Unispect.CLI.Helpers;
using Unispect.SDK;
using Unispect.SDK.Models;

namespace Unispect.CLI.Commands
{
    public static class Dump
    {
        public static void HandleDumpCommand(string processName, string? plugin, string? outputPath, string? format, bool refresh, bool verbose)
        {
            try
            {
                ConsoleFormatting.WriteHeader($"Dumping type definitions from {processName}");
                
                var inspector = new Inspector();
                
                // Check if cache exists and is not being refreshed
                if (!refresh && inspector.LoadFromCache(processName))
                {
                    ConsoleFormatting.WriteSuccess($"Loaded {inspector.TypeDefinitions.Count} types from cache");
                }
                else
                {
                    // Need to perform fresh dump
                    ConsoleFormatting.WriteInfo("Performing fresh memory dump...");
                    
                    var availablePlugins = LoadPlugins();
                    var pluginType = ResolvePluginType(plugin, availablePlugins);
                    
                    if (pluginType == null)
                    {
                        ConsoleFormatting.WriteError($"Plugin '{plugin}' not found. Available plugins:");
                        foreach (var p in availablePlugins)
                        {
                            ConsoleFormatting.WriteInfo($"  - {p.Name}");
                        }
                        return;
                    }
                    
                    ConsoleFormatting.WriteInfo($"Using plugin: {pluginType.Name}");
                    
                    // Set up progress reporting
                    var progressBar = new ProgressBar();
                    
                    inspector.ProgressChanged += (_, pct) =>
                    {
                        progressBar.Report(pct * 100, "Dumping...");
                    };
                    
                    try
                    {
                        Program.SuppressSdkLogs = true;
                        inspector.DumpTypes(string.Empty, pluginType, verbose, processName);
                        progressBar.Complete();
                        
                        ConsoleFormatting.WriteSuccess($"Successfully dumped {inspector.TypeDefinitions.Count} type definitions");
                        
                        // Always save to cache after successful dump
                        var cachePath = Inspector.GetCacheFilePath(processName, "Assembly-CSharp");
                        ConsoleFormatting.WriteInfo($"Saving cache to: {cachePath}");
                        inspector.SaveToCache(processName, "Assembly-CSharp");
                        
                        // Verify cache was saved
                        if (Inspector.IsCacheAvailable(processName, "Assembly-CSharp"))
                        {
                            ConsoleFormatting.WriteSuccess("✅ Cache saved successfully");
                        }
                        else
                        {
                            ConsoleFormatting.WriteError("❌ Cache save failed - file not found after save");
                        }
                    }
                    catch (Exception ex)
                    {
                        ConsoleFormatting.WriteError($"Dump failed: {ex.Message}");
                        return;
                    }
                }
                
                // Export data if output path specified
                if (!string.IsNullOrEmpty(outputPath))
                {
                    ExportData(inspector, outputPath, format, verbose);
                }
                else
                {
                    ConsoleFormatting.WriteInfo($"Dump completed. {inspector.TypeDefinitions.Count} types cached for future queries.");
                }
            }
            catch (Exception ex)
            {
                ConsoleFormatting.WriteError($"Operation failed: {ex.Message}");
                if (verbose)
                {
                    ConsoleFormatting.WriteError($"Stack trace: {ex.StackTrace}");
                }
            }
            finally
            {
                Program.SuppressSdkLogs = false;
                if (!Console.IsOutputRedirected)
                {
                    Console.CursorVisible = true;
                }
                // Nothing else to clean here.
            }
        }

        private static List<Type> LoadPlugins()
        {
            var plugins = new List<Type>();
            
            // Add built-in BasicMemory
            plugins.Add(typeof(BasicMemory));
            
            // Load external plugins
            var pluginDirectory = Path.Combine(Directory.GetCurrentDirectory(), "Plugins");
            if (!Directory.Exists(pluginDirectory))
                return plugins;

            foreach (var subfolder in Directory.GetDirectories(pluginDirectory))
            {
                var folderName = Path.GetFileName(subfolder);
                var expectedDllPath = Path.Combine(subfolder, $"{folderName}.dll");
                
                if (File.Exists(expectedDllPath))
                {
                    try
                    {
                        var assembly = Assembly.LoadFrom(expectedDllPath);
                        var pluginTypes = assembly.GetTypes()
                            .Where(t => t.IsSubclassOf(typeof(MemoryProxy)) && 
                                       t.GetCustomAttribute<UnispectPluginAttribute>() != null)
                            .ToList();
                        
                        plugins.AddRange(pluginTypes);
                    }
                    catch (Exception ex)
                    {
                        ConsoleFormatting.WriteError($"Failed to load plugin {folderName}: {ex.Message}");
                    }
                }
            }
            
            return plugins;
        }

        private static Type? ResolvePluginType(string? pluginName, List<Type> availablePlugins)
        {
            if (string.IsNullOrEmpty(pluginName))
                return typeof(BasicMemory);

            // Try exact match first
            var exactMatch = availablePlugins.FirstOrDefault(p => 
                string.Equals(p.Name, pluginName, StringComparison.OrdinalIgnoreCase));
            if (exactMatch != null)
                return exactMatch;

            // Try partial match
            var partialMatch = availablePlugins.FirstOrDefault(p => 
                p.Name.Contains(pluginName, StringComparison.OrdinalIgnoreCase));
            
            return partialMatch;
        }

        private static void ExportData(Inspector inspector, string outputPath, string? format, bool verbose)
        {
            ConsoleFormatting.WriteInfo($"Exporting to {outputPath} in {format} format...");
            
            try
            {
                using var progressBar = new ProgressBar();
                
                switch (format?.ToLower())
                {
                    case "text":
                        ExportTextFormat(inspector, outputPath, verbose, progressBar);
                        break;
                    case "json":
                        ExportJsonFormat(inspector, outputPath, progressBar);
                        break;
                    case "utd":
                        ExportUtdFormat(inspector, outputPath, progressBar);
                        break;
                    case "csharp-intptr":
                        ExportCSharpStructs(inspector, outputPath, "IntPtr", verbose, progressBar);
                        break;
                    case "csharp-ulong":
                        ExportCSharpStructs(inspector, outputPath, "ulong", verbose, progressBar);
                        break;
                    default:
                        ConsoleFormatting.WriteError($"Unknown format: {format}");
                        return;
                }
                
                progressBar.Complete();
                ConsoleFormatting.WriteSuccess($"Export completed: {outputPath}");
            }
            catch (Exception ex)
            {
                ConsoleFormatting.WriteError($"Export failed: {ex.Message}");
            }
        }

        private static void ExportTextFormat(Inspector inspector, string outputPath, bool verbose, ProgressBar progressBar)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Generated by Unispect CLI v{ConsoleFormatting.GetVersion()}");
            sb.AppendLine($"Generated on: {DateTime.Now}");
            sb.AppendLine();
            sb.AppendLine("S = Static");
            sb.AppendLine("C = Constant");
            sb.AppendLine();

            var totalTypes = inspector.TypeDefinitions.Count;
            for (int i = 0; i < totalTypes; i++)
            {
                var typeDef = inspector.TypeDefinitions[i];
                
                if (verbose)
                    sb.Append($"[{typeDef.ClassType}] ");
                sb.Append(typeDef.FullName);
                
                if (verbose && typeDef.Parent != null)
                {
                    sb.Append($" : {typeDef.Parent.Name}");
                    if (typeDef.Interfaces?.Count > 0)
                    {
                        foreach (var iface in typeDef.Interfaces)
                        {
                            sb.Append($", {iface.Name}");
                        }
                    }
                }
                
                sb.AppendLine();
                
                if (typeDef.Fields != null)
                {
                    foreach (var field in typeDef.Fields)
                    {
                        if (!verbose && field.HasValue)
                            continue;

                        var fieldName = field.Name;
                        var fieldType = field.FieldType;
                        sb.AppendLine(field.HasValue
                            ? $"    [{field.Offset:X2}][{field.ConstantValueTypeShort}] {fieldName} : {fieldType}"
                            : $"    [{field.Offset:X2}] {fieldName} : {fieldType}");
                    }
                }
                
                sb.AppendLine();
                
                // Update progress
                progressBar.Report((double)(i + 1) / totalTypes * 100, "Writing text...");
            }
            File.WriteAllText(outputPath, sb.ToString());
        }

        private static void ExportJsonFormat(Inspector inspector, string outputPath, ProgressBar progressBar)
        {
            var exportData = new
            {
                GeneratedBy = $"Unispect CLI v{ConsoleFormatting.GetVersion()}",
                GeneratedOn = DateTime.Now,
                TypeCount = inspector.TypeDefinitions.Count,
                Types = inspector.TypeDefinitions.Select(t => new
                {
                    Name = t.FullName,
                    Parent = t.Parent?.FullName,
                    IsValueType = t.InnerDefinition.IsValueType,
                    Fields = (t.Fields ?? new()).Select(f => new
                    {
                        Name = f.Name,
                        Type = f.FieldType,
                        Offset = f.Offset
                    }).ToArray()
                }).ToArray()
            };

            var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(outputPath, json);
            progressBar.Report(100, "Serializing JSON...");
        }

        private static void ExportUtdFormat(Inspector inspector, string outputPath, ProgressBar progressBar)
        {
            Serializer.SaveCompressed(outputPath, inspector.TypeDefinitions);
            progressBar.Report(100, "Saving UTD...");
        }

        private static void ExportCSharpStructs(Inspector inspector, string outputPath, string ptrType, bool verbose, ProgressBar progressBar)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"// Generated by Unispect CLI v{ConsoleFormatting.GetVersion()}");
            sb.AppendLine($"// Generated on: {DateTime.Now}");
            sb.AppendLine("// Pointer type: " + ptrType);
            sb.AppendLine();
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Runtime.InteropServices;");
            sb.AppendLine();

            var totalTypes = inspector.TypeDefinitions.Count;
            for (int i = 0; i < totalTypes; i++)
            {
                var typeDef = inspector.TypeDefinitions[i];
                sb.AppendLine(typeDef.ToCSharpString(ptrType, !verbose));
                sb.AppendLine();
                
                // Update progress
                progressBar.Report((double)(i + 1) / totalTypes * 100, "Generating C# structs...");
            }

            File.WriteAllText(outputPath, sb.ToString());
        }
    }
} 