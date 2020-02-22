using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Net;
using System.IO;
using System.Threading;

namespace SteamAccountSwitcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
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

        public class SteamAccountDisplay
        {
            public string Text { get; set; }
            public string Image { get; set; }
        }

        RegistryKey steamReg = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam", true);

        List<SteamAccount> steamAccounts = new List<SteamAccount>();

        public string parseSteamIDFromConfig(string config, string username)
        {
            Match m = Regex.Match(config, "\"Accounts\"[\\s\\S]+?{[\\s\\S]+?\"" + username + "\"[\\s\\S]+?{[\\s\\S]+?\"SteamID\"[\\s\\S]+?\"([0-9]+?)\"", RegexOptions.IgnoreCase);
            if (m.Success)
                return m.Groups[1].Value;
            return null;
        }

        public void getCurrentData()
        {
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://steamcommunity.com/profiles/" + ((SteamAccount)currentUser.Items[0]).steamID);
            HttpWebResponse response = (HttpWebResponse)req.GetResponse();
            Stream resStream = response.GetResponseStream();
            var sr = new StreamReader(response.GetResponseStream());
            string responseText = sr.ReadToEnd();

            Match m;

            Dispatcher.Invoke((Action)(() =>
            {
                m = Regex.Match(responseText, "<div class=\"playerAvatarAutoSizeInner\">[\\s\\S]+?<img src=\"([\\s\\S]+?)\">", RegexOptions.IgnoreCase);
                if (m.Success) ((SteamAccount)currentUser.Items[0]).profilePhoto = m.Groups[1].Value;

                m = Regex.Match(responseText, "<span class=\"actual_persona_name\">([\\s\\S]+?)<\\/span>", RegexOptions.IgnoreCase);
                if (m.Success) ((SteamAccount)currentUser.Items[0]).name = m.Groups[1].Value;

                currentUser.Items.Refresh();
            }));
        }
        Thread updateInfo;
        public void buildMenu()
        {
            // Current account
            if(updateInfo != null) updateInfo.Abort();
            currentUser.Items.Clear();
            if ((string)steamReg.GetValue("AutoLoginUser", "") != "")
            {
                SteamAccount localUser = new SteamAccount()
                {
                    username = (string)steamReg.GetValue("AutoLoginUser", "No data"),
                    name = (string)steamReg.GetValue("LastGameNameUsed", "No data"),
                    uuid = (string)steamReg.GetValue("PseudoUUID", ""),
                    selected = true,
                    offline = ((int)(steamReg.GetValue("AlreadyRetriedOfflineMode", 0)) == 1),
                    steamID = parseSteamIDFromConfig(System.IO.File.ReadAllText(steam_path.Text + "/config/config.vdf"), (string)steamReg.GetValue("AutoLoginUser", "No data"))
                };

                currentUser.Items.Add(localUser);

                updateInfo = new Thread(getCurrentData);
                updateInfo.Start();
            }

            // Other accounts
            steamAccounts.Clear();
            string mainDir = "accounts";
            if (System.IO.Directory.Exists(mainDir))
            {
                foreach (string subdirectory in System.IO.Directory.GetDirectories(mainDir).Select(System.IO.Path.GetFileName))
                {
                    string profileDir = mainDir + "/" + subdirectory;
                    string configFile = profileDir + "/account.json";
                    if (System.IO.File.Exists(configFile))
                    {
                        SteamAccount account = JsonConvert.DeserializeObject<SteamAccount>(System.IO.File.ReadAllText(configFile));
                        account.selected = false;
                        account.filePath = profileDir;

                        if (currentUser.Items.Count == 0 || !(((SteamAccount)currentUser.Items[0]).selected && ((SteamAccount)currentUser.Items[0]).username == account.username))
                        {
                            steamAccounts.Add(account);
                        }
                    }
                }
            }

            // Show all accounts
            accountsList.Items.Clear();
            foreach (SteamAccount account in steamAccounts)
            {
                int itemID = accountsList.Items.Count;
                accountsList.Items.Add(account);
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            steam_path.Text = (string)steamReg.GetValue("SteamPath", "C:/Program Files (x86)/Steam");
            buildMenu();
        }

        private void changeDirBtn_Click(object sender, RoutedEventArgs e)
        {
            var fbd = new FolderBrowserDialog();
            DialogResult result = fbd.ShowDialog();
            if (!string.IsNullOrWhiteSpace(fbd.SelectedPath))
            {
                steam_path.Text = fbd.SelectedPath;
            }
        }

        private void closeSteam()
        {
            var steamProcesses = Process.GetProcesses().Where(pr => pr.ProcessName.ToLower() == "steam");

            foreach (var process in steamProcesses)
            {
                try
                {
                    process.Kill();
                }
                catch (Exception) {}
            }
        }
        private void openSteam()
        {
            Process.Start(steam_path.Text + "/steam.exe");
        }
        private void backupUser()
        {
            if (currentUser.Items.Count != 0)
            {
                SteamAccount currentAccount = (SteamAccount)currentUser.Items[0];

                string sourceFile, newFile;
                string userFolder = "accounts/" + currentAccount.username;
                System.IO.Directory.CreateDirectory(userFolder + "/steamfiles/config/");

                // Backup %STEAM%/config/config.vdf
                sourceFile = steam_path.Text + "/config/config.vdf";
                newFile = userFolder + "/steamfiles/config/config.vdf";
                if (System.IO.File.Exists(sourceFile))
                {
                    if (System.IO.File.Exists(newFile)) System.IO.File.Delete(newFile);
                    System.IO.File.Copy(sourceFile, newFile);
                }

                // Backup %STEAM%/ssfn* files
                foreach (string file in System.IO.Directory.GetFiles(steam_path.Text, "ssfn*").Select(System.IO.Path.GetFileName))
                {
                    sourceFile = steam_path.Text + "/" + file;
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

        private void logoutUser()
        {
            string sourceFile;

            // Delete %STEAM%/config/config.vdf
            sourceFile = steam_path.Text + "/config/config.vdf";
            if (System.IO.File.Exists(sourceFile)) System.IO.File.Delete(sourceFile);

            // Delete %STEAM%/ssfn* files
            foreach (string file in System.IO.Directory.GetFiles(steam_path.Text, "ssfn*"))
            {
                System.IO.File.Delete(file);
            }

            // Delete RegKeys
            if ((int)steamReg.GetValue("AlreadyRetriedOfflineMode", 99) != 99) steamReg.DeleteValue("AlreadyRetriedOfflineMode");
            if ((string)steamReg.GetValue("AutoLoginUser", "") != "") steamReg.DeleteValue("AutoLoginUser");
            if ((string)steamReg.GetValue("PseudoUUID", "") != "") steamReg.DeleteValue("PseudoUUID");
            if ((string)steamReg.GetValue("LastGameNameUsed", "") != "") steamReg.DeleteValue("LastGameNameUsed");
        }
        private void switchBtn_Click(object sender, RoutedEventArgs e)
        {
            closeSteam();
            backupUser();
            logoutUser();
            
            // Restore from backup
            SteamAccount newAccount = steamAccounts[accountsList.SelectedIndex];

            steamReg.SetValue("AlreadyRetriedOfflineMode", newAccount.offline, RegistryValueKind.DWord);
            steamReg.SetValue("AutoLoginUser", newAccount.username);
            steamReg.SetValue("PseudoUUID", newAccount.uuid);

            string sourceFile, newFile;

            // Restore %STEAM%/config/config.vdf
            sourceFile = newAccount.filePath + "/steamfiles/config/config.vdf";
            newFile = steam_path.Text + "/config/config.vdf";
            if (System.IO.File.Exists(sourceFile))
            {
                if (System.IO.File.Exists(newFile)) System.IO.File.Delete(newFile);
                System.IO.File.Copy(sourceFile, newFile);
            }

            // Restore %STEAM%/ssfn* files
            foreach (string file in System.IO.Directory.GetFiles(newAccount.filePath + "/steamfiles/", "ssfn*").Select(System.IO.Path.GetFileName))
            {
                sourceFile = newAccount.filePath + "/steamfiles/" + file;
                newFile = steam_path.Text + "/" + file;
                if (System.IO.File.Exists(newFile)) System.IO.File.Delete(newFile);
                System.IO.File.Copy(sourceFile, newFile);
            }
            
            openSteam();
            buildMenu();
        }

        private void newUserBtn_Click(object sender, RoutedEventArgs e)
        {
            closeSteam();
            backupUser();
            logoutUser();
            openSteam();
            buildMenu();
        }

        private void accountsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (steamAccounts[accountsList.SelectedIndex].selected)
                {
                    switchBtn.IsEnabled = false;
                }
                else
                {
                    switchBtn.IsEnabled = true;
                }
            }
            catch (Exception)
            {
                switchBtn.IsEnabled = false;
            }
        }
    }
}
