using Microsoft.Win32;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
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

namespace SteamAccountSwitcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public struct SteamAccount
        {
            public Boolean selected, offline;
            public string username, uuid;
        }

        RegistryKey steamReg = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam", true);

        public SteamAccount[] accountsData;

        public void buildMenu()
        {
            accounts.Items.Clear();
            accountsData = new SteamAccount[255];

            // Current account
            steam_path.Text = (string)steamReg.GetValue("SteamPath", "C:/Program Files (x86)/Steam");
            if ((string)steamReg.GetValue("AutoLoginUser", "") != "")
            {
                string steam_username = (string)steamReg.GetValue("AutoLoginUser", "No data");
                accountsData[0].username = steam_username;
                accountsData[0].uuid = (string)steamReg.GetValue("PseudoUUID", "");
                accountsData[0].selected = true;
                accountsData[0].offline = ((int)(steamReg.GetValue("AlreadyRetriedOfflineMode", 0)) == 1);

                accounts.Items.Add("(Current) " + accountsData[0].username);
            }

            // Other accounts
            if (System.IO.Directory.Exists("accounts/"))
            {
                string mainDir = "accounts";
                foreach (string subdirectory in System.IO.Directory.GetDirectories(mainDir).Select(System.IO.Path.GetFileName))
                {
                    string configFile = mainDir + "/" + subdirectory + "/account.json";
                    if (System.IO.File.Exists(configFile))
                    {
                        SteamAccount account = JsonConvert.DeserializeObject<SteamAccount>(System.IO.File.ReadAllText(configFile));
                        account.selected = false;
                        if (!(accountsData[0].selected && accountsData[0].username == account.username))
                        {
                            accountsData[accounts.Items.Count] = account;
                            accounts.Items.Add(account.username);
                        }
                    }
                }
            }
        }
        public MainWindow()
        {
            InitializeComponent();
            buildMenu();
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            var fbd = new FolderBrowserDialog();
            DialogResult result = fbd.ShowDialog();
            if (!string.IsNullOrWhiteSpace(fbd.SelectedPath))
            {
                steam_path.Text = fbd.SelectedPath;
            }
        }

        private void accounts_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (accountsData[accounts.SelectedIndex].selected)
                {
                    switchBtn.IsEnabled = false;
                }
                else
                {
                    switchBtn.IsEnabled = true;
                }
            }catch (Exception)
            {
                switchBtn.IsEnabled = false;
            }
        }

        private void closeSteam()
        {
            var steamProcesses = Process.GetProcesses().Where(pr => pr.ProcessName.ToLower() == "steam");

            foreach (var process in steamProcesses)
            {
                process.Kill();
            }
        }
        private void openSteam()
        {
            Process.Start(steam_path.Text + "/steam.exe");
        }
        private void backupUser()
        {
            if ((string)steamReg.GetValue("AutoLoginUser", "") != "")
            {
                SteamAccount currentAccount;
                currentAccount.username = (string)steamReg.GetValue("AutoLoginUser", "No data");
                currentAccount.uuid = (string)steamReg.GetValue("PseudoUUID", "");
                currentAccount.selected = true;
                currentAccount.offline = ((int)(steamReg.GetValue("AlreadyRetriedOfflineMode", 0)) == 1);

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
        }
        private void switchBtn_Click(object sender, RoutedEventArgs e)
        {
            closeSteam();
            backupUser();
            logoutUser();

            // Restore from backup
            int selectedIndex = accounts.SelectedIndex;
            SteamAccount newAccount = accountsData[selectedIndex];

            steamReg.SetValue("AlreadyRetriedOfflineMode", newAccount.offline, RegistryValueKind.DWord);
            steamReg.SetValue("AutoLoginUser", newAccount.username);
            steamReg.SetValue("PseudoUUID", newAccount.uuid);

            string sourceFile, newFile;
            string userFolder = "accounts/" + newAccount.username;

            // Restore %STEAM%/config/config.vdf
            sourceFile = userFolder + "/steamfiles/config/config.vdf";
            newFile = steam_path.Text + "/config/config.vdf";
            if (System.IO.File.Exists(sourceFile))
            {
                if (System.IO.File.Exists(newFile)) System.IO.File.Delete(newFile);
                System.IO.File.Copy(sourceFile, newFile);
            }

            // Restore %STEAM%/ssfn* files
            foreach (string file in System.IO.Directory.GetFiles(userFolder + "/steamfiles/", "ssfn*").Select(System.IO.Path.GetFileName))
            {
                sourceFile = userFolder + "/steamfiles/" + file;
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
    }
}
