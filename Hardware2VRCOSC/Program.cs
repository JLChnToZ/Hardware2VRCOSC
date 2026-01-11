using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Reflection;
using YamlDotNet.Serialization;
using System.Collections.Generic;

[assembly: AssemblyProduct("Hardware2VRCOSC")]
[assembly: AssemblyTitle("Hardware2VRCOSC")]
[assembly: AssemblyDescription("Quick an dirty helper application for sending hardware real-time information (load, temperature, memory usage, date time, etc.) to VRChat via OSC.")]
[assembly: AssemblyCompany("Explosive Theorem Lab.")]
[assembly: AssemblyCopyright("Copyright © 2023-2026 Jeremy Lam (JLChnToZ). Licensed under MIT License.")]
[assembly: AssemblyFileVersion("0.1.0.0")]
[assembly: AssemblyInformationalVersion("0.1.0")]

namespace Hardware2VRCOSC {
    internal static class Program {
        const string CONFIG_FILE_NAME = "config.yml";
        static RedirectorV2? redirector;
        static FileSystemWatcher? fileWatcher;

        static void Main() {
            var mutex = new Mutex(true, "Hardware2VRCOSC", out var createdNew);
            if (!createdNew) {
                Console.WriteLine("Another instance of this program is already running.");
                return;
            }
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath))
                Environment.CurrentDirectory = Path.GetDirectoryName(processPath)!;
            bool hasConfig = TryGetConfig(out var config);
            if (OperatingSystem.IsWindows() && !config.skipAdminCheck.GetValueOrDefault() && !PrilvagesUtils.IsAdministrator()) {
                Console.WriteLine("You are running this program as a non-administrator user.");
                Console.WriteLine("If this program running in non-administrator mode, it may not be able to read some hardware information. (e.g. CPU temperature)");
                if (!string.IsNullOrEmpty(processPath))
                    while (true) {
                        Console.Write("Do you want to restart this program as administrator? (Y/N) ");
                        switch (Console.ReadKey(true).Key) {
                            case ConsoleKey.Y:
                                Console.WriteLine('Y');
                                mutex.Close();
                                try {
                                    PrilvagesUtils.RunCurrentProcessAsAdmin();
                                    return;
                                } catch (Exception ex) {
                                    Console.WriteLine($"Failed to restart as administrator: {ex.Message}");
                                }
                                Console.WriteLine("Will continue running in non-administrator mode.");
                                mutex = new Mutex(true, "Hardware2VRCOSC", out createdNew);
                                if (!createdNew) {
                                    Console.WriteLine("Another instance of this program is already running.");
                                    return;
                                }
                                goto ignoreAdmin;
                            case ConsoleKey.N:
                                Console.WriteLine('N');
                                goto ignoreAdmin;
                            default:
                                Console.WriteLine();
                                break;
                        }
                    }
            }
            ignoreAdmin:
            Console.WriteLine("Starting hardware info to VRChat OSC reporter");
            try {
                redirector = new(config);
                redirector.DefaultAddressConfigGenerated += DefaultAddressConfigGenerated;
                Console.WriteLine("Hint: Please edit config.yml to change the behavior of this program.");
            } catch (Exception ex) {
                Console.WriteLine(ex);
            }
            fileWatcher = new FileSystemWatcher(Environment.CurrentDirectory, CONFIG_FILE_NAME) {
                NotifyFilter = NotifyFilters.Attributes |
                                NotifyFilters.CreationTime |
                                NotifyFilters.DirectoryName |
                                NotifyFilters.FileName |
                                NotifyFilters.LastAccess |
                                NotifyFilters.LastWrite |
                                NotifyFilters.Security |
                                NotifyFilters.Size,
                EnableRaisingEvents = true,
            };
            fileWatcher.Changed += OnConfigChanged;
        }

        private static void OnConfigChanged(object sender, FileSystemEventArgs e) {
            Thread.Sleep(100);
            Console.WriteLine("Config changed, reloading...");
            try {
                _ = TryGetConfig(out var config);
                if (redirector == null)
                    redirector = new(config);
                else
                    redirector.Config = config;
            } catch (Exception ex) {
                Console.WriteLine(ex);
            }
        }

        private static void DefaultAddressConfigGenerated(Dictionary<string, string> config) {
            if (TryGetConfig(out var currentConfig)) {
                if (currentConfig.addresses == null || currentConfig.addresses.Count == 0) {
                    currentConfig.addresses = config;
                    WriteConfig(currentConfig);
                    Console.WriteLine("Default address config generated and saved to config.yml.");
                } else {
                    Console.WriteLine("Default address config already exists, skipping generation.");
                }
            }
        }

        static bool TryGetConfig(out ConfigV2 config) {
            var configPath = Path.Combine(Environment.CurrentDirectory, CONFIG_FILE_NAME);
            if (!File.Exists(configPath)) {
                Console.WriteLine("Config not found, creating one...");
                config = redirector?.Config ?? new ConfigV2 {
                    skipAdminCheck = false,
                    ipAddress = "127.0.0.1",
                    port = 9000,
                    updateInterval = 1000,
                };
                WriteConfig(config);
                return false;
            } else {
                using var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var reader = new StreamReader(stream, Encoding.UTF8);
                config = new Deserializer().Deserialize<ConfigV2>(reader);
                return true;
            }
        }

        static void WriteConfig(ConfigV2 config) {
            if (fileWatcher != null) fileWatcher.EnableRaisingEvents = false;
            try {
                var configPath = Path.Combine(Environment.CurrentDirectory, CONFIG_FILE_NAME);
                using var stream = new FileStream(configPath, FileMode.Create, FileAccess.Write, FileShare.None);
                using var writer = new StreamWriter(stream, Encoding.UTF8);
                new SerializerBuilder()
                    .DisableAliases()
                    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                    .Build()
                    .Serialize(writer, config);
            } finally {
                if (fileWatcher != null) fileWatcher.EnableRaisingEvents = true;
            }
        }
    }
}