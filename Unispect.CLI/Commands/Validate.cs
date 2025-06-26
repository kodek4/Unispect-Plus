using System;
using System.IO;
using System.Threading.Tasks;
using Unispect.CLI.Helpers;
using Unispect.SDK;

namespace Unispect.CLI.Commands
{
    public static class Validate
    {
        public static void HandleValidateCommand(string process, bool fix)
        {
            try
            {
                ConsoleFormatting.WriteHeader($"Validating cache for '{process}'");
                
                if (!Inspector.IsCacheAvailable(process))
                {
                    ConsoleFormatting.WriteError($"No cache found for process '{process}'");
                    return;
                }

                var cachePath = Inspector.GetCacheFilePath(process);
                var issues = 0;
                
                // Check if file exists
                if (!File.Exists(cachePath))
                {
                    ConsoleFormatting.WriteError("Cache file does not exist");
                    return;
                }
                
                ConsoleFormatting.WriteInfo("✓ Cache file exists");
                
                // Check file size
                var fileInfo = new FileInfo(cachePath);
                if (fileInfo.Length == 0)
                {
                    ConsoleFormatting.WriteError("Cache file is empty");
                    issues++;
                    
                    if (fix)
                    {
                        ConsoleFormatting.WriteInfo("🔧 Deleting empty cache file...");
                        File.Delete(cachePath);
                        ConsoleFormatting.WriteSuccess("Empty cache file deleted");
                    }
                }
                else
                {
                    ConsoleFormatting.WriteInfo($"✓ Cache file size: {ConsoleFormatting.FormatBytes(fileInfo.Length)}");
                }
                
                // Try to load cache
                var inspector = new Inspector();
                try
                {
                    if (inspector.LoadFromCache(process))
                    {
                        ConsoleFormatting.WriteInfo("✓ Cache file loads successfully");
                        
                        // Validate type definitions
                        if (inspector.TypeDefinitions == null || inspector.TypeDefinitions.Count == 0)
                        {
                            ConsoleFormatting.WriteError("Cache contains no type definitions");
                            issues++;
                            
                            if (fix)
                            {
                                ConsoleFormatting.WriteInfo("🔧 Deleting invalid cache file...");
                                File.Delete(cachePath);
                                ConsoleFormatting.WriteSuccess("Invalid cache file deleted");
                            }
                        }
                        else
                        {
                            ConsoleFormatting.WriteInfo($"✓ Found {inspector.TypeDefinitions.Count} type definitions");
                            
                            // Check for corrupt type definitions
                            var corruptTypes = 0;
                            foreach (var type in inspector.TypeDefinitions)
                            {
                                try
                                {
                                    // Try to access basic properties
                                    var name = type.FullName;
                                    var fields = type.Fields;
                                    var parent = type.Parent;
                                }
                                catch
                                {
                                    corruptTypes++;
                                }
                            }
                            
                            if (corruptTypes > 0)
                            {
                                ConsoleFormatting.WriteError($"Found {corruptTypes} corrupt type definitions");
                                issues++;
                                
                                if (fix)
                                {
                                    ConsoleFormatting.WriteInfo("🔧 Deleting corrupted cache file...");
                                    File.Delete(cachePath);
                                    ConsoleFormatting.WriteSuccess("Corrupted cache file deleted");
                                }
                            }
                            else
                            {
                                ConsoleFormatting.WriteInfo("✓ All type definitions are valid");
                            }
                        }
                    }
                    else
                    {
                        ConsoleFormatting.WriteError($"Failed to load cache for '{process}'");
                        // Print last 10 log lines for debugging
                        var lastLogs = Unispect.SDK.Log.GetLast(10);
                        foreach (var log in lastLogs)
                        {
                            ConsoleFormatting.WriteError($"[SDK Log] {log}");
                        }
                        return;
                    }
                }
                catch (Exception ex)
                {
                    ConsoleFormatting.WriteError($"Cache validation failed: {ex.Message}");
                    issues++;
                    
                    if (fix)
                    {
                        ConsoleFormatting.WriteInfo("🔧 Deleting corrupted cache file...");
                        try
                        {
                            File.Delete(cachePath);
                            ConsoleFormatting.WriteSuccess("Corrupted cache file deleted");
                        }
                        catch (Exception deleteEx)
                        {
                            ConsoleFormatting.WriteError($"Failed to delete corrupted cache: {deleteEx.Message}");
                        }
                    }
                }
                
                // Check cache age
                var cacheAge = Inspector.GetCacheAge(process);
                if (cacheAge > 24) // Older than 24 hours
                {
                    ConsoleFormatting.WriteInfo($"⚠️  Cache is {cacheAge:F1} hours old (consider refreshing)");
                }
                else
                {
                    ConsoleFormatting.WriteInfo($"✓ Cache age: {cacheAge:F1} hours");
                }
                
                // Summary
                Console.WriteLine();
                if (issues == 0)
                {
                    ConsoleFormatting.WriteSuccess("✅ Cache validation passed - no issues found");
                }
                else
                {
                    if (fix)
                    {
                        ConsoleFormatting.WriteSuccess($"🔧 Cache validation completed - {issues} issues fixed");
                    }
                    else
                    {
                        ConsoleFormatting.WriteError($"❌ Cache validation found {issues} issues (use --fix to resolve)");
                    }
                }
            }
            catch (Exception ex)
            {
                ConsoleFormatting.WriteError($"Validation failed: {ex.Message}");
            }
        }
    }
} 