using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Newtonsoft.Json;
using Unispect.SDK.Models;

namespace Unispect.SDK
{
    // Todo add support for il2cpp ?
    [Serializable]
    public sealed class Inspector : Progress<float>, IDisposable
    {
        private MemoryProxy _memory;
        private float _progressTotal;
        private float ProgressTotal
        {
            get => _progressTotal;
            set
            {
                _progressTotal = value;
                OnReport(_progressTotal);
            }
        }

        private ConcurrentDictionary<ulong, TypeDefinition> _typeDefinitions
            = new ConcurrentDictionary<ulong, TypeDefinition>();

        public List<TypeDefWrapper> TypeDefinitions { get; private set; } = new List<TypeDefWrapper>();

        /// <summary>
        /// Number of Mono classes found before expansion (raw count shown during dump)
        /// </summary>
        public int RawClassCount { get; private set; }

        // If you add any progress lengths, you should increase this.
        // Every progress task represents 1 length
        private const int TotalProgressLength = 9;

        protected override void OnReport(float value)
        {
            value /= TotalProgressLength;
            base.OnReport(value);
        }

        public void DumpTypes(string fileName, Type memoryProxyType,
            bool verbose = true,
            string processHandle = "SomeGame",
            //string monoModuleName = "mono-2.0-bdwgc.dll",
            string moduleToDump = "Assembly-CSharp")
        {
            Log.Add($"Initializing memory proxy of type '{memoryProxyType.Name}'");
            using (_memory = (MemoryProxy)Activator.CreateInstance(memoryProxyType))
            {
                ProgressTotal += 0.16f;

                Log.Add($"Attaching to process '{processHandle}'");
                var success = _memory.AttachToProcess(processHandle);

                if (!success)
                    throw new Exception("Could not attach to the remote process.");

                ProgressTotal += 0.16f;

                //Log.Add($"Obtaining {monoModuleName} module details");
                var monoModule = GetMonoModule(out var monoModuleName);
                if (monoModule == null)
                {
                    throw new NotSupportedException();
                }

                Log.Add($"Module {monoModule.Name} loaded. " +
                        $"(BaseAddress: 0x{monoModule.BaseAddress:X16})");

                ProgressTotal += 0.16f;

                Log.Add($"Copying {monoModuleName} module to local memory {(monoModule.Size / (float)0x100000):###,###.00}MB");
                var monoDump = _memory.Read(monoModule.BaseAddress, monoModule.Size);

                ProgressTotal += 0.16f;

                Log.Add($"Traversing PE of {monoModuleName}");
                var rdfa = GetRootDomainFunctionAddress(monoDump, monoModule);

                ProgressTotal += 0.16f;

                Log.Add($"Getting MonoImage address for {moduleToDump}");
                var monoImage = GetAssemblyImageAddress(rdfa, moduleToDump); // _MonoImage of moduleToDump (Assembly-CSharp)

                ProgressTotal += 0.16f;

                // Retrieve type definitions (raw Mono classes) and record raw count
                _typeDefinitions = GetRemoteTypeDefinitions(monoImage);

                Log.Add("Propogating types and fields");
                PropogateTypes();

                // If this is true, then the user does not want to save to file
                if (!string.IsNullOrWhiteSpace(fileName))
                {
                    DumpToFile(fileName, verbose, false);
                    ProgressTotal += 0.15f;
                    SaveTypeDefDb(processHandle, moduleToDump);
                }

                OnReport(TotalProgressLength); // Set to 100%

                Log.Add("Operation completed successfully.");
            }
        }

        public void SaveTypeDefDb(string processHandle, string moduleToDump)
        {
            Log.Add("Saving Type Definition database");
            //Log.Add("Compressing Type Definition database");

            if (!System.IO.Directory.Exists("TypeDbs"))
                System.IO.Directory.CreateDirectory("TypeDbs");

            // Todo if we plan on storing multiple, perhaps make it a cyclic storage system.
            //var fileName = $"{processHandle} {moduleToDump} ({DateTime.Now.ToFileTime():X8}).gz";
            //var fileName = $"{processHandle} {moduleToDump}.gz";
            var fileName = $"{processHandle} {moduleToDump}.utd";
            //Serializer.SaveCompressed($"TypeDbs\\{fileName.SanitizeFileName().ToLower()}", TypeDefinitions);
            Serializer.Save($"TypeDbs\\{fileName.SanitizeFileName().ToLower()}", TypeDefinitions);
        }

        private ModuleProxy GetMonoModule(out string moduleName)
        {
            //Log.Add("Looking for the mono module (mono, mono-2.0-bdwgc)");
            Log.Add("Looking for the mono module (mono-2.0-bdwgc)");
            var module = _memory.GetModule("mono-2.0-bdwgc.dll");
            if (module != null)
            {
                moduleName = "Found mono-2.0-bdwgc.dll";
                return module;
            }

            // Currently unsupported.
            // todo: return to this when dynamic structures are implemented and consider adding support
            //module = _memory.GetModule("mono.dll");
            //if (module != null)
            //{
            //    moduleName = "mono.dll";
            //    return module;
            //}

            moduleName = "";
            return null;
        }

        private void PropogateTypes()
        {
            var typeDefWrappers = new List<TypeDefWrapper>();
            var progressIncrement = 1f / _typeDefinitions.Count * 3f;
            foreach (var t in _typeDefinitions.AsParallel())
            {
                ProgressTotal += progressIncrement;

                var typeDef = t.Value;
                typeDefWrappers.Add(new TypeDefWrapper(typeDef));
            }

            Log.Add("Sorting type definitions by path");

            TypeDefinitions = new List<TypeDefWrapper>(typeDefWrappers.OrderBy(wrapper => wrapper.FullName));
        }

        public void DumpToFile(string fileName, bool verbose = true, List<TypeDefWrapper> tdlToDump = null)
        {
            DumpToFile(fileName, verbose, true, tdlToDump);
        }

        private void DumpToFile(string fileName, bool verbose, bool adjustProgressIncr, List<TypeDefWrapper> tdlToDump = null)
        {
            // ****************** Formatting below
            Log.Add("Formatting dump");
            var sb = new StringBuilder();

            if (tdlToDump == null)
                tdlToDump = TypeDefinitions;

            var progressIncrement = 1f / tdlToDump.Count * (adjustProgressIncr
                                        ? TotalProgressLength
                                        : 2);

            sb.AppendLine("    [<index>] <name> : <type>");
            sb.AppendLine();
            sb.AppendLine($"Generated by Unispect v{Utilities.CurrentVersion} - by kodek4 {Utilities.GithubLink}");
            sb.AppendLine();

            foreach (var typeDef in tdlToDump)
            {
                // Progress 1 
                ProgressTotal += progressIncrement;

                if (verbose)
                    sb.Append($"[{typeDef.ClassType}] ");
                sb.Append(typeDef.FullName);
                if (verbose)
                {
                    //sb.AppendLine($" [{typeDef.GetClassType()}]");
                    var parent = typeDef.Parent;
                    if (parent != null)
                    {
                        sb.Append($" : {parent.Name}");
                        var interfaceList = typeDef.Interfaces;
                        if (interfaceList.Count > 0)
                        {
                            foreach (var iface in interfaceList)
                            {
                                sb.Append($", {iface.Name}");
                            }
                        }
                    }
                }

                sb.AppendLine();

                var fields = typeDef.Fields;
                if (fields == null)
                    continue;

                foreach (var field in fields)
                {
                    if (field.Offset > 0x2000)
                        continue;

                    var fieldName = field.Name;
                    var fieldType = field.FieldType;
                    sb.AppendLine(field.HasValue
                        ? $"    [{field.Offset:X2}][{field.ConstantValueTypeShort}] {fieldName} : {fieldType}"
                        : $"    [{field.Offset:X2}] {fieldName} : {fieldType}");
                }
            }

            System.IO.File.WriteAllText(fileName, sb.ToString());

            Log.Add($"Your definitions and offsets dump was saved to: {fileName}");
        }

        private ConcurrentDictionary<ulong, TypeDefinition> GetRemoteTypeDefinitions(ulong monoImageAddress)
        {
            var classCache = _memory.Read<InternalHashTable>(monoImageAddress + (uint)Offsets.ImageClassCache);
            var typeDefs = new Dictionary<ulong, TypeDefinition>();

            Log.Add($"Processing {classCache.Size} classes. This may take some time.");

            // Multiplying this by two will make it use two progress lengths.
            // Since it does a lot of the hard work, I think it fits nicely.
            var progressIncrement = 1f / classCache.Size * 2;

            // Store raw (unexpanded) Mono class count
            RawClassCount = (int)classCache.Size;

            for (var i = 0u; i < classCache.Size; i++)
            {
                // Progress 0
                ProgressTotal += progressIncrement;

                for (var d = _memory.Read<ulong>(classCache.Table + i * 8);
                    d != 0;
                    d = _memory.Read<ulong>(d + (uint)Offsets.ClassNextClassCache))
                {
                    var typeDef = _memory.Read<TypeDefinition>(d);
                    typeDefs.Add(d, typeDef);
                }
            }

            return new ConcurrentDictionary<ulong, TypeDefinition>(typeDefs);
        }

        private ulong GetAssemblyImageAddress(ulong rootDomainFunctionAddress, string name = "Assembly-CSharp")
        {
            var relativeOffset = _memory.Read<uint>(rootDomainFunctionAddress + 3);      // mov rax, 0x004671B9
            var domainAddress = relativeOffset + rootDomainFunctionAddress + 7;     // rdfa + 0x4671C0 // RootDomain (Unity Root Domain)

            var domain = _memory.Read<ulong>(domainAddress);

            var assemblyArrayAddress = _memory.Read<ulong>(domain + (uint)Offsets.DomainDomainAssemblies);
            for (var assemblyAddress = assemblyArrayAddress;
                assemblyAddress != 0;
                assemblyAddress = _memory.Read<ulong>(assemblyAddress + 0x8))
            {
                var assembly = _memory.Read<ulong>(assemblyAddress);
                var assemblyNameAddress = _memory.Read<ulong>(assembly + 0x10);
                var assemblyName = _memory.Read(assemblyNameAddress, 1024).ToAsciiString();
                if (assemblyName != name)
                    continue;

                return _memory.Read<ulong>(assembly + (uint)Offsets.AssemblyImage);
            }

            throw new InvalidOperationException($"Unable to find assembly '{name}'");
        }

        private static ulong GetRootDomainFunctionAddress(byte[] moduleDump, ModuleProxy monoModuleInfo)
        {
            // Traverse the PE header to get mono_get_root_domain
            var startIndex = moduleDump.ToInt32(Offsets.ImageDosHeaderELfanew);

            var exportDirectoryIndex = startIndex + Offsets.ImageNtHeadersExportDirectoryAddress;
            var exportDirectory = moduleDump.ToInt32(exportDirectoryIndex);

            var numberOfFunctions = moduleDump.ToInt32(exportDirectory + Offsets.ImageExportDirectoryNumberOfFunctions);
            var functionAddressArrayIndex = moduleDump.ToInt32(exportDirectory + Offsets.ImageExportDirectoryAddressOfFunctions);
            var functionNameArrayIndex = moduleDump.ToInt32(exportDirectory + Offsets.ImageExportDirectoryAddressOfNames);

            Log.Add($"e_lfanew: 0x{startIndex:X4}, Export Directory Entry: 0x{exportDirectory:X4}");
            Log.Add("Searching exports for 'mono_get_root_domain'");
            var rootDomainFunctionAddress = 0ul;

            Parallel.ForEach(Utilities.Step(0, numberOfFunctions * 4, 4), (functionIndex, state) =>
            {
                var functionNameIndex = moduleDump.ToInt32(functionNameArrayIndex + functionIndex);
                var functionName = moduleDump.ToAsciiString(functionNameIndex);

                if (functionName != "mono_get_root_domain")
                    return;

                //var realIndex = functionIndex / 4;
                var rva = moduleDump.ToInt32(functionAddressArrayIndex + functionIndex);
                rootDomainFunctionAddress = monoModuleInfo.BaseAddress + (ulong)rva;

                state.Stop();
            }
            );

            if (rootDomainFunctionAddress == 0)
            {
                throw new InvalidOperationException("Failed to find mono_get_root_domain function.");
            }
            Log.Add($"Function 'mono_get_root_domain' found. (Address: {rootDomainFunctionAddress:X16})");
            return rootDomainFunctionAddress;
        }

        #region Cache Management APIs

        /// <summary>
        /// Gets the standard cache file path for a process and module
        /// </summary>
        public static string GetCacheFilePath(string processName, string moduleName = "Assembly-CSharp")
        {
            // Normalize process name: strip .exe, lowercase, sanitize
            if (processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                processName = processName.Substring(0, processName.Length - 4);
            processName = processName.ToLowerInvariant().SanitizeFileName();
            moduleName = moduleName.ToLowerInvariant().SanitizeFileName();

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var cacheDir = Path.Combine(localAppData, "Unispect", "Cache");
            if (!Directory.Exists(cacheDir))
                Directory.CreateDirectory(cacheDir);
            var fileName = $"{processName}_{moduleName}.utd";
            return Path.Combine(cacheDir, fileName);
        }

        /// <summary>
        /// Checks if a cache file exists for the specified process and module
        /// </summary>
        public static bool IsCacheAvailable(string processName, string moduleName = "Assembly-CSharp")
        {
            var cachePath = GetCacheFilePath(processName, moduleName);
            return File.Exists(cachePath);
        }

        /// <summary>
        /// Gets the age of the cache file in hours, or -1 if not found
        /// </summary>
        public static double GetCacheAge(string processName, string moduleName = "Assembly-CSharp")
        {
            var cachePath = GetCacheFilePath(processName, moduleName);
            if (!File.Exists(cachePath))
                return -1;
            var fileInfo = new FileInfo(cachePath);
            return (DateTime.Now - fileInfo.LastWriteTime).TotalHours;
        }

        /// <summary>
        /// Loads type definitions from cache file
        /// </summary>
        public bool LoadFromCache(string processName, string moduleName = "Assembly-CSharp")
        {
            var cachePath = GetCacheFilePath(processName, moduleName);
            if (!File.Exists(cachePath))
            {
                Log.Add($"Cache file not found: {cachePath}");
                return false;
            }
            try
            {
                Log.Add($"Loading type definitions from cache: {cachePath}");

                var rawJson = File.ReadAllText(cachePath, Encoding.UTF8);

                // Detect whether the root JSON token is an array (legacy) or an object (new format)
                var firstNonWhitespace = rawJson.SkipWhile(char.IsWhiteSpace).FirstOrDefault();
                if (firstNonWhitespace == '[')
                {
                    // Legacy format – just a list
                    TypeDefinitions = JsonConvert.DeserializeObject<List<TypeDefWrapper>>(rawJson) ?? new List<TypeDefWrapper>();
                    RawClassCount = TypeDefinitions.Count; // best guess
                }
                else
                {
                    var cacheFile = JsonConvert.DeserializeObject<CacheFile>(rawJson) ?? new CacheFile();
                    TypeDefinitions = cacheFile.TypeDefinitions ?? new List<TypeDefWrapper>();
                    RawClassCount = cacheFile.RawClassCount > 0 ? cacheFile.RawClassCount : TypeDefinitions.Count;
                }

                Log.Add($"Loaded {TypeDefinitions.Count} type definitions from cache");
                return true;
            }
            catch (Exception ex)
            {
                Log.Add($"Failed to load cache: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Saves current type definitions to cache
        /// </summary>
        public void SaveToCache(string processName, string moduleName = "Assembly-CSharp", string customPath = null)
        {
            var cachePath = customPath ?? GetCacheFilePath(processName, moduleName);
            try
            {
                Log.Add($"Saving type definitions to cache: {cachePath}");
                var cacheObj = new CacheFile { RawClassCount = RawClassCount, TypeDefinitions = TypeDefinitions };
                Serializer.Save(cachePath, cacheObj);
                Log.Add($"Saved {TypeDefinitions.Count} type definitions to cache");
            }
            catch (Exception ex)
            {
                Log.Add($"Failed to save cache: {ex.Message}");
            }
        }

        /// <summary>
        /// Lists all available cache files
        /// </summary>
        public static List<CacheInfo> ListCacheFiles()
        {
            var cacheFiles = new List<CacheInfo>();
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var cacheDir = Path.Combine(localAppData, "Unispect", "Cache");
            if (!Directory.Exists(cacheDir))
                return cacheFiles;
            var files = Directory.GetFiles(cacheDir, "*.utd");
            foreach (var file in files)
            {
                var fileInfo = new FileInfo(file);
                var fileName = Path.GetFileNameWithoutExtension(fileInfo.Name);
                // Try to parse process and module from filename
                var parts = fileName.Split('_');
                var processName = parts.Length > 0 ? parts[0] : "Unknown";
                var moduleName = parts.Length > 1 ? string.Join("_", parts.Skip(1)) : "Unknown";
                cacheFiles.Add(new CacheInfo
                {
                    ProcessName = processName,
                    ModuleName = moduleName,
                    FilePath = file,
                    Size = fileInfo.Length,
                    LastModified = fileInfo.LastWriteTime,
                    Age = (DateTime.Now - fileInfo.LastWriteTime).TotalHours
                });
            }
            return cacheFiles.OrderByDescending(c => c.LastModified).ToList();
        }

        /// <summary>
        /// Deletes cache file for specific process
        /// </summary>
        public static bool DeleteCache(string processName, string moduleName = "Assembly-CSharp")
        {
            var cachePath = GetCacheFilePath(processName, moduleName);
            if (!File.Exists(cachePath))
                return false;
            try
            {
                File.Delete(cachePath);
                Log.Add($"Deleted cache file: {cachePath}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Add($"Failed to delete cache: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deletes all cache files
        /// </summary>
        public static int DeleteAllCache()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var cacheDir = Path.Combine(localAppData, "Unispect", "Cache");
            if (!Directory.Exists(cacheDir))
                return 0;
            var files = Directory.GetFiles(cacheDir, "*.utd");
            var deletedCount = 0;
            foreach (var file in files)
            {
                try
                {
                    File.Delete(file);
                    deletedCount++;
                }
                catch (Exception ex)
                {
                    Log.Add($"Failed to delete cache file {file}: {ex.Message}");
                }
            }
            Log.Add($"Deleted {deletedCount} cache files");
            return deletedCount;
        }

        /// <summary>
        /// Gets the total size of all cache files in bytes
        /// </summary>
        public static long GetTotalCacheSize()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var cacheDir = Path.Combine(localAppData, "Unispect", "Cache");
            if (!Directory.Exists(cacheDir))
                return 0;
            var files = Directory.GetFiles(cacheDir, "*.utd");
            return files.Sum(file => new FileInfo(file).Length);
        }

        /// <summary>
        /// Gets the cache directory path
        /// </summary>
        public static string GetCacheDirectory()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            return Path.Combine(localAppData, "Unispect", "Cache");
        }

        #endregion

        #region Search and Query APIs

        /// <summary>
        /// Searches for types matching the specified pattern
        /// </summary>
        /// <param name="pattern">Search pattern (supports * and ? wildcards, or regex if useRegex is true)</param>
        /// <param name="useRegex">Whether to treat pattern as regex</param>
        public List<TypeDefWrapper> SearchTypes(string pattern, bool useRegex = false)
        {
            if (TypeDefinitions == null || TypeDefinitions.Count == 0)
            {
                Log.Add("No type definitions loaded. Run DumpTypes or LoadFromCache first.");
                return new List<TypeDefWrapper>();
            }

            var results = new List<TypeDefWrapper>();
            
            if (useRegex)
            {
                try
                {
                    var regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                    results = TypeDefinitions.Where(t => regex.IsMatch(t.FullName)).ToList();
                }
                catch (Exception ex)
                {
                    Log.Add($"Invalid regex pattern: {ex.Message}");
                    return new List<TypeDefWrapper>();
                }
            }
            else
            {
                // Convert wildcard pattern to regex
                var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                    .Replace(@"\*", ".*")
                    .Replace(@"\?", ".") + "$";
                
                var regex = new System.Text.RegularExpressions.Regex(regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                results = TypeDefinitions.Where(t => regex.IsMatch(t.FullName)).ToList();
            }

            Log.Add($"Found {results.Count} types matching '{pattern}'");
            return results;
        }

        /// <summary>
        /// Searches for fields matching the specified pattern across all types
        /// </summary>
        public List<FieldSearchResult> SearchFields(string pattern, bool useRegex = false)
        {
            if (TypeDefinitions == null || TypeDefinitions.Count == 0)
            {
                Log.Add("No type definitions loaded. Run DumpTypes or LoadFromCache first.");
                return new List<FieldSearchResult>();
            }

            var results = new List<FieldSearchResult>();
            System.Text.RegularExpressions.Regex regex;

            try
            {
                if (useRegex)
                {
                    regex = new System.Text.RegularExpressions.Regex(pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
                else
                {
                    var regexPattern = "^" + System.Text.RegularExpressions.Regex.Escape(pattern)
                        .Replace(@"\*", ".*")
                        .Replace(@"\?", ".") + "$";
                    regex = new System.Text.RegularExpressions.Regex(regexPattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Invalid pattern: {ex.Message}");
                return new List<FieldSearchResult>();
            }

            foreach (var type in TypeDefinitions)
            {
                if (type.Fields == null) continue;

                foreach (var field in type.Fields)
                {
                    if (regex.IsMatch(field.Name))
                    {
                        results.Add(new FieldSearchResult
                        {
                            TypeName = type.FullName,
                            Field = field
                        });
                    }
                }
            }

            Log.Add($"Found {results.Count} fields matching '{pattern}'");
            return results;
        }

        /// <summary>
        /// Searches for fields within a specific offset range
        /// </summary>
        public List<FieldSearchResult> SearchFieldsByOffset(uint minOffset, uint maxOffset)
        {
            if (TypeDefinitions == null || TypeDefinitions.Count == 0)
            {
                Log.Add("No type definitions loaded. Run DumpTypes or LoadFromCache first.");
                return new List<FieldSearchResult>();
            }

            var results = new List<FieldSearchResult>();

            foreach (var type in TypeDefinitions)
            {
                if (type.Fields == null) continue;

                foreach (var field in type.Fields)
                {
                    if (field.Offset >= minOffset && field.Offset <= maxOffset)
                    {
                        results.Add(new FieldSearchResult
                        {
                            TypeName = type.FullName,
                            Field = field
                        });
                    }
                }
            }

            Log.Add($"Found {results.Count} fields in offset range 0x{minOffset:X}-0x{maxOffset:X}");
            return results.OrderBy(r => r.Field.Offset).ToList();
        }

        /// <summary>
        /// Searches for anything (types, fields, etc.) matching the pattern
        /// </summary>
        public SearchResults SearchAll(string pattern, bool useRegex = false)
        {
            var results = new SearchResults
            {
                Types = SearchTypes(pattern, useRegex),
                Fields = SearchFields(pattern, useRegex)
            };

            Log.Add($"Search '{pattern}' found {results.Types.Count} types and {results.Fields.Count} fields");
            return results;
        }

        /// <summary>
        /// Gets a specific type by exact name
        /// </summary>
        public TypeDefWrapper GetType(string typeName)
        {
            if (TypeDefinitions == null || TypeDefinitions.Count == 0)
            {
                Log.Add("No type definitions loaded. Run DumpTypes or LoadFromCache first.");
                return null;
            }

            var result = TypeDefinitions.FirstOrDefault(t => 
                string.Equals(t.FullName, typeName, StringComparison.OrdinalIgnoreCase));

            if (result != null)
                Log.Add($"Found type: {result.FullName}");
            else
                Log.Add($"Type not found: {typeName}");

            return result;
        }

        /// <summary>
        /// Gets a specific field from a type
        /// </summary>
        public FieldDefWrapper GetField(string typeName, string fieldName)
        {
            var type = GetType(typeName);
            if (type?.Fields == null)
                return null;

            var field = type.Fields.FirstOrDefault(f => 
                string.Equals(f.Name, fieldName, StringComparison.OrdinalIgnoreCase));

            if (field != null)
                Log.Add($"Found field: {typeName}.{field.Name} at offset 0x{field.Offset:X}");
            else
                Log.Add($"Field not found: {typeName}.{fieldName}");

            return field;
        }

        /// <summary>
        /// Gets types by their kind (Class, Interface, Enum, etc.)
        /// </summary>
        public List<TypeDefWrapper> GetTypesByKind(string typeKind)
        {
            if (TypeDefinitions == null || TypeDefinitions.Count == 0)
            {
                Log.Add("No type definitions loaded. Run DumpTypes or LoadFromCache first.");
                return new List<TypeDefWrapper>();
            }

            var results = TypeDefinitions.Where(t => 
                string.Equals(t.ClassType, typeKind, StringComparison.OrdinalIgnoreCase)).ToList();
            Log.Add($"Found {results.Count} types of kind {typeKind}");
            return results;
        }

        #endregion

        public void Dispose()
        {
            _memory?.Dispose();
        }
    }
}