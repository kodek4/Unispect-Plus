# Unispect.SDK Examples

This folder contains examples demonstrating how to use the `Unispect.SDK.dll` as a standalone library in different programming languages.

These examples show the basic workflow of:
1. Initializing the `Inspector`.
2. Dumping types from a target process.
3. Searching the dumped types for a specific class.
4. Writing the results to a file.

## Prerequisites

1.  **Unispect.SDK.dll**: You must have a compiled version of the SDK. You can get this by building the `Unispect.SDK` project in Visual Studio.
2.  **Target Process**: You need a game or application process running that you want to inspect. These examples are configured to work with Unity games by default.

## C# Example

The C# example (`csharp-example.cs`) shows how to use the SDK directly in a .NET project.

### How to Run

1.  **Create a new .NET Console Application**:
    ```sh
    dotnet new console -n CSharpExample
    cd CSharpExample
    ```
2.  **Copy the Files**:
    *   Copy `csharp-example.cs` into your new project, renaming it to `Program.cs`.
    *   Copy `Unispect.SDK.dll` into the project directory.
3.  **Add a Reference to the SDK**:
    ```sh
    dotnet add reference ../Unispect.SDK/Unispect.SDK.csproj 
    ```
    *(Adjust the path to your `.csproj` file if necessary)*
4.  **Edit `Program.cs`**:
    *   Change the `processName` variable to match your target process (e.g., `"escapefromtarkov"`).
    *   Ensure `moduleToDump` is correct for your target (usually `"Assembly-CSharp"` for Unity games).
5.  **Run the project**:
    ```sh
    dotnet run
    ```

## Python Example

The Python example (`python-example.py`) uses the `pythonnet` library to interface with the .NET SDK.

### How to Run

1.  **Install `pythonnet`**:
    ```sh
    pip install pythonnet
    ```
2.  **Place Files**:
    *   Ensure `python-example.py` and `Unispect.SDK.dll` are in the same directory.
3.  **Edit `python-example.py`**:
    *   Change the `process_name` variable to match your target process.
    *   Ensure `module_to_dump` is correct.
4.  **Run the script**:
    ```sh
    python python-example.py
    ``` 