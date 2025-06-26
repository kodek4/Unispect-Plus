# Unispect SDK

The Unispect SDK is the core library component of the Unispect-Plus project. It provides developers with programmatic access to the type dumping, inspection, caching, and searching logic, allowing integration into custom tools, analysis platforms, or other .NET applications.

The SDK is designed with extensibility in mind, primarily through the `MemoryProxy` abstract class, which enables implementing custom methods for reading remote process memory (e.g., standard OS APIs, DMA).

## Table of Contents

*   [Obtaining the SDK](#obtaining-the-sdk)
*   [Core Concepts](#core-concepts)
*   [Integrating the SDK](#integrating-the-sdk)
    *   [Loading from Cache](#loading-from-cache)
    *   [Performing a Live Dump](#performing-a-live-dump)
    *   [Querying and Searching](#querying-and-searching)
    *   [Cache Management](#cache-management-sdk)
*   [SDK API Reference](#sdk-api-reference)
*   [Plugin Development](#plugin-development)

## Obtaining the SDK

The Unispect SDK is produced as a standard .NET library (`Unispect.SDK.dll`) as part of the Unispect-Plus build process. Obtain the latest release from the main [Unispect-Plus GitHub repository](https://github.com/kodek4/Unispect-Plus) and locate the `Unispect-SDK.zip` artifact.

Include `Unispect.SDK.dll` and its dependencies (`Newtonsoft.Json.dll`) as references in your .NET project. Ensure your project targets a compatible framework (`net9.0-windows` or newer, x64 platform).

## Core Concepts

*   **`Inspector`**: The primary class for initiating dump operations, loading/saving caches, and performing searches/queries on loaded type definitions.
*   **`MemoryProxy`**: An abstract base class that defines the interface for reading memory from a target process. The SDK includes a default `BasicMemory` implementation using standard Windows APIs. Custom implementations (like the DMA plugin) inherit from this.
*   **`TypeDefWrapper` / `FieldDefWrapper`**: Wrapped representations of the discovered Mono type and field definitions, providing simplified access to properties like name, type, offset, parent, and fields. These are the main data structures you will work with after loading a dump.
*   **`Log`**: A static class for logging messages from the SDK and plugins. You can subscribe to its `LogMessageAdded` event to capture output.
*   **`PluginLoader`**: A static helper class to discover available `MemoryProxy` implementations (including plugins) within the application's execution environment.

## Integrating the SDK

This section provides examples of common tasks using the Unispect SDK.

Ensure you have added references to `Unispect.SDK.dll` and `Newtonsoft.Json.dll`. You will typically need to include the following namespaces:

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Unispect.SDK;
using Unispect.SDK.Models; // For UnispectOptions and other models
```

Optionally, subscribe to SDK logs for visibility into the SDK's operations:

```csharp
Log.LogMessageAdded += (sender, e) =>
{
    Console.WriteLine($"[SDK Log - {e.Type}] {e.Message}");
};
```

Operations on an `Inspector` instance should ideally be wrapped in a `try...finally` block to ensure `Dispose()` is called. Many operations, especially dumping, should be run asynchronously to avoid blocking the main thread in GUI applications.

### Loading from Cache

Loading from a previously created cache file (`.utd`) is the fastest way to access type definitions for analysis.

```csharp
string processName = "unityprocess";
string moduleName = "Assembly-CSharp";
Inspector inspector = null;

try
{
    inspector = new Inspector();
    if (Inspector.IsCacheAvailable(processName, moduleName))
    {
        Console.WriteLine($"Cache found for '{processName}'. Loading...");
        if (inspector.LoadFromCache(processName, moduleName))
        {
            Console.WriteLine($"Successfully loaded {inspector.TypeDefinitions.Count} types from cache.");
            // Now you can use inspector.TypeDefinitions for searching/querying
        }
        else
        {
            Console.WriteLine($"Failed to load cache for '{processName}'. Cache might be corrupted.");
            // Handle corrupted cache, potentially delete it
            // Inspector.DeleteCache(processName, moduleName);
        }
    }
    else
    {
        Console.WriteLine($"No cache found for '{processName}'. A dump is required.");
        // Proceed to Performing a Live Dump
    }
}
catch (Exception ex)
{
    Console.WriteLine($"An error occurred while loading cache: {ex.Message}");
}
finally
{
    inspector?.Dispose(); // Clean up the Inspector
}
```

### Performing a Live Dump

If no cache is available or you need fresh data, perform a live memory dump from the target process. This requires selecting a `MemoryProxy` implementation.

```csharp
string processName = "unityprocess";
string moduleToDump = "Assembly-CSharp";
Inspector inspector = null;

try
{
    inspector = new Inspector();

    // 1. Discover available memory plugins
    var availablePlugins = PluginLoader.DiscoverMemoryPlugins();
    // Find the desired plugin type (e.g., BasicMemory or a custom plugin)
    var memoryProxyType = availablePlugins.FirstOrDefault(p => p == typeof(BasicMemory));

    if (memoryProxyType == null)
    {
        Console.WriteLine("Desired MemoryProxy plugin not found.");
        return;
    }

    // 2. (Optional) Set up progress reporting
    var progress = new Progress<float>(p =>
    {
        // Update UI or console with progress (0.0 to 1.0)
        Console.Write($"\rDump Progress: {p:P0}");
    });

    // 3. Create dump options
    var options = new UnispectOptions
    {
        ProcessName = processName,
        ModuleName = moduleToDump,
        MemoryProxyType = memoryProxyType,
        // UnityTargetPath = "path/to/targets/vYYYY.json", // Optional: specify Unity version offsets if needed
        Verbose = true // Set to true for more detailed logs during dump
    };
    options.Validate(); // Crucial: Ensures required options are set

    Console.WriteLine($"Starting live dump for process '{processName}' using {memoryProxyType.Name}...");

    // 4. Execute the dump operation (typically on a background thread/task)
    // DumpTypes automatically saves to the standard cache location on successful completion
    // if the fileName argument is empty or null.
    await Task.Run(() => inspector.DumpTypes("", options.MemoryProxyType, options.Verbose, options.ProcessName, options.ModuleName));

    Console.WriteLine("\nDump completed.");
    Console.WriteLine($"Discovered {inspector.TypeDefinitions.Count} types.");

    // Now you can use inspector.TypeDefinitions for searching/querying or it's saved to cache
}
catch (Exception ex)
{
    Console.WriteLine($"An error occurred during the dump operation: {ex.Message}");
    // Log.Exception("Dump failed", ex); // Use SDK Log for detailed exception info
}
finally
{
    inspector?.Dispose(); // Clean up the Inspector and its MemoryProxy
}
```

### Querying and Searching

Once type definitions are loaded into the `Inspector` instance (either from cache or a live dump), you can use its methods to find specific types or fields.

```csharp
// Assume 'inspector' is a valid Inspector instance with TypeDefinitions loaded

// 1. Get a specific type by exact name
string targetType = "Player";
Console.WriteLine($"\nQuerying type: {targetType}");
var playerType = inspector.GetType(targetType); // Case-insensitive match

if (playerType != null)
{
    Console.WriteLine($"Found Type: {playerType.FullName} ({playerType.ClassType})");
    Console.WriteLine($"  Parent: {playerType.ParentName}");
    Console.WriteLine($"  Fields ({playerType.Fields.Count}):");
    foreach (var field in playerType.Fields)
    {
        Console.WriteLine($"    - 0x{field.Offset:X2} {field.ConstantValueType} : {field.FieldType} {field.Name}");
    }
     if(playerType.Fields.Count > 5) Console.WriteLine("    ...");
}
else
{
    Console.WriteLine($"Type '{targetType}' not found.");
}

// 2. Get a specific field from a type
string targetFieldName = "m_health";
Console.WriteLine($"\nQuerying field: {targetType}.{targetFieldName}");
var healthField = inspector.GetField(targetType, targetFieldName); // Case-insensitive type and field name

if (healthField != null)
{
    Console.WriteLine($"Found Field: {healthField.Name}");
    Console.WriteLine($"  Type: {healthField.FieldType}");
    Console.WriteLine($"  Offset: 0x{healthField.Offset:X}");
    Console.WriteLine($"  Is Pointer: {healthField.IsPointer}");
    Console.WriteLine($"  Is Value Type: {healthField.IsValueType}");
    Console.WriteLine($"  Has Constant Value: {healthField.HasValue}");
}
else
{
    Console.WriteLine($"Field '{targetFieldName}' not found in type '{targetType}'.");
}


// 3. Search for types matching a pattern (wildcard or regex)
string typePattern = "*Manager";
Console.WriteLine($"\nSearching for types matching: '{typePattern}'");
var managerTypes = inspector.SearchTypes(typePattern); // Wildcard search by default

if (managerTypes.Any())
{
    Console.WriteLine($"Found {managerTypes.Count} types matching '{typePattern}':");
    foreach (var type in managerTypes)
    {
        Console.WriteLine($"  - {type.FullName} ({type.Fields.Count} fields)");
    }
}


// 4. Search for fields matching a pattern (wildcard or regex)
string fieldPattern = "m_*";
Console.WriteLine($"\nSearching for fields matching: '{fieldPattern}'");
var mPrefixedFields = inspector.SearchFields(fieldPattern); // Wildcard search by default

if (mPrefixedFields.Any())
{
    Console.WriteLine($"Found {mPrefixedFields.Count} fields matching '{fieldPattern}':");
    foreach (var fieldResult in mPrefixedFields.Take(10)) // Limit results for display
    {
        Console.WriteLine($"  - {fieldResult.DisplayText}");
    }
    if (mPrefixedFields.Count > 10) Console.WriteLine("  ...");
}

// 5. Search for fields by offset range
uint minOffset = 0x20;
uint maxOffset = 0x40;
Console.WriteLine($"\nSearching for fields in offset range 0x{minOffset:X}-0x{maxOffset:X}");
var fieldsInRange = inspector.SearchFieldsByOffset(minOffset, maxOffset);

if (fieldsInRange.Any())
{
    Console.WriteLine($"Found {fieldsInRange.Count} fields in range:");
    foreach (var fieldResult in fieldsInRange.Take(10)) // Limit results for display
    {
        Console.WriteLine($"  - {fieldResult.DisplayText}");
    }
    if (fieldsInRange.Count > 10) Console.WriteLine("  ...");
}
```

### Cache Management (SDK)

The `Inspector` class provides static methods for managing the cache files programmatically.

```csharp
string processName = "unityprocess";
string moduleName = "Assembly-CSharp";

// Check if cache exists
bool cacheExists = Inspector.IsCacheAvailable(processName, moduleName);
Console.WriteLine($"Cache for '{processName}' exists: {cacheExists}");

if (cacheExists)
{
    // Get cache information
    var cachePath = Inspector.GetCacheFilePath(processName, moduleName);
    var cacheAge = Inspector.GetCacheAge(processName, moduleName);
    Console.WriteLine($"Cache Path: {cachePath}");
    Console.WriteLine($"Cache Age: {cacheAge:F1} hours");

    // List all caches
    var allCaches = Inspector.ListCacheFiles();
    Console.WriteLine($"Total cache files found: {allCaches.Count}");
    foreach(var cacheInfo in allCaches)
    {
        Console.WriteLine($" - {cacheInfo.ProcessName} ({cacheInfo.ModuleName}) - {cacheInfo.FormattedSize} - {cacheInfo.FormattedAge}");
    }

    // Get total cache size
    var totalSize = Inspector.GetTotalCacheSize();
    Console.WriteLine($"Total cache size: {totalSize}");

    // Delete a specific cache
    // bool deleted = Inspector.DeleteCache(processName, moduleName);
    // Console.WriteLine($"Cache deleted for '{processName}': {deleted}");

    // Delete all caches
    // int deletedCount = Inspector.DeleteAllCache();
    // Console.WriteLine($"Deleted {deletedCount} cache files.");
}
```

## SDK API Reference

This section provides a reference to the key classes and members available in the Unispect SDK.

### `Inspector` Class

The primary class for dumping, caching, searching, and querying type definitions. Use `Dispose()` to clean up.

#### Instance Methods

*   `DumpTypes(string fileName, Type memoryProxyType, bool verbose, string processHandle, string moduleToDump)`: Initiates the type dumping process from a live process using the specified `memoryProxyType`.
*   `LoadFromCache(string processName, string moduleName)`: Loads previously dumped type definitions from the standard cache file for the specified process and module. Returns `true` on success.
*   `SaveToCache(string processName, string moduleName, string customPath = null)`: Saves the current set of loaded type definitions to a `.utd` cache file. Defaults to the standard cache location.
*   `SearchTypes(string pattern, bool useRegex = false)`: Searches through the loaded type names (TypeDefWrapper.FullName) for matches based on the provided `pattern`. Supports wildcards (`*`, `?`) or regex. Returns a `List<TypeDefWrapper>`.
*   `SearchFields(string pattern, bool useRegex = false)`: Searches through all fields (FieldDefWrapper.Name) across all loaded types for matches based on the provided `pattern`. Supports wildcards (`*`, `?`) or regex. Returns a `List<FieldSearchResult>`.
*   `SearchFieldsByOffset(uint minOffset, uint maxOffset)`: Searches through all fields for those whose offset falls within the specified range (inclusive). Returns a `List<FieldSearchResult>`.
*   `SearchResults SearchAll(string pattern, bool useRegex = false)`: Performs both `SearchTypes` and `SearchFields` and returns the combined results in a `SearchResults` object.
*   `TypeDefWrapper GetType(string typeName)`: Retrieves a single `TypeDefWrapper` matching the exact `typeName` (case-insensitive). Returns `null` if not found.
*   `FieldDefWrapper GetField(string typeName, string fieldName)`: Retrieves a single `FieldDefWrapper` with the specified `fieldName` (case-insensitive) from the type matching `typeName` (case-insensitive). Returns `null` if the type or field is not found.
*   `List<TypeDefWrapper> GetTypesByKind(string typeKind)`: Retrieves all `TypeDefWrapper` instances whose `ClassType` matches the specified `typeKind` (e.g., "Class", "Struct", "Interface", "Enum") (case-insensitive).
*   `Dispose()`: Cleans up the internal `MemoryProxy` instance. **Important** to call when the `Inspector` is no longer needed, especially after a live dump.

#### Properties

*   `List<TypeDefWrapper> TypeDefinitions`: Gets the list of type definitions currently loaded into the inspector. This list is populated after calling `DumpTypes` or `LoadFromCache`.
*   `int RawClassCount`: Gets the count of raw Mono classes discovered during the dump process before the full type propagation and wrapping occurs. Useful for basic statistics.

#### Events

*   `event ProgressChanged`: Event that fires periodically during the `DumpTypes` operation, reporting a `float` value indicating progress from 0.0 to 1.0.

#### Static Methods (Cache Management)

*   `static string GetCacheFilePath(string processName, string moduleName = "Assembly-CSharp")`: Gets the standard file system path where the cache file for the given process and module is stored.
*   `static bool IsCacheAvailable(string processName, string moduleName = "Assembly-CSharp")`: Checks if a cache file exists at the standard path for the given process and module.
*   `static double GetCacheAge(string processName, string moduleName = "Assembly-CSharp")`: Gets the age of the cache file in hours since its last modification. Returns -1 if the cache file does not exist.
*   `static List<CacheInfo> ListCacheFiles()`: Returns a list of all cache files (`.utd`) found in the standard cache directory.
*   `static bool DeleteCache(string processName, string moduleName = "Assembly-CSharp")`: Deletes the cache file for the specified process and module. Returns `true` if deleted, `false` if not found or deletion failed.
*   `static int DeleteAllCache()`: Deletes all cache files in the standard cache directory. Returns the count of files successfully deleted.
*   `static long GetTotalCacheSize()`: Gets the total size in bytes of all cache files in the standard cache directory.
*   `static string GetCacheDirectory()`: Gets the path to the standard directory where cache files are stored.

### `MemoryProxy` Abstract Class

Base class for implementing memory access methods.

#### Abstract Methods

*   `GetModule(string moduleName)`: Implementations must find and return a `ModuleProxy` representing the base address and size of the specified module in the target process's memory. Return `null` if the module is not found.
*   `AttachToProcess(string handle)`: Implementations must connect to the target process identified by the `handle` string. Return `true` on successful attachment.
*   `Read(ulong address, int length)`: Implementations must read `length` bytes from the specified memory `address` in the target process and return them as a `byte[]`. Return `null` on failure.
*   `Dispose()`: Implementations must clean up any native resources (e.g., process handles, DMA connections).

#### Provided Method

*   `T Read<T>(ulong address, int length = 0)`: Generic helper method (implemented in the base class) that uses the abstract `Read(ulong, int)` to read raw bytes and then marshals them into a struct of type `T`.

### `UnispectPluginAttribute`

*   `[UnispectPlugin]`: Attribute used to mark classes that implement `MemoryProxy` so they can be discovered by `PluginLoader`.

### `PluginLoader` Static Class

Helper for discovering available `MemoryProxy` implementations.

#### Static Methods

*   `static List<Type> DiscoverMemoryPlugins()`: Scans the application's `Plugins` subdirectory (and includes `BasicMemory`) to find types marked with `[UnispectPlugin]` that inherit from `MemoryProxy`. Returns a list of these `Type` objects.

### `Log` Static Class

Provides logging facilities for the SDK and plugins.

#### Static Methods

*   `static void Add(string text)` / `static void Info(string text)` / `static void Warn(string text)` / `static void Error(string text)`: Methods for logging messages with different severity levels.
*   `static void Exception(string text, Exception ex)`: Logs an exception, optionally with additional text.
*   `static string[] GetLast(int count)`: Retrieves the last `count` log messages as an array of strings.

#### Events

*   `static event EventHandler<Log.MessageAddedEventArgs> LogMessageAdded`: Subscribe to this event to receive log messages as they are added. The `MessageAddedEventArgs` includes the message text and the message type (`Log.MessageType`).

### `Utilities` Static Class

Provides miscellaneous helper methods used internally and potentially useful to SDK users.

#### Static Methods

*   Includes methods for string manipulation (e.g., `ToAsciiString`, `SanitizeFileName`), version information (`CurrentVersion`), launching URLs (`LaunchUrl`), etc.

### Data Structures (`Structs` / `Models` / `Enums`)

These types represent data used by the SDK.

*   `TypeDefWrapper`, `FieldDefWrapper`: Object representations of type and field definitions.
*   `ModuleProxy`: Represents a loaded module's base address and size.
*   `CacheInfo`: Information about a specific cache file.
*   `CacheFile`: Internal structure of the `.utd` cache file.
*   `SearchResults`, `FieldSearchResult`: Structures for search results.
*   `UnispectOptions`, `UnispectResult`: Structures for dump operation configuration and results (less used when interacting directly with `Inspector`).
*   `TypeEnum`, `UnknownPrefix`: Enumerations related to type kinds.
*   Internal structs (`FieldDefinition`, `MonoType`, etc.): Low-level representations of Mono structures, primarily used by `Inspector` and `MemoryProxy` implementations. Not typically interacted with directly by SDK consumers.

### `CacheStore` Static Class

*   Internal cache for strings (field names, class names). **Not typically used directly** by SDK users; managed internally by the `Inspector`.

## Plugin Development

To create a custom memory access plugin for use with the Unispect SDK (and thus the GUI and CLI), follow these steps:

1.  Create a new .NET Class Library project (targeting `net9.0-windows` x64).
2.  Add a reference to `Unispect.SDK.dll`.
3.  Create a public class that inherits from `Unispect.SDK.MemoryProxy`.
4.  Decorate the class with the `[UnispectPlugin]` attribute.
5.  Implement the abstract methods required by `MemoryProxy`:
    *   `GetModule(string moduleName)`: Implement logic to find a loaded module (e.g., `mono-2.0-bdwgc.dll`) in the target process and return its base address and size as a `ModuleProxy`. Return `null` if the module is not found.
    *   `AttachToProcess(string handle)`: Implement logic to connect to the target process identified by the `handle` string. Return `true` on successful attachment.
    *   `Read(ulong address, int length)`: Implement logic to read `length` bytes from `address` in the target process's memory and return them as a `byte[]`. Return `null` on failure.
    *   `Dispose()`: Clean up any native handles or resources used by your plugin.
6.  Build your plugin project. Place your plugin's DLL (`YourPluginName.dll`) and any native dependencies (`.dll`, `.so`, etc.) into a subfolder named after your plugin within the main Unispect `Plugins` directory (e.g., `Unispect/Plugins/YourPluginName/YourPluginName.dll`). The SDK's `PluginLoader` will discover it.

Refer to the `Unispect.DMA` project source code for a complex plugin example or the `Plugins/MemoryPluginTemplate.cs` file (distributed with the GUI release) for a minimal template.
