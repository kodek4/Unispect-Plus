using System;
using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Threading.Tasks;
using Unispect.CLI.Commands;
using Unispect.CLI.Helpers;

namespace Unispect.CLI
{
    class Program
    {
        public static bool SuppressSdkLogs { get; set; } = false;

        static async Task<int> Main(string[] args)
        {
            SDK.Log.LogMessageAdded += (sender, e) =>
            {
                if (SuppressSdkLogs)
                    return; // Skip console output while a progress bar is active

                // Clear any in-progress progress-bar line so the SDK log appears cleanly.
                if (!Console.IsOutputRedirected)
                {
                    var clearLen = 120; // bar width + status text buffer
                    Console.Out.Write("\r" + new string(' ', clearLen) + "\r");
                }

                var originalColor = Console.ForegroundColor;
                Console.ForegroundColor = e.Type switch
                {
                    SDK.Log.MessageType.Warning => ConsoleColor.Yellow,
                    SDK.Log.MessageType.Error => ConsoleColor.Red,
                    SDK.Log.MessageType.Exception => ConsoleColor.Red,
                    _ => originalColor
                };
                Console.WriteLine(e.Message);
                Console.ForegroundColor = originalColor;
            };

            // Show banner for interactive usage (when no args provided)
            if (args.Length == 0)
            {
                ConsoleFormatting.ShowBanner();
                Console.WriteLine("Use --help to see available commands.");
                Console.WriteLine();
                return 0;
            }

            var rootCommand = new RootCommand("Unispect CLI - Unity Memory Inspector")
            {
                CreateDumpCommand(),
                CreateSearchCommand(),
                CreateQueryCommand(),
                CreateCacheCommand(),
                CreateValidateCommand(),
                CreateCompareCommand(),
                CreateStatsCommand()
            };

            var builder = new CommandLineBuilder(rootCommand);
            builder.UseDefaults(); // This enables --help, --version, and other standard features

            var parser = builder.Build();
            return await parser.InvokeAsync(args);
        }

        static Command CreateDumpCommand()
        {
            var processOption = new Option<string>("--process", "Target process name") { IsRequired = true };
            var pluginOption = new Option<string>("--plugin", "Memory plugin to use") { IsRequired = false };
            var outputOption = new Option<string>("--output", "Output file path (optional)");
            var formatOption = new Option<string>("--format", () => "text", "Output format: text, json, utd, csharp-intptr, csharp-ulong");
            var refreshOption = new Option<bool>("--refresh", "Force refresh cache");
            var verboseOption = new Option<bool>("--verbose", () => true, "Verbose output");

            var command = new Command("dump", "Dump type definitions from target process")
            {
                processOption,
                pluginOption,
                outputOption,
                formatOption,
                refreshOption,
                verboseOption
            };

            command.SetHandler((context) =>
            {
                var process = context.ParseResult.GetValueForOption(processOption);
                if (string.IsNullOrEmpty(process))
                {
                    Console.WriteLine("Error: --process is required.");
                    context.ExitCode = 1;
                    return;
                }
                
                var plugin = context.ParseResult.GetValueForOption(pluginOption);
                var output = context.ParseResult.GetValueForOption(outputOption);
                var format = context.ParseResult.GetValueForOption(formatOption);
                var refresh = context.ParseResult.GetValueForOption(refreshOption);
                var verbose = context.ParseResult.GetValueForOption(verboseOption);

                Dump.HandleDumpCommand(process, plugin, output, format, refresh, verbose);
            });

            return command;
        }

        static Command CreateSearchCommand()
        {
            var processOption = new Option<string>("--process", "Target process name") { IsRequired = true };
            var patternOption = new Option<string>("--pattern", "Search pattern") { IsRequired = true };
            var regexOption = new Option<bool>("--regex", "Use regex pattern matching");
            var typeOption = new Option<string>("--type", "Search type: all, types, fields");
            var offsetRangeOption = new Option<string>("--offset-range", "Offset range (e.g., 0x10-0x50)");
            var limitOption = new Option<int>("--limit", () => 50, "Maximum results to show");
            
            // Advanced search options
            var includeParentOption = new Option<bool>("--include-parent", "Include parent class names in search");
            var includeInterfacesOption = new Option<bool>("--include-interfaces", "Include interface names in search");
            var excludeSystemOption = new Option<bool>("--exclude-system", "Exclude system types (System.*, UnityEngine.*)");
            var minFieldsOption = new Option<int>("--min-fields", "Minimum number of fields");
            var maxFieldsOption = new Option<int>("--max-fields", "Maximum number of fields");

            var command = new Command("search", "Search through cached type definitions")
            {
                processOption,
                patternOption,
                regexOption,
                typeOption,
                offsetRangeOption,
                limitOption,
                includeParentOption,
                includeInterfacesOption,
                excludeSystemOption,
                minFieldsOption,
                maxFieldsOption
            };

            command.SetHandler((context) =>
            {
                var process = context.ParseResult.GetValueForOption(processOption);
                var pattern = context.ParseResult.GetValueForOption(patternOption);

                if (string.IsNullOrEmpty(process) || string.IsNullOrEmpty(pattern))
                {
                    Console.WriteLine("Error: --process and --pattern are required.");
                    context.ExitCode = 1;
                    return;
                }
                
                var regex = context.ParseResult.GetValueForOption(regexOption);
                var type = context.ParseResult.GetValueForOption(typeOption);
                var offsetRange = context.ParseResult.GetValueForOption(offsetRangeOption);
                var limit = context.ParseResult.GetValueForOption(limitOption);
                var includeParent = context.ParseResult.GetValueForOption(includeParentOption);
                var includeInterfaces = context.ParseResult.GetValueForOption(includeInterfacesOption);
                var excludeSystem = context.ParseResult.GetValueForOption(excludeSystemOption);
                var minFields = context.ParseResult.GetValueForOption(minFieldsOption);
                var maxFields = context.ParseResult.GetValueForOption(maxFieldsOption);

                Search.HandleSearchCommand(process, pattern, regex, type, offsetRange, limit, 
                    includeParent, includeInterfaces, excludeSystem, minFields, maxFields);
            });

            return command;
        }

        static Command CreateQueryCommand()
        {
            var processOption = new Option<string>("--process", "Target process name") { IsRequired = true };
            var queryOption = new Option<string>("--query", "Query (e.g., 'Player.health')") { IsRequired = true };
            var formatOption = new Option<string>("--format", () => "full", "Output format: full, offset-only, type-only");

            var command = new Command("query", "Query specific type or field")
            {
                processOption,
                queryOption,
                formatOption
            };

            command.SetHandler((context) =>
            {
                var process = context.ParseResult.GetValueForOption(processOption);
                var query = context.ParseResult.GetValueForOption(queryOption);
                
                if (string.IsNullOrEmpty(process) || string.IsNullOrEmpty(query))
                {
                    Console.WriteLine("Error: --process and --query are required.");
                    context.ExitCode = 1;
                    return;
                }

                var format = context.ParseResult.GetValueForOption(formatOption);

                Query.HandleQueryCommand(process, query, format);
            });

            return command;
        }

        static Command CreateCacheCommand()
        {
            var listCommand = new Command("list", "List cached type definitions");
            listCommand.SetHandler(() => Cache.HandleListCommand());

            var infoCommand = new Command("info", "Show cache information");
            var processOption = new Option<string>("--process", "Process name") { IsRequired = true };
            infoCommand.AddOption(processOption);
            infoCommand.SetHandler((string process) => Cache.HandleInfoCommand(process), processOption);

            var clearCommand = new Command("clear", "Clear cache");
            var clearProcessOption = new Option<string>("--process", "Process name (or 'all')") { IsRequired = true };
            clearCommand.AddOption(clearProcessOption);
            clearCommand.SetHandler((string process) => Cache.HandleClearCommand(process), clearProcessOption);

            var command = new Command("cache", "Cache management commands")
            {
                listCommand,
                infoCommand,
                clearCommand
            };

            return command;
        }

        static Command CreateValidateCommand()
        {
            var processOption = new Option<string>("--process", "Target process name") { IsRequired = true };
            var fixOption = new Option<bool>("--fix", "Attempt to fix issues");

            var command = new Command("validate", "Validate cache integrity")
            {
                processOption,
                fixOption
            };

            command.SetHandler((context) =>
            {
                var process = context.ParseResult.GetValueForOption(processOption);
                if (string.IsNullOrEmpty(process))
                {
                    Console.WriteLine("Error: --process is required.");
                    context.ExitCode = 1;
                    return;
                }
                var fix = context.ParseResult.GetValueForOption(fixOption);

                Validate.HandleValidateCommand(process, fix);
            });

            return command;
        }

        static Command CreateCompareCommand()
        {
            var process1Option = new Option<string>("--process1", "First process name") { IsRequired = true };
            var process2Option = new Option<string>("--process2", "Second process name") { IsRequired = true };
            var outputOption = new Option<string>("--output", "Output file path (optional)");
            var formatOption = new Option<string>("--format", () => "text", "Output format: text, json");

            var command = new Command("compare", "Compare two cached dumps")
            {
                process1Option,
                process2Option,
                outputOption,
                formatOption
            };

            command.SetHandler((context) =>
            {
                var process1 = context.ParseResult.GetValueForOption(process1Option);
                var process2 = context.ParseResult.GetValueForOption(process2Option);

                if (string.IsNullOrEmpty(process1) || string.IsNullOrEmpty(process2))
                {
                    Console.WriteLine("Error: --process1 and --process2 are required.");
                    context.ExitCode = 1;
                    return;
                }

                var output = context.ParseResult.GetValueForOption(outputOption);
                var format = context.ParseResult.GetValueForOption(formatOption);

                Compare.HandleCompareCommand(process1, process2, output, format);
            });

            return command;
        }

        static Command CreateStatsCommand()
        {
            var processOption = new Option<string>("--process", "Target process name") { IsRequired = true };
            var outputOption = new Option<string>("--output", "Output file path (optional)");
            var formatOption = new Option<string>("--format", () => "text", "Output format: text, json");
            var detailedOption = new Option<bool>("--detailed", "Show detailed stats");

            var command = new Command("stats", "Show statistics about a cached dump")
            {
                processOption,
                outputOption,
                formatOption,
                detailedOption
            };

            command.SetHandler((context) =>
            {
                var process = context.ParseResult.GetValueForOption(processOption);
                if (string.IsNullOrEmpty(process))
                {
                    Console.WriteLine("Error: --process is required.");
                    context.ExitCode = 1;
                    return;
                }
                var outputPath = context.ParseResult.GetValueForOption(outputOption);
                var format = context.ParseResult.GetValueForOption(formatOption);
                var detailed = context.ParseResult.GetValueForOption(detailedOption);

                Stats.HandleStatsCommand(process, outputPath, format, detailed);
            });

            return command;
        }
    }
}