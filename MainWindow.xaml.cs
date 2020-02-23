using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Threading;


namespace SteamAccountSwitcher
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Steam steam = new Steam();
        Thread updateInfoThread;
        string accountDir = "accounts";
        public void buildMenu()
        {
            // Current account
            if(updateInfoThread != null) updateInfoThread.Abort();
            currentUser.Items.Clear();
            if (steam.IsAuthorized())
            {
                currentUser.Items.Add(steam.GetCurrentUser());
                // Get avatar & profile name [sync void]
                updateInfoThread = new Thread(() => {
                    steam.UpdateInfo((SteamAccount)currentUser.Items[0]);
                    Dispatcher.Invoke((Action)(() =>
                    {
                        currentUser.Items.Refresh();
                    }));
                });
                updateInfoThread.Start();
            }

            // Other accounts
            steamAccounts.Items.Clear();
            if (System.IO.Directory.Exists(accountDir))
            {
                foreach (string subdirectory in System.IO.Directory.GetDirectories(accountDir).Select(System.IO.Path.GetFileName))
                {
                    string profileDir = accountDir + "/" + subdirectory;
                    string configFile = profileDir + "/account.json";
                    if (System.IO.File.Exists(configFile))
                    {
                        SteamAccount account = JsonConvert.DeserializeObject<SteamAccount>(System.IO.File.ReadAllText(configFile));
                        account.selected = false;
                        account.filePath = profileDir;

                        if (currentUser.Items.Count == 0 || !(((SteamAccount)currentUser.Items[0]).selected && ((SteamAccount)currentUser.Items[0]).username == account.username))
                        {
                            steamAccounts.Items.Add(account);
                        }
                    }
                }
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            steam_path.Text = steam.GetPath();
            buildMenu();
        }

        private void changeDirBtn_Click(object sender, RoutedEventArgs e)
        {
            var fbd = new FolderBrowserDialog();
            fbd.ShowDialog();
            if (fbd.SelectedPath != "")
            {
                steam.SetPath(fbd.SelectedPath);
                steam_path.Text = steam.GetPath();
            }
        }

        private void switchBtn_Click(object sender, RoutedEventArgs e)
        {
            steam.Close();
            if (steam.IsAuthorized()) steam.Backup(accountDir);
            steam.Logout();
            steam.Restore((SteamAccount)steamAccounts.Items[steamAccounts.SelectedIndex]);
            steam.Open();
            buildMenu();
        }

        private void newUserBtn_Click(object sender, RoutedEventArgs e)
        {
            steam.Close();
            if (steam.IsAuthorized()) steam.Backup(accountDir);
            steam.Logout();
            steam.Open();
            buildMenu();
        }

        private void accountsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switchBtn.IsEnabled = steamAccounts.SelectedIndex != -1;
        }
    }
}
