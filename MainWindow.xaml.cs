﻿using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using iTunesLib;
using iTunesRichPresence_Rewrite.Properties;
using MahApps.Metro;
using MahApps.Metro.Controls.Dialogs;
using Microsoft.Win32;
using Octokit;
using Application = System.Windows.Application;
using Button = System.Windows.Controls.Button;
using TextBox = System.Windows.Controls.TextBox;

namespace iTunesRichPresence_Rewrite {
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow {

        private readonly NotifyIcon _notifyIcon;
        private DiscordBridge _bridge;

        private readonly Release _latestRelease;

        private TextBox _lastFocusedTextBox;

        public MainWindow() {
            InitializeComponent();

            Globals.LogBox = LogBox;

            _lastFocusedTextBox = PlayingTopLineFormatTextBox;

            _notifyIcon = new NotifyIcon {Text = "iTunesRichPresence", Visible = false, Icon = System.Drawing.Icon.ExtractAssociatedIcon(Assembly.GetExecutingAssembly().Location)};
            _notifyIcon.MouseDoubleClick += (sender, args) => { WindowState = WindowState.Normal; };

            ThemeComboBox.ItemsSource = ThemeManager.Accents.Select(accent => accent.Name);
            ThemeComboBox.SelectedItem = Settings.Default.Accent;

            ThemeManager.ChangeAppStyle(Application.Current,
                                        ThemeManager.GetAccent(Settings.Default.Accent),
                                        ThemeManager.GetAppTheme("BaseLight"));

            RunOnStartupCheckBox.IsChecked = Settings.Default.RunOnStartup;
            PlayingTopLineFormatTextBox.Text = Settings.Default.PlayingTopLine;
            PlayingBottomLineFormatTextBox.Text = Settings.Default.PlayingBottomLine;
            PausedTopLineFormatTextBox.Text = Settings.Default.PausedTopLine;
            PausedBottomLineFormatTextBox.Text = Settings.Default.PausedBottomLine;
            PlaybackDurationCheckBox.IsChecked = Settings.Default.DisplayPlaybackDuration;
            ExperimentsCheckBox.IsChecked = Settings.Default.ExperimentsEnabled;
            ExperimentsButton.Visibility =
                Settings.Default.ExperimentsEnabled ? Visibility.Visible : Visibility.Collapsed;

            AppNameComboBox.Items.Add("iTunes");
            AppNameComboBox.Items.Add("Apple Music");
            CreateBridge();

            AppNameComboBox.SelectedItem = Settings.Default.AppName;

            var gitHubClient = new GitHubClient(new ProductHeaderValue("iTunesRichPresence"));
            _latestRelease = gitHubClient.Repository.Release.GetLatest("nint8835", "iTunesRichPresence").Result;
            if (!Assembly.GetExecutingAssembly().GetName().Version.ToString().StartsWith(_latestRelease.Name.Substring(1))) {
                UpdateButton.Visibility = Visibility.Visible;
            }

            PopulateToolbox();

        }

        private void CreateBridge() {
            _bridge?.Shutdown();
            _bridge = (string)AppNameComboBox.SelectedItem == "iTunes" ? new DiscordBridge("383816327850360843") : new DiscordBridge("529435150472183819");
        }

        private void PopulateToolbox() {
            var currentToken = 0;
            foreach (var token in _bridge.tokens) {
                if (!token.ShowInToolbox) continue;
                var button = new Button {Content = token.DisplayName};
                button.Click += (sender, args) => {
                    if (_lastFocusedTextBox.SelectionLength != 0) {
                        _lastFocusedTextBox.Text = _lastFocusedTextBox.Text.Replace(_lastFocusedTextBox.SelectedText, token.Token);
                    }
                    else {
                        _lastFocusedTextBox.Text += token.Token; 
                    }
                    
                };
                ToolboxGrid.Children.Add(button);
                Grid.SetRow(button, (int)Math.Floor((double)currentToken/ToolboxGrid.ColumnDefinitions.Count));
                Grid.SetColumn(button, currentToken % ToolboxGrid.ColumnDefinitions.Count);
                currentToken++;
            }
        }

        private void MetroWindow_StateChanged(object sender, EventArgs e) {
            if (WindowState == WindowState.Minimized) {
                ShowInTaskbar = false;
                _notifyIcon.Visible = true;
                _notifyIcon.ShowBalloonTip(5000, "iTunesRichPresence hidden to tray", "iTunesRichPresence has been minimized to the system tray. Double click the icon to show the window again.", ToolTipIcon.None);
            }
            else {
                ShowInTaskbar = true;
                _notifyIcon.Visible = false;
            }
        }

        private void RunOnStartupCheckBox_OnClick(object sender, RoutedEventArgs e) {
            if (RunOnStartupCheckBox.IsChecked ?? false) {
                Settings.Default.RunOnStartup = true;
                Settings.Default.Save();

                Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true)?.SetValue("iTunesRichPresence", Assembly.GetExecutingAssembly().Location);

            }
            else {
                Settings.Default.RunOnStartup = false;
                Settings.Default.Save();

                Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true)?.DeleteValue("iTunesRichPresence");
            }
        }

        private async void AboutButton_OnClick(object sender, RoutedEventArgs e) {
            await this.ShowMessageAsync("",$"iTunesRichPresence v{Assembly.GetExecutingAssembly().GetName().Version}\n\nDeveloped by nint8835 (Riley Flynn)\n\niTunesRichPresence includes portions of a number of open source projects. The licenses of these projects can be found in this program's installation directory.");
        }

        private void MetroWindow_Closing(object sender, CancelEventArgs e) {
            _bridge.Shutdown();
        }

        private void PlayingTopLineFormatTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            Settings.Default.PlayingTopLine = PlayingTopLineFormatTextBox.Text;
            Settings.Default.Save();
        }

        private void PlayingBottomLineFormatTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            Settings.Default.PlayingBottomLine = PlayingBottomLineFormatTextBox.Text;
            Settings.Default.Save();
        }

        private void PausedTopLineFormatTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            Settings.Default.PausedTopLine = PausedTopLineFormatTextBox.Text;
            Settings.Default.Save();
        }

        private void PausedBottomLineFormatTextBox_TextChanged(object sender, TextChangedEventArgs e) {
            Settings.Default.PausedBottomLine = PausedBottomLineFormatTextBox.Text;
            Settings.Default.Save();
        }

        private void TextBox_GotFocus(object sender, RoutedEventArgs e) {
            _lastFocusedTextBox = e.Source as TextBox;
        }

        private void PlaybackDurationCheckBox_Click(object sender, RoutedEventArgs e) {
            Settings.Default.DisplayPlaybackDuration = PlaybackDurationCheckBox.IsChecked ?? true;
            Settings.Default.Save();
        }

        private async void UpdateButton_OnClick(object sender, RoutedEventArgs e) {
            var result = await this.ShowMessageAsync("New version available!", "A new version of iTunesRichPresence is available. Would you like to download it now?", MessageDialogStyle.AffirmativeAndNegative);
            if (result == MessageDialogResult.Affirmative) {
                Process.Start(_latestRelease.HtmlUrl);
            }
        }

        private void SettingsButton_OnClick(object sender, RoutedEventArgs e) {
            SettingsFlyout.IsOpen = true;
        }

        private void ThemeComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            Settings.Default.Accent = (string) ThemeComboBox.SelectedItem;
            Settings.Default.Save();
            ThemeManager.ChangeAppStyle(Application.Current,
                                        ThemeManager.GetAccent(Settings.Default.Accent),
                                        ThemeManager.GetAppTheme("BaseLight"));

        }

        private void ExperimentsCheckBox_OnClick(object sender, RoutedEventArgs e) {
            Settings.Default.ExperimentsEnabled = ExperimentsCheckBox.IsChecked ?? false;
            Settings.Default.Save();
            ExperimentsButton.Visibility =
                Settings.Default.ExperimentsEnabled ? Visibility.Visible : Visibility.Collapsed;
            var state = Settings.Default.ExperimentsEnabled ? "enabled" : "disabled";
            Globals.Log($"Experiments {state}");
        }

        private void ExperimentsButton_OnClick(object sender, RoutedEventArgs e) {
            ExperimentsFlyout.IsOpen = true;
        }

        private void Experiment_PlayButton_OnClick(object sender, RoutedEventArgs e) {
            var track = _bridge.ITunes.LibraryPlaylist.Tracks.ItemByName[Experiment_TrackNameTextBox.Text];
            Globals.Log($"Playing {track.Name} by {track.Artist}");
            track.Play();
        }

        private void AppNameComboBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e) {
            Settings.Default.AppName = (string) AppNameComboBox.SelectedItem;
            CreateBridge();
            Settings.Default.Save();
        }
    }
}
