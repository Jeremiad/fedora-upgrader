using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using HtmlAgilityPack;
using System.Linq;
using System.Text.RegularExpressions;
using System.Diagnostics;
using System.Text;
using System.IO;
using Serilog;

namespace fedora_upgrader
{
    class Program
    {
        private const string url = "https://www.nic.funet.fi/pub/mirrors/fedora.redhat.com/pub/fedora/linux/releases/";
        //private const string url = "https://ftp.halifax.rwth-aachen.de/fedora/linux/releases/";
        private static readonly HttpClient httpClient = new();

        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(path: "./fedora-upgrader-.log", rollingInterval: RollingInterval.Day)
                .MinimumLevel.Information()
                .CreateLogger();

            try
            {
                if (args.Length == 0)
                {
                    Log.Error("Supported arguments: \n upgrade \n post-upgrade \n version-check");
                    return;
                }

                switch (args[0])
                {
                    case "upgrade":
                        if (!IsRunningAsRoot())
                        {
                            Log.Error("This command must be run as root. Please re-run with 'sudo' or as the root user.");
                            return;
                        }
                        int localVersion = await GetLocalVersion();
                        int currentVersion = await GetCurrentVersion();
                        if (localVersion < currentVersion)
                        {
                            Upgrade(currentVersion);
                        }
                        else
                        {
                            Log.Information("Local version is {LocalVersion} and current version is {CurrentVersion}. No need to upgrade", localVersion, currentVersion);
                        }
                        break;
                    case "post-upgrade":
                        if (!IsRunningAsRoot())
                        {
                            Log.Error("This command must be run as root. Please re-run with 'sudo' or as the root user.");
                            return;
                        }
                        PostUpgrade();
                        break;
                    case "version-check":
                        Log.Information("Checking versions...");
                        Log.Information("Local version: {LocalVersion}", await GetLocalVersion());
                        Log.Information("Current version: {CurrentVersion}", await GetCurrentVersion());
                        break;
                    default:
                        Log.Error("Unknown argument: {Argument}. Supported arguments: upgrade, post-upgrade, version-check", args[0]);
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "An unhandled exception occurred");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }

        private static bool IsRunningAsRoot()
        {
            return Environment.IsPrivilegedProcess;
        }

        private static void Upgrade(int latestVersion)
        {
            List<string> commands =
            [
                "dnf upgrade --refresh -y",
                "dnf install dnf-plugin-system-upgrade -y",
                $"dnf system-upgrade download --refresh --releasever={latestVersion} -y",
                "dnf system-upgrade reboot -y",
            ];
            RunCommands(commands);
        }

        private static void PostUpgrade()
        {
            List<string> commands =
            [
                //"dnf system-upgrade clean",
                //"dnf clean packages",
                "dnf install rpmconf remove-retired-packages clean-rpm-gpg-pubkey symlinks dracut-config-rescue -y",
                //"remove-retired-packages", // Requires interaction, so disabled for now
                "rpmconf -at",
                "dnf repoquery --duplicates",
                "dnf remove --duplicates",
                "dnf list --extras",
                @"dnf remove $(sudo dnf repoquery --extras --exclude=kernel,kernel-\*,kmod-\*)",
                "dnf autoremove -y",
                "symlinks -r /usr | grep dangling",
                "symlinks -r -d /usr",
                "clean-rpm-gpg-pubkey",
                "dracut-config-rescue",
            ];
            RunCommands(commands);
            RemoveOldKernels();
        }

        private static void RemoveOldKernels()
        {
            Log.Information("Checking for old kernels to remove...");

            string script = """
                #!/usr/bin/env bash
                old_kernels=($(dnf repoquery --installonly --latest-limit=-1 -q))
                if [ "${#old_kernels[@]}" -eq 0 ]; then
                    echo "No old kernels found"
                    exit 0
                fi
                if ! dnf remove "${old_kernels[@]}"; then
                    echo "Failed to remove old kernels"
                    exit 1
                fi
                echo "Removed old kernels"
                exit 0
                """;

            ProcessStartInfo processStartInfo = new("/usr/bin/bash", ["-c", script])
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process process = new() { StartInfo = processStartInfo };

            StringBuilder output = new();
            StringBuilder error = new();

            process.OutputDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    output.AppendLine(e.Data);
            };
            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    error.AppendLine(e.Data);
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();

            if (output.Length > 0)
                Log.Information("Output:\n{Output}", output);
            if (error.Length > 0)
                Log.Warning("Stderr:\n{Error}", error);

            if (process.ExitCode != 0)
                Log.Error("RemoveOldKernels exited with code {ExitCode}", process.ExitCode);
        }

        private static async Task<int> GetCurrentVersion()
        {
            HtmlDocument document = new();
            Regex regex = new("[0-9]+");
            List<ushort> versions = [];
            var html = await httpClient.GetStringAsync(url);
            document.LoadHtml(html);
            var nodes = document.DocumentNode.SelectNodes("//a/@href");

            foreach (var node in nodes)
            {
                var match = regex.Match(node.InnerText).Value;
                if (!string.IsNullOrEmpty(match))
                {
                    versions.Add(ushort.Parse(match));
                }
            }

            var latest = versions.OrderByDescending(n => n).FirstOrDefault();
            Log.Information("Latest version: {LatestVersion}", latest);
            return latest;
        }

        private static async Task<int> GetLocalVersion()
        {
            string fedoraRelease = await File.ReadAllTextAsync("/etc/fedora-release");
            Regex regex = new("[0-9]+");
            var strversion = regex.Match(fedoraRelease);
            Log.Information("Local version: {LocalVersion}", strversion.Value);
            return int.Parse(strversion.Value);
        }

        private static void RunCommands(List<string> commands)
        {
            foreach (var command in commands)
            {
                Log.Information("Running command: {Command}", command);
                ProcessStartInfo processStartInfo = new("/usr/bin/sudo", command)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using Process process = new()
                {
                    StartInfo = processStartInfo
                };

                StringBuilder output = new();
                StringBuilder error = new();

                process.OutputDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        output.AppendLine(e.Data);
                    }
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                    {
                        error.AppendLine(e.Data);
                    }
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (output.Length > 0)
                    Log.Information("Output:\n{Output}", output);
                if (error.Length > 0)
                    Log.Warning("Stderr:\n{Error}", error);

                if (process.ExitCode != 0)
                    Log.Error("Command {Command} exited with code {ExitCode}", command, process.ExitCode);
            }
        }
    }
}
