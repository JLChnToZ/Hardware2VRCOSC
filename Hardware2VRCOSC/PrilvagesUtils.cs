using System;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using Mono.Unix;

namespace Hardware2VRCOSC {
    public static class PrilvagesUtils {
        static bool? isAdministrator;

        public static bool IsAdministrator() {
            if (!isAdministrator.HasValue) {
                if (OperatingSystem.IsWindows())
                    isAdministrator = new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);
                else
                    isAdministrator = new UnixUserInfo(UnixUserInfo.GetRealUserId()).UserId == 0;
            }
            return isAdministrator.Value;
        }

        [SupportedOSPlatform("windows")]
        public static void RunCurrentProcessAsAdmin() {
            string processName;
            var commandLine = Environment.CommandLine;
            if (string.IsNullOrWhiteSpace(commandLine))
                throw new InvalidOperationException("Command line is empty.");
            switch (commandLine[0]) {
                case '"': {
                        int index = commandLine.IndexOf('"', 1);
                        if (index <= 0)
                            throw new InvalidOperationException("Command line is invalid, missing closing quote.");
                        processName = commandLine[1..index];
                        commandLine = commandLine[(index + 1)..].TrimStart();
                        break;
                    }
                default: {
                        int index = commandLine.IndexOf(' ');
                        if (index > 0) {
                            processName = commandLine[..index];
                            commandLine = commandLine[(index + 1)..].TrimStart();
                        } else {
                            processName = commandLine;
                            commandLine = "";
                        }
                        break;
                    }
            }
            Process.Start(new ProcessStartInfo(processName, commandLine) {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                Verb = "runas",
            });
        }
    }
}