# Unispect.Core

A .NET Framework 4.8 library that provides programmatic access to all Unispect functionality. Perfect for automation scenarios where you need to dump Unity game offsets without manual GUI interaction.

## Features

✅ **Both DMA and BasicMemory Support** - Works with any `MemoryProxy` implementation  
✅ **Multiple Export Formats** - Text, JSON, and TypeDatabase (.utd) formats  
✅ **All GUI Options Available** - Every configuration option exposed programmatically  
✅ **Sync & Async APIs** - Choose the right approach for your use case  
✅ **Plugin System** - Full support for custom memory implementations  
✅ **Upstream Compatible** - Zero changes to existing Unispect code  

## Quick Start

```csharp
using Unispect.Core;
using Unispect.Core.Models;

var engine = new UnispectEngine();

var options = new UnispectOptions
{
    ProcessName = "YourGame",           // Without .exe for BasicMemory
    ModuleName = "Assembly-CSharp",     // Target module
    MemoryProxyType = typeof(Unispect.BasicMemory),
    UnityTargetPath = "targets/v2022.json",  // Optional: Unity offsets
    Verbose = true
};

// Synchronous (perfect for automation)
var result = engine.DumpTypes(options);

if (result.Success)
{
    Console.WriteLine($"Found {result.TypeCount} types in {result.Duration.TotalSeconds:F2}s");
    
    // Export formats
    engine.ExportToFile(result, "offsets.txt", ExportFormat.Text);
    engine.ExportToFile(result, "offsets.json", ExportFormat.Json);
    
    // Or get JSON as string for processing
    var json = engine.ExportToJson(result);
    // ... process in your automation app
}
```

## DMA Support

When you have a DMA plugin available, simply change the memory proxy type:

```csharp
var options = new UnispectOptions
{
    ProcessName = "YourGame.exe",       // Can include .exe for DMA
    MemoryProxyType = typeof(YourDmaPlugin),  // Your DMA implementation
    // ... other options
};
```

## Async with Progress

```csharp
var progress = new Progress<float>(p => Console.WriteLine($"Progress: {p:P1}"));
var result = await engine.DumpTypesAsync(options, progress);
```

## Plugin Loading

```csharp
var plugins = engine.LoadPlugins();
foreach (var plugin in plugins)
{
    Console.WriteLine($"Available: {plugin.Name}");
}
```

## JSON Output Format

The JSON export provides structured data perfect for automation:

```json
{
  "ProcessName": "YourGame",
  "ModuleName": "Assembly-CSharp", 
  "DumpTime": "2025-01-15T10:30:00",
  "TypeCount": 1234,
  "TypeDefinitions": [
    {
      "FullName": "PlayerController",
      "ClassType": "Class",
      "Fields": [
        {
          "Name": "position",
          "FieldType": "UnityEngine.Vector3",
          "Offset": 24
        }
      ]
    }
  ]
}
```

## Integration Example

Your automation app can now be as simple as:

```csharp
// Game updated? Get new offsets in 2 seconds:
var result = engine.DumpTypes(options);
var json = engine.ExportToJson(result);

// Process JSON to update your specific offset format
UpdateGameOffsets(json);
```

## Requirements

- .NET Framework 4.8
- Reference to main Unispect project
- Newtonsoft.Json (included via NuGet)

## Project Structure

```
Unispect-Lib/
├── Unispect/           # Original GUI (unchanged)
├── Unispect.Core/      # This library  
└── Examples/           # Usage examples
```

Perfect for maintaining upstream compatibility while adding powerful automation capabilities! 