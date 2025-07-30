using System.Runtime.Versioning;
using System.Security.Principal;

namespace Hardware2VRCOSC {
    public static class Utils {
        [SupportedOSPlatform("windows")]
        public static bool IsAdministrator() => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
    }
}