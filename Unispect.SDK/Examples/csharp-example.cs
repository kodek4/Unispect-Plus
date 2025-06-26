// To run this example, you need to reference the Unispect.SDK.dll in your project.

using System;
using System.IO;
using System.Linq;
using Unispect.SDK;

public class UnispectExample
{
    public static void Main(string[] args)
    {
        Console.WriteLine("[DEBUG] Starting example execution...");
        try
        {
            Console.WriteLine("[DEBUG] Creating Inspector instance...");
            var inspector = new Inspector();
            
            var processName = "YourGame"; 
            // The module name should NOT include the .dll extension, as the SDK looks for the simple assembly name.
            var moduleToDump = "Assembly-CSharp"; 
            
            Console.WriteLine($"[DEBUG] Using memory plugin: {nameof(BasicMemory)}");
            Console.WriteLine($"[DEBUG] Starting type dump for '{processName}/{moduleToDump}'...");

            // We pass string.Empty for the fileName to prevent the SDK from writing the file itself.
            inspector.DumpTypes(string.Empty, typeof(BasicMemory), true, processName, moduleToDump);

            Console.WriteLine("\n[SUCCESS] Dump successful!");
            
            // Now that the dump is complete, we can save the results to a file.
            var outputFileName = "dump.txt";
            Console.WriteLine($"[DEBUG] Saving results to {outputFileName}...");
            File.WriteAllLines(outputFileName, inspector.TypeDefinitions.Select(td => td.FullName));
            Console.WriteLine($"[SUCCESS] Results saved to {Path.GetFullPath(outputFileName)}");

            // --- Post-Dump Operations ---
            Console.WriteLine($"\n[INFO] Collected {inspector.TypeDefinitions.Count} type definitions.");
            
            Console.WriteLine("\n--- Example: Searching for 'Player' types ---");
            var playerTypes = inspector.SearchTypes("*Player*");
            if (playerTypes.Any())
            {
                foreach(var type in playerTypes)
                {
                    Console.WriteLine($"- Found: {type.FullName}");
                }
            }
            else
            {
                Console.WriteLine("No types found matching '*Player*'.");
            }
            
            Console.WriteLine("\n--- Example: Getting field 'm_Health' from a Player type ---");
            var firstPlayerType = playerTypes.FirstOrDefault();
            if (firstPlayerType != null)
            {
                var healthField = inspector.GetField(firstPlayerType.FullName, "m_Health");
                if (healthField != null)
                {
                    Console.WriteLine($"- Field '{healthField.Name}' in type '{firstPlayerType.FullName}' has offset: {healthField.OffsetHex}");
                }
                else
                {
                    Console.WriteLine($"Field 'm_Health' not found in '{firstPlayerType.FullName}'.");
                }
            }
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[FATAL_ERROR] An unhandled exception occurred.");
            Console.WriteLine(ex.ToString()); // Print full exception details, including stack trace
            Console.ResetColor();
            Console.WriteLine("\nPlease ensure the target game is running and you have entered the correct process name.");
        }
    }
} 