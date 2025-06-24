using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Unispect.Core;
using Unispect.Core.Models;

namespace Unispect.CLI
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            try
            {
                var options = ParseArguments(args);
                if (options == null)
                {
                    ShowHelp();
                    return 1;
                }

                if (options.ShowHelp)
                {
                    ShowHelp();
                    return 0;
                }

                if (options.ListPlugins)
                {
                    ListPlugins();
                    return 0;
                }

                return await RunDump(options);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Fatal error: {ex.Message}");
                return 1;
            }
        }

        static async Task<int> RunDump(CliOptions options)
        {
            UnispectEngine engine;
            try
            {
                engine = new UnispectEngine();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Failed to initialize Unispect engine: {ex.Message}");
                Console.Error.WriteLine($"Exception type: {ex.GetType().Name}");
                
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    Console.Error.WriteLine($"Inner exception type: {ex.InnerException.GetType().Name}");
                }
                
                Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.Error.WriteLine("This might be due to missing dependencies or static initialization issues.");
                return 1;
            }
            
            // Load plugins and find the requested memory proxy
            var plugins = engine.LoadPlugins();
            var memoryProxyType = plugins.FirstOrDefault(p => 
                p.Name.Equals(options.MemoryProxy, StringComparison.OrdinalIgnoreCase));
            
            if (memoryProxyType == null)
            {
                Console.Error.WriteLine($"Memory proxy '{options.MemoryProxy}' not found.");
                Console.Error.WriteLine("Available plugins:");
                foreach (var plugin in plugins)
                    Console.Error.WriteLine($"  - {plugin.Name}");
                return 1;
            }

            var unispectOptions = new UnispectOptions
            {
                ProcessName = options.ProcessName,
                ModuleName = options.ModuleName,
                MemoryProxyType = memoryProxyType,
                UnityTargetPath = options.UnityTarget,
                Verbose = options.Verbose
            };

            Console.WriteLine($"Unispect CLI - Dumping types from '{options.ProcessName}'");
            Console.WriteLine($"Module: {options.ModuleName}");
            Console.WriteLine($"Memory Proxy: {memoryProxyType.Name}");
            if (!string.IsNullOrEmpty(options.UnityTarget))
                Console.WriteLine($"Unity Target: {options.UnityTarget}");
            Console.WriteLine();

            UnispectResult result;
            
            if (options.ShowProgress)
            {
                var progress = new Progress<float>(percentage =>
                {
                    Console.Write($"\rProgress: {percentage:P1}");
                });
                
                result = await engine.DumpTypesAsync(unispectOptions, progress);
                Console.WriteLine(); // New line after progress
            }
            else
            {
                result = engine.DumpTypes(unispectOptions);
            }

            if (!result.Success)
            {
                Console.Error.WriteLine($"Dump failed: {result.ErrorMessage}");
                if (options.Verbose && result.Exception != null)
                {
                    Console.Error.WriteLine($"Exception: {result.Exception}");
                }
                return 1;
            }

            Console.WriteLine($"✓ Success! Found {result.TypeCount} types in {result.Duration.TotalSeconds:F2}s");

            // Output results
            if (!string.IsNullOrEmpty(options.OutputFile))
            {
                try
                {
                    engine.ExportToFile(result, options.OutputFile, options.Format, options.Verbose);
                    Console.WriteLine($"✓ Results saved to: {options.OutputFile}");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"Failed to save output: {ex.Message}");
                    return 1;
                }
            }

            if (options.OutputToConsole)
            {
                Console.WriteLine();
                Console.WriteLine("=== RESULTS ===");
                
                switch (options.Format)
                {
                    case ExportFormat.Json:
                        Console.WriteLine(engine.ExportToJson(result));
                        break;
                    case ExportFormat.Text:
                        Console.WriteLine(engine.ExportToText(result, options.Verbose));
                        break;
                    default:
                        Console.WriteLine("TypeDatabase format cannot be output to console.");
                        break;
                }
            }

            return 0;
        }

        static void ListPlugins()
        {
            try
            {
                Console.WriteLine("[DEBUG] Creating UnispectEngine...");
                var engine = new UnispectEngine();
                Console.WriteLine("[DEBUG] UnispectEngine created successfully");
                
                Console.WriteLine("[DEBUG] Calling LoadPlugins...");
                var plugins = engine.LoadPlugins();
                Console.WriteLine("[DEBUG] LoadPlugins returned successfully");
                
                Console.WriteLine("Available Memory Proxy Plugins:");
                foreach (var plugin in plugins)
                {
                    Console.WriteLine($"  - {plugin.Name}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Error loading plugins: {ex.Message}");
                Console.Error.WriteLine($"Exception type: {ex.GetType().Name}");
                
                if (ex.InnerException != null)
                {
                    Console.Error.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    Console.Error.WriteLine($"Inner exception type: {ex.InnerException.GetType().Name}");
                }
                
                Console.Error.WriteLine($"Stack trace: {ex.StackTrace}");
                Console.Error.WriteLine("This might be due to missing dependencies or initialization issues.");
                Console.Error.WriteLine("Try running from the application directory or with a Unity process running.");
            }
        }

        static void ShowHelp()
        {
            Console.WriteLine("Unispect CLI - Unity Memory Inspector Command Line Tool");
            Console.WriteLine();
            Console.WriteLine("USAGE:");
            Console.WriteLine("  unispect-cli [OPTIONS]");
            Console.WriteLine();
            Console.WriteLine("REQUIRED OPTIONS:");
            Console.WriteLine("  -p, --process <name>     Target process name (with or without .exe)");
            Console.WriteLine("  -m, --module <name>      Module to dump (default: Assembly-CSharp)");
            Console.WriteLine();
            Console.WriteLine("OUTPUT OPTIONS:");
            Console.WriteLine("  -o, --output <file>      Output file path");
            Console.WriteLine("  -f, --format <format>    Output format: text, json, typedb (default: text)");
            Console.WriteLine("  -c, --console            Also output results to console");
            Console.WriteLine();
            Console.WriteLine("MEMORY OPTIONS:");
            Console.WriteLine("  --memory <proxy>         Memory proxy: BasicMemory, or plugin name (default: BasicMemory)");
            Console.WriteLine("  --unity-target <file>    Unity target offsets JSON file");
            Console.WriteLine();
            Console.WriteLine("DISPLAY OPTIONS:");
            Console.WriteLine("  -v, --verbose            Verbose output (includes field details)");
            Console.WriteLine("  --progress               Show progress during dump");
            Console.WriteLine();
            Console.WriteLine("UTILITY OPTIONS:");
            Console.WriteLine("  --list-plugins           List available memory proxy plugins");
            Console.WriteLine("  -h, --help               Show this help");
            Console.WriteLine();
            Console.WriteLine("EXAMPLES:");
            Console.WriteLine("  unispect-cli -p \"YourGame\" -o offsets.txt");
            Console.WriteLine("  unispect-cli -p \"YourGame\" -f json -o offsets.json -c");
            Console.WriteLine("  unispect-cli -p \"YourGame\" --memory DmaMemory --unity-target targets/v2022.json");
            Console.WriteLine("  unispect-cli --list-plugins");
        }

        static CliOptions ParseArguments(string[] args)
        {
            var options = new CliOptions();
            
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i].ToLower())
                {
                    case "-p":
                    case "--process":
                        if (i + 1 >= args.Length) return null;
                        options.ProcessName = args[++i];
                        break;
                        
                    case "-m":
                    case "--module":
                        if (i + 1 >= args.Length) return null;
                        options.ModuleName = args[++i];
                        break;
                        
                    case "-o":
                    case "--output":
                        if (i + 1 >= args.Length) return null;
                        options.OutputFile = args[++i];
                        break;
                        
                    case "-f":
                    case "--format":
                        if (i + 1 >= args.Length) return null;
                        if (!Enum.TryParse<ExportFormat>(args[++i], true, out var format))
                            return null;
                        options.Format = format;
                        break;
                        
                    case "-c":
                    case "--console":
                        options.OutputToConsole = true;
                        break;
                        
                    case "--memory":
                        if (i + 1 >= args.Length) return null;
                        options.MemoryProxy = args[++i];
                        break;
                        
                    case "--unity-target":
                        if (i + 1 >= args.Length) return null;
                        options.UnityTarget = args[++i];
                        break;
                        
                    case "-v":
                    case "--verbose":
                        options.Verbose = true;
                        break;
                        
                    case "--progress":
                        options.ShowProgress = true;
                        break;
                        
                    case "--list-plugins":
                        options.ListPlugins = true;
                        break;
                        
                    case "-h":
                    case "--help":
                        options.ShowHelp = true;
                        break;
                        
                    default:
                        Console.Error.WriteLine($"Unknown option: {args[i]}");
                        return null;
                }
            }

            // Validate required options
            if (!options.ShowHelp && !options.ListPlugins)
            {
                if (string.IsNullOrEmpty(options.ProcessName))
                {
                    Console.Error.WriteLine("Process name is required. Use -p or --process.");
                    return null;
                }
                
                if (string.IsNullOrEmpty(options.OutputFile) && !options.OutputToConsole)
                {
                    Console.Error.WriteLine("Either output file (-o) or console output (-c) is required.");
                    return null;
                }
            }

            return options;
        }
    }

    class CliOptions
    {
        public string ProcessName { get; set; }
        public string ModuleName { get; set; } = "Assembly-CSharp";
        public string OutputFile { get; set; }
        public ExportFormat Format { get; set; } = ExportFormat.Text;
        public bool OutputToConsole { get; set; }
        public string MemoryProxy { get; set; } = "BasicMemory";
        public string UnityTarget { get; set; }
        public bool Verbose { get; set; }
        public bool ShowProgress { get; set; }
        public bool ListPlugins { get; set; }
        public bool ShowHelp { get; set; }
    }
} 