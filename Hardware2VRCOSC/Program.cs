using System;
using System.IO;
using System.Threading;
using System.Security.Principal;
using System.Text;
using System.Reflection;
using System.Diagnostics;
using YamlDotNet.Serialization;

namespace Hardware2VRCOSC {
    internal static class Program {
        const string CONFIG_FILE_NAME = "config.yml";
        static HardwareInfoRedirector redirector;

        static void Main() { 
            var mutex = new Mutex(true, "Hardware2VRCOSC", out var createdNew);
            if (!createdNew) {
                Console.WriteLine("Another instance of this program is already running.");
                return;
            }
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath))
                Environment.CurrentDirectory = Path.GetDirectoryName(processPath);
            var config = GetConfig();
            if (!config.skipAdminCheck.GetValueOrDefault() && !IsAdministrator()) {
                Console.WriteLine("You are running this program as a non-administrator user.");
                Console.WriteLine("If this program running in non-administrator mode, it may not be able to read some hardware information. (e.g. CPU temperature)");
                if (!string.IsNullOrEmpty(processPath)) {
                    while (true) {
                        Console.Write("Do you want to restart this program as administrator? (Y/N) ");
                        var key = Console.ReadKey(true);
                        switch (key.Key) {
                            case ConsoleKey.Y:
                                Console.WriteLine('Y');
                                mutex.Close();
                                Process.Start(new ProcessStartInfo(processPath, Environment.CommandLine) {
                                    UseShellExecute = true,
                                    WorkingDirectory = Environment.CurrentDirectory,
                                    Verb = "runas",
                                });
                                return;
                            case ConsoleKey.N:
                                Console.WriteLine('N');
                                goto ignoreAdmin;
                            default:
                                Console.WriteLine();
                                break;
                        }
                    }
                }
            }
            ignoreAdmin:
            Console.WriteLine("Starting hardware info to VRChat OSC reporter");
            try {
                redirector = new HardwareInfoRedirector(config);
                Console.WriteLine("Hint: You can edit config.yml to change the behavior of this program.");
            } catch (Exception ex) {
                Console.WriteLine(ex);
            }
            var fileWatcher = new FileSystemWatcher(Environment.CurrentDirectory, CONFIG_FILE_NAME) {
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
                if (redirector == null)
                    redirector = new HardwareInfoRedirector(GetConfig());
                else
                    redirector.UpdateConfig(GetConfig());
            } catch (Exception ex) {
                Console.WriteLine(ex);
            }
        }

        static Config GetConfig() {
            var configPath = Path.Combine(Environment.CurrentDirectory, CONFIG_FILE_NAME);
            Config config;
            if (!File.Exists(configPath)) {
                Console.WriteLine("Config not found, creating one...");
                config = Config.defaultConfig;
                using (var stream = new FileStream(configPath, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, Encoding.UTF8))
                    new SerializerBuilder()
                    .DisableAliases()
                    .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull)
                    .Build()
                    .Serialize(writer, config);
            } else
                using (var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var reader = new StreamReader(stream, Encoding.UTF8))
                    config = new Deserializer().Deserialize<Config>(reader);
            return config;
        }

        static bool IsAdministrator() => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    }
}