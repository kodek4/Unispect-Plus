# To run this example, you need to have the 'pythonnet' package installed.
# You can install it via pip: pip install pythonnet

import sys
import os
# Add the path to the Unispect.SDK.dll to the system path
# Adjust this path to where your DLL is located.
# For example, if your script is in 'Examples' and the DLL is in 'bin/Debug/net9.0-windows'
sdk_path = os.path.abspath(os.path.join(os.path.dirname(__file__), '..', 'bin', 'Debug', 'net9.0-windows'))
sys.path.append(sdk_path)

# It's important to add the path BEFORE importing clr
import clr

# Load the Unispect.SDK assembly
clr.AddReference("Unispect.SDK")

# Import the necessary classes from the SDK
from Unispect.SDK import Inspector, BasicMemory, Log

def on_progress_changed(sender, progress):
    """Callback function for progress updates."""
    print(f"[PROGRESS] {progress:.0%}")

def on_log_message_added(sender, message):
    """Callback function for log messages."""
    print(f"[LOG] {message}")

def main():
    """Main function to run the Unispect SDK from Python."""
    # 1. Initialize the Inspector
    inspector = Inspector()
    
    # 2. (Optional) Subscribe to progress updates
    inspector.ProgressChanged += on_progress_changed
    
    # Optional: Subscribe to log messages
    Log.LogMessageAdded += on_log_message_added

    try:
        # 3. Define the parameters for the dump
        output_filename = "dump_python.txt"
        memory_proxy_type = BasicMemory # Pass the type object directly
        process_name = "YourGame" # <-- IMPORTANT: Change this to your target game's process name
        module_to_dump = "Assembly-CSharp" # <-- IMPORTANT: Do NOT include the .dll extension

        print(f"Starting type dump for '{process_name}/{module_to_dump}'...")
        
        # 4. Execute the dump
        inspector.DumpTypes(output_filename, memory_proxy_type, True, process_name, module_to_dump)
        
        print("Dump successful!")
        print(f"Results saved to {os.path.abspath(output_filename)}")

        # 5. You can now access the collected type definitions
        print(f"\nCollected {inspector.TypeDefinitions.Count} type definitions.")
        
        # Example: Print the first 10 types and their field counts
        print("\n--- Example: First 10 Types ---")
        # In pythonnet, we need to handle collections carefully
        type_definitions = list(inspector.TypeDefinitions)
        for i in range(min(10, len(type_definitions))):
            type_def = type_definitions[i]
            print(f"- {type_def.FullName} (Fields: {type_def.Fields.Count})")

        # 6. Example 2: Search for a specific type
        print("\n--- Example: Searching for 'Player' types ---")
        player_types = list(inspector.SearchTypes("*Player*"))
        if player_types:
            for type_def in player_types:
                print(f"- Found: {type_def.FullName}")
        else:
            print("No types found matching '*Player*'.")

        # 7. Example 3: Get a specific field from a type
        print("\n--- Example: Getting field 'm_Health' from a Player type ---")
        if player_types:
            first_player_type_name = player_types[0].FullName
            health_field = inspector.GetField(first_player_type_name, "m_Health")
            if health_field:
                print(f"- Field '{health_field.Name}' in type '{first_player_type_name}' has offset: {health_field.OffsetHex}")
            else:
                print(f"Field 'm_Health' not found in '{first_player_type_name}'.")

    except Exception as ex:
        print(f"\n[ERROR] An error occurred: {ex}")
        print("Please ensure the target game is running and you have entered the correct process name.")

if __name__ == "__main__":
    main() 