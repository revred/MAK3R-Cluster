using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.CommandLine;
using System.Text.Json;
using Mak3r.Edge.Services;
using Mak3r.Edge.Models;

namespace Mak3r.Edge.Tools;

/// <summary>
/// Command-line tool for validating Edge configurations
/// Usage: MAK3R.Edge.exe validate-config --edge-config path/to/edge-config.json --machines-config path/to/machines.json
/// </summary>
public static class ConfigValidator
{
    public static Command CreateValidateCommand()
    {
        var edgeConfigOption = new Option<FileInfo?>(
            name: "--edge-config",
            description: "Path to edge configuration file");

        var machinesConfigOption = new Option<FileInfo?>(
            name: "--machines-config", 
            description: "Path to machines configuration file");

        var verboseOption = new Option<bool>(
            name: "--verbose",
            description: "Enable verbose output");

        var command = new Command("validate-config", "Validate Edge configuration files")
        {
            edgeConfigOption,
            machinesConfigOption,
            verboseOption
        };

        command.SetHandler(async (edgeConfigFile, machinesConfigFile, verbose) =>
        {
            await ValidateConfigAsync(edgeConfigFile, machinesConfigFile, verbose);
        }, edgeConfigOption, machinesConfigOption, verboseOption);

        return command;
    }

    private static async Task ValidateConfigAsync(FileInfo? edgeConfigFile, FileInfo? machinesConfigFile, bool verbose)
    {
        var services = new ServiceCollection()
            .AddLogging(builder => builder.AddConsole().SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Information))
            .AddSingleton<ConfigValidationService>()
            .BuildServiceProvider();

        var validator = services.GetRequiredService<ConfigValidationService>();
        var logger = services.GetRequiredService<ILogger<ConfigValidationService>>();

        bool allValid = true;

        Console.WriteLine("=== MAK3R Edge Configuration Validator ===\n");

        // Validate edge configuration
        if (edgeConfigFile != null)
        {
            allValid &= await ValidateEdgeConfigFile(validator, logger, edgeConfigFile, verbose);
        }

        // Validate machines configuration
        if (machinesConfigFile != null)
        {
            allValid &= await ValidateMachinesConfigFile(validator, logger, machinesConfigFile, verbose);
        }

        // Cross-validation if both files provided
        if (edgeConfigFile != null && machinesConfigFile != null)
        {
            allValid &= await CrossValidateConfigs(validator, logger, edgeConfigFile, machinesConfigFile, verbose);
        }

        Console.WriteLine("\n=== Validation Summary ===");
        if (allValid)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ All configurations are valid!");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("❌ Configuration validation failed!");
            Environment.Exit(1);
        }
        Console.ResetColor();
    }

    private static async Task<bool> ValidateEdgeConfigFile(ConfigValidationService validator, ILogger logger, FileInfo file, bool verbose)
    {
        Console.WriteLine($"Validating Edge Configuration: {file.FullName}");

        if (!file.Exists)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ File not found: {file.FullName}");
            Console.ResetColor();
            return false;
        }

        try
        {
            var configJson = await File.ReadAllTextAsync(file.FullName);
            var result = validator.ValidateJsonConfiguration(configJson, "edge");

            if (result.IsValid)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ Edge configuration is valid");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Edge configuration has errors:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"   • {error}");
                }
            }

            if (result.Warnings.Any())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️  Edge configuration warnings:");
                foreach (var warning in result.Warnings)
                {
                    Console.WriteLine($"   • {warning}");
                }
            }

            Console.ResetColor();
            return result.IsValid;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Error reading edge configuration: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    private static async Task<bool> ValidateMachinesConfigFile(ConfigValidationService validator, ILogger logger, FileInfo file, bool verbose)
    {
        Console.WriteLine($"\nValidating Machines Configuration: {file.FullName}");

        if (!file.Exists)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ File not found: {file.FullName}");
            Console.ResetColor();
            return false;
        }

        try
        {
            var configJson = await File.ReadAllTextAsync(file.FullName);
            var result = validator.ValidateJsonConfiguration(configJson, "machines");

            if (result.IsValid)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ Machines configuration is valid");

                if (verbose)
                {
                    var config = JsonSerializer.Deserialize<MachinesConfigWrapper>(configJson);
                    Console.WriteLine($"   📊 Found {config?.Machines.Count} machine(s) configured:");
                    foreach (var machine in config?.Machines ?? new List<EdgeConnectorConfig>())
                    {
                        var status = machine.Enabled ? "🟢" : "🔴";
                        Console.WriteLine($"   {status} {machine.MachineId} ({machine.Make} - {machine.Protocol})");
                    }
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Machines configuration has errors:");
                foreach (var error in result.Errors)
                {
                    Console.WriteLine($"   • {error}");
                }
            }

            if (result.Warnings.Any())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️  Machines configuration warnings:");
                foreach (var warning in result.Warnings)
                {
                    Console.WriteLine($"   • {warning}");
                }
            }

            Console.ResetColor();
            return result.IsValid;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Error reading machines configuration: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    private static async Task<bool> CrossValidateConfigs(ConfigValidationService validator, ILogger logger, FileInfo edgeFile, FileInfo machinesFile, bool verbose)
    {
        Console.WriteLine("\nPerforming cross-validation checks...");

        try
        {
            var edgeConfigJson = await File.ReadAllTextAsync(edgeFile.FullName);
            var machinesConfigJson = await File.ReadAllTextAsync(machinesFile.FullName);

            var edgeConfig = JsonSerializer.Deserialize<EdgeConfig>(edgeConfigJson);
            var machinesWrapper = JsonSerializer.Deserialize<MachinesConfigWrapper>(machinesConfigJson);

            var warnings = new List<string>();
            var errors = new List<string>();

            // Check if load generator settings align with actual machine count
            if (edgeConfig?.LoadGen.Enabled == true && machinesWrapper?.Machines != null)
            {
                var actualMachineCount = machinesWrapper.Machines.Count(m => m.Enabled);
                if (edgeConfig.LoadGen.Machines != actualMachineCount)
                {
                    warnings.Add($"Load generator configured for {edgeConfig.LoadGen.Machines} machines, but {actualMachineCount} machines are enabled");
                }
            }

            // Check queue capacity vs expected load
            if (edgeConfig != null && machinesWrapper?.Machines != null)
            {
                var enabledMachines = machinesWrapper.Machines.Count(m => m.Enabled);
                var estimatedEventsPerSecond = enabledMachines * 2; // rough estimate
                var queueTimeSeconds = edgeConfig.Queue.Capacity / Math.Max(1, estimatedEventsPerSecond);
                
                if (queueTimeSeconds < 60)
                {
                    warnings.Add($"Queue capacity may be too small for {enabledMachines} machines (estimated {queueTimeSeconds:F0} seconds buffer)");
                }
            }

            // Report cross-validation results
            if (errors.Any())
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("❌ Cross-validation errors:");
                foreach (var error in errors)
                {
                    Console.WriteLine($"   • {error}");
                }
                Console.ResetColor();
                return false;
            }

            if (warnings.Any())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("⚠️  Cross-validation warnings:");
                foreach (var warning in warnings)
                {
                    Console.WriteLine($"   • {warning}");
                }
                Console.ResetColor();
            }

            if (!warnings.Any() && !errors.Any())
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("✅ Cross-validation passed");
                Console.ResetColor();
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Cross-validation error: {ex.Message}");
            Console.ResetColor();
            return false;
        }
    }

    private class MachinesConfigWrapper
    {
        public List<EdgeConnectorConfig> Machines { get; set; } = new();
    }
}