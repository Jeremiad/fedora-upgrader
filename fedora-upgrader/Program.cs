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
using Serilog.Core;

namespace fedora_upgrader
{
    class Program
    {
        //private const string url = "http://www.nic.funet.fi/pub/mirrors/fedora.redhat.com/pub/fedora/linux/releases/";
        private const string url = "https://ftp.halifax.rwth-aachen.de/fedora/linux/releases/";
        static async Task Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .WriteTo.Console()
                .WriteTo.File(path: "./fedora-upgrader-.log", rollingInterval: RollingInterval.Day)
                .MinimumLevel.Information()
                .CreateLogger();

            if (args.FirstOrDefault() != null)
            {
                if (args[0] == "upgrade")
                {
                    int localVersion = await GetLocalVersion();
                    int currentVersion = await GetCurrentVersion();
                    if (localVersion < currentVersion)
                    {
                        Upgrade(currentVersion);
                    }
                    else
                    {
                        Log.Information($"Local version is {localVersion} and current version is {currentVersion}. No need to upgrade");
                    }
                }
                if (args[0] == "post-upgrade") 
                {
                    PostUpgrade();
                }
                if (args[0] == "version-check")
                {
                    Log.Information("Checking versions...");
                    Log.Information("Local version: {0}", await GetLocalVersion());
                    Log.Information("Current version: {0}", await GetCurrentVersion());
                }
            }
            else
            {
                Log.Error("Supported arguments: \n upgrade \n post-upgrade \n version-check");
            }
            Log.CloseAndFlush();
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
                "dnf system-upgrade clean",
                "dnf clean packages",
                "dnf install rpmconf remove-retired-packages clean-rpm-gpg-pubkey symlinks dracut-config-rescue -y",
                //"remove-retired-packages",
                "rpmconf -at",
                "dnf repoquery --unsatisfied",
                "dnf repoquery --duplicates",
                "dnf remove --duplicates",
                "dnf list --extras",
                @"dnf remove $(dnf repoquery --extras --exclude=kernel,kernel-\*)",
                "dnf autoremove -y",
                "symlinks -r /usr | grep dangling",
                "symlinks -r -d /usr",
                "clean-rpm-gpg-pubkey",
            ];

            RunCommands(commands);
        }
        private static async Task<int> GetCurrentVersion()
        {
            HttpClient httpClient = new ();
            HtmlDocument document = new ();
            Regex regex = new("[0-9]+");
            List<ushort> versions = [];
            var html = await httpClient.GetStringAsync(url);
            httpClient.Dispose();
            document.LoadHtml(html);
            var nodes = document.DocumentNode.SelectNodes("//a/@href");

            foreach (var node in nodes)
            {
                if (!string.IsNullOrEmpty(regex.Match(node.InnerText).ToString()))
                {
                    versions.Add(ushort.Parse(regex.Match(node.InnerText).ToString()));
                }
            }
            Log.Information("Latest version: {0}", versions.OrderByDescending(n => n).FirstOrDefault());
            return versions.OrderByDescending(n => n).FirstOrDefault();
        }

        private static async Task<int> GetLocalVersion()
        {
            string fedoraRelease = await File.ReadAllTextAsync("/etc/fedora-release");
            Regex regex = new ("[0-9]+");
            var strversion = regex.Match(fedoraRelease);
            Log.Information($"Local version: {strversion}");
            return int.Parse(strversion.Value);
        }

        private static void RunCommands(List<string> commands)
        {
            foreach(var command in commands)
            {
                Log.Information("Running command: {0}", command);
                ProcessStartInfo processStartInfo = new("/usr/bin/sudo", command)
                {
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    RedirectStandardInput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process process = new()
                {
                    StartInfo = processStartInfo
                };
                StringBuilder output = new ();
                process.OutputDataReceived += new DataReceivedEventHandler((SocketsHttpHandler, e) =>
                {
                    if(!string.IsNullOrEmpty(e.Data))
                    {
                        output.Append(e.Data +"\n");
                    }
                });
                process.Start();
                process.BeginErrorReadLine();
                process.BeginOutputReadLine();
                process.WaitForExit();
                Log.Information(output.ToString());
                process.Close();
            }
        }
    }
}
