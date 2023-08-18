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
        public string[] filteredSensorTypes;
        public Dictionary<string, PatternConfig> patternConfigs;

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
            patternConfigs = new() {
                { "/**/temperature/*", new PatternConfig { min = 30, max = 100 } },
                { "/**/load/*", new PatternConfig { min = 0, max = 100 } },
                { "/**/control/*", new PatternConfig { min = 0, max = 100 } },
                { "/**/level/*", new PatternConfig { min = 0, max = 100 } },
                { "/**/data/*", new PatternConfig { ignore = true } },
                { "/**/smalldata/*", new PatternConfig { ignore = true } },
                { "/**/rawvalue/*", new PatternConfig { ignore = true } },
                { "/**/clock/*", new PatternConfig { ignore = true } },
                { "/**/power/*", new PatternConfig { ignore = true } },
                { "/**/fan/*", new PatternConfig { ignore = true } },
                { "/**/flow/*", new PatternConfig { ignore = true } },
                { "/**/voltage/*", new PatternConfig { ignore = true } },
                { "/**/throughput/*", new PatternConfig { ignore = true } },
            },
        };
    }

    public struct PatternConfig {
        public bool? ignore;
        public float? min;
        public float? max;
    }
}