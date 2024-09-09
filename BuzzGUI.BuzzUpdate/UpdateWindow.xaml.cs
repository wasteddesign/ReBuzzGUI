using BuzzGUI.Interfaces;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Windows;

namespace BuzzGUI.BuzzUpdate
{
    public partial class UpdateWindow : Window
    {
        static string UserAgentString;
        static int currentBuild;
        static int latestBuild;
        string localFile;
        string localSignatureFile;

        string setupUrl { get { return Environment.Is64BitProcess ? "http://jeskola.net/buzz/beta/files/setup_x64/" : "http://jeskola.net/buzz/beta/files/setup/"; } }
        string setupExe { get { return Environment.Is64BitProcess ? "BuzzSetup_x64_" : "BuzzSetup"; } }

        static int ParseBuildNumber(string s)
        {
            s = s.Replace("\"", "");
            int x;
            if (!int.TryParse(s, out x) || x < 1000)
                return 0;

            return x;
        }

        public UpdateWindow()
        {
            InitializeComponent();

            verText.Text = "Current Build: " + currentBuild.ToString() + "   Latest Build: " + latestBuild.ToString();

            changelogBox.Text = "Downloading changelog...";
            DownloadChangelog();

        }

        public static void DownloadBuildCount(IBuzz buzz)
        {
            currentBuild = buzz.BuildNumber;
            UserAgentString = "Buzz Update " + currentBuild.ToString();

            WebRequest.DefaultWebProxy = null;
            WebClient wc = new WebClient();
            wc.Headers.Add("user-agent", UserAgentString);
            wc.DownloadStringCompleted += (sender, e) =>
            {
                if (!e.Cancelled && e.Error == null)
                {
                    latestBuild = ParseBuildNumber(e.Result);

                    if (currentBuild >= latestBuild)
                    {
                        buzz.DCWriteLine("[BuzzUpdate] No updates available.");
                    }
                    else
                    {
                        UpdateWindow w = new UpdateWindow();
                        w.Show();
                    }


                }
                else if (e.Error != null)
                {
                    buzz.DCWriteLine("[BuzzUpdate] " + e.Error.ToString());
                }
            };

            try
            {
                wc.DownloadStringAsync(new Uri("http://jeskola.net/buzz/buildcount"));
            }
            catch (Exception e)
            {
                buzz.DCWriteLine("[BuzzUpdate] " + e.Message);
            }

        }



        void DownloadChangelog()
        {
            WebClient wc = new WebClient();
            wc.Headers.Add("user-agent", UserAgentString);
            wc.DownloadStringCompleted += (sender, e) =>
            {
                if (!e.Cancelled && e.Error == null)
                {
                    changelogBox.Text = e.Result;
                }
                else if (e.Error != null)
                {
                    changelogBox.Text = e.Error.ToString();
                }
            };

            try
            {
                wc.DownloadStringAsync(new Uri("http://jeskola.net/buzz/beta/files/changelog.txt"));
            }
            catch (Exception e)
            {
                changelogBox.Text = e.Message;
            }
        }

        void VerifySignature()
        {
            var signature = File.ReadAllBytes(localSignatureFile);
            var key = CngKey.Import(Resource1.InstallerSignKey, CngKeyBlobFormat.EccPublicBlob);
            var dsa = new ECDsaCng(key);
            using (var fs = File.OpenRead(localFile))
            {
                if (!dsa.VerifyData(fs, signature))
                    throw new Exception();
            }
        }

        void DownloadInstaller()
        {
            WebClient wc = new WebClient();
            wc.Headers.Add("user-agent", UserAgentString);
            wc.DownloadFileCompleted += (sender, e) =>
            {
                if (!e.Cancelled && e.Error == null)
                {
                    progressBar.Value = 0;
                    progressBar.Visibility = Visibility.Collapsed;
                    try
                    {
                        VerifySignature();
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Signature verification failed.\n" + ex.Message, "Buzz Update");
                    }

                    button.Content = "Install...";
                    button.IsEnabled = true;
                }
                else if (e.Error != null)
                {
                    MessageBox.Show(e.Error.ToString(), "Buzz Update");
                }
            };
            wc.DownloadProgressChanged += (sender, e) =>
            {
                progressBar.Value = e.ProgressPercentage;
            };

            progressBar.Visibility = Visibility.Visible;

            try
            {
                string exename = setupExe + latestBuild.ToString() + ".exe";
                localFile = System.IO.Path.GetTempPath() + exename;

                wc.DownloadFileAsync(new Uri(setupUrl + exename), localFile);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Buzz Update");
            }
        }

        void DownloadSignature()
        {
            WebClient wc = new WebClient();
            wc.Headers.Add("user-agent", UserAgentString);
            wc.DownloadFileCompleted += (sender, e) =>
            {
                if (!e.Cancelled && e.Error == null)
                {
                    progressBar.Value = 0;
                    progressBar.Visibility = Visibility.Collapsed;
                    DownloadInstaller();
                }
                else if (e.Error != null)
                {
                    MessageBox.Show(e.Error.ToString(), "Buzz Update");
                }
            };
            wc.DownloadProgressChanged += (sender, e) =>
            {
                progressBar.Value = e.ProgressPercentage;
            };

            progressBar.Visibility = Visibility.Visible;

            try
            {
                string signaturename = setupExe + latestBuild.ToString() + ".exe.ecdsa";
                localSignatureFile = System.IO.Path.GetTempPath() + signaturename;

                wc.DownloadFileAsync(new Uri(setupUrl + "signatures/" + signaturename), localSignatureFile);
            }
            catch (Exception e)
            {
                MessageBox.Show(e.Message, "Buzz Update");
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (localFile == null)
            {
                button.IsEnabled = false;
                DownloadSignature();
            }
            else
            {

                Process p = new Process();
                p.StartInfo.FileName = localFile;
                p.StartInfo.UseShellExecute = true;
                p.StartInfo.Arguments = "/silent";
                p.Start();

                Process.GetCurrentProcess().Kill();
            }
        }

    }
}
