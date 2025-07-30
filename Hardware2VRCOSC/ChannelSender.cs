using System;
using System.IO;
using System.Net.Sockets;
using MathUtilities;
using LibreHardwareMonitor.Hardware;
using OscCore;
using OscCore.LowLevel;

namespace Hardware2VRCOSC {
    public abstract class ChannelSender {
        static readonly MemoryStream bufferStream = new();
        static readonly object[] oscArgs = new object[1];
        static readonly object @lock = new();

        public readonly string channel;
        public PatternConfig patternConfig;
        public string? channelAlias;

        public virtual string Unit { get; } = "";

        public ChannelSender(string channel) {
            this.channel = channel;
        }

        public void Send(UdpClient client) {
            lock (@lock) {
                if (patternConfig.ignore.GetValueOrDefault()) return;
                var value = GetValue();
                if (patternConfig.stepped.GetValueOrDefault()) value = (float)Math.Round(value);
                if (patternConfig.min.HasValue && patternConfig.max.HasValue)
                    value = Math.Clamp((value - patternConfig.min.Value) / (patternConfig.max.Value - patternConfig.min.Value), 0, 1);
                else {
                    if (patternConfig.min.HasValue && (float)value < patternConfig.min.Value)
                        value = patternConfig.min.Value;
                    if (patternConfig.max.HasValue && (float)value > patternConfig.max.Value)
                        value = patternConfig.max.Value;
                }
                oscArgs[0] = value;
                new OscMessage(string.IsNullOrEmpty(channelAlias) ? channel : channelAlias, oscArgs).Write(new OscWriter(bufferStream));
                bufferStream.TryGetBuffer(out var buffer);
                client.Send(buffer.AsSpan());
                bufferStream.SetLength(0);
                bufferStream.Position = 0;
            }
        }

        protected abstract float GetValue();

        public virtual void PrintPatternConfig() {
            Console.WriteLine("Available OSC channels:");
            var unit = Unit;
            Console.WriteLine($"> {channel}");
            if (!string.IsNullOrEmpty(channelAlias)) Console.WriteLine($"  -> {channelAlias}");
            if (!string.IsNullOrEmpty(unit)) Console.WriteLine($"  Unit: {unit}");
            if (patternConfig.ignore.GetValueOrDefault(false))
                Console.WriteLine("  Ignored: This channel will not send any OSC message.");
            else if (patternConfig.min.HasValue && patternConfig.max.HasValue)
                Console.WriteLine($"  Range: {patternConfig.min}{unit} - {patternConfig.max}{unit} (Will remapped to 0.0 - 1.0)");
            else if (patternConfig.min.HasValue)
                Console.WriteLine($"  Min Value: {patternConfig.min}{unit}");
            else if (patternConfig.max.HasValue)
                Console.WriteLine($"  Max Value: {patternConfig.max}{unit}");
            Console.WriteLine();
        }
    }

    public class SensorSender : ChannelSender {
        public readonly ISensor sensor;

        public override string Unit => sensor.SensorType switch {
            SensorType.Temperature => "°C",
            SensorType.Load => "%",
            SensorType.Control => "%",
            SensorType.Level => "%",
            SensorType.Data => "GB",
            SensorType.SmallData => "MB",
            SensorType.Fan => "RPM",
            SensorType.Flow => "L/h",
            SensorType.Voltage => "V",
            SensorType.Throughput => "KB/s",
            _ => "",
        };

        public SensorSender(string channel, ISensor sensor) : base(channel) {
            this.sensor = sensor;
        }

        protected override float GetValue() => (float)sensor.Value.GetValueOrDefault();
    }

    public abstract class DateTimeSender : ChannelSender {
        public readonly bool isUTC;
        public readonly bool isStepped;
        protected readonly float scale;

        protected DateTime DateTime => isUTC ? DateTime.UtcNow : DateTime.Now;

        protected float TimeOfDay {
            get {
                float value = DateTime.TimeOfDay.Ticks * scale;
                if (isStepped) value = (float)Math.Floor(value);
                return value;
            }
        }

        protected DateTimeSender(string channel, bool isUTC, bool isStepped = false, long scale = TimeSpan.TicksPerDay) :
            base($"{channel}/{(isUTC ? "utc" : "local")}/{(isStepped ? "stepped" : "smooth")}") {
            this.isUTC = isUTC;
            this.isStepped = isStepped;
            this.scale = 1F / scale;
        }
    }

    public class MonthSender : DateTimeSender {
        public MonthSender(string channelPrefix, bool isUTC) : base($"{channelPrefix}/month", isUTC) { }

        protected override float GetValue() => DateTime.Month;
    }

    public class DaySender : DateTimeSender {
        public DaySender(string channelPrefix, bool isUTC, bool isStepped) : base($"{channelPrefix}/day", isUTC, isStepped) { }

        protected override float GetValue() => DateTime.Day + TimeOfDay;
    }

    public class DayOfWeekSender : DateTimeSender {
        public DayOfWeekSender(string channelPrefix, bool isUTC, bool isStepped) : base($"{channelPrefix}/dayofweek", isUTC, isStepped) { }

        protected override float GetValue() => (int)DateTime.DayOfWeek + TimeOfDay;
    }

    public class TimeOfDaySender : DateTimeSender {
        public TimeOfDaySender(string channelPrefix, bool isUTC, bool stepped) :
            this(channelPrefix, "timeofday", TimeSpan.TicksPerDay, isUTC, stepped) { }

        protected TimeOfDaySender(string channelPrefix, string channel, long scale, bool isUTC, bool stepped) :
            base($"{channelPrefix}/{channel}", isUTC, stepped, scale) {
        }

        protected override float GetValue() => TimeOfDay;
    }

    public class HourSender : TimeOfDaySender {
        public HourSender(string channelPrefix, bool isUTC, bool stepped) :
            base(channelPrefix, "hour", TimeSpan.TicksPerHour, isUTC, stepped) { }
    }

    public class MinuteSender : TimeOfDaySender {
        public MinuteSender(string channelPrefix, bool isUTC, bool stepped) :
            base(channelPrefix, "minute", TimeSpan.TicksPerMinute, isUTC, stepped) { }

        protected override float GetValue() => base.GetValue() % 60;
    }

    public class SecondSender : TimeOfDaySender {
        public SecondSender(string channelPrefix, bool isUTC, bool stepped) :
            base(channelPrefix, "second", TimeSpan.TicksPerSecond, isUTC, stepped) { }

        protected override float GetValue() => base.GetValue() % 60;
    }

    public class MillisecondSender : TimeOfDaySender {
        public MillisecondSender(string channelPrefix, bool isUTC, bool stepped) :
            base(channelPrefix, "millisecond", TimeSpan.TicksPerMillisecond, isUTC, stepped) { }

        protected override float GetValue() => base.GetValue() % 1000;
    }

    public class ExpressionSender : ChannelSender {
        readonly MathEvalulator mathEvalulator;
        readonly AbstractMathEvalulator<double>.Token[]? tokens;

        public ExpressionSender(string channel, MathEvalulator mathEvalulator) : base(channel) {
            this.mathEvalulator = mathEvalulator;
            tokens = mathEvalulator.Tokens;
        }

        protected override float GetValue() {
            if (tokens == null || tokens.Length <= 0) return float.NaN;
            mathEvalulator.Tokens = tokens;
            return (float)mathEvalulator.Evaluate();
        }
    }
}