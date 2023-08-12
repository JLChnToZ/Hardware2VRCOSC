using System;
using System.Collections.Generic;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;
using System.Net.Sockets;
using OscCore;
using OpenHardwareMonitor.Hardware;

namespace Hardware2VRCOSC {
    internal class HardwareInfoRedirector : IDisposable {
        static readonly Regex oscNameSanitizer = new(@"[^a-zA-Z0-9_]+", RegexOptions.Compiled);
        readonly Dictionary<HardwareSensorType, HardwareSensorRedirectConfig> configs = new();
        readonly HashSet<HardwareType> watchingHardwares = new();
        readonly Computer computer;
        readonly HashSet<IHardware> hardwares = new();
        readonly HashSet<ISensor> sensors = new();
        readonly Thread readThread;
        readonly StringBuilder sb = new();
        UdpClient? udpClient;
        bool isDisposed;

        public int UpdateInterval { get; set; } = 1000;

        public Thread ReadThread => readThread;

        public HardwareInfoRedirector() {
            computer = new Computer();
            computer.HardwareAdded += OnHardwareAdded;
            computer.HardwareRemoved += OnHardwareRemoved;
            computer.Open();
            foreach (var hardaware in computer.Hardware) OnHardwareAdded(hardaware);
            readThread = new Thread(ReadHardware);
            readThread.Start();
        }

        public void Connect(string ip = "127.0.0.1", int port = 9000) {
            if (isDisposed) throw new ObjectDisposedException(nameof(HardwareInfoRedirector));
            udpClient = new UdpClient(ip, port);
        }

        public void Disconnect() {
            udpClient?.Close();
            udpClient = null;
        }

        public void UpdateConfig(HardwareSensorRedirectConfig config) {
            if (isDisposed) throw new ObjectDisposedException(nameof(HardwareInfoRedirector));
            configs[new HardwareSensorType(config.hardwareType, config.sensorType)] = config;
            watchingHardwares.Add(config.hardwareType);
            switch (config.hardwareType) {
                case HardwareType.Mainboard: computer.MainboardEnabled = true; break;
                case HardwareType.CPU: computer.CPUEnabled = true; break;
                case HardwareType.RAM: computer.RAMEnabled = true; break;
                case HardwareType.GpuAti:
                case HardwareType.GpuNvidia: computer.GPUEnabled = true; break;
                case HardwareType.HDD: computer.HDDEnabled = true; break;
                case HardwareType.TBalancer:
                case HardwareType.Heatmaster: computer.FanControllerEnabled = true; break;
                case HardwareType.Network: computer.NetworkEnabled = true; break;
            }
        }

        public void OverwriteConfigs(IEnumerable<HardwareSensorRedirectConfig> newConfigs) {
            if (isDisposed) throw new ObjectDisposedException(nameof(HardwareInfoRedirector));
            configs.Clear();
            watchingHardwares.Clear();
            foreach (var config in newConfigs) UpdateConfig(config);
        }

        public void RemoveConfig(HardwareSensorRedirectConfig config) {
            if (isDisposed) throw new ObjectDisposedException(nameof(HardwareInfoRedirector));
            configs.Remove(new HardwareSensorType(config.hardwareType, config.sensorType, config.hardwareName, config.sensorName));
            var hasThisHardware = false;
            foreach (var key in configs.Keys)
                if (key.hardwareType == config.hardwareType) {
                    hasThisHardware = true;
                    break;
                }
            if (!hasThisHardware) watchingHardwares.Remove(config.hardwareType);
        }

        public void Dispose() {
            udpClient?.Close();
            computer?.Close();
            sensors.Clear();
            hardwares.Clear();
            isDisposed = true;
            udpClient = null;
        }

        void OnHardwareAdded(IHardware hardware) {
            Console.WriteLine($"Hardware added: <{hardware.HardwareType}> {hardware.Name}");
            hardwares.Add(hardware);
            hardware.SensorAdded += OnSensorAdded;
            hardware.SensorRemoved += OnSensorRemoved;
            foreach (var sensor in hardware.Sensors) OnSensorAdded(sensor);
        }

        void OnHardwareRemoved(IHardware hardware) {
            Console.WriteLine($"Hardware removed: <{hardware.HardwareType}> {hardware.Name}");
            hardwares.Remove(hardware);
            hardware.SensorAdded -= OnSensorAdded;
            hardware.SensorRemoved -= OnSensorRemoved;
            foreach (var sensor in hardware.Sensors) OnSensorRemoved(sensor);
        }

        void OnSensorAdded(ISensor sensor) {
            Console.WriteLine($"Sensor added: <{sensor.SensorType}> {sensor.Name}");
            sensors.Add(sensor);
            Console.WriteLine("Available OSC channels:");
            var hardware = sensor.Hardware;
            var hardwareName = Sanitize(hardware.Name);
            var sensorName = Sanitize(sensor.Name);
            Console.WriteLine($"> /hardwares/{hardware.HardwareType}/{sensor.SensorType}");
            Console.WriteLine($"> /hardwares/{hardwareName}/{sensor.SensorType}");
            Console.WriteLine($"> /hardwares/{hardware.HardwareType}/{sensorName}/{sensor.SensorType}");
            Console.WriteLine($"> /hardwares/{hardwareName}/{sensorName}/{sensor.SensorType}");
        }

        void OnSensorRemoved(ISensor sensor) {
            Console.WriteLine($"Sensor removed: <{sensor.SensorType}> {sensor.Name}");
            sensors.Remove(sensor);
        }

        void ReadHardware() {
            while (!isDisposed) {
                if (udpClient != null) {
                    foreach (var hardware in hardwares)
                        if (watchingHardwares.Contains(hardware.HardwareType))
                            hardware.Update();
                    foreach (var sensor in sensors) {
                        var hardware = sensor.Hardware;
                        if (!FindSensorConfig(sensor, out var config)) continue;
                        sb.Clear();
                        sb.Append("/hardwares/");
                        if (!string.IsNullOrEmpty(config.hardwareName)) sb.Append(Sanitize(config.hardwareName)).Append('/');
                        else sb.Append(hardware.HardwareType).Append('/');
                        if (!string.IsNullOrEmpty(config.sensorName)) sb.Append(Sanitize(config.sensorName)).Append('/');
                        sb.Append(sensor.SensorType);
                        var channel = sb.ToString();
                        if (sensor.Value.HasValue)
                            udpClient.Send(new OscMessage(channel, ClampLerpValue((float)sensor.Value, config)).ToByteArray());
                        if (config.sendMin && sensor.Min.HasValue)
                            udpClient.Send(new OscMessage($"{channel}/min", ClampLerpValue((float)sensor.Min, config)).ToByteArray());
                        if (config.sendMax && sensor.Max.HasValue)
                            udpClient.Send(new OscMessage($"{channel}/max", ClampLerpValue((float)sensor.Max, config)).ToByteArray());
                    }
                }
                Thread.Sleep(UpdateInterval);
            }
        }

        bool FindSensorConfig(ISensor sensor, out HardwareSensorRedirectConfig config) {
            var hardware = sensor.Hardware;
            var hardwareSensorType = new HardwareSensorType(hardware.HardwareType, sensor.SensorType, hardware.Name, sensor.Name);
            if (configs.TryGetValue(hardwareSensorType, out config)) return true;
            hardwareSensorType = new HardwareSensorType(hardware.HardwareType, sensor.SensorType, hardware.Name);
            if (configs.TryGetValue(hardwareSensorType, out config)) return true;
            hardwareSensorType = new HardwareSensorType(hardware.HardwareType, sensor.SensorType, sensorName: sensor.Name);
            if (configs.TryGetValue(hardwareSensorType, out config)) return true;
            hardwareSensorType = new HardwareSensorType(hardware.HardwareType, sensor.SensorType);
            if (configs.TryGetValue(hardwareSensorType, out config)) return true;
            return false;

        }

        static string Sanitize(string name) => oscNameSanitizer.Replace(name, "_");

        static float ClampLerpValue(float value, HardwareSensorRedirectConfig config) {
            if (config.maxValue > config.minValue) {
                if (value < config.minValue)
                    value = config.minValue;
                else if (value > config.maxValue)
                    value = config.maxValue;
                else
                    value = (value - config.minValue) / (config.maxValue - config.minValue);
            }
            return value;
        }
    }

    public readonly struct HardwareSensorType : IEquatable<HardwareSensorType> {
        public readonly HardwareType hardwareType;
        public readonly string hardwareName;
        public readonly SensorType sensorType;
        public readonly string sensorName;

        public HardwareSensorType(HardwareType hardwareType, SensorType sensorType, string hardwareName = "", string sensorName = "") {
            this.hardwareType = hardwareType;
            this.hardwareName = hardwareName ?? "";
            this.sensorType = sensorType;
            this.sensorName = sensorName ?? "";
        }

        public bool Equals(HardwareSensorType other) =>
            hardwareType == other.hardwareType &&
            sensorType == other.sensorType &&
            hardwareName == other.hardwareName &&
            sensorName == other.sensorName;

        public override bool Equals(object obj) => obj is HardwareSensorType other && Equals(other);

        public override int GetHashCode() {
            unchecked {
                var hashCode = (int)hardwareType;
                hashCode = (hashCode * 397) ^ (hardwareName != null ? hardwareName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (int)sensorType;
                hashCode = (hashCode * 397) ^ (sensorName != null ? sensorName.GetHashCode() : 0);
                return hashCode;
            }
        }
    }

    public struct HardwareSensorRedirectConfig {
        public HardwareType hardwareType;
        public string hardwareName;
        public SensorType sensorType;
        public string sensorName;
        public bool sendMin, sendMax;
        public float minValue, maxValue;
    }
}
