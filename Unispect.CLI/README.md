
# Unispect CLI

Unispect CLI is the command-line interface component of the Unispect-Plus project. It provides access to the core type dumping and inspection capabilities via a terminal, facilitating automation, scripting, and integration into workflows where a graphical interface is not required.

It utilizes the Unispect SDK for its underlying functionality and supports the same memory access plugins as the GUI.

## Table of Contents

*   [Obtaining the CLI](#obtaining-the-cli)
*   [Usage](#usage)
    *   [`dump`](#dump)
    *   [`search`](#search)
    *   [`query`](#query)
    *   [`cache`](#cache)
    *   [`validate`](#validate)
    *   [`compare`](#compare)
    *   [`stats`](#stats)

## Obtaining the CLI

The Unispect CLI executable (`unispect-cli.exe`) is produced as part of the Unispect-Plus build process. Obtain the latest release from the main [Unispect-Plus GitHub repository](https://github.com/kodek4/Unispect-Plus) and locate the `Unispect-CLI.zip` artifact. The executable and its dependencies are contained within this archive.

Place the contents of the ZIP archive in a directory on your system. Ensure that `unispect-cli.exe` is accessible from your terminal (either by navigating to its directory or adding it to your system's PATH).

## Usage

Run `unispect-cli --help` to see the list of available commands:

```bash
./unispect-cli --help

Description:
  Unispect CLI - Unity Memory Inspector

Usage:
  unispect-cli [command] [options]

Options:
  --version       Show version information
  --help          Show help and usage information

Commands:
  dump      Dump type definitions from target process
  search    Search through cached type definitions
  query     Query specific type or field
  cache     Cache management commands
  validate  Validate cache integrity
  compare   Compare two cached dumps
  stats     Show statistics about a cached dump
```

Commands operate primarily on a cached dump for performance. A dump must be created using the `dump` command before using `search`, `query`, `cache info`, `validate`, `compare`, or `stats`.

The target process name is typically the executable name *without* the `.exe` extension when using the default `BasicMemory` plugin (e.g., `unityprocess`). Custom plugins may have different requirements as specified by the plugin author.

### `dump`

Dumps type definitions from a target process and saves them to a local cache file for subsequent operations. Optionally exports the dump in various formats.

```bash
./unispect-cli dump --help

Description:
  Dump type definitions from target process

Usage:
  unispect-cli dump --process <process> [options]

Options:
  --process <process>   Target process name [required]
  --plugin <plugin>     Memory plugin to use (e.g., BasicMemory, DMAMemoryPlugin). Defaults to BasicMemory.
  --output <output>     Output file path (optional). If omitted, only the cache is saved.
  --format <format>     Output format: text, json, utd, csharp-intptr, csharp-ulong. Defaults to text.
  --refresh             Force refresh cache. Ignores existing cache and performs a new memory dump.
  --verbose             Verbose output in text format (includes class types, inheritance, etc.). Defaults to true for text, ignored otherwise.
```

**Examples:**

Dump `unityprocess` and save to cache:
```bash
./unispect-cli dump --process unityprocess
```
*Output will show progress and completion messages)*

Dump `unityprocess` using a DMA plugin and save to a text file, forcing a refresh:
```bash
./unispect-cli dump --process unityprocess --plugin DMAMemoryPlugin --output dump.txt --format text --refresh
```
*Output will show plugin loading, progress, and file save confirmation)*

Dump `unityprocess` and save to a JSON file, using non-verbose text format (relevant if `--format text` is also used):
```bash
./unispect-cli dump --process unityprocess --output dump.json --format json --verbose false
```
*Output will show dump progress, file save confirmation. The JSON itself will not contain verbose details if `--verbose false` applied to the dump process itself, though the CLI verbose flag primarily impacts text output).*

### `search`

Searches through cached type definitions using a pattern. Supports wildcards and regex.

```bash
./unispect-cli search --help

Description:
  Search through cached type definitions

Usage:
  unispect-cli search --process <process> --pattern <pattern> [options]

Options:
  --process <process>   Target process name [required]
  --pattern <pattern>   Search pattern [required]
  --regex               Use regex pattern matching instead of wildcards
  --type <type>         Search type: all, types, fields. Defaults to all.
  --offset-range <range> Offset range for field search (e.g., 0x10-0x50). Overrides pattern and type=fields if specified.
  --limit <limit>       Maximum results to show. Defaults to 50.
  --include-parent      Include parent class names when searching types
  --include-interfaces  Include interface names when searching types
  --exclude-system      Exclude system types (System.*, UnityEngine.*, etc.) from results
  --min-fields <count>  Minimum number of fields a type must have to be included in results
  --max-fields <count>  Maximum number of fields a type can have to be included in results
```

**Examples:**

Search for types containing "Manager" (wildcard, case-insensitive):
```bash
./unispect-cli search --process unityprocess --pattern *Manager --type types
```
*Output*
```
🔍 Searching for '*Manager' in 12345 types
ℹ️  Found 5 types
🏷️  GameManager (15 fields)
   ↳ extends UnityEngine.MonoBehaviour
🏷️  AssetBundleManager (5 fields)
   ↳ extends System.Object
🏷️  UIManager (25 fields)
   ↳ extends UnityEngine.MonoBehaviour
🏷️  AudioManager (10 fields)
   ↳ extends System.Object
🏷️  InputManager (8 fields)
   ↳ extends System.Object
```

Search for fields named `m_.*` using regex:
```bash
./unispect-cli search --process unityprocess --pattern ^m_.*$ --regex --type fields --limit 10
```
*Output*
```
🔍 Searching for '^m_.*$' in 12345 types
ℹ️  Found 1234 fields
🔧 Player.m_health : System.Int32 @ 0x18
🔧 Player.m_mana : System.Int32 @ 0x1C
🔧 GameManager.m_isInitialized : System.Boolean @ 0x20
🔧 UIManager.m_canvas : UnityEngine.Canvas @ 0x10
🔧 AudioManager.m_soundClip : UnityEngine.AudioClip @ 0x18
🔧 AudioManager.m_volume : System.Single @ 0x1C
🔧 InputManager.m_sensitivity : System.Single @ 0x10
🔧 InputManager.m_invertMouse : System.Boolean @ 0x14
🔧 PlayerController.m_speed : System.Single @ 0x24
🔧 CameraController.m_target : UnityEngine.Transform @ 0x20
```

Search for fields within the offset range `0x20` to `0x40`:
```bash
./unispect-cli search --process unityprocess --offset-range 0x20-0x40
```
*Output*
```
🔍 Searching for '0x20-0x40' in 12345 types
ℹ️  Found 56 fields in offset range 0x20-0x40
🔧 GameManager.m_isInitialized : System.Boolean @ 0x20
🔧 UIManager.m_canvas : UnityEngine.Canvas @ 0x10
🔧 PlayerController.m_speed : System.Single @ 0x24
🔧 CameraController.m_target : UnityEngine.Transform @ 0x20
🔧 HealthComponent.maxHealth : System.Int32 @ 0x20
🔧 TransformComponent.position : UnityEngine.Vector3 @ 0x24
... (truncated)
```

### `query`

Retrieves and displays details for a specific type or field by its exact name.

```bash
./unispect-cli query --help

Description:
  Query specific type or field

Usage:
  unispect-cli query --process <process> --query <query> [options]

Options:
  --process <process>   Target process name [required]
  --query <query>       Query (e.g., 'Player', 'Player.health', 'Player.health,GameManager') [required]
  --format <format>     Output format: full, offset-only, type-only. Defaults to full.
```

**Examples:**

Query full details for type `Player`:
```bash
./unispect-cli query --process unityprocess --query Player
```
*Output - full format*
```
🔍 Querying 'Player' in 12345 types
✅ Found type: Player
ℹ️  Type: Player
ℹ️  Kind: Class
ℹ️  Parent: MonoBehaviour
ℹ️  Interfaces: ISerializable, IDisposable
ℹ️  Field Count: 12

📋 Fields
  [0x18] m_health : System.Int32
  [0x1C] m_mana : System.Int32
  [0x20][S] s_defaultDamage : System.Single
  ... (truncated)
```

Query offset for field `Player.health`:
```bash
./unispect-cli query --process unityprocess --query Player.health --format offset-only
```
*Output*
```
🔍 Querying 'Player.health' in 12345 types
✅ Found field: Player.m_health
0x18
```

Query multiple items (type and field):
```bash
./unispect-cli query --process unityprocess --query GameManager,Player.mana
```
*Output*
```
🔍 Querying 'GameManager,Player.mana' in 12345 types

📋 Query: GameManager
✅ Found type: GameManager
ℹ️  Type: GameManager
ℹ️  Kind: Class
ℹ️  Parent: MonoBehaviour
ℹ️  Interfaces:
ℹ️  Field Count: 15

📋 Fields
  [0x18] m_instance : GameManager
  [0x20] m_isInitialized : System.Boolean
  ... (truncated)

📋 Query: Player.mana
✅ Found field: Player.m_mana
ℹ️  Container Type: Player
ℹ️  Field Name: m_mana
ℹ️  Field Type: System.Int32
ℹ️  Offset: 0x1C
ℹ️  Is Pointer: False
ℹ️  Is Value Type: False
ℹ️  Constant Value:
```

### `cache`

Manages the local Unispect cache files.

```bash
./unispect-cli cache --help

Description:
  Cache management commands

Usage:
  unispect-cli cache [command]

Commands:
  list    List cached type definitions
  info    Show cache information (--process <process> required)
  clear   Clear cache (--process <process> or 'all' required)
```

**Examples:**

List all available caches:
```bash
./unispect-cli cache list
```
*Output*
```
📋 Found 2 cache files
📁 unityprocess (Assembly-CSharp)
  Size: 2.5 MB
  Age: 3.5 hours ago
  Path: C:\Users\user\AppData\Local\Unispect\Cache\unityprocess_assembly-csharp.utd

📁 othergame (Assembly-CSharp)
  Size: 1.1 MB
  Age: 2 days ago
  Path: C:\Users\user\AppData\Local\Unispect\Cache\othergame_assembly-csharp.utd

ℹ️  Total cache size: 3.6 MB
```

Show information for a specific cache:
```bash
./unispect-cli cache info --process unityprocess
```
*Output*
```
📋 Cache Information: unityprocess
ℹ️  Process: unityprocess
ℹ️  Cache Path: C:\Users\user\AppData\Local\Unispect\Cache\unityprocess_assembly-csharp.utd
ℹ️  Age: 3.5 hours
ℹ️  Type Count: 12345
ℹ️  File Size: 2.5 MB
ℹ️  Created: 2024-01-01 10:00:00 AM
ℹ️  Modified: 2024-01-01 01:30:00 PM

📋 Sample Types
  🏷️  GameManager (15 fields)
  🏷️  Player (12 fields)
  🏷️  UIManager (25 fields)
  🏷️  AudioManager (10 fields)
  🏷️  InputManager (8 fields)
  ℹ️  ... and 12340 more types
```

Clear the cache for `unityprocess`:
```bash
./unispect-cli cache clear --process unityprocess
```
*Output*
```
✅ Deleted cache for 'unityprocess'
```

Clear all caches:
```bash
./unispect-cli cache clear --process all
```
*Output*
```
✅ Deleted 2 cache files
```

### `validate`

Validates the integrity of a cached dump file.

```bash
./unispect-cli validate --help

Description:
  Validate cache integrity

Usage:
  unispect-cli validate --process <process> [options]

Options:
  --process <process>   Target process name [required]
  --fix                 Attempt to fix issues (e.g., delete corrupted cache file)
```

**Examples:**

Validate the cache for `unityprocess`:
```bash
./unispect-cli validate --process unityprocess
```
*Output - successful*
```
📋 Validating cache for 'unityprocess'
ℹ️  ✓ Cache file exists
ℹ️  ✓ Cache file size: 2.5 MB
ℹ️  ✓ Cache file loads successfully
ℹ️  ✓ Found 12345 type definitions
ℹ️  ✓ All type definitions are valid
ℹ️  ✓ Cache age: 3.5 hours

✅ Cache validation passed - no issues found
```
*Output - corrupted, without --fix*
```
📋 Validating cache for 'unityprocess'
ℹ️  ✓ Cache file exists
ℹ️  ✓ Cache file size: 1.8 MB
❌ Failed to load cache for 'unityprocess'
❌ Cache validation found 1 issues (use --fix to resolve)
```
*Output - corrupted, with --fix*
```
📋 Validating cache for 'unityprocess'
ℹ️  ✓ Cache file exists
ℹ️  ✓ Cache file size: 1.8 MB
❌ Failed to load cache for 'unityprocess'
ℹ️  🔧 Deleting corrupted cache file...
✅ Corrupted cache file deleted
✅ Cache validation completed - 1 issues fixed
```

### `compare`

Compares two cached dumps and reports differences in types and fields.

```bash
./unispect-cli compare --help

Description:
  Compare two cached dumps

Usage:
  unispect-cli compare --process1 <process1> --process2 <process2> [options]

Options:
  --process1 <process1> First process name [required]
  --process2 <process2> Second process name [required]
  --output <output>     Output file path (optional). If omitted, results are printed to console.
  --format <format>     Output format: text, json. Defaults to text.
```

**Examples:**

Compare `unityprocess` cache with `unityprocess_v2` cache:
```bash
./unispect-cli compare --process1 unityprocess --process2 unityprocess_v2
```
*Output - text format)*
```
📋 Comparing 'unityprocess' vs 'unityprocess_v2'
ℹ️  Loaded 12345 types from 'unityprocess'
ℹ️  Loaded 12500 types from 'unityprocess_v2'

📋 Comparison Summary
ℹ️  Types only in first: 50
ℹ️  Types only in second: 205
ℹ️  Modified types: 15

📋 Types only in second process
  + NewFeatureClass
  + AnotherNewType
  ... (truncated)

📋 Modified types
  ~ Player
    Field count: 12 → 13
    + Field: m_newField
    ~ Field: m_health
      Offset: 0x18 → 0x1C
  ~ GameManager
    ~ Field: m_instance
      Offset: 0x18 → 0x20
    ~ Field: m_initialized
      Type: System.Boolean → System.Byte

ℹ️  Workflow test complete!
```

Compare and save results to a JSON file:
```bash
./unispect-cli compare --process1 unityprocess --process2 unityprocess_v2 --output diff.json --format json
```
*Output will show confirmation of export)*

### `stats`

Generates and displays statistics about a cached dump.

```bash
./unispect-cli stats --help

Description:
  Show statistics about a cached dump

Usage:
  unispect-cli stats --process <process> [options]

Options:
  --process <process>   Target process name [required]
  --output <output>     Output file path (optional). If omitted, results are printed to console.
  --format <format>     Output format: text, json. Defaults to text.
  --detailed            Show detailed statistics (namespaces, common types, etc.)
```

**Examples:**

Show basic statistics for `unityprocess`:
```bash
./unispect-cli stats --process unityprocess
```
*Output - text format*
```
📋 Statistics for 'unityprocess'
📋 Overview
ℹ️  Total Types: 12345
ℹ️  Raw Class Count: 12000
ℹ️  Total Fields: 56789

📋 Type Breakdown
ℹ️  Classes: 10000
ℹ️  Structs: 1500
ℹ️  Interfaces: 500
ℹ️  Enums: 345

📋 Field Statistics
ℹ️  Total Fields: 56789
ℹ️  Static Fields: 1200
ℹ️  Constant Fields: 500
ℹ️  Average Fields/Type: 4.6
ℹ️  Largest Type: BigDataContainer (250 fields)
```

Show detailed statistics and save to JSON:
```bash
./unispect-cli stats --process unityprocess --detailed --output stats.json --format json
```
*Output will show confirmation of export*

