using System;
using System.IO;
using Unispect.Core;
using Unispect.Core.Models;

namespace ConsoleExample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Unispect.Core Library Example");
            Console.WriteLine("=============================");

            // Example 1: Basic Memory Dump (synchronous)
            Console.WriteLine("\n1. Basic Memory Dump Example:");
            BasicMemoryExample();

            // Example 2: Custom DMA Dump (when DMA plugin is available)
            Console.WriteLine("\n2. DMA Memory Dump Example:");
            DmaMemoryExample();

            // Example 3: Async with Progress
            Console.WriteLine("\n3. Async Dump with Progress:");
            AsyncExample().Wait();

            Console.WriteLine("\n\nPress any key to exit...");
            Console.ReadKey();
        }

        static void BasicMemoryExample()
        {
            var engine = new UnispectEngine();
            
            var options = new UnispectOptions
            {
                ProcessName = "YourGameProcess",  // Without .exe extension for BasicMemory
                ModuleName = "Assembly-CSharp",
                MemoryProxyType = typeof(Unispect.BasicMemory),
                UnityTargetPath = "targets/v2022.json",  // Optional: Unity version offsets
                Verbose = true
            };

            try
            {
                Console.WriteLine($"Dumping types from {options.ProcessName}...");
                var result = engine.DumpTypes(options);

                if (result.Success)
                {
                    Console.WriteLine($"✓ Success! Found {result.TypeCount} types in {result.Duration.TotalSeconds:F2}s");
                    
                    // Export to different formats
                    engine.ExportToFile(result, "output.txt", ExportFormat.Text);
                    engine.ExportToFile(result, "output.json", ExportFormat.Json);
                    
                    Console.WriteLine("✓ Exported to output.txt and output.json");
                    
                    // You can also get the JSON as a string for processing
                    var json = engine.ExportToJson(result);
                    // Process the JSON in your automation app...
                }
                else
                {
                    Console.WriteLine($"✗ Failed: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Exception: {ex.Message}");
            }
        }

        static void DmaMemoryExample()
        {
            var engine = new UnispectEngine();
            
            // Load available plugins (including DMA when available)
            var plugins = engine.LoadPlugins();
            Console.WriteLine($"Available memory plugins: {string.Join(", ", plugins.ConvertAll(p => p.Name))}");

            // Find DMA plugin (this would be your custom DMA implementation)
            var dmaPlugin = plugins.Find(p => p.Name.Contains("Dma") || p.Name.Contains("DMA"));
            
            if (dmaPlugin != null)
            {
                var options = new UnispectOptions
                {
                    ProcessName = "YourGameProcess.exe",  // Can include .exe for DMA
                    ModuleName = "Assembly-CSharp",
                    MemoryProxyType = dmaPlugin,
                    Verbose = true
                };

                try
                {
                    Console.WriteLine($"Using DMA plugin: {dmaPlugin.Name}");
                    var result = engine.DumpTypes(options);

                    if (result.Success)
                    {
                        Console.WriteLine($"✓ DMA Success! Found {result.TypeCount} types in {result.Duration.TotalSeconds:F2}s");
                        
                        // Get JSON for your automation processing
                        var json = engine.ExportToJson(result);
                        Console.WriteLine($"✓ JSON output ready ({json.Length} characters)");
                    }
                    else
                    {
                        Console.WriteLine($"✗ DMA Failed: {result.ErrorMessage}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"✗ DMA Exception: {ex.Message}");
                }
            }
            else
            {
                Console.WriteLine("No DMA plugin found - using BasicMemory as fallback");
                // Fall back to BasicMemory
                BasicMemoryExample();
            }
        }

        static async System.Threading.Tasks.Task AsyncExample()
        {
            var engine = new UnispectEngine();
            
            var options = new UnispectOptions
            {
                ProcessName = "YourGameProcess",
                ModuleName = "Assembly-CSharp",
                MemoryProxyType = typeof(Unispect.BasicMemory),
                Verbose = true
            };

            try
            {
                Console.WriteLine("Starting async dump with progress...");
                
                var progress = new Progress<float>(percentage =>
                {
                    Console.Write($"\rProgress: {percentage:P1}");
                });

                var result = await engine.DumpTypesAsync(options, progress);
                Console.WriteLine(); // New line after progress

                if (result.Success)
                {
                    Console.WriteLine($"✓ Async Success! Found {result.TypeCount} types");
                }
                else
                {
                    Console.WriteLine($"✗ Async Failed: {result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Async Exception: {ex.Message}");
            }
        }
    }
} 