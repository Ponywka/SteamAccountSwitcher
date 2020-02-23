using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace SteamAccountSwitcher
{
    public class SteamAccount
    {
        public Boolean selected { get; set; }
        public Boolean offline { get; set; }
        public string username { get; set; }
        public string name { get; set; }
        public string uuid { get; set; }
        public string filePath { get; set; }
        public string steamID { get; set; }
        public string profilePhoto { get; set; }
    }
    public class Steam
    {
        protected RegistryKey steamReg = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam", true);
        private string Path;

        public Steam()
        {
            Path = steamReg.GetValue("SteamPath", "C:/Program Files (x86)/Steam").ToString();
        }

        public string GetPath()
        {
            return Path;
        }
        public void SetPath(string NewPath)
        {
            Path = NewPath;
        }

        // Steam relaunch

        public void Open()
        {
            Process.Start(Path + "/steam.exe");
        }

        public void Close()
        {
            var steamProcesses = Process.GetProcesses().Where(pr => pr.ProcessName.ToLower() == "steam");

            foreach (var process in steamProcesses)
            {
                try
                {
                    process.Kill();
                }
                catch (Exception) { }
            }
        }

        // Authorized profile

        private string parseSteamIDFromConfig(string config, string username)
        {
            Match m = Regex.Match(config, "\"Accounts\"[\\s\\S]+?{[\\s\\S]+?\"" + username + "\"[\\s\\S]+?{[\\s\\S]+?\"SteamID\"[\\s\\S]+?\"([0-9]+?)\"", RegexOptions.IgnoreCase);
            if (m.Success)
                return m.Groups[1].Value;
            return null;
        }

        public bool IsAuthorized()
        {
            return (string)steamReg.GetValue("AutoLoginUser", "") != "";
        }
        public SteamAccount GetCurrentUser()
        {
            string username = (string)steamReg.GetValue("AutoLoginUser", "No data");
            string steamID = parseSteamIDFromConfig(System.IO.File.ReadAllText(Path + "/config/config.vdf"), username);
            return new SteamAccount()
            {
                username = username,
                name = (string)steamReg.GetValue("LastGameNameUsed", "No data"),
                uuid = (string)steamReg.GetValue("PseudoUUID", ""),
                selected = true,
                offline = ((int)(steamReg.GetValue("AlreadyRetriedOfflineMode", 0)) == 1),
                steamID = steamID
            };
        }

        public void UpdateInfo(SteamAccount account)
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://steamcommunity.com/profiles/" + account.steamID);
            HttpWebResponse response = (HttpWebResponse)req.GetResponse();
            Stream resStream = response.GetResponseStream();
            var sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();

            Match m;

            m = Regex.Match(responseText, "<div class=\"playerAvatarAutoSizeInner\">[\\s\\S]+?<img src=\"([\\s\\S]+?)\">", RegexOptions.IgnoreCase);
            if (m.Success) account.profilePhoto = m.Groups[1].Value;

            m = Regex.Match(responseText, "<span class=\"actual_persona_name\">([\\s\\S]+?)<\\/span>", RegexOptions.IgnoreCase);
            if (m.Success) account.name = m.Groups[1].Value;
        }

        public void Logout()
        {
            string sourceFile;

            // Delete %STEAM%/config/config.vdf
            sourceFile = Path + "/config/config.vdf";
            if (System.IO.File.Exists(sourceFile)) System.IO.File.Delete(sourceFile);

            // Delete %STEAM%/ssfn* files
            foreach (string file in System.IO.Directory.GetFiles(Path, "ssfn*"))
            {
                System.IO.File.Delete(file);
            }

            // Delete RegKeys
            if (steamReg.GetValueNames().Contains("AlreadyRetriedOfflineMode")) steamReg.DeleteValue("AlreadyRetriedOfflineMode");
            if (steamReg.GetValueNames().Contains("AutoLoginUser")) steamReg.DeleteValue("AutoLoginUser");
            if (steamReg.GetValueNames().Contains("PseudoUUID")) steamReg.DeleteValue("PseudoUUID");
            if (steamReg.GetValueNames().Contains("LastGameNameUsed")) steamReg.DeleteValue("LastGameNameUsed");
        }
        
        public void Restore(SteamAccount newAccount)
        {
            // Restore from backup

            steamReg.SetValue("AlreadyRetriedOfflineMode", newAccount.offline, RegistryValueKind.DWord);
            steamReg.SetValue("AutoLoginUser", newAccount.username);
            steamReg.SetValue("PseudoUUID", newAccount.uuid);

            string sourceFile, newFile;

            // Restore %STEAM%/config/config.vdf
            sourceFile = newAccount.filePath + "/steamfiles/config/config.vdf";
            newFile = Path + "/config/config.vdf";
            if (System.IO.File.Exists(sourceFile))
            {
                if (System.IO.File.Exists(newFile)) System.IO.File.Delete(newFile);
                System.IO.File.Copy(sourceFile, newFile);
            }

            // Restore %STEAM%/ssfn* files
            foreach (string file in System.IO.Directory.GetFiles(newAccount.filePath + "/steamfiles/", "ssfn*").Select(System.IO.Path.GetFileName))
            {
                sourceFile = newAccount.filePath + "/steamfiles/" + file;
                newFile = Path + "/" + file;
                if (System.IO.File.Exists(newFile)) System.IO.File.Delete(newFile);
                System.IO.File.Copy(sourceFile, newFile);
            }
        }

        public void Backup(string outputDir)
        {
            SteamAccount currentAccount = GetCurrentUser();
            UpdateInfo(currentAccount);
                string sourceFile, newFile;
                string userFolder = outputDir + "/" + currentAccount.username;
                System.IO.Directory.CreateDirectory(userFolder + "/steamfiles/config/");

                // Backup %STEAM%/config/config.vdf
                sourceFile = Path + "/config/config.vdf";
                newFile = userFolder + "/steamfiles/config/config.vdf";
                if (System.IO.File.Exists(sourceFile))
                {
                    if (System.IO.File.Exists(newFile)) System.IO.File.Delete(newFile);
                    System.IO.File.Copy(sourceFile, newFile);
                }

                // Backup %STEAM%/ssfn* files
                foreach (string file in System.IO.Directory.GetFiles(Path, "ssfn*").Select(System.IO.Path.GetFileName))
                {
                    sourceFile = Path + "/" + file;
                    newFile = userFolder + "/steamfiles/" + file;
                    if (System.IO.File.Exists(newFile)) System.IO.File.Delete(newFile);
                    System.IO.File.Copy(sourceFile, newFile);
                }

                // Export data
                newFile = userFolder + "/account.json";
                if (System.IO.File.Exists(newFile)) System.IO.File.Delete(newFile);

                string jsonString = JsonConvert.SerializeObject(currentAccount);
                System.IO.File.WriteAllText(newFile, jsonString);
        }
    }
}
