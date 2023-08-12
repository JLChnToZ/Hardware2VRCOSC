using System;
using System.IO;
using YamlDotNet.Serialization;
using OpenHardwareMonitor.Hardware;

namespace Hardware2VRCOSC {
    internal static class Program {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            var configPath = Path.Combine(Environment.CurrentDirectory, "config.yml");
            Config config;
            if (!File.Exists(configPath)) {
                Console.WriteLine("Config not found, creating one...");
                File.WriteAllText(configPath, new Serializer().Serialize(config = new Config {
                    ipAddress = "127.0.0.1",
                    port = 9000,
                    updateInterval = 1000,
                    entries = new[] {
                        new HardwareReadConfigYAML {
                            hardwareType = nameof(HardwareType.GpuNvidia),
                            sensorType = nameof(SensorType.Temperature),
                            hardwareName = "",
                            sensorName = "",
                            minValue = 0,
                            maxValue = 100,
                        },
                    },
                }));
            } else
                config = new Deserializer().Deserialize<Config>(File.ReadAllText(configPath));
            var redirector = new HardwareInfoRedirector {
                UpdateInterval = config.updateInterval,
            };
            Console.WriteLine($"Connecting to {config.ipAddress}:{config.port}");
            redirector.Connect(config.ipAddress, config.port);
            foreach (var entry in config.entries) {
                Console.WriteLine($"Adding config: {entry.hardwareName} ({entry.hardwareType}) {entry.sensorName} ({entry.sensorType}), mapped to range ({entry.minValue}~{entry.maxValue}).");
                redirector.UpdateConfig(new HardwareSensorRedirectConfig {
                    hardwareType = Enum.Parse<HardwareType>(entry.hardwareType),
                    sensorType = Enum.Parse<SensorType>(entry.sensorType),
                    hardwareName = entry.hardwareName,
                    sensorName = entry.sensorName,
                    minValue = entry.minValue,
                    maxValue = entry.maxValue,
                });
            }
        }
    }

    struct Config {
        public string ipAddress;
        public int port;
        public int updateInterval;
        public HardwareReadConfigYAML[] entries;
    }

    struct HardwareReadConfigYAML {
        public string hardwareType;
        public string sensorType;
        public string hardwareName;
        public string sensorName;
        public float minValue;
        public float maxValue;
    }
}