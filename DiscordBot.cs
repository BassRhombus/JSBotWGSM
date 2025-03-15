using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.GameServer.Query;
using Newtonsoft.Json.Linq;

namespace WindowsGSM.Plugins
{
    public class DiscordBot : SteamCMDAgent
    {
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGSM Discord.js Bot",
            author = "Bass Rhombus",
            description = "WindowsGSM plugin for hosting Discord.js bots",
            version = "1.0",
            url = "https://github.com/YourUsername/WindowsGSM.DiscordBot",
            color = "#7289DA" // Discord color
        };

        // - Standard Constructor and Properties
        public DiscordBot(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;
        public new string Error, Notice;

        // - Game server Fixed variables
        public override string StartPath => "node";
        public string FullName = "Discord.js Bot";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 1;
        public object QueryMethod = new A2S();

        // - Game server default values
        public string Port = "3000";
        public string QueryPort = "3000";
        public string Defaultmap = "";
        public string Maxplayers = "1";
        public string Additional = "";

        // - GitHub Settings
        public string GitHubUsername = "";
        public string GitHubToken = "";
        public string GitHubBranch = "main";
        public string GitHubRepo = "";
        public bool AutoUpdateOnRestart = false;

        // Add missing ServerName property
        public string ServerName = "Discord.js Bot";

        public async Task<Process> Start()
        {
            string path = ServerPath.GetServersServerFiles(_serverData.ServerID);
            string fullPath = Path.Combine(path, "bot.js");

            // Check if directory exists
            if (!Directory.Exists(path))
            {
                Error = $"Directory {path} does not exist";
                return null;
            }

            // Check if bot.js exists, if not try to pull from GitHub
            if (!File.Exists(fullPath) && !string.IsNullOrEmpty(_serverData.GetType().GetProperty("GitHubRepo")?.GetValue(_serverData)?.ToString()))
            {
                await InstallFromGitHub();
            }

            // Check if auto-update is enabled and update if needed
            if (await ShouldUpdate())
            {
                await UpdateFromGitHub();
            }

            // Ensure npm packages are installed
            if (Directory.Exists(path) && File.Exists(Path.Combine(path, "package.json")))
            {
                var npmProcess = new Process
                {
                    StartInfo =
                    {
                        FileName = "npm",
                        Arguments = "install",
                        WorkingDirectory = path,
                        WindowStyle = ProcessWindowStyle.Hidden,
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    }
                };

                try
                {
                    npmProcess.Start();
                    await npmProcess.WaitForExitAsync();
                }
                catch (Exception e)
                {
                    Error = e.Message;
                }
            }

            // Start the Discord.js bot
            var startProcess = new Process
            {
                StartInfo =
                {
                    FileName = StartPath,
                    Arguments = $"bot.js {_serverData.GetType().GetProperty("Additional")?.GetValue(_serverData)}",
                    WorkingDirectory = path,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                },
                EnableRaisingEvents = true
            };

            // Start Discord.js bot
            try
            {
                startProcess.Start();
                return startProcess;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null;
            }
        }

        public async Task<bool> ShouldUpdate()
        {
            bool autoUpdate = false;
            var autoUpdateProp = _serverData.GetType().GetProperty("AutoUpdateOnRestart");
            if (autoUpdateProp != null)
            {
                var autoUpdateValue = autoUpdateProp.GetValue(_serverData)?.ToString();
                bool.TryParse(autoUpdateValue, out autoUpdate);
            }
            
            if (!autoUpdate)
                return false;

            var repoProp = _serverData.GetType().GetProperty("GitHubRepo");
            return repoProp != null && !string.IsNullOrEmpty(repoProp.GetValue(_serverData)?.ToString());
        }

        public async Task InstallFromGitHub()
        {
            string path = ServerPath.GetServersServerFiles(_serverData.ServerID);
            
            // Get properties via reflection
            var usernameProp = _serverData.GetType().GetProperty("GitHubUsername");
            var tokenProp = _serverData.GetType().GetProperty("GitHubToken");
            var repoProp = _serverData.GetType().GetProperty("GitHubRepo");
            var branchProp = _serverData.GetType().GetProperty("GitHubBranch");
            
            string username = usernameProp?.GetValue(_serverData)?.ToString() ?? "";
            string token = tokenProp?.GetValue(_serverData)?.ToString() ?? "";
            string repo = repoProp?.GetValue(_serverData)?.ToString() ?? "";
            string branch = branchProp?.GetValue(_serverData)?.ToString() ?? "main";

            if (string.IsNullOrEmpty(repo))
            {
                Error = "GitHub repository URL is required";
                return;
            }

            // Clone the repository
            string gitUrl = repo;
            if (!string.IsNullOrEmpty(username) && !string.IsNullOrEmpty(token))
            {
                // Format URL with authentication if provided
                Uri repoUri = new Uri(repo);
                string userInfo = $"{username}:{token}@";
                gitUrl = $"{repoUri.Scheme}://{userInfo}{repoUri.Host}{repoUri.PathAndQuery}";
            }

            var gitProcess = new Process
            {
                StartInfo =
                {
                    FileName = "git",
                    Arguments = $"clone -b {branch} {gitUrl} \"{path}\"",
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            try
            {
                gitProcess.Start();
                await gitProcess.WaitForExitAsync();
            }
            catch (Exception e)
            {
                Error = e.Message;
            }
        }

        public async Task UpdateFromGitHub()
        {
            string path = ServerPath.GetServersServerFiles(_serverData.ServerID);

            var gitProcess = new Process
            {
                StartInfo =
                {
                    FileName = "git",
                    Arguments = "pull",
                    WorkingDirectory = path,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };

            try
            {
                gitProcess.Start();
                await gitProcess.WaitForExitAsync();
            }
            catch (Exception e)
            {
                Error = e.Message;
            }
        }

        public async Task<Process> Stop(Process process)
        {
            await Task.Run(() =>
            {
                if (process.StartInfo.FileName.Equals("node"))
                {
                    process.Kill();
                }
            });
            return process;
        }

        public new async Task<Process> Install()
        {
            await InstallFromGitHub();
            return null;
        }

        public new async Task<string> GetLocalBuild()
        {
            return "1.0.0";
        }

        public new async Task<string> GetRemoteBuild()
        {
            return "1.0.0";
        }

        public new bool IsInstallValid()
        {
            return true;
        }

        public new bool IsImportValid(string path)
        {
            return true;
        }

        public async Task<object> Update()
        {
            await UpdateFromGitHub();
            return null;
        }

        public JObject GetServerConfig()
        {
            return new JObject
            {
                ["ServerName"] = ServerName,
                ["GitHubUsername"] = GitHubUsername,
                ["GitHubToken"] = GitHubToken,
                ["GitHubBranch"] = GitHubBranch,
                ["GitHubRepo"] = GitHubRepo,
                ["AutoUpdateOnRestart"] = AutoUpdateOnRestart.ToString(),
                ["Additional"] = Additional
            };
        }
    }
}