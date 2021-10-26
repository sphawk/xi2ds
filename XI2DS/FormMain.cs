﻿using System;
using System.Drawing;
using System.Diagnostics;
using System.Windows.Forms;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using SharpDX.XInput;
using XI2DS.Xinput;
using XI2DS.DualShock4;
using System.Reflection;

namespace XI2DS
{
    public partial class FormMain : Form, ControllerStautsReceiver, XInputStateReceiver, FeedBackReceiver
    {
        readonly ViGEmClient client;
        readonly XInputController[] xInputControllers;
        readonly DS4Controller[] ds4Controllers;
        readonly Button[] connectionButtons;
        readonly PictureBox[] batteryIndicators;
        readonly PictureBox[] connectionIndicators;        
        readonly FormTest formTest = new FormTest();

        public FormMain()
        {
            InitializeComponent();

            this.Text = String.Format("{0} v{1}", AssemblyTitle, AssemblyVersion);

            connectionButtons = new[] {
                uiButtonConnectController1, uiButtonConnectController2,
                uiButtonConnectController3, uiButtonConnectController4
            };

            batteryIndicators = new[] {
                uiImageBatteryInfoController1, uiImageBatteryInfoController2,
                uiImageBatteryInfoController3, uiImageBatteryInfoController4
            };

            connectionIndicators = new[] {
               uiImageConnectionController1, uiImageConnectionController2,
               uiImageConnectionController3, uiImageConnectionController4
            };

            xInputControllers = new[] {
                new XInputController(UserIndex.One, this, this),
                new XInputController(UserIndex.Two, this, this),
                new XInputController(UserIndex.Three, this, this),
                new XInputController(UserIndex.Four, this, this)
            };
                        
            notifyIcon.ContextMenuStrip = new ContextMenuStrip();
            notifyIcon.ContextMenuStrip.Items.Add("Show").Click += (object sender, EventArgs e) =>
            {
                ShowApplication();
            };

            notifyIcon.ContextMenuStrip.Items.Add("Hide").Click += (object sender, EventArgs e) =>
            {
                HideApplication();
            };

            notifyIcon.ContextMenuStrip.Items.Add("Exit").Click += (object sender, EventArgs e) =>
            {
                ExitApplication();
            };
            
            try
            {
                client = new ViGEmClient();
                ds4Controllers = new[] {
                    new DS4Controller(client, (int)UserIndex.One, this),
                    new DS4Controller(client, (int)UserIndex.Two, this),
                    new DS4Controller(client, (int)UserIndex.Three, this),
                    new DS4Controller(client, (int)UserIndex.Four, this)
                };
                
            }
            catch (VigemBusNotFoundException e)
            {                
                if (MessageBox.Show("ViGEm bus driver is not found. Please check to install driver. " +
                    "Press OK button to go to driver download page.", 
                    "Driver Not Found",
                    MessageBoxButtons.OKCancel, MessageBoxIcon.Information) == DialogResult.OK)
                {
                    Process.Start("https://github.com/ViGEm/ViGEmBus/releases");
                }
                throw e;
            }
        }

        public string AssemblyTitle {
            get {
                object[] attributes = Assembly.GetExecutingAssembly().GetCustomAttributes(typeof(AssemblyTitleAttribute), false);
                if (attributes.Length > 0)
                {
                    AssemblyTitleAttribute titleAttribute = (AssemblyTitleAttribute)attributes[0];
                    if (titleAttribute.Title != "")
                    {
                        return titleAttribute.Title;
                    }
                }
                return System.IO.Path.GetFileNameWithoutExtension(Assembly.GetExecutingAssembly().CodeBase);
            }
        }

        public string AssemblyVersion {
            get {
                return Assembly.GetExecutingAssembly().GetName().Version.ToString();
            }
        }

        private Image GetBatteryImage(BatteryType type, BatteryLevel level)
        {
            int index = 0;
            switch (type)
            {
                case BatteryType.Alkaline:
                case BatteryType.Nimh:
                    index = (int)level;
                    break;

                case BatteryType.Wired:
                    index = 5;
                    break;
                default:
                case BatteryType.Disconnected:
                case BatteryType.Unknown:
                    index = 4;
                    break;
            }            
            return imageListBattery.Images[index];
        }

        private Image GetConnectionImage(bool isConnected)
        {
            return isConnected ? Properties.Resources.controller_connected 
                : Properties.Resources.controller_not_connected;
        }

        private void ShowApplication()
        {
            this.ShowInTaskbar = true;
            this.Visible = true;
        }
        private void HideApplication()
        {            
            this.ShowInTaskbar = false;
            this.Visible = false;            
        }
        private void ExitApplication()
        {
            foreach (DS4Controller controller in ds4Controllers)
            {
                controller.Disconnect();
            }

            foreach (XInputController controller in xInputControllers)
            {
                controller.StopScan();
            }
            
            client.Dispose();

            Application.ExitThread();
            Application.Exit();

        }

        private void ButtonConnect_Click(object sender, EventArgs e)
        {
            Button button = (Button)sender;
            int userIndex = Convert.ToInt32(button.Tag);

            if (ds4Controllers[userIndex].IsConnected)
            {
                xInputControllers[userIndex].StopScan();
                ds4Controllers[userIndex].Disconnect();
                connectionButtons[userIndex].Text = "DS4 Connect";
            } else {
                ds4Controllers[userIndex].Connect();
                xInputControllers[userIndex].StartScan();
                connectionButtons[userIndex].Text = "DS4 Disconnect";
            }
        }       

        private void FormMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            e.Cancel = true;
            HideApplication();
        }

        public void OnFeedBackReceived(int userIndex, byte smallMotor, byte largeMotor)
        {
            Debug.WriteLine("{0}, {1}, {2}", userIndex, smallMotor, largeMotor);
            xInputControllers[userIndex].Vibrate(smallMotor, largeMotor);
        }

        public void OnStatusUpdated(int userIndex, bool isConnected, BatteryInformation information)
        {
            Debug.WriteLine("{0}, {1}, {2}, {3}", userIndex, isConnected, information.BatteryType, information.BatteryLevel);
            this.Invoke((MethodInvoker) delegate {
                if (isConnected)
                {
                    connectionButtons[userIndex].Enabled = true;
                }
                else
                {
                    connectionButtons[userIndex].Text = "DS4 Connect";
                    connectionButtons[userIndex].Enabled = false;
                    ds4Controllers[userIndex].Disconnect();
                }
                batteryIndicators[userIndex].Image = GetBatteryImage(information.BatteryType, information.BatteryLevel);
                connectionIndicators[userIndex].Image = GetConnectionImage(isConnected);
            });
        }

        public void OnStateUpdated(int userIndex, State state)
        {
            var report = Utils.XInputStateToDS4Report(state);
            ds4Controllers[userIndex].SendReport(report);
            if (formTest.Visible)
            {
                formTest.ShowState(userIndex, state);
            }
        }

        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            FormAbout formAbout = new FormAbout();
            formAbout.ShowDialog();
        }

        private void InputTestToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (formTest.Visible)
            {
                formTest.Hide();
            }
            else
            {
                formTest.Show();
            }
        }

        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ExitApplication();
        }

        private void NotifyIcon_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            ShowApplication();
        }

    }
}