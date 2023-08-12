using System;
using System.IO;
using YamlDotNet.Serialization;

namespace Hardware2VRCOSC {
    internal static class Program {
        const string CONFIG_FILE_NAME = "config.yml";
        static HardwareInfoRedirector redirector;

        [STAThread]
        static void Main() {
            Console.WriteLine("Starting hardware info to VRChat OSC reporter");
            try {
                redirector = new HardwareInfoRedirector(GetConfig());
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
                using (var stringWriter = new StreamWriter(stream, System.Text.Encoding.UTF8))
                    new Serializer().Serialize(stringWriter, config);
            } else
                using (var stream = new FileStream(configPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (var stringReader = new StreamReader(stream, System.Text.Encoding.UTF8))
                    config = new Deserializer().Deserialize<Config>(stringReader);
            return config;
        }
    }
}