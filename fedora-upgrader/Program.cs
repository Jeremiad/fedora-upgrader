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
            }
            else
            {
                Log.Error("Supported arguments: \n upgrade \n post-upgrade");
            }

            Log.CloseAndFlush();
        }
        private static void Upgrade(int latestVersion)
        {
            List<string> commands = new List<string>();
            commands.Add("dnf upgrade --refresh -y");
            commands.Add("dnf install dnf-plugin-system-upgrade -y");
            commands.Add($"dnf system-upgrade download --refresh --releasever={latestVersion} -y");
            commands.Add("dnf system-upgrade reboot");

            RunCommands(commands);
        }
        private static void PostUpgrade()
        {
            List<string> commands = new List<string>();

            commands.Add("dnf install rpmconf -y");
            commands.Add("rpmconf -at");
            commands.Add("dnf repoquery --unsatisfied");
            commands.Add("dnf repoquery --duplicates");
            commands.Add("dnf list extras");
            commands.Add(@"dnf remove $(dnf repoquery --extras --exclude=kernel,kernel-\*)");
            commands.Add("dnf autoremove -y");
            commands.Add("dnf install symlinks -y");
            commands.Add("symlinks -r /usr | grep dangling");
            commands.Add("symlinks -r -d /usr");

            RunCommands(commands);
        }
        private static async Task<int> GetCurrentVersion()
        {
            HttpClient httpClient = new HttpClient();
            HtmlDocument document = new HtmlDocument();
            var regex = new Regex("[0-9]+");
            List<ushort> versions = new List<ushort>();
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
            var regex = new Regex("[0-9]+");
            var strversion = regex.Match(fedoraRelease);
            Log.Information($"Local version: {strversion}");
            return int.Parse(strversion.Value);
        }

        private static void RunCommands(List<string> commands)
        {
            foreach(var command in commands)
            {
                Log.Information("Running command: {0}", command);
                ProcessStartInfo processStartInfo = new ProcessStartInfo("/usr/bin/sudo", command);
                processStartInfo.RedirectStandardOutput = true;
                processStartInfo.RedirectStandardError = true;
                processStartInfo.RedirectStandardInput = true;
                processStartInfo.UseShellExecute = false;
                processStartInfo.CreateNoWindow = true;
                Process process = new Process();
                process.StartInfo = processStartInfo;
                StringBuilder output = new StringBuilder();
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
