using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Unispect.Core.Models;

namespace Unispect.Core
{
    /// <summary>
    /// Main engine for programmatic access to Unispect functionality.
    /// Provides both synchronous and asynchronous APIs for all GUI features.
    /// </summary>
    public class UnispectEngine
    {
        /// <summary>
        /// Dump types synchronously (preferred for automation scenarios)
        /// </summary>
        /// <param name="options">Configuration options</param>
        /// <returns>Result containing type definitions or error information</returns>
        public UnispectResult DumpTypes(UnispectOptions options)
        {
            try
            {
                options.Validate();
                return DumpTypesInternal(options);
            }
            catch (Exception ex)
            {
                return UnispectResult.CreateFailure($"Failed to dump types: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Dump types asynchronously with progress reporting (preferred for UI scenarios)
        /// </summary>
        /// <param name="options">Configuration options</param>
        /// <param name="progress">Optional progress reporter</param>
        /// <returns>Result containing type definitions or error information</returns>
        public async Task<UnispectResult> DumpTypesAsync(UnispectOptions options, IProgress<float> progress = null)
        {
            return await Task.Run(() =>
            {
                try
                {
                    options.Validate();
                    return DumpTypesInternal(options, progress);
                }
                catch (Exception ex)
                {
                    return UnispectResult.CreateFailure($"Failed to dump types: {ex.Message}", ex);
                }
            });
        }

        /// <summary>
        /// Export result to file in specified format
        /// </summary>
        /// <param name="result">Unispect result to export</param>
        /// <param name="filePath">Output file path</param>
        /// <param name="format">Export format</param>
        /// <param name="verbose">Include verbose information (for text format)</param>
        public void ExportToFile(UnispectResult result, string filePath, ExportFormat format, bool verbose = true)
        {
            if (!result.Success)
                throw new InvalidOperationException("Cannot export failed result");

            switch (format)
            {
                case ExportFormat.Text:
                    File.WriteAllText(filePath, ExportToText(result, verbose));
                    break;
                case ExportFormat.Json:
                    File.WriteAllText(filePath, ExportToJson(result));
                    break;
                case ExportFormat.TypeDatabase:
                    Unispect.Serializer.Save(filePath, result.TypeDefinitions);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(format), format, null);
            }
        }

        /// <summary>
        /// Export result to JSON string
        /// </summary>
        /// <param name="result">Unispect result to export</param>
        /// <returns>JSON representation</returns>
        public string ExportToJson(UnispectResult result)
        {
            if (!result.Success)
                throw new InvalidOperationException("Cannot export failed result");

            var exportData = new
            {
                result.ProcessName,
                result.ModuleName,
                result.MemoryProxyType,
                result.DumpTime,
                result.Duration,
                result.TypeCount,
                result.UnityTarget,
                TypeDefinitions = result.TypeDefinitions.Select(td => new
                {
                    td.FullName,
                    td.Name,
                    td.Namespace,
                    td.ClassType,
                    ParentName = td.Parent?.Name,
                    Interfaces = td.Interfaces?.Select(i => i.Name).ToArray(),
                    Fields = td.Fields?.Select(f => new
                    {
                        f.Name,
                        f.FieldType,
                        f.Offset,
                        f.HasValue,
                        f.ConstantValueType,
                        f.ConstantValueTypeShort
                    }).ToArray()
                }).ToArray()
            };

            return JsonConvert.SerializeObject(exportData, Formatting.Indented);
        }

        /// <summary>
        /// Export result to text format (same as GUI output)
        /// </summary>
        /// <param name="result">Unispect result to export</param>
        /// <param name="verbose">Include verbose information</param>
        /// <returns>Text representation</returns>
        public string ExportToText(UnispectResult result, bool verbose = true)
        {
            if (!result.Success)
                throw new InvalidOperationException("Cannot export failed result");

            // Use the existing Inspector logic for consistent formatting
            using (var inspector = new Unispect.Inspector())
            {
                inspector.TypeDefinitions.Clear();
                inspector.TypeDefinitions.AddRange(result.TypeDefinitions);
                
                var tempFile = Path.GetTempFileName();
                try
                {
                    inspector.DumpToFile(tempFile, verbose);
                    return File.ReadAllText(tempFile);
                }
                finally
                {
                    if (File.Exists(tempFile))
                        File.Delete(tempFile);
                }
            }
        }

        /// <summary>
        /// Load plugins from the Plugins directory
        /// </summary>
        /// <returns>List of available memory proxy types</returns>
        public List<Type> LoadPlugins()
        {
            try
            {
                // Copy the exact approach from the GUI MainWindow.LoadPlugins()
                var retList = new List<Type>();

                var pluginsDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
                if (!Directory.Exists(pluginsDir))
                    Directory.CreateDirectory(pluginsDir);

                // Add BasicMemory directly like the GUI does
                System.Console.WriteLine($"[DEBUG] Adding BasicMemory type...");
                retList.Add(typeof(Unispect.BasicMemory));
                System.Console.WriteLine($"[DEBUG] BasicMemory added successfully. Total plugins: {retList.Count}");

                // Search for plugin DLLs (copy from GUI)
                System.Console.WriteLine($"[DEBUG] Searching for plugins in: {pluginsDir}");
                var pluginFiles = Directory.GetFiles(pluginsDir, "*.dll", SearchOption.AllDirectories);
                System.Console.WriteLine($"[DEBUG] Found {pluginFiles.Length} DLL files");
                
                foreach (var fileName in pluginFiles)
                {
                    try
                    {
                        System.Console.WriteLine($"[DEBUG] Loading plugin: {fileName}");
                        var assembly = System.Reflection.Assembly.LoadFrom(fileName);

                        // Get the first type found marked with our custom attribute
                        var targetClass = assembly.GetTypes().FirstOrDefault(type =>
                            type.GetCustomAttributes(typeof(Unispect.UnispectPluginAttribute), true).Length > 0);

                        if (targetClass != null)
                        {
                            System.Console.WriteLine($"[DEBUG] Found plugin class: {targetClass.Name}");
                            retList.Add(targetClass);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Console.WriteLine($"[DEBUG] Failed to load plugin {fileName}: {ex.Message}");
                    }
                }

                System.Console.WriteLine($"[DEBUG] LoadPlugins completed. Total plugins: {retList.Count}");
                return retList;
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"[DEBUG] LoadPlugins failed: {ex.Message}");
                System.Console.WriteLine($"[DEBUG] Exception type: {ex.GetType().Name}");
                if (ex.InnerException != null)
                {
                    System.Console.WriteLine($"[DEBUG] Inner exception: {ex.InnerException.Message}");
                }
                System.Console.WriteLine($"[DEBUG] Stack trace: {ex.StackTrace}");
                throw;
            }
        }

        /// <summary>
        /// Internal implementation of type dumping
        /// </summary>
        private UnispectResult DumpTypesInternal(UnispectOptions options, IProgress<float> progress = null)
        {
            var stopwatch = Stopwatch.StartNew();
            
            try
            {
                // Load Unity target offsets if specified
                if (!string.IsNullOrWhiteSpace(options.UnityTargetPath) && File.Exists(options.UnityTargetPath))
                {
                    Unispect.Offsets.Load(options.UnityTargetPath);
                }

                // Clean process name for BasicMemory
                var processName = options.ProcessName;
                if (options.MemoryProxyType == typeof(Unispect.BasicMemory) && processName.ToLower().EndsWith(".exe"))
                {
                    processName = processName.Substring(0, processName.Length - 4);
                }

                // Create and configure inspector
                using (var inspector = new Unispect.Inspector())
                {
                    if (progress != null)
                    {
                        inspector.ProgressChanged += (sender, e) => progress.Report(e);
                    }

                    // Perform the dump (passing null to avoid file output)
                    inspector.DumpTypes(
                        fileName: null,
                        memoryProxyType: options.MemoryProxyType,
                        verbose: options.Verbose,
                        processHandle: processName,
                        moduleToDump: options.ModuleName
                    );

                    stopwatch.Stop();

                    // Create successful result
                    var result = UnispectResult.CreateSuccess(
                        inspector.TypeDefinitions,
                        options.ProcessName,
                        options.ModuleName,
                        options.MemoryProxyType.Name,
                        stopwatch.Elapsed
                    );

                    result.UnityTarget = options.UnityTargetPath;
                    return result;
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return UnispectResult.CreateFailure($"Type dumping failed: {ex.Message}", ex);
            }
        }
    }
} 