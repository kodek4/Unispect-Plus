# Unispect SDK

The Unispect SDK is the core library component of the Unispect-Plus project. It provides developers with programmatic access to type dumping, inspection, caching, and searching logic, allowing integration into custom tools, analysis platforms, or other .NET applications.

The SDK is designed with extensibility in mind through the `MemoryProxy` abstract class, which enables implementing custom methods for reading remote process memory (e.g., standard OS APIs, DMA).

## Table of Contents

- [Obtaining the SDK](#obtaining-the-sdk)
- [Core Concepts](#core-concepts)
- [Integrating the SDK](#integrating-the-sdk)
  - [Loading from Cache](#loading-from-cache)
  - [Performing a Live Dump](#performing-a-live-dump)
  - [Querying and Searching](#querying-and-searching)
  - [Cache Management](#cache-management)
- [SDK API Reference](#sdk-api-reference)
  - [Inspector Class](#inspector-class)
  - [MemoryProxy Abstract Class](#memoryproxy-abstract-class)
  - [Data Structures](#data-structures)
  - [Utility Classes](#utility-classes)
- [Plugin Development](#plugin-development)

## Obtaining the SDK

The Unispect SDK is produced as a standard .NET library (`Unispect.SDK.dll`) as part of the Unispect-Plus build process. 

1. Obtain the latest release from the [Unispect-Plus GitHub repository](https://github.com/kodek4/Unispect-Plus)
2. Download the `Unispect-SDK.zip` artifact
3. Include `Unispect.SDK.dll` and its dependencies (`Newtonsoft.Json.dll`) as references in your .NET project
4. Ensure your project targets a compatible framework (`net9.0-windows` or newer, x64 platform)

## Core Concepts

### Primary Components

**Inspector**  
The main class for initiating dump operations, loading/saving caches, and performing searches/queries on loaded type definitions.

**MemoryProxy**  
An abstract base class defining the interface for reading memory from a target process. The SDK includes a default `BasicMemory` implementation using standard Windows APIs.

**TypeDefWrapper / FieldDefWrapper**  
Wrapped representations of discovered Mono type and field definitions, providing simplified access to properties like name, type, offset, parent, and fields.

**Log**  
A static class for logging messages from the SDK and plugins. Subscribe to its `LogMessageAdded` event to capture output.

**PluginLoader**  
A static helper class to discover available `MemoryProxy` implementations within the application's execution environment.

## Integrating the SDK

### Prerequisites

Add references to `Unispect.SDK.dll` and `Newtonsoft.Json.dll`, then include the following namespaces:

```csharp
using System;
using System.Linq;
using System.Threading.Tasks;
using Unispect.SDK;
using Unispect.SDK.Models;
```

### Logging Setup

Subscribe to SDK logs for visibility into operations:

```csharp
Log.LogMessageAdded += (sender, e) =>
{
    Console.WriteLine($"[SDK Log - {e.Type}] {e.Message}");
};
```

### Loading from Cache

Loading from a previously created cache file (`.utd`) is the fastest way to access type definitions:

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
            // TypeDefinitions are now available for searching/querying
        }
        else
        {
            Console.WriteLine($"Failed to load cache. Cache might be corrupted.");
            // Consider deleting corrupted cache:
            // Inspector.DeleteCache(processName, moduleName);
        }
    }
    else
    {
        Console.WriteLine($"No cache found for '{processName}'. A dump is required.");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error loading cache: {ex.Message}");
}
finally
{
    inspector?.Dispose();
}
```

### Performing a Live Dump

When no cache is available or fresh data is needed, perform a live memory dump:

```csharp
string processName = "unityprocess";
string moduleToDump = "Assembly-CSharp";
Inspector inspector = null;

try
{
    inspector = new Inspector();

    // 1. Discover available memory plugins
    var availablePlugins = PluginLoader.DiscoverMemoryPlugins();
    var memoryProxyType = availablePlugins.FirstOrDefault(p => p == typeof(BasicMemory));

    if (memoryProxyType == null)
    {
        Console.WriteLine("Desired MemoryProxy plugin not found.");
        return;
    }

    // 2. Set up progress reporting (optional)
    var progress = new Progress<float>(p =>
    {
        Console.Write($"\rDump Progress: {p:P0}");
    });

    // 3. Create dump options
    var options = new UnispectOptions
    {
        ProcessName = processName,
        ModuleName = moduleToDump,
        MemoryProxyType = memoryProxyType,
        Verbose = true
    };
    options.Validate();

    Console.WriteLine($"Starting live dump for '{processName}' using {memoryProxyType.Name}...");

    // 4. Execute dump operation
    await Task.Run(() => 
        inspector.DumpTypes("", options.MemoryProxyType, options.Verbose, 
                          options.ProcessName, options.ModuleName)
    );

    Console.WriteLine($"\nDump completed. Discovered {inspector.TypeDefinitions.Count} types.");
}
catch (Exception ex)
{
    Console.WriteLine($"Dump operation failed: {ex.Message}");
}
finally
{
    inspector?.Dispose();
}
```

### Querying and Searching

Once type definitions are loaded, use these methods to find specific types or fields:

#### Get Specific Type

```csharp
var playerType = inspector.GetType("Player");
if (playerType != null)
{
    Console.WriteLine($"Found: {playerType.FullName} ({playerType.ClassType})");
    Console.WriteLine($"Parent: {playerType.ParentName}");
    Console.WriteLine($"Fields: {playerType.Fields.Count}");
}
```

#### Get Specific Field

```csharp
var healthField = inspector.GetField("Player", "m_health");
if (healthField != null)
{
    Console.WriteLine($"Field: {healthField.Name}");
    Console.WriteLine($"Type: {healthField.FieldType}");
    Console.WriteLine($"Offset: 0x{healthField.Offset:X}");
}
```

#### Search Operations

```csharp
// Search types by pattern
var managerTypes = inspector.SearchTypes("*Manager");

// Search fields by pattern
var mPrefixedFields = inspector.SearchFields("m_*");

// Search fields by offset range
var fieldsInRange = inspector.SearchFieldsByOffset(0x20, 0x40);

// Combined search
var allResults = inspector.SearchAll("player");
```

### Cache Management

The `Inspector` class provides static methods for cache management:

```csharp
// Check cache existence
bool exists = Inspector.IsCacheAvailable("process", "module");

// Get cache information
string path = Inspector.GetCacheFilePath("process", "module");
double age = Inspector.GetCacheAge("process", "module");

// List all caches
var caches = Inspector.ListCacheFiles();
foreach (var cache in caches)
{
    Console.WriteLine($"{cache.ProcessName} - {cache.FormattedSize} - {cache.FormattedAge}");
}

// Delete operations
bool deleted = Inspector.DeleteCache("process", "module");
int deletedCount = Inspector.DeleteAllCache();

// Get total cache size
long totalSize = Inspector.GetTotalCacheSize();
```

## SDK API Reference

### Inspector Class

The primary class for dumping, caching, searching, and querying type definitions.

#### Instance Properties

| Property | Type | Description |
|----------|------|-------------|
| `TypeDefinitions` | `List<TypeDefWrapper>` | Currently loaded type definitions |
| `RawClassCount` | `int` | Count of raw Mono classes discovered during dump |


#### Instance Methods

##### Core Operations

| Method | Description |
|--------|-------------|
| `DumpTypes(string fileName, Type memoryProxyType, bool verbose, string processHandle, string moduleToDump)` | Initiates type dumping from a live process |
| `LoadFromCache(string processName, string moduleName)` | Loads type definitions from cache file |
| `SaveToCache(string processName, string moduleName, string customPath = null)` | Saves current type definitions to cache |
| `Dispose()` | Cleans up resources (important after live dumps) |


##### Search Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `SearchTypes(string pattern, bool useRegex = false)` | `List<TypeDefWrapper>` | Search type names (supports wildcards) |
| `SearchFields(string pattern, bool useRegex = false)` | `List<FieldSearchResult>` | Search field names across all types |
| `SearchFieldsByOffset(uint minOffset, uint maxOffset)` | `List<FieldSearchResult>` | Search fields within offset range |
| `SearchAll(string pattern, bool useRegex = false)` | `SearchResults` | Combined type and field search |


##### Query Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `GetType(string typeName)` | `TypeDefWrapper` | Get single type by exact name (case-insensitive) |
| `GetField(string typeName, string fieldName)` | `FieldDefWrapper` | Get specific field from type |
| `GetTypesByKind(string typeKind)` | `List<TypeDefWrapper>` | Get all types of specific kind (Class, Struct, etc.) |


#### Static Methods (Cache Management)

| Method | Returns | Description |
|--------|---------|-------------|
| `GetCacheFilePath(string processName, string moduleName)` | `string` | Get cache file path |
| `IsCacheAvailable(string processName, string moduleName)` | `bool` | Check if cache exists |
| `GetCacheAge(string processName, string moduleName)` | `double` | Get cache age in hours |
| `ListCacheFiles()` | `List<CacheInfo>` | List all cache files |
| `DeleteCache(string processName, string moduleName)` | `bool` | Delete specific cache |
| `DeleteAllCache()` | `int` | Delete all caches, returns count |
| `GetTotalCacheSize()` | `long` | Get total cache size in bytes |
| `GetCacheDirectory()` | `string` | Get cache directory path |


#### Events

| Event | Description |
|-------|-------------|
| `ProgressChanged` | Reports dump progress (0.0 to 1.0) |


### MemoryProxy Abstract Class

Base class for implementing memory access methods.

#### Abstract Methods to Implement

| Method | Returns | Description |
|--------|---------|-------------|
| `GetModule(string moduleName)` | `ModuleProxy` | Find and return module information |
| `AttachToProcess(string handle)` | `bool` | Connect to target process |
| `Read(ulong address, int length)` | `byte[]` | Read bytes from memory address |
| `Dispose()` | `void` | Clean up native resources |


#### Provided Methods

| Method | Returns | Description |
|--------|---------|-------------|
| `Read<T>(ulong address, int length = 0)` | `T` | Generic helper to read and marshal structs |

### Data Structures

#### Primary Data Types

**TypeDefWrapper**  
Represents a type definition with properties:
- `FullName`: Complete type name
- `ClassType`: Type kind (Class, Struct, Interface, Enum)
- `ParentName`: Parent type name
- `Fields`: List of field definitions


**FieldDefWrapper**  
Represents a field definition with properties:
- `Name`: Field name
- `FieldType`: Field type name
- `Offset`: Memory offset
- `IsPointer`: Whether field is a pointer
- `IsValueType`: Whether field is a value type
- `HasValue`: Whether field has a constant value

#### Search Results


**SearchResults**  
Combined results from type and field searches:
- `Types`: List of matching types
- `Fields`: List of matching fields


**FieldSearchResult**  
Individual field search result:
- `DisplayText`: Formatted display string
- `Type`: Parent type reference
- `Field`: Field reference

#### Cache Information


**CacheInfo**  
Information about a cache file:
- `ProcessName`: Associated process name
- `ModuleName`: Associated module name
- `FormattedSize`: Human-readable file size
- `FormattedAge`: Human-readable age

### Utility Classes

#### Log Class

Static logging facilities for SDK and plugins.


**Methods**
- `Add(string text)`: Add general log entry
- `Info(string text)`: Add info-level log
- `Warn(string text)`: Add warning-level log
- `Error(string text)`: Add error-level log
- `Exception(string text, Exception ex)`: Log exception details
- `GetLast(int count)`: Retrieve recent log messages

**Events**
- `LogMessageAdded`: Subscribe to receive log messages in real-time

#### PluginLoader Class

**Methods**
- `DiscoverMemoryPlugins()`: Find all available MemoryProxy implementations

#### Attributes

**UnispectPluginAttribute**  
Mark classes as discoverable plugins:
```csharp
[UnispectPlugin]
public class CustomMemoryProxy : MemoryProxy
{
    // Implementation
}
```

## Plugin Development

Create custom memory access plugins by following these steps:

### 1. Project Setup

Create a new .NET Class Library project:
- Target: `net9.0-windows` x64
- Reference: `Unispect.SDK.dll`

### 2. Implementation

```csharp
using Unispect.SDK;

[UnispectPlugin]
public class YourMemoryPlugin : MemoryProxy
{
    public override ModuleProxy GetModule(string moduleName)
    {
        // Find module in target process
        // Return ModuleProxy with base address and size
        // Return null if not found
    }

    public override bool AttachToProcess(string handle)
    {
        // Connect to target process
        // Return true on success
    }

    public override byte[] Read(ulong address, int length)
    {
        // Read bytes from target process memory
        // Return null on failure
    }

    public override void Dispose()
    {
        // Clean up resources
    }
}
```

### 3. Deployment

1. Build your plugin project
2. Create plugin folder: `Unispect/Plugins/YourPluginName/`
3. Place your DLL and dependencies in the plugin folder
4. The SDK's PluginLoader will automatically discover it

### Resources

- Complex example: `Unispect.DMA` project source
- Minimal template: `Plugins/MemoryPluginTemplate.cs` (distributed with GUI release)
