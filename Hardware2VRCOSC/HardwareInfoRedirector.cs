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
        const string PREFIX = "/hardwares";
        readonly HashSet<HardwareType> watchingHardwares = new();
        readonly Computer computer;
        readonly HashSet<IHardware> hardwares = new();
        readonly HashSet<ISensor> sensors = new();
        readonly Thread readThread;
        readonly StringBuilder sb = new();
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
            computer.HardwareAdded += OnHardwareAdded;
            computer.HardwareRemoved += OnHardwareRemoved;
            computer.Open();
            foreach (var hardaware in computer.Hardware) OnHardwareAdded(hardaware);
            readThread = new Thread(ReadHardware);
            readThread.Start();
            if (!string.IsNullOrEmpty(config.ipAddress) && config.port > 0) Connect();
        }

        public void UpdateConfig(Config config) {
            computer.GPUEnabled = config.gpu;
            computer.CPUEnabled = config.cpu;
            computer.RAMEnabled = config.ram;
            computer.MainboardEnabled = config.mainboard;
            computer.HDDEnabled = config.hdd;
            computer.FanControllerEnabled = config.fanController;
            computer.NetworkEnabled = config.network;
            UpdateInterval = config.updateInterval;
            if (IP != config.ipAddress || Port != config.port) {
                Disconnect();
                IP = config.ipAddress;
                Port = config.port;
                if (!string.IsNullOrEmpty(config.ipAddress) && config.port > 0) Connect();
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
            Console.WriteLine($"> {PREFIX}{sensor.Identifier}");
            Console.WriteLine($"> {PREFIX}{sensor.Identifier}/min");
            Console.WriteLine($"> {PREFIX}{sensor.Identifier}/max");
        }

        void OnSensorRemoved(ISensor sensor) {
            if (sensors.Remove(sensor))
                Console.WriteLine($"Sensor unwatched: <{sensor.SensorType}> {sensor.Name}");
        }

        void ReadHardware() {
            while (!isDisposed) {
                try {
                    if (udpClient != null) {
                        foreach (var hardware in hardwares)
                            hardware.Update();
                        foreach (var sensor in sensors) {
                            var hardware = sensor.Hardware;
                            var sensorType = sensor.SensorType;
                            var channel = $"{PREFIX}{sensor.Identifier}";
                            if (sensor.Value.HasValue)
                                udpClient.Send(new OscMessage(channel, ClampLerpValue((float)sensor.Value, sensorType)).ToByteArray());
                            if (sensor.Min.HasValue)
                                udpClient.Send(new OscMessage($"{channel}/min", ClampLerpValue((float)sensor.Min, sensorType)).ToByteArray());
                            if (sensor.Max.HasValue)
                                udpClient.Send(new OscMessage($"{channel}/max", ClampLerpValue((float)sensor.Max, sensorType)).ToByteArray());
                        }
                    }
                } catch (Exception e) {
                    Console.WriteLine(e);
                }
                Thread.Sleep(UpdateInterval);
            }
        }

        static float ClampLerpValue(float value, SensorType sensorType) {
            switch (sensorType) {
                case SensorType.Load:
                case SensorType.Control:
                case SensorType.Level:
                case SensorType.Temperature:
                    return value / 100F;
                default:
                    return value;
            }
        }
    }
}
