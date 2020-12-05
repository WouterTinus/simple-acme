﻿using System;
using System.IO;
using System.Linq;
using System.Reflection;

namespace PKISharp.WACS.Services
{
    public class VersionService
    {
        public VersionService(ILogService log)
        {
            if (ExePath == null)
            {
                log.Error("Unable to determine main module filename.");
                throw new InvalidOperationException();
            }
            var processInfo = new FileInfo(ExePath);
            if (processInfo.Name == "dotnet.exe")
            {
                log.Error("Running as a local dotnet tool is not supported. Please install using the --global option.");
                throw new InvalidOperationException();
            }
            log.Verbose("ExePath: {ex}", ExePath);
            log.Verbose("ResourcePath: {ex}", ResourcePath);
        }
        public static string BasePath { get; private set; } = AppContext.BaseDirectory;
        public static string PluginPath { get; private set; } = AppContext.BaseDirectory;
        public static string ExePath => Environment.GetCommandLineArgs().First();
        public static string ResourcePath { get; private set; } = AppContext.BaseDirectory;
        public static string Bitness => Environment.Is64BitProcess ? "64-bit" : "32-bit";
        public static bool Pluggable =>
#if DEBUG || PLUGGABLE
                true;
#else
                false;
#endif
        public static bool Debug =>
#if DEBUG
                true;
#else
                false;
#endif

        public static string BuildType 
        { 
            get
            {
                var build = $"{(Debug ? "DEBUG" : "RELEASE")}, {(Pluggable ? "PLUGGABLE" : "TRIMMED")}";
                return build;
            }
        }

        public static Version SoftwareVersion => Assembly.GetEntryAssembly()?.GetName().Version!;
    }
}