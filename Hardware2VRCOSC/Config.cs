using System.Collections.Generic;

namespace Hardware2VRCOSC {
    struct Config {
        public bool? skipAdminCheck;
        public string ipAddress;
        public int port;
        public int updateInterval;
        public bool ram;
        public bool mainboard;
        public bool cpu;
        public bool gpu;
        public bool hdd;
        public bool fanController;
        public bool network;
        public bool psu;
        public bool clock;
        public string[] filteredSensorTypes;
        public Dictionary<string, PatternConfig> patternConfigs;
        public Dictionary<string, string> channelAliases;
        public Dictionary<string, string> expressions;

        public static readonly Config defaultConfig = new() {
            ipAddress = "127.0.0.1",
            port = 9000,
            updateInterval = 1000,
            ram = true,
            mainboard = false,
            cpu = true,
            gpu = true,
            hdd = false,
            fanController = false,
            network = false,
            clock = true,
            psu = false,
            patternConfigs = new() {
                // Hardwares
                { "/**/temperature/*", new PatternConfig (30, 100) },
                { "/**/load/*", new PatternConfig(0, 100) },
                { "/**/control/*", new PatternConfig(0, 100) },
                { "/**/level/*", new PatternConfig(0, 100) },
                { "/**/data/*", PatternConfig.Ignored },
                { "/**/smalldata/*", PatternConfig.Ignored },
                { "/**/rawvalue/*", PatternConfig.Ignored },
                { "/**/clock/*", PatternConfig.Ignored },
                { "/**/power/*", PatternConfig.Ignored },
                { "/**/fan/*", PatternConfig.Ignored },
                { "/**/flow/*", PatternConfig.Ignored },
                { "/**/voltage/*", PatternConfig.Ignored },
                { "/**/throughput/*", PatternConfig.Ignored },

                // Date time
                { "/datetime/**/utc", PatternConfig.Ignored },
                { "/datetime/month/**", new PatternConfig(0, 12, ignore: true) },
                { "/datetime/day/**", new PatternConfig(1, 32, ignore: true) },
                { "/datetime/dayofweek/**", new PatternConfig(0, 6, ignore: true) },
                { "/datetime/hour/**", new PatternConfig(0, 24) },
                { "/datetime/minute/**", new PatternConfig(0, 60) },
                { "/datetime/second/**", new PatternConfig(0, 60) },
                { "/datetime/millisecond/**", new PatternConfig(0, 1000, ignore: true) },
            },
            channelAliases = new() {
                { "/hardwares/*cpu/0/temperature/0", "/avatar/parameters/cpu_temp" },
                { "/hardwares/*gpu/0/temperature/0", "/avatar/parameters/gpu_temp" },
                { "/datetime/hour/local/smooth", "/avatar/parameters/time_hour" },
                { "/datetime/minute/local/smooth", "/avatar/parameters/time_minute" },
                { "/datetime/second/local/smooth", "/avatar/parameters/time_second" },
            },
            expressions = new(),
        };
    }

    public struct PatternConfig {
        public static readonly PatternConfig Ignored = new(null, null);
        public bool? ignore;
        public bool? stepped;
        public float? min;
        public float? max;

        public PatternConfig(float? min, float? max, bool? stepped = null, bool? ignore = null) {
            this.ignore = ignore.HasValue ? ignore : (min.HasValue || max.HasValue) ? null : true;
            this.stepped = stepped;
            this.min = min;
            this.max = max;
        }
    }
}