using System;
using System.Linq;
using Unispect.CLI.Helpers;
using Unispect.SDK;

namespace Unispect.CLI.Commands
{
    public static class Cache
    {
        public static void HandleListCommand()
        {
            try
            {
                var cacheFiles = Inspector.ListCacheFiles();
                
                if (!cacheFiles.Any())
                {
                    ConsoleFormatting.WriteInfo("No cache files found");
                    return;
                }

                ConsoleFormatting.WriteHeader($"Found {cacheFiles.Count} cache files");
                
                foreach (var cache in cacheFiles)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.Write($"📁 {cache.ProcessName}");
                    
                    if (!string.IsNullOrEmpty(cache.ModuleName) && cache.ModuleName != "Unknown")
                    {
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.Write($" ({cache.ModuleName})");
                    }
                    
                    Console.WriteLine();
                    
                    ConsoleFormatting.WriteProperty("Size", cache.FormattedSize);
                    ConsoleFormatting.WriteProperty("Age", cache.FormattedAge);
                    ConsoleFormatting.WriteProperty("Path", cache.FilePath);
                    
                    Console.WriteLine();
                }
                
                var totalSize = cacheFiles.Sum(c => c.Size);
                ConsoleFormatting.WriteInfo($"Total cache size: {ConsoleFormatting.FormatBytes(totalSize)}");
            }
            catch (Exception ex)
            {
                ConsoleFormatting.WriteError($"Failed to list cache files: {ex.Message}");
            }
        }

        public static void HandleInfoCommand(string process)
        {
            try
            {
                if (!Inspector.IsCacheAvailable(process))
                {
                    ConsoleFormatting.WriteError($"No cache found for process '{process}'");
                    return;
                }

                var cachePath = Inspector.GetCacheFilePath(process);
                var cacheAge = Inspector.GetCacheAge(process);
                
                // Load cache to get type count
                var inspector = new Inspector();
                if (!inspector.LoadFromCache(process))
                {
                    ConsoleFormatting.WriteError($"Failed to load cache for '{process}'");
                    return;
                }

                ConsoleFormatting.WriteHeader($"Cache Information: {process}");
                ConsoleFormatting.WriteProperty("Process", process);
                ConsoleFormatting.WriteProperty("Cache Path", cachePath);
                ConsoleFormatting.WriteProperty("Age", $"{cacheAge:F1} hours");
                ConsoleFormatting.WriteProperty("Type Count", inspector.TypeDefinitions.Count.ToString());
                
                if (System.IO.File.Exists(cachePath))
                {
                    var fileInfo = new System.IO.FileInfo(cachePath);
                    ConsoleFormatting.WriteProperty("File Size", ConsoleFormatting.FormatBytes(fileInfo.Length));
                    ConsoleFormatting.WriteProperty("Created", fileInfo.CreationTime.ToString());
                    ConsoleFormatting.WriteProperty("Modified", fileInfo.LastWriteTime.ToString());
                }

                // Show some sample types
                if (inspector.TypeDefinitions.Any())
                {
                    Console.WriteLine();
                    ConsoleFormatting.WriteSubHeader("Sample Types");
                    
                    var sampleTypes = inspector.TypeDefinitions
                        .Where(t => !t.FullName.StartsWith("System."))
                        .Take(5)
                        .ToList();
                    
                    foreach (var type in sampleTypes)
                    {
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine($"  🏷️  {type.FullName} ({type.Fields?.Count ?? 0} fields)");
                    }
                    
                    Console.ResetColor();
                    
                    if (inspector.TypeDefinitions.Count > 5)
                    {
                        ConsoleFormatting.WriteInfo($"... and {inspector.TypeDefinitions.Count - 5} more types");
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleFormatting.WriteError($"Failed to get cache info: {ex.Message}");
            }
        }

        public static void HandleClearCommand(string process)
        {
            try
            {
                if (process.ToLower() == "all")
                {
                    var deletedCount = Inspector.DeleteAllCache();
                    if (deletedCount > 0)
                    {
                        ConsoleFormatting.WriteSuccess($"Deleted {deletedCount} cache files");
                    }
                    else
                    {
                        ConsoleFormatting.WriteInfo("No cache files found to delete");
                    }
                }
                else
                {
                    if (Inspector.DeleteCache(process))
                    {
                        ConsoleFormatting.WriteSuccess($"Deleted cache for '{process}'");
                    }
                    else
                    {
                        ConsoleFormatting.WriteError($"No cache found for '{process}' or deletion failed");
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleFormatting.WriteError($"Failed to clear cache: {ex.Message}");
            }
        }
    }
} 