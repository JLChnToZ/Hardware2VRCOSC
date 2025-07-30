using System;
using System.Collections.Generic;
using System.Threading;
using System.Net.Sockets;
using LibreHardwareMonitor.Hardware;
using DotNet.Globbing;
using MathUtilities;

namespace Hardware2VRCOSC {
    internal class HardwareInfoRedirector : IDisposable {
        const string HARDWARE_PREFIX = "/hardwares";
        const string TIME_PREFIX = "/datetime";
        readonly MathEvalulator mathEvalulator = new();
        readonly HashSet<HardwareType> watchingHardwares = new();
        readonly Computer computer;
        readonly HashSet<IHardware> hardwares = new();
        readonly HashSet<ISensor> sensors = new();
        readonly Dictionary<string, ISensor> sensorLookup = new(StringComparer.OrdinalIgnoreCase);
        readonly Thread readThread;
        readonly Dictionary<Glob, PatternConfig> patternConfigs = new();
        readonly Dictionary<ISensor, ChannelSender> sensorSenders = new();
        readonly HashSet<ChannelSender> channelSenders = new();
        readonly HashSet<DateTimeSender> dateTimeSenders = new();
        Dictionary<string, string> channelAliases;
        UdpClient? udpClient;
        bool isDisposed;
        bool clockEnabled;

        public int UpdateInterval { get; set; }

        public string IP { get; set; }

        public int Port { get; set; }

        public bool RAMEnabled { get => computer.IsMemoryEnabled; set => computer.IsMemoryEnabled = value; }

        public bool MainboardEnabled { get => computer.IsMotherboardEnabled; set => computer.IsMotherboardEnabled = value; }

        public bool CPUEnabled { get => computer.IsCpuEnabled; set => computer.IsCpuEnabled = value; }

        public bool GPUEnabled { get => computer.IsGpuEnabled; set => computer.IsGpuEnabled = value; }

        public bool HDDEnabled { get => computer.IsStorageEnabled; set => computer.IsStorageEnabled = value; }

        public bool PSUEnabled { get => computer.IsPsuEnabled; set => computer.IsPsuEnabled = value; }

        public bool FanControllerEnabled { get => computer.IsControllerEnabled; set => computer.IsControllerEnabled = value; }

        public bool NetworkEnabled { get => computer.IsNetworkEnabled; set => computer.IsNetworkEnabled = value; }

        public bool ClockEnabled {
            get => clockEnabled;
            set {
                if (clockEnabled == value) return;
                clockEnabled = value;
                if (value) {
                    Console.WriteLine("Clock enabled");
                    if (dateTimeSenders.Count == 0) {
                        AddDateTimeSender(new MonthSender(TIME_PREFIX, false));
                        AddDateTimeSender(new MonthSender(TIME_PREFIX, true));
                        AddDateTimeSender(new DaySender(TIME_PREFIX, false, false));
                        AddDateTimeSender(new DaySender(TIME_PREFIX, true, false));
                        AddDateTimeSender(new DaySender(TIME_PREFIX, false, true));
                        AddDateTimeSender(new DaySender(TIME_PREFIX, true, true));
                        AddDateTimeSender(new DayOfWeekSender(TIME_PREFIX, false, false));
                        AddDateTimeSender(new DayOfWeekSender(TIME_PREFIX, true, false));
                        AddDateTimeSender(new DayOfWeekSender(TIME_PREFIX, false, true));
                        AddDateTimeSender(new DayOfWeekSender(TIME_PREFIX, true, true));
                        AddDateTimeSender(new TimeOfDaySender(TIME_PREFIX, false, false));
                        AddDateTimeSender(new TimeOfDaySender(TIME_PREFIX, true, false));
                        AddDateTimeSender(new TimeOfDaySender(TIME_PREFIX, false, true));
                        AddDateTimeSender(new TimeOfDaySender(TIME_PREFIX, true, true));
                        AddDateTimeSender(new HourSender(TIME_PREFIX, false, false));
                        AddDateTimeSender(new HourSender(TIME_PREFIX, true, false));
                        AddDateTimeSender(new HourSender(TIME_PREFIX, false, true));
                        AddDateTimeSender(new HourSender(TIME_PREFIX, true, true));
                        AddDateTimeSender(new MinuteSender(TIME_PREFIX, false, false));
                        AddDateTimeSender(new MinuteSender(TIME_PREFIX, true, false));
                        AddDateTimeSender(new MinuteSender(TIME_PREFIX, false, true));
                        AddDateTimeSender(new MinuteSender(TIME_PREFIX, true, true));
                        AddDateTimeSender(new SecondSender(TIME_PREFIX, false, false));
                        AddDateTimeSender(new SecondSender(TIME_PREFIX, true, false));
                        AddDateTimeSender(new SecondSender(TIME_PREFIX, false, true));
                        AddDateTimeSender(new SecondSender(TIME_PREFIX, true, true));
                        AddDateTimeSender(new MillisecondSender(TIME_PREFIX, false, false));
                        AddDateTimeSender(new MillisecondSender(TIME_PREFIX, true, false));
                        AddDateTimeSender(new MillisecondSender(TIME_PREFIX, false, true));
                        AddDateTimeSender(new MillisecondSender(TIME_PREFIX, true, true));
                    } else
                        channelSenders.UnionWith(dateTimeSenders);
                } else {
                    Console.WriteLine("Clock disabled");
                    channelSenders.ExceptWith(dateTimeSenders);
                }
            }
        }

        static string ChannelToLookupVariable(string channel) {
            if (string.IsNullOrWhiteSpace(channel)) return string.Empty;
            channel = channel.Replace('-', '_');
            var channelSplit = channel.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (channelSplit.Length == 0) return string.Empty;
            return string.Join(".", channelSplit);
        }

        public HardwareInfoRedirector() : this(Config.defaultConfig) {}

        public HardwareInfoRedirector(Config config) {
            UpdateInterval = config.updateInterval;
            IP = config.ipAddress;
            Port = config.port;
            computer = new Computer {
                IsMemoryEnabled = config.ram,
                IsMotherboardEnabled = config.mainboard,
                IsCpuEnabled = config.cpu,
                IsGpuEnabled = config.gpu,
                IsStorageEnabled = config.hdd,
                IsControllerEnabled = config.fanController,
                IsNetworkEnabled = config.network,
                IsPsuEnabled = config.psu,
                IsRing0Enabled = OperatingSystem.IsWindows() && Utils.IsAdministrator(),
            };
            SetPatternConfig(config.patternConfigs);
            SetExpressions(config.expressions);
            channelAliases = config.channelAliases ?? new();
            computer.HardwareAdded += OnHardwareAdded;
            computer.HardwareRemoved += OnHardwareRemoved;
            computer.Open();
            foreach (var hardware in computer.Hardware) OnHardwareAdded(hardware);
            readThread = new Thread(ReadHardware);
            readThread.Start();
            if (!string.IsNullOrEmpty(config.ipAddress) && config.port > 0) Connect();
            ClockEnabled = config.clock;
            mathEvalulator.RegisterDefaultFunctions();
            mathEvalulator.GetVariableFunc = GetVariable;
        }

        public void UpdateConfig(Config config) {
            if (isDisposed) throw new ObjectDisposedException(nameof(HardwareInfoRedirector));
            computer.IsMemoryEnabled = config.ram;
            computer.IsMotherboardEnabled = config.mainboard;
            computer.IsCpuEnabled = config.cpu;
            computer.IsGpuEnabled = config.gpu;
            computer.IsStorageEnabled = config.hdd;
            computer.IsControllerEnabled = config.fanController;
            computer.IsNetworkEnabled = config.network;
            computer.IsPsuEnabled = config.psu;
            UpdateInterval = config.updateInterval;
            patternConfigs.Clear();
            sensorSenders.Clear();
            SetPatternConfig(config.patternConfigs);
            SetExpressions(config.expressions);
            channelAliases = config.channelAliases ?? new();
            if (IP != config.ipAddress || Port != config.port) {
                Disconnect();
                IP = config.ipAddress;
                Port = config.port;
                if (!string.IsNullOrEmpty(config.ipAddress) && config.port > 0) Connect();
            }
            ClockEnabled = config.clock;
        }

        void SetPatternConfig(Dictionary<string, PatternConfig> configs) {
            if (configs == null) return;
            foreach (var (pattern, config) in configs) {
                if (string.IsNullOrEmpty(pattern)) continue;
                patternConfigs.Add(Glob.Parse(pattern), config);
            }
            foreach (var sender in channelSenders) {
                GetPattern(sender.channel, out var newPattern);
                if (sender.patternConfig.ignore != newPattern.ignore) {
                    if (newPattern.ignore.GetValueOrDefault())
                        Console.WriteLine($"Channel ignored: {sender.channel}");
                    else
                        Console.WriteLine($"Channel unignored: {sender.channel}");
                }
                if (sender.patternConfig.min != newPattern.min || sender.patternConfig.max != newPattern.max) {
                    if (newPattern.min.HasValue && newPattern.max.HasValue)
                        Console.WriteLine($"Channel range changed: {sender.channel} {newPattern.min} - {newPattern.max}");
                    else if (newPattern.min.HasValue)
                        Console.WriteLine($"Channel min value changed: {sender.channel} {newPattern.min}");
                    else if (newPattern.max.HasValue)
                        Console.WriteLine($"Channel max value changed: {sender.channel} {newPattern.max}");
                }
                sender.patternConfig = newPattern;
                CheckChannelAlias(sender);
            }
        }

        void SetExpressions(Dictionary<string, string> srcExpression) {
            if (srcExpression == null) return;
            channelSenders.RemoveWhere(sender => sender is ExpressionSender);
            foreach (var kv in srcExpression) {
                var channel = kv.Key;
                var expression = kv.Value;
                if (string.IsNullOrWhiteSpace(expression)) continue;
                try {
                    mathEvalulator.Parse(expression);
                    channelSenders.Add(new ExpressionSender(channel, mathEvalulator));
                } catch (Exception e) {
                    Console.WriteLine($"Error parsing expression for channel {channel}: {e.Message}");
                }
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
            sensorSenders.Clear();
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
            var channel = $"{HARDWARE_PREFIX}{sensor.Identifier}";
            var sender = new SensorSender(channel, sensor);
            var identifier = ChannelToLookupVariable(channel);
            Console.WriteLine($"Identifier: {identifier}");
            if (GetPattern(channel, out var pattern)) sender.patternConfig = pattern;
            CheckChannelAlias(sender);
            sender.PrintPatternConfig();
            sensorSenders.Add(sensor, sender);
            channelSenders.Add(sender);
            sensorLookup[identifier] = sensor;
        }

        void OnSensorRemoved(ISensor sensor) {
            if (sensors.Remove(sensor))
                Console.WriteLine($"Sensor unwatched: <{sensor.SensorType}> {sensor.Name}");
            if (sensorSenders.TryGetValue(sensor, out var sender)) {
                sensorSenders.Remove(sensor);
                channelSenders.Remove(sender);
            }
            var identifier = ChannelToLookupVariable($"{HARDWARE_PREFIX}{sensor.Identifier}");
            if (sensorLookup.TryGetValue(identifier, out var existingSensor) && existingSensor == sensor)
                sensorLookup.Remove(identifier);
        }

        void AddDateTimeSender(DateTimeSender sender) {
            if (GetPattern(sender.channel, out var pattern))
                sender.patternConfig = pattern;
            CheckChannelAlias(sender);
            sender.PrintPatternConfig();
            dateTimeSenders.Add(sender);
            channelSenders.Add(sender);
        }

        double GetVariable(string identifier) {
            if (sensorLookup.TryGetValue(identifier, out var sensor))
                return sensor.Value.GetValueOrDefault(float.NaN);
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
                try {
                    if (udpClient != null) {
                        foreach (var hardware in hardwares)
                            hardware.Update();
                        foreach (var sender in channelSenders) {
                            try {
                                sender.Send(udpClient);
                            } catch (Exception e) {
                                Console.WriteLine(e);
                            }
                        }
                    }
                } catch (Exception e) {
                    Console.WriteLine(e);
                }
                GC.Collect();
                Thread.Sleep(UpdateInterval);
            }
        }

        bool GetPattern(string channel, out PatternConfig matchedConfig) {
            foreach (var (glob, pattern) in patternConfigs)
                if (glob.IsMatch(channel)) {
                    matchedConfig = pattern;
                    return true;
                }
            matchedConfig = default;
            return false;
        }

        void CheckChannelAlias(ChannelSender sender) {
            foreach (var alias in channelAliases)
                if (Glob.Parse(alias.Key).IsMatch(sender.channel)) {
                    if (sender.channelAlias == alias.Value) continue;
                    sender.channelAlias = alias.Value;
                    Console.WriteLine($"Channel alias: {sender.channel} -> {sender.channelAlias}");
                }
        }
    }
}
