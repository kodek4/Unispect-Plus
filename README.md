# Unispect Plus

![Unispect Logo](https://github.com/Razchek/Unispect/blob/master/Gallery/UnispectLogo.png?raw=true)

**An Expanded Suite for Exploring Unity Mono Runtime Type Information**

Unispect Plus is a fork and continuation of the original [Unispect project by @Razchek](https://github.com/Razchek/Unispect). This project builds upon the original's foundation of inspecting and dumping type definitions from Unity games compiled with the Mono scripting backend by refactoring the core logic into a reusable Software Development Kit (SDK) and adding a Command-Line Interface (CLI) alongside the original Graphical User Interface (GUI).

The goal is to provide a more modular, scriptable, and extensible platform for interacting with Unity's Mono runtime.

![Powered By Coffee](https://github.com/Razchek/Unispect/blob/master/Gallery/poweredByCoffee.png?raw=true)

## Table of Contents

*   [Original Unispect Legacy](#original-unispect-legacy)
*   [Components of Unispect Plus](#components-of-unispect-plus)
*   [Screenshots (GUI)](#screenshots-gui)
*   [Getting Started (End Users)](#getting-started-end-users)
*   [Features](#features)
*   [Limitations & Considerations](#limitations--considerations)
*   [Building from Source](#building-from-source)
*   [Requirements](#requirements)
*   [Technology Stack](#technology-stack)
*   [Support](#support)
*   [License](#license)

## Original Unispect Legacy

The original Unispect project by @Razchek established the core methodology of accessing a running Unity process's memory to extract and display Mono type and field definitions. This approach provided accurate runtime layout information. Unispect Plus directly inherits this core capability and aims to preserve the original intent and existing knowledge base while introducing new tools and a more flexible architecture.

## Components of Unispect Plus

Unispect Plus is structured into three primary components:

*   **Unispect (GUI)**: The refactored graphical user interface, retaining the familiar window for interactive dumping, browsing, searching, and exporting type definitions. It now utilizes the Unispect SDK internally.
*   **Unispect.CLI (CLI)**: A new command-line application built on the Unispect SDK. It provides a terminal-based interface for initiating dumps, managing the cache, searching, querying specific definitions, comparing dumps, and generating statistics. This component is suitable for scripting, automation, and use in environments without a GUI.
    *   For detailed CLI usage, refer to [Unispect.CLI/README.md](Unispect.CLI/README.md).
*   **Unispect.SDK (SDK)**: A dedicated .NET library containing the shared, core logic. This includes the memory access abstraction, Mono runtime inspection, type definition parsing, caching, and search/query algorithms. The SDK is designed to be integrable into other .NET applications and provides the base for implementing custom memory access methods via plugins (`MemoryProxy`).
    *   For SDK API details and plugin development guidance, refer to [Unispect.SDK/README.md](Unispect.SDK/README.md).

## Screenshots (GUI)

*(These screenshots depict the **Unispect (GUI)** component, largely similar in layout to the original Unispect)*

![Screenshot 1](https://github.com/Razchek/Unispect/blob/master/Gallery/screenshot1.png?raw=true)
*Main GUI Window (Mono Type Dumper)*

![Screenshot 2](https://github.com/Razchek/Unispect/blob/master/Gallery/screenshot2.png?raw=true)
*Type Inspector View*

![Screenshot 3](https://github.com/Razchek/Unispect/blob/master/Gallery/screenshot3.png?raw=true)
*Loading Memory Plugin in GUI*

## Getting Started (End Users)

To use a pre-built release of Unispect Plus (either the GUI or CLI), download the appropriate ZIP artifact from the [releases page](https://github.com/kodek4/Unispect-Plus/releases).

*   **For GUI:** Download `Unispect-GUI.zip`. Extract its contents to a directory. Run `Unispect.exe`. Requires the [.NET 9.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) to be installed on your system.
*   **For CLI:** Download `Unispect-CLI.zip`. Extract its contents to a directory. Run `unispect-cli.exe` from a command prompt or PowerShell window. This build is self-contained and typically does not require a separate runtime installation.
*   **For SDK:** Download `Unispect-SDK.zip`. Extract its contents. Include `Unispect.SDK.dll` and its dependencies (`Newtonsoft.Json.dll`) as references in your own .NET project.

## Features

Leveraging the shared SDK, Unispect Plus offers the following capabilities, accessible via the GUI, CLI, or directly through the SDK:

*   **Direct Memory Access**: Inspect live Unity processes by reading memory, supporting both standard OS APIs and custom methods via plugins (e.g., DMA).
*   **Comprehensive Type Information**: Retrieve and display definitions for classes, structs, interfaces, and enums.
*   **Accurate Field Details**: Obtain field definitions, including memory offsets, types, and indicators for static/constant fields.
*   **Runtime Deobfuscation**: Basic heuristic-based deobfuscation for common patterns found in managed assemblies.
*   **Persistent Cache**: Save discovered definitions to a local `.utd` cache file for fast loading and offline analysis, eliminating the need for repeated memory dumps on the same game version.
*   **Powerful Search**: Search loaded definitions by type name or field name, supporting wildcards and regex. Also supports searching for fields within specific offset ranges.
*   **Precise Querying**: Retrieve detailed information for specific types or fields by their exact names.
*   **Data Export**: Export dumped definitions to various formats, including human-readable text, JSON for programmatic use, the Unispect Type Database format (`.utd`), and C# struct definitions (using `IntPtr` or `ulong` for pointer types).
*   **Cache Management**: CLI and SDK provide utilities to list, inspect, validate, and clear cache files.
*   **Comparison Tool**: The CLI offers a command to compare two cached dumps, highlighting added, removed, and modified types and fields (especially useful for tracking game updates).
*   **Statistics Generation**: The CLI/SDK can generate statistics about a dump, such as total types, fields, distribution by type kind, common field types, etc.
*   **Extensible Memory Access**: Implement custom memory reading logic by creating a `MemoryProxy` plugin using the SDK.

**GUI Specific Features (Inherited/Enhanced):**

*   Interactive tree view for browsing the hierarchical structure of types and fields.
*   Easy navigation to parent types or field types via clickable links in the inspector.
*   Drag & drop functionality to copy definition strings (formatted based on user selection) directly into other applications like text editors.

## Limitations & Considerations

*   Primarily tested and designed for Unity builds using the **Mono scripting backend**. Games built with IL2CPP are currently not supported.
*   Supports **x64** processes only. x32 processes are not supported.
*   The target Unity process **must be running** and accessible by the selected Memory Proxy plugin. Memory reading permissions are required.
*   Retrieving runtime *values* of fields (other than basic constant/static indicators) is not currently implemented. Focus is on type and field *definitions* and *offsets*.
*   Method definitions are generally not collected, with a focus on the memory layout of types and fields.
*   The default `BasicMemory` plugin uses standard Windows API (`ReadProcessMemory`), which may be detected by some anti-cheat systems. Custom Memory Proxy plugins (like DMA) are intended as alternatives.
*   Correct interpretation relies on specific offsets within the Mono runtime structures, which can vary between Unity versions or specific Mono configurations. While the project includes a `targets` directory for configurable offsets via `.json` files, support for new/different Unity versions might require manually finding and adding new offsets.

## Building from Source

To build Unispect Plus from the source code:

1.  **Prerequisites**:
    *   [.NET SDK](https://dotnet.microsoft.com/download/dotnet/9.0) (`.NET 9.0` or newer, x64).
    *   Visual Studio 2022 or later, or the `dotnet` CLI build tools.
    *   PowerShell (for running the build script).
2.  **Clone the Repository**:
    ```bash
    git clone https://github.com/kodek4/Unispect-Plus.git
    cd Unispect-Plus
    ```
3.  **Build the Solution**:
    *   **Using `dotnet` CLI**:
        ```bash
        dotnet build Unispect.sln -c Release
        ```
    *   **Using Visual Studio**: Open `Unispect.sln` and build the solution in `Release` configuration.
4.  **Create Release Packages (Optional but Recommended)**: The `build.ps1` script automates the creation of production-ready ZIP packages for the GUI, CLI, and SDK.
    *   Run the script from PowerShell within the repository root:
        ```powershell
        ./build.ps1
        ```
    *   Release packages (`Unispect-GUI.zip`, `Unispect-CLI.zip`, `Unispect-SDK.zip`) will be created in a `./Release` directory.

## Requirements

*   **End Users (GUI)**: [.NET 9.0 Desktop Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/9.0) or newer. Windows x64 operating system.
*   **End Users (CLI)**: Windows x64 operating system. The default CLI build is self-contained and should not require a separate runtime installation.
*   **Developers (Building from Source)**: [.NET SDK 9.0](https://dotnet.microsoft.com/download/dotnet/9.0) or newer (x64). Windows x64 operating system.

## Technology Stack

Unispect Plus is built using:

*   **.NET 9.0** (Core SDK & CLI)
*   **.NET Framework 4.8** (GUI - maintained for broader compatibility, built with .NET 9 SDK)
*   **C#**
*   **WPF** (for the GUI)
*   [MahApps.Metro](https://github.com/MahApps/MahApps.Metro) (GUI styling)
*   [System.CommandLine](https://github.com/dotnet/command-line-api) (for the CLI)
*   [Newtonsoft.Json](https://github.com/JamesNK/Newtonsoft.Json) (for serialization and caching)
*   [Costura.Fody](https://github.com/Fody/Costura) (for embedding dependencies in GUI release)

## Support

Contributions are welcome! Feel free to fork the repository and submit pull requests.

For issues or questions, please use the [GitHub Issues page](https://github.com/kodek4/Unispect-Plus/issues).

You can also [![Buy Me A Coffee](https://img.shields.io/badge/--_Coffee%3F-blue?logo=ko-fi&label=Powered%20by&color=orange)](https://ko-fi.com/razchek)

## License

Unispect Plus is licensed under the [MIT License (MIT)](LICENSE), inherited from the original project.
