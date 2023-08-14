using System;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using System.Net.Sockets;
using OscCore;
using OpenHardwareMonitor.Hardware;
using DotNet.Globbing;

namespace Hardware2VRCOSC {
    internal class HardwareInfoRedirector : IDisposable {
        const string PREFIX = "/hardwares";
        readonly HashSet<HardwareType> watchingHardwares = new();
        readonly Computer computer;
        readonly HashSet<IHardware> hardwares = new();
        readonly HashSet<ISensor> sensors = new();
        readonly Thread readThread;
        readonly Dictionary<Glob, PatternConfig> patternConfigs = new();
        readonly Dictionary<ISensor, PatternConfig> mappedConfigs = new();
        UdpClient? udpClient;
        bool isDisposed;

        public int UpdateInterval { get; set; }

        public string IP { get; set; }

        public int Port { get; set; }

        public bool RAMEnabled { get => computer.RAMEnabled; set => computer.RAMEnabled = value; }

        public bool MainboardEnabled { get => computer.MainboardEnabled; set => computer.MainboardEnabled = value; }

        public bool CPUEnabled { get => computer.CPUEnabled; set => computer.CPUEnabled = value; }

        public bool GPUEnabled { get => computer.GPUEnabled; set => computer.GPUEnabled = value; }

        public bool HDDEnabled { get => computer.HDDEnabled; set => computer.HDDEnabled = value; }

        public bool FanControllerEnabled { get => computer.FanControllerEnabled; set => computer.FanControllerEnabled = value; }

        public bool NetworkEnabled { get => computer.NetworkEnabled; set => computer.NetworkEnabled = value; }

        public HardwareInfoRedirector() : this(Config.defaultConfig) {}

        public HardwareInfoRedirector(Config config) {
            UpdateInterval = config.updateInterval;
            IP = config.ipAddress;
            Port = config.port;
            computer = new Computer {
                RAMEnabled = config.ram,
                MainboardEnabled = config.mainboard,
                CPUEnabled = config.cpu,
                GPUEnabled = config.gpu,
                HDDEnabled = config.hdd,
                FanControllerEnabled = config.fanController,
                NetworkEnabled = config.network,
            };
            SetPatternConfig(config.patternConfigs);
            computer.HardwareAdded += OnHardwareAdded;
            computer.HardwareRemoved += OnHardwareRemoved;
            computer.Open();
            foreach (var hardaware in computer.Hardware) OnHardwareAdded(hardaware);
            readThread = new Thread(ReadHardware);
            readThread.Start();
            if (!string.IsNullOrEmpty(config.ipAddress) && config.port > 0) Connect();
        }

        public void UpdateConfig(Config config) {
            if (isDisposed) throw new ObjectDisposedException(nameof(HardwareInfoRedirector));
            computer.RAMEnabled = config.ram;
            computer.MainboardEnabled = config.mainboard;
            computer.CPUEnabled = config.cpu;
            computer.GPUEnabled = config.gpu;
            computer.HDDEnabled = config.hdd;
            computer.FanControllerEnabled = config.fanController;
            computer.NetworkEnabled = config.network;
            UpdateInterval = config.updateInterval;
            patternConfigs.Clear();
            mappedConfigs.Clear();
            SetPatternConfig(config.patternConfigs);
            if (IP != config.ipAddress || Port != config.port) {
                Disconnect();
                IP = config.ipAddress;
                Port = config.port;
                if (!string.IsNullOrEmpty(config.ipAddress) && config.port > 0) Connect();
            } 
        }

        void SetPatternConfig(Dictionary<string, PatternConfig> configs) {
            if (configs == null) return;
            foreach (var (pattern, config) in configs) {
                if (string.IsNullOrEmpty(pattern)) continue;
                patternConfigs.Add(Glob.Parse(pattern), config);
            }
        }

        public void Connect() {
            if (isDisposed) throw new ObjectDisposedException(nameof(HardwareInfoRedirector));
            Console.WriteLine($"Connecting to {IP}:{Port}");
            udpClient = new UdpClient(IP, Port);
        }

        public void Disconnect() {
            Console.WriteLine($"Disconnecting from {IP}:{Port}");
            udpClient?.Close();
            udpClient = null;
        }

        public void Dispose() {
            isDisposed = true;
            udpClient?.Close();
            udpClient = null;
            computer?.Close();
            sensors.Clear();
            hardwares.Clear();
            mappedConfigs.Clear();
        }

        void OnHardwareAdded(IHardware hardware) {
            if (!hardwares.Add(hardware)) return;
            Console.WriteLine($"Hardware watched: <{hardware.HardwareType}> {hardware.Name}");
            hardware.SensorAdded += OnSensorAdded;
            hardware.SensorRemoved += OnSensorRemoved;
            foreach (var sensor in hardware.Sensors) OnSensorAdded(sensor);
        }

        void OnHardwareRemoved(IHardware hardware) {
            if (hardwares.Remove(hardware))
                Console.WriteLine($"Hardware unwatched: <{hardware.HardwareType}> {hardware.Name}");
            hardware.SensorAdded -= OnSensorAdded;
            hardware.SensorRemoved -= OnSensorRemoved;
            foreach (var sensor in hardware.Sensors) OnSensorRemoved(sensor);
        }

        void OnSensorAdded(ISensor sensor) {
            if (!sensors.Add(sensor)) return;
            Console.WriteLine($"Sensor watched: <{sensor.SensorType}> {sensor.Name}");
            Console.WriteLine("Available OSC channels:");
            var channel = $"{PREFIX}{sensor.Identifier}";
            Console.WriteLine($"> {channel}");
            string unit = sensor.SensorType switch {
                SensorType.Voltage => "V",
                SensorType.Clock => "MHz",
                SensorType.Temperature => "°C",
                SensorType.Load or SensorType.Control or SensorType.Level => "%",
                SensorType.Fan => "RPM",
                SensorType.Flow => "L/h",
                SensorType.Power => "W",
                SensorType.Data => "GB",
                SensorType.SmallData => "MB",
                SensorType.Throughput => "MB/s",
                SensorType.TimeSpan => "s",
                _ => "",
            };
            if (!string.IsNullOrEmpty(unit)) Console.WriteLine($"  Unit: {unit}");
            var pattern = GetPattern(sensor);
            if (pattern.ignore.GetValueOrDefault(false)) {
                Console.WriteLine("  Ignored: This channel will not send any OSC message.");
            } else if (pattern.min.HasValue && pattern.max.HasValue) {
                Console.WriteLine($"  Range: {pattern.min}{unit} - {pattern.max}{unit} (Will remapped to 0.0 - 1.0)");
            } else if (pattern.min.HasValue) {
                Console.WriteLine($"  Min Value: {pattern.min}{unit}");
            } else if (pattern.max.HasValue) {
                Console.WriteLine($"  Max Value: {pattern.max}{unit}");
            }
        }

        void OnSensorRemoved(ISensor sensor) {
            if (sensors.Remove(sensor))
                Console.WriteLine($"Sensor unwatched: <{sensor.SensorType}> {sensor.Name}");
            mappedConfigs.Remove(sensor);
        }

        void ReadHardware() {
            while (!isDisposed) {
                try {
                    if (udpClient != null) {
                        foreach (var hardware in hardwares)
                            hardware.Update();
                        foreach (var sensor in sensors) {
                            var channel = $"{PREFIX}{sensor.Identifier}";
                            var matchedPattern = GetPattern(sensor);
                            if (matchedPattern.ignore.GetValueOrDefault(false)) continue;
                            var sensorValue = sensor.Value;
                            if (sensorValue.HasValue) {
                                var value = (float)sensorValue.Value;
                                if (matchedPattern.min.HasValue && value < matchedPattern.min.Value)
                                    value = matchedPattern.min.Value;
                                else if (matchedPattern.max.HasValue && value > matchedPattern.max.Value)
                                    value = matchedPattern.max.Value;
                                else if (matchedPattern.min.HasValue && matchedPattern.max.HasValue)
                                    value = (value - matchedPattern.min.Value) / (matchedPattern.max.Value - matchedPattern.min.Value);
                                udpClient.Send(new OscMessage(channel, value).ToByteArray());
                            }
                        }
                    }
                } catch (Exception e) {
                    Console.WriteLine(e);
                }
                Thread.Sleep(UpdateInterval);
            }
        }

        PatternConfig GetPattern(ISensor sensor) {
            if (mappedConfigs.TryGetValue(sensor, out var config)) return config;
            var channel = $"{PREFIX}{sensor.Identifier}";
            PatternConfig matchedConfig = default;
            foreach (var (glob, pattern) in patternConfigs)
                if (glob.IsMatch(channel)) {
                    if (pattern.ignore.HasValue) matchedConfig.ignore = pattern.ignore;
                    if (pattern.min.HasValue) matchedConfig.min = pattern.min;
                    if (pattern.max.HasValue) matchedConfig.max = pattern.max;
                }
            mappedConfigs.Add(sensor, matchedConfig);
            return matchedConfig;
        }
    }
}
