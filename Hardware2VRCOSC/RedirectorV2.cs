using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Threading;
using System.Net.Sockets;
using OscCore;
using OscCore.LowLevel;
using LibreHardwareMonitor.Hardware;
using MathUtilities;

using ExprToken = MathUtilities.AbstractMathEvalulator<double>.Token;
using System.Diagnostics;

namespace Hardware2VRCOSC {
    public class RedirectorV2 : IDisposable {
        private static readonly Regex variableNameIllegalChars = new(@"[^a-zA-Z0-9_.]+", RegexOptions.Compiled);
        private readonly object[] arguments = new object[1];
        private readonly MemoryStream bufferStream = new();
        private readonly MathEvalulator evalulator = new();
        private readonly Computer computer;
        private readonly Thread readThread;
        private ConfigV2 config;
        private UdpClient? udpClient;
        private readonly Dictionary<string, ExprToken[]> addressExpressions = new();
        private readonly HashSet<IHardware> hardwares = new();
        private readonly Dictionary<ISensor, string> sensors = new();
        private readonly Dictionary<string, ISensor> invSensors = new();
        private readonly HashSet<string> requiredSources = new();
        private bool isDetectionMode, isDisposed;

        public event Action<Dictionary<string, string>>? DefaultAddressConfigGenerated;

        public ConfigV2 Config {
            get => config;
            set {
                if (isDisposed) throw new ObjectDisposedException(nameof(RedirectorV2));
                bool shouldConnect = false;
                if (udpClient != null && (config.ipAddress != value.ipAddress || config.port != value.port)) {
                    Disconnect();
                    shouldConnect = !string.IsNullOrEmpty(value.ipAddress) && value.port > 0;
                }
                config = value;
                DelayDetectRequiredSources();
                if (shouldConnect) Connect();
            }
        }

        static string ChannelToLookupVariable(string channel) {
            if (string.IsNullOrWhiteSpace(channel)) return string.Empty;
            var channelSplit = channel.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (channelSplit.Length == 0) return string.Empty;
            for (int i = 0; i < channelSplit.Length; i++)
                channelSplit[i] = variableNameIllegalChars.Replace(Uri.UnescapeDataString(channelSplit[i]), "_");
            return string.Join(".", channelSplit);
        }

        public RedirectorV2(ConfigV2 config) {
            this.config = config;
            evalulator.RegisterDefaultFunctions();
            evalulator.GetVariableFunc = GetVariable;
            computer = new Computer {
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true,
                IsNetworkEnabled = true,
                IsStorageEnabled = true,
                IsControllerEnabled = true,
                IsBatteryEnabled = true,
                IsPsuEnabled = true,
            };
            computer.HardwareAdded += OnHardwareAdded;
            computer.HardwareRemoved += OnHardwareRemoved;
            computer.Open();
            Console.WriteLine(computer.GetReport());
            DelayDetectRequiredSources();
            readThread = new Thread(ReadHardware);
            readThread.Start();
            Connect();
        }

        void DelayDetectRequiredSources() {
            new Thread(DetectRequiredSources) {
                IsBackground = true,
            }.Start();
        }

        void DetectRequiredSources() {
            Thread.Sleep(3000);
            bool requireCpu = false,
                requireGpu = false,
                requireMemory = false,
                requireMotherboard = false,
                requireNetwork = false,
                requireStorage = false,
                requireController = false,
                requireBattery = false,
                requirePsu = false;
            foreach (var hardware in computer.Hardware)
                OnHardwareAdded(hardware);
            lock (evalulator)
                try {
                    isDetectionMode = true;
                    addressExpressions.Clear();
                    requiredSources.Clear();
                    if (config.addresses != null && config.addresses.Count > 0)
                        foreach (var kvp in config.addresses) {
                            try {
                                var tokens = evalulator.Parse(kvp.Value);
                                addressExpressions[kvp.Key] = tokens;
                                evalulator.Evaluate(tokens);
                            } catch {
                                continue;
                            }
                        }
                    else {
                        var addressConfigs = config.addresses ??= new();
                        foreach (var kvp in invSensors) {
                            var identifier = kvp.Key;
                            var rawIdentifier = $"/hardware{kvp.Value.Identifier}";
                            addressExpressions[rawIdentifier] = new[] { new ExprToken(TokenType.Identifier, identifier) };
                            addressConfigs[rawIdentifier] = identifier;
                            requiredSources.Add(identifier);
                        }
                        DefaultAddressConfigGenerated?.Invoke(addressConfigs);
                    }
                } finally {
                    isDetectionMode = false;
                }
            foreach (var identifier in requiredSources) {
                if (!invSensors.TryGetValue(identifier, out var sensor)) continue;
                switch (sensor.Hardware.HardwareType) {
                    case HardwareType.Motherboard:
                    case HardwareType.SuperIO:
                        requireMotherboard = true;
                        break;
                    case HardwareType.Cpu:
                        requireCpu = true;
                        break;
                    case HardwareType.Memory:
                        requireMemory = true;
                        break;
                    case HardwareType.GpuNvidia:
                    case HardwareType.GpuAmd:
                    case HardwareType.GpuIntel:
                        requireGpu = true;
                        break;
                    case HardwareType.Storage:
                        requireStorage = true;
                        break;
                    case HardwareType.Network:
                        requireNetwork = true;
                        break;
                    case HardwareType.Cooler:
                    case HardwareType.EmbeddedController:
                        requireController = true;
                        break;
                    case HardwareType.Psu:
                        requirePsu = true;
                        break;
                    case HardwareType.Battery:
                        requireBattery = true;
                        break;
                }
            }
            computer.IsCpuEnabled = requireCpu;
            computer.IsGpuEnabled = requireGpu;
            computer.IsMemoryEnabled = requireMemory;
            computer.IsMotherboardEnabled = requireMotherboard;
            computer.IsNetworkEnabled = requireNetwork;
            computer.IsStorageEnabled = requireStorage;
            computer.IsControllerEnabled = requireController;
            computer.IsBatteryEnabled = requireBattery;
            computer.IsPsuEnabled = requirePsu;
        }

        public void Connect() {
            if (isDisposed) throw new ObjectDisposedException(nameof(RedirectorV2));
            Console.WriteLine($"Connecting to {config.ipAddress}:{config.port}");
            udpClient = new UdpClient(config.ipAddress, config.port);
        }

        public void Disconnect() {
            Console.WriteLine($"Disconnecting from {config.ipAddress}:{config.port}");
            udpClient?.Close();
            udpClient = null;
        }

        public void Dispose() {
            isDisposed = true;
            udpClient?.Close();
            udpClient = null;
            computer.Close();
            sensors.Clear();
            invSensors.Clear();
            hardwares.Clear();
        }

        void OnHardwareAdded(IHardware hardware) {
            if (!hardwares.Add(hardware)) return;
            hardware.SensorAdded += OnSensorAdded;
            hardware.SensorRemoved += OnSensorRemoved;
            foreach (var sensor in hardware.Sensors) OnSensorAdded(sensor);
            hardware.Update();
        }

        void OnHardwareRemoved(IHardware hardware) {
            hardwares.Remove(hardware);
            hardware.SensorAdded -= OnSensorAdded;
            hardware.SensorRemoved -= OnSensorRemoved;
            foreach (var sensor in hardware.Sensors) OnSensorRemoved(sensor);
        }

        void OnSensorAdded(ISensor sensor) {
            if (sensors.ContainsKey(sensor)) return;
            var identifier = ChannelToLookupVariable(sensor.Identifier.ToString());
            if (string.IsNullOrWhiteSpace(identifier)) return;
            if (!invSensors.ContainsKey(identifier)) {
                Console.Write("New sensor discovered: ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                var hardware = sensor.Hardware;
                var hardwareTypeStr = hardware.HardwareType switch {
                    HardwareType.Cpu => "CPU",
                    HardwareType.GpuNvidia => "Nvidia GPU",
                    HardwareType.GpuAmd => "AMD GPU",
                    HardwareType.GpuIntel => "Intel GPU",
                    HardwareType.Memory => "Memory",
                    HardwareType.Motherboard => "Motherboard",
                    HardwareType.Network => "Network",
                    HardwareType.Storage => "Storage",
                    HardwareType.Cooler => "Cooler",
                    HardwareType.EmbeddedController => "Embedded Controller",
                    HardwareType.Psu => "PSU",
                    HardwareType.Battery => "Battery",
                    _ => hardware.HardwareType.ToString(),
                };
                var sensorUnit = sensor.SensorType switch {
                    SensorType.Voltage => "Voltage, V",
                    SensorType.Current => "Current, A",
                    SensorType.Power => "Power, W",
                    SensorType.Clock => "Clock, Hz",
                    SensorType.Temperature => "Temperature, °C",
                    SensorType.Load => "Load, %",
                    SensorType.Frequency => "Frequency, Hz",
                    SensorType.Fan => "Fan, RPM",
                    SensorType.Flow => "Flow, L/h",
                    SensorType.Control => "Control, %",
                    SensorType.Level => "Level, %",
                    SensorType.Factor => "Factor",
                    SensorType.Data => "Data, GB",
                    SensorType.SmallData => "Data, MB",
                    SensorType.Throughput => "Throughput, KB/s",
                    SensorType.TimeSpan => "Time Span, s",
                    SensorType.Energy => "Energy, J",
                    SensorType.Noise => "Noise, dB",
                    SensorType.Conductivity => "Conductivity, S/m",
                    SensorType.Humidity => "Humidity, %",
                    _ => sensor.SensorType.ToString(),
                };
                Console.WriteLine($"{hardware.Name} ({hardwareTypeStr}): {sensor.Name} ({sensorUnit})");
                Console.ResetColor();
                Console.Write($"  Identifier: ");
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine(identifier);
                Console.ResetColor();
            }
            sensors[sensor] = identifier;
            invSensors[identifier] = sensor;
        }

        void OnSensorRemoved(ISensor sensor) {
            sensors.Remove(sensor);
        }

        double GetVariable(string identifier) {
            if (isDetectionMode) {
                requiredSources.Add(identifier);
                return 1;
            }
            if (invSensors.TryGetValue(identifier, out var sensor))
                return sensor.Value.GetValueOrDefault();
            return identifier.ToUpper() switch {
                "LOCALTIME.YEAR" => DateTime.Now.Year,
                "LOCALTIME.MONTH" => DateTime.Now.Month,
                "LOCALTIME.DAY" => DateTime.Now.Day,
                "LOCALTIME.DAYOFWEEK" => (int)DateTime.Now.DayOfWeek,
                "LOCALTIME.TIMEOFDAY" => DateTime.Now.TimeOfDay.TotalDays,
                "LOCALTIME.TIMESTAMP" => DateTimeOffset.Now.ToUnixTimeMilliseconds() * 0.001,
                "UTCTIME.YEAR" => DateTime.UtcNow.Year,
                "UTCTIME.MONTH" => DateTime.UtcNow.Month,
                "UTCTIME.DAY" => DateTime.UtcNow.Day,
                "UTCTIME.DAYOFWEEK" => (int)DateTime.UtcNow.DayOfWeek,
                "UTCTIME.TIMEOFDAY" => DateTime.UtcNow.TimeOfDay.TotalDays,
                "UTCTIME.TIMESTAMP" => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() * 0.001,
                _ => double.NaN,
            };
        }

        void ReadHardware() {
            while (!isDisposed) {
                if (udpClient != null) {
                    lock (evalulator)
                        try {
                            foreach (var hardware in hardwares)
                                hardware.Update();
                            foreach (var kv in addressExpressions) {
                                try {
                                    var value = (float)evalulator.Evaluate(kv.Value);
                                    if (float.IsNaN(value)) continue;
                                    arguments[0] = value;
                                    new OscMessage(kv.Key, arguments).Write(new OscWriter(bufferStream));
                                    bufferStream.TryGetBuffer(out var buffer);
                                    udpClient.Send(buffer.AsSpan());
                                    bufferStream.SetLength(0);
                                    bufferStream.Position = 0;
                                } catch (Exception e) {
                                    Console.WriteLine($"Error sending message to {kv.Key}: {e.Message}");
                                    continue;
                                }
                            }
                        } catch (Exception e) {
                            Console.WriteLine(e.Message);
                        }
                }
                GC.Collect();
                Thread.Sleep(config.updateInterval);
            }
        }
    }
}