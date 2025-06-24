using System;
using Unispect.Core;

class TestMinimal
{
    static void Main()
    {
        try
        {
            Console.WriteLine("Creating UnispectEngine...");
            var engine = new UnispectEngine();
            Console.WriteLine("✓ UnispectEngine created successfully");
            
            Console.WriteLine("Testing LoadPlugins...");
            var plugins = engine.LoadPlugins();
            Console.WriteLine($"✓ Found {plugins.Count} plugins");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"✗ Error: {ex}");
        }
    }
} 