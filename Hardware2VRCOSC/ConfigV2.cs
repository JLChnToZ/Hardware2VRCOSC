using System.Collections.Generic;

namespace Hardware2VRCOSC {
    public struct ConfigV2 {
        public bool? skipAdminCheck;
        public string ipAddress;
        public int port;
        public int updateInterval;
        public Dictionary<string, string> addresses;
    }
}