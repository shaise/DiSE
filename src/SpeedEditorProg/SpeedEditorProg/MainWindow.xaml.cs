using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using HidLibrary;
using Microsoft.Win32;
using System.Windows.Threading;

namespace SpeedEditorProg
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private const int VendorId = 1155;
        private const int ProductId = 22334;

        private const int MSG_SET_KEY = 0x01;
        private const int MSG_GET_KEY = 0x02;
        private const int MSG_SAVE_SETTINGS	= 0x03;
        private const int MSG_SET_JOG_DATA = 0x04;
        private const int MSG_GET_JOG_DATA = 0x05;
        private const int MSG_SET_JOG_TYPE = 0x06;
        private const int MSG_GET_JOG_TYPE = 0x07;
        private const int MSG_FACTORY_DEFAULT = 0x08;
        private const int MSG_GET_VERSION = 0x09;

        private const int MSG_KEY_DATA = 0x41;
        private const int MSG_KEY_ACTIONS = 0x42;
        private const int MSG_SAVE_RESULT = 0x43;
        private const int MSG_JOG_DATA = 0x44;
        private const int MSG_JOG_TYPE = 0x45;
        private const int MSG_VERSION = 0x46;

        private const int ACTIONS_PRESS_START = 2;
        private const int ACTIONS_RELEASE_START = 10;
        private const int MAX_ACTIONS = 8;
        private const int JOG_ANG_LOC = ACTIONS_RELEASE_START + MAX_ACTIONS;
        private const int JOG_RPM_LOC = JOG_ANG_LOC + 2;


        private bool deviceActive = false;
        private HidDevice kbdDevice = null;
        System.Windows.Threading.DispatcherTimer dispatcherTimer;
        string initialTitle;
        KbdHandler kbdHandler;
        Brush pressedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFE2CCE2"));
        Brush releasedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#f8f8f8"));
        Brush checkedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#CCC"));
        Brush uncheckedBrush;
        WUpdateCode setKeyWin;
        int curKey = 0;
        int curCode = 0;
        int curAltCode = 0;
        int curAltType = 0;
        int curGroup = 0;
        int curModifiers = 0;
        int curAltModifiers = 0;
        int curJogMode = 0;
        int curJogRateSelect = 0;
        int curJogDir = 0;
        int curJogMinRate = 0;
        int curJogMaxRate = 0;
        int curJogRpm = 0;
        int curJogAngle = 0;
        bool curShuttleMode = false;
        System.Windows.Shapes.Path pconnect = null;
        System.Windows.Shapes.Path pdisconnect = null;
        bool batchmode = false;
        Semaphore smReadSem;
        HidReport lastReport;
        string saveFileName;
        string lastError;
        SaveFileDialog saveFileDlg;
        OpenFileDialog openFileDlg;
        int saveProgressCnt = 0;
        bool dataReady = false;
        double jogCentX, jogCentY, jogWheelRadius;
        string connectionTitle = "";

        bool isCstyle = false;


        public MainWindow()
        {
            InitializeComponent();
            initialTitle = Title;
            kbdHandler = new KbdHandler();
            dispatcherTimer = new System.Windows.Threading.DispatcherTimer();
            dispatcherTimer.Tick += DispatcherTimer_Tick;
            dispatcherTimer.Interval = new TimeSpan(0,0,0,0,500);
            dispatcherTimer.Start();
            setKeyWin = new WUpdateCode(kbdHandler);
            setKeyWin.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            SetTitle(false, "Disconnected");
            pconnect = (System.Windows.Shapes.Path)FindResource("connectedIcon");
            pdisconnect = (System.Windows.Shapes.Path)FindResource("disconnectedIcon");
            string appFolder = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            saveFileDlg = new SaveFileDialog
            {
                FileName = "",
                Filter = "DiSE programing files (*.DiSE)|*.DiSE",
                InitialDirectory = appFolder
            };
            openFileDlg = new OpenFileDialog
            {
                Filter = "DiSE programing files (*.DiSE)|*.DiSE",
                InitialDirectory = appFolder
            };
            smReadSem = new Semaphore(0, 1);
            jogCentX = JogWheel.Margin.Left + JogWheel.Width / 2 - JogWheelAngle.Width / 2;
            jogCentY = JogWheel.Margin.Top + JogWheel.Height / 2 - JogWheelAngle.Height / 2;
            jogWheelRadius = JogWheel.Height / 2;
        }

        int WheelJogMode
        {
            get { return comboJogMode.SelectedIndex;  }
            set { comboJogMode.SelectedIndex = value; }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (setKeyWin != null)
                setKeyWin.Close();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);
            e.Handled = true;
        }

        private void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            InitDevice();
        }

        private void KeyButtClick(object sender, RoutedEventArgs e)
        {
            setKeyWin.UpdateKeyData(curKey, curCode, curModifiers, curGroup, curAltType, curAltCode, curAltModifiers, curJogMode);
            setKeyWin.Owner = this;
            setKeyWin.ShowDialog();
            if (setKeyWin.OkPressed == true)
            {
                curCode = setKeyWin.Code;
                curModifiers = setKeyWin.Modifiers;
                curAltCode = setKeyWin.AltCode;
                curAltModifiers = setKeyWin.AltModifiers;
                curGroup = setKeyWin.Group;
                curAltType = setKeyWin.AltType;
                curJogMode = setKeyWin.JogMode;
                keyData.Content = kbdHandler.KeyCombinationName(curCode, curModifiers);
                UpdateKbdKey(curKey, curCode, curModifiers, curGroup, curAltType, curAltCode, curAltModifiers, curJogMode);
            }
        }

        private void UpdateKbdKey(int key, int code, int mod, int grp, int alttype, int altcode, int altmod, int jog_sel)
        {
            byte[] data = new byte[64];
            data[1] = MSG_SET_KEY;
            data[2] = 7;
            data[3] = (byte)key;
            data[4] = (byte)code;
            data[5] = (byte)mod;
            data[6] = (byte)grp;
            data[7] = (byte)alttype;
            data[8] = (byte)altcode;
            data[9] = (byte)altmod;
            data[10] = (byte)jog_sel;
            HidReport report = new HidReport(64, new HidDeviceData(data, HidDeviceData.ReadStatus.Success));
            kbdDevice.WriteReport(report);
        }

        // typedef struct
        // { 
        //    uint8_t cmd;
        //    uint8_t datalen;
        //    uint8_t mode;       // one of 6 modes (0 - 5)
        //    uint8_t rate_sel;   // one of 3 rates (0 - 2)
        //    uint8_t dir;        // 0 = cw, 1 = ccw
        //    uint8_t keycode;
        //    uint8_t modifiers;
        //    uint8_t min_rate;
        //    uint8_t max_rate;
        // } sMsgJogData;
        private void UpdateJogData(int mode, int rate_sel, int dir, int code, int modifiers, int min_rate, int max_rate)
        {
            byte[] data = new byte[64];
            data[1] = MSG_SET_JOG_DATA;
            data[2] = 7;
            data[3] = (byte)mode;
            data[4] = (byte)rate_sel;
            data[5] = (byte)dir;
            data[6] = (byte)code;
            data[7] = (byte)modifiers;
            data[8] = (byte)min_rate;
            data[9] = (byte)max_rate;
            HidReport report = new HidReport(64, new HidDeviceData(data, HidDeviceData.ReadStatus.Success));
            kbdDevice.WriteReport(report);
        }

        private void UsbGetKeyData(int keyid)
        {
            dataReady = false;
            byte[] data = new byte[64];
            data[1] = MSG_GET_KEY;
            data[2] = 1;
            data[3] = (byte)keyid;
            HidReport report = new HidReport(64, new HidDeviceData(data, HidDeviceData.ReadStatus.Success));
            kbdDevice.WriteReport(report);
        }

        private void KeyMouseEnter(object sender, MouseEventArgs e)
        {
            if (!deviceActive || batchmode)
                return;
            // request key data
            string keyidstr = ((Button)sender).Name.Substring(4);
            int keyid = int.Parse(keyidstr);
            UsbGetKeyData(keyid);
        }

        private void SetTitle(bool isConnected, string ttl)
        {
            connectionTitle = initialTitle + " - " + ttl;
            Title = connectionTitle;
            labelConnect.Content = isConnected ?  pconnect : pdisconnect;
            if (isConnected)
                UsbGetFwVersion();
        }

        private void InitDevice()
        {
            if (kbdDevice != null)
                return;
            kbdDevice = HidDevices.Enumerate(VendorId, ProductId).LastOrDefault();
            if (kbdDevice == null)
                return;
            dispatcherTimer.Stop();
            SetTitle(true, "Connected");
            kbdDevice.Inserted += KbdDevice_Inserted;
            kbdDevice.Removed += KbdDevice_Removed;
            kbdDevice.MonitorDeviceEvents = true;
        }

        private void OnReport(HidReport report)
        {
            if (batchmode)
            {
                lastReport = report;
                smReadSem.Release();
            }
            else
                Dispatcher.Invoke(new Action(() => { OnReportInvoked(report); }));
            if (report.Data[0] != 0)
                kbdDevice.ReadReport(OnReport);
            dataReady = true;
        }

        private void OnReportInvoked(HidReport report)
        {
            switch (report.Data[0])
            {
                case MSG_KEY_ACTIONS:
                    HandleKeyActions(report.Data);
                    break;

                case MSG_KEY_DATA:
                    HandleKeyData(report.Data);
                    break;

                case MSG_SAVE_RESULT:
                    HandleSaveResult(report.Data);
                    break;

                case MSG_JOG_TYPE:
                    HandleJogType(report.Data);
                    break;

                case MSG_JOG_DATA:
                    HandleJogData(report.Data);
                    break;

                case MSG_VERSION:
                    HandleVersion(report.Data);
                    break;
            }
        }

        private void SetShuttleMode(bool isShuttle)
        {
            curShuttleMode = isShuttle;
            buttShtl.Background = curShuttleMode ? checkedBrush : uncheckedBrush;
        }

        //    typedef struct
        //    {
        //        uint8_t cmd;
        //        uint8_t datalen;
        //        uint8_t mode;   // one of 3 modes (0 - 2)
        //        uint8_t type;   // 0 = normal, 1 = shuttle mode
        //    }
        //    sMsgJogType;

        private void HandleJogType(byte[] data)
        {
            SetShuttleMode(data[3] == 1);
        }

        // typedef struct
        // { 
        //    uint8_t cmd;
        //    uint8_t datalen;
        //    uint8_t mode;       // one of 6 modes (0 - 5)
        //    uint8_t rate_sel;   // one of 3 rates (0 - 2)
        //    uint8_t dir;        // 0 = cw, 1 = ccw
        //    uint8_t keycode;
        //    uint8_t modifiers;
        //    uint8_t min_rate;
        //    uint8_t max_rate;
        // } sMsgJogData;
        private void HandleJogData(byte[] data)
        {
            curJogRateSelect = data[3];
            curJogDir = data[4];
            curCode = data[5];
            curModifiers = data[6];
            curJogMinRate = data[7];
            curJogMaxRate = data[8];
            if ((curCode != 0 || curModifiers != 0) && curJogMinRate != 0)
                keyData.Content = kbdHandler.KeyCombinationName(curCode, curModifiers) + ", R" + curJogMinRate.ToString() + "-" + curJogMaxRate.ToString();
            else
                keyData.Content = "";
            setKeyWin.UpdateJogData(curJogDir, curJogRateSelect, curCode, curModifiers, curJogMinRate, curJogMaxRate, curShuttleMode);
        }


        //  typedef struct
        //  {
        //    uint8_t cmd;
        //    uint8_t datalen;
        //    uint8_t ver_major;
        //    uint8_t ver_minor;
        //    uint16_t reserve;
        //  } sMsgVersion;

        private void HandleVersion(byte[] data)
        {
            int vmajor = data[2];
            int vminor = data[3];
            Title = connectionTitle + string.Format(". FwVer {0}.{1:00}", vmajor, vminor);
        }

        private void KbdDevice_Removed()
        {
            Dispatcher.Invoke(new Action(() => { SetTitle(false, "Removed"); }));
        }

        private void KbdDevice_Inserted()
        {
            Dispatcher.Invoke(new Action(() => { SetTitle(true, "Inserted"); }));
            kbdDevice.ReadReport(OnReport);
            deviceActive = true;
        }

        private string JogModeText(int jogmode)
        {
            bool jogtemp = (jogmode & 0x10) != 0;
            jogmode &= 0xF;
            if (jogmode == 0)
                return "No Jog action";
            string mode = " J" + jogmode.ToString();
            if (jogtemp)
                return "Jog temp select" + mode;
            return "Jog select" + mode;
        }

        /*
         * typedef struct
         * {
         * 	uint8_t cmd;
         * 	uint8_t datalen;
         * 	uint8_t keyid;
         * 	uint8_t keycode;
         * 	uint8_t modifiers;
         * 	uint8_t group;
         * 	uint8_t alt_type;
         * 	uint8_t alt_keycode;
         * 	uint8_t alt_modifiers;
         * 	uint8_t jog_sel;
         * } sMsgKeyData;
         */
        private void HandleKeyData(byte[] data)
        {
            curKey = data[2];
            curCode = data[3];
            curModifiers = data[4];
            curGroup = data[5];
            curAltType = data[6];
            curAltCode = data[7];
            curAltModifiers = data[8];
            curJogMode = data[9];
            StringBuilder sb = new StringBuilder();
            sb.Append(kbdHandler.KeyCombinationName(curCode, curModifiers));
            if (curAltCode > 0)
            {
                if (sb.Length > 0)
                    sb.Append(" / ");
                sb.Append(kbdHandler.KeyCombinationName(curAltCode, curAltModifiers));
            }
            if (curJogMode > 0)
            {
                if (sb.Length > 0)
                    sb.Append(" / ");
                sb.Append(JogModeText(curJogMode));
                UpdateJogView();
            }
            keyData.Content = sb.ToString();
            setKeyWin.UpdateKeyData(curKey, curCode, curModifiers, curGroup, curAltType, curAltCode, curAltModifiers, curJogMode);
        }

        private void UpdateJogAngle(int angle, int rpm)
        {
            double angRad = angle * Math.PI / 180.0;
            double newposx = jogCentX + Math.Sin(angRad) * jogWheelRadius;
            double newposy = jogCentY - Math.Cos(angRad) * jogWheelRadius;
            JogWheelAngle.Margin = new Thickness(newposx, newposy, 0, 0);
            labelRpm.Content = rpm > 0 ? rpm.ToString() : "";
        }

        private void HandleKeyActions(byte [] data)
        {
            for (int i = ACTIONS_PRESS_START; i < ACTIONS_PRESS_START + MAX_ACTIONS; i++)
            {
                if (data[i] == 0)
                    break;
                object item = FindName("butt" + data[i].ToString());
                if (item == null)
                    continue;
                Button b = (Button)item;
                b.Background = pressedBrush;
            }

            for (int i = ACTIONS_RELEASE_START; i < ACTIONS_RELEASE_START + MAX_ACTIONS; i++)
            {
                if (data[i] == 0)
                    break;
                object item = FindName("butt" + data[i].ToString());
                if (item == null)
                    continue;
                Button b = (Button)item;
                b.Background = releasedBrush;
            }

            curJogAngle = data[JOG_ANG_LOC] + 256 * data[JOG_ANG_LOC + 1];
            curJogRpm = data[JOG_RPM_LOC] + 256 * data[JOG_RPM_LOC + 1];
            UpdateJogAngle(curJogAngle, curJogRpm);
        }

        private void HandleSaveResult(byte[] data)
        {
            int res = data[2];
            if (res == 0)
                MessageBox.Show("Device programmed successfuly", "Success");
            else
                MessageBox.Show("Error " + res.ToString() +  " detected while programming device", "Error");
        }

        private void ButtProgram_Click(object sender, RoutedEventArgs e)
        {
            if (!deviceActive || batchmode)
                return;
            // request key data
            int cmd = Keyboard.Modifiers.HasFlag(ModifierKeys.Control) ? MSG_FACTORY_DEFAULT : MSG_SAVE_SETTINGS;
            if (cmd == MSG_FACTORY_DEFAULT)
            {
                if (MessageBox.Show("Are you sure you want to reset the device to factory configuration?", "Warning", MessageBoxButton.YesNo, MessageBoxImage.Exclamation)
                    != MessageBoxResult.Yes)
                    return;
            }
            byte[] data = new byte[64];
            data[1] = (byte)cmd;
            data[2] = 0;
            HidReport report = new HidReport(64, new HidDeviceData(data, HidDeviceData.ReadStatus.Success));
            kbdDevice.WriteReport(report);
        }

        private void DialButtClick(object sender, RoutedEventArgs e)
        {
            if (batchmode)
                return;
            setKeyWin.UpdateJogData(curJogDir, curJogRateSelect, curCode, curModifiers, curJogMinRate, curJogMaxRate, curShuttleMode);
            setKeyWin.Owner = this;
            setKeyWin.ShowDialog();
            if (setKeyWin.OkPressed == true)
            {
                curCode = setKeyWin.Code;
                curModifiers = setKeyWin.Modifiers;
                curJogMinRate = setKeyWin.MinRate;
                curJogMaxRate = setKeyWin.MaxRate;
                string jogcode = kbdHandler.KeyCombinationName(curCode, curModifiers);
                if (jogcode.Length != 0)
                    jogcode += ", T" + curJogMinRate.ToString();
                keyData.Content = jogcode;
                UpdateJogData(WheelJogMode, curJogRateSelect, curJogDir, curCode, curModifiers, curJogMinRate, curJogMaxRate);
            }
        }

        // typedef struct
        // { 
        //    uint8_t cmd;
        //    uint8_t datalen;
        //    uint8_t mode;   // one of 6 modes (0 - 5)
        //    uint8_t rate;   // one of 3 rates (0 - 2)
        //    uint8_t dir;    // 0 = cw, 1 = ccw
        // } sMsgGetJogData;
        private void UsbGetJogData(int mode, int rate_select, int dir)
        {
            dataReady = false;
            byte[] data = new byte[64];
            data[1] = MSG_GET_JOG_DATA;
            data[2] = 3;
            data[3] = (byte)mode;
            data[4] = (byte)rate_select;
            data[5] = (byte)dir;
            HidReport report = new HidReport(64, new HidDeviceData(data, HidDeviceData.ReadStatus.Success));
            kbdDevice.WriteReport(report);

        }

        private void DialButtEnter(object sender, MouseEventArgs e)
        {
            if (!deviceActive || batchmode)
                return;
            // request jog wheel data
            Button b = (Button)sender;
            string name = b.Name;
            curJogDir = name[4] == 'R' ? 0 : 1;
            curJogRateSelect = name[5] - '1';
            UsbGetJogData(WheelJogMode, curJogRateSelect, curJogDir);
        }


        //typedef struct
        //{
        //	uint8_t cmd;
        //	uint8_t datalen;
        //	uint8_t mode;   // one of 6 modes (0 - 5)
        //	uint8_t type;   // 0 = normal, 1 = shuttle mode
        //} sMsgJogType;
        private void ButtShtl_Click(object sender, RoutedEventArgs e)
        {
            if (!deviceActive || batchmode)
                return;
            SetShuttleMode(!curShuttleMode);
            byte[] data = new byte[64];
            data[1] = MSG_SET_JOG_TYPE;
            data[2] = 2;
            data[3] = (byte)WheelJogMode;
            data[4] = (byte)(curShuttleMode ? 1  : 0);
            HidReport report = new HidReport(64, new HidDeviceData(data, HidDeviceData.ReadStatus.Success));
            kbdDevice.WriteReport(report);
        }

        private void UsbGetShuttleMode(int jogmode)
        {
            dataReady = false;
            // get shuttle mode from device
            byte[] data = new byte[64];
            data[1] = MSG_GET_JOG_TYPE;
            data[2] = 1;
            data[3] = (byte)jogmode;
            HidReport report = new HidReport(64, new HidDeviceData(data, HidDeviceData.ReadStatus.Success));
            kbdDevice.WriteReport(report);
        }

        private void UsbGetFwVersion()
        {
            byte[] data = new byte[64];
            data[1] = MSG_GET_VERSION;
            data[2] = 0;
            HidReport report = new HidReport(64, new HidDeviceData(data, HidDeviceData.ReadStatus.Success));
            kbdDevice.WriteReport(report);
        }

        private void ShowSaveProgress()
        {
            keyData.Content = "Saving..... " + @"/-\|"[saveProgressCnt++];
            if (saveProgressCnt >= 4)
                saveProgressCnt = 0;
        }

        private void ShowLoadProgress()
        {
            keyData.Content = "loading..... " + @"/-\|"[saveProgressCnt++];
            if (saveProgressCnt >= 4)
                saveProgressCnt = 0;
        }

        private void InvokeShowSaveProgress()
        {
            Dispatcher.Invoke(new Action(() => { ShowSaveProgress(); }));
        }


        private void ShowSaveResults(bool isSuccess, string errMsg)
        {
            if (errMsg == null)
                errMsg = lastError;
            if (isSuccess)
                MessageBox.Show("Settings saved to file successfuly", "Success");
            else
                MessageBox.Show(errMsg, "Error");
            buttSave.IsEnabled = true;
            keyData.Content = "";
        }

        private void UpdateJogView()
        {
            if (batchmode)
                return;

            int modeSel = curJogMode - 1;
            if (modeSel < 0 || modeSel >= WUpdateCode.JOG_MAX_MODES || comboJogMode.SelectedIndex == modeSel)
                return;
            WheelJogMode = modeSel;

            if (deviceActive)
                UsbGetShuttleMode(modeSel);
        }

        private string MakeSettingsLine(string name, byte [] msg, int keycode_ix = -1, int c_style = -1)
        {
            StringBuilder sb = new StringBuilder();
            if (c_style >= 0)
            {
                sb.Append("        /* ");
            }
            else
            {
                sb.Append(name);
                sb.Append("=");
            }
            
            for (int i = 0; i < msg[1]; i++)
            {
                if (i != 0 && i != c_style)
                    sb.Append(",");
                if (i == c_style)
                    sb.Append(" */ { ");
                if (i == keycode_ix)
                    sb.Append(kbdHandler.CodeNameC(msg[2 + i]));
                else
                    sb.Append(msg[2 + i]);
            }
            if (c_style >= 0)
                sb.Append(" },");
            return sb.ToString();
        }

        HidReport ReadReportTimeout(int timeout_ms)
        {
            if (smReadSem.WaitOne(timeout_ms))
                return lastReport;
            return null;
         /*   while (timeout_ms > 0 && dataReady == false)
            {
                Thread.Sleep(1);
                timeout_ms--;
            }
            if (!dataReady)
                return null;
            return lastReport;*/
        }

        private bool BatchReadDevice()
        {
            batchmode = true;
            List<string> lines = new List<string>();
            lastError = "Timeout reading device";
            lines.Add("# key programming settings: Key=keyid (0-49), key_code, modifiers, group (0-50), alt_key_code, alt_modifiers");
            for (int keyid = 1; keyid < 50; keyid++)
            {
                UsbGetKeyData(keyid);
                HidReport rep = ReadReportTimeout(1000);
                if (rep == null)
                    return false;
                InvokeShowSaveProgress();
                lines.Add(MakeSettingsLine("Key", rep.Data, 1, isCstyle ? 1 : -1));
            }

            lines.Add("# Jog dial programming settings: JogData=mode_select (0-2), rateSelect (0-2), dir (0=right, 1=left), key_code, modifiers, min_rate, max_rate");
            for (int jogmode = 0; jogmode < WUpdateCode.JOG_MAX_MODES; jogmode++)
            {
                for (int rate_sel = 0; rate_sel < 3; rate_sel++)
                {
                    for (int dir = 0; dir < 2; dir++)
                    {
                        UsbGetJogData(jogmode, rate_sel, dir);
                        HidReport rep = ReadReportTimeout(1000);
                        if (rep == null)
                            return false;

                        InvokeShowSaveProgress();
                        lines.Add(MakeSettingsLine("JogData", rep.Data, 3, isCstyle ? 3 : -1));
                    }
                }
            }

            lines.Add("# shuttle mode settings: JogType=mode_select (0-2), is_shuttle (0 or 1)");
            for (int mode = 0; mode < WUpdateCode.JOG_MAX_MODES; mode++)
            {
                UsbGetShuttleMode(mode);
                HidReport rep = ReadReportTimeout(1000);
                if (rep == null)
                    return false;

                InvokeShowSaveProgress();
                lines.Add(MakeSettingsLine("JogType", rep.Data, -1, isCstyle ? 1 : -1));
            }


            try
            {
                File.WriteAllLines(saveFileName, lines);
            }
            catch
            {
                lastError = "Unable to save file: " + saveFileName;
                return false;
            }

            return true;
        }

        private static void ReportReader(object data)
        {
            MainWindow w = (MainWindow)data;
            bool result = w.BatchReadDevice();
            w.Dispatcher.Invoke(new Action(() => { w.ShowSaveResults(result, null); }));
            w.batchmode = false;
            w.kbdDevice.ReadReport(w.OnReport);
        }

        private bool ParseSettingLine(string data, int cmd, int datalen, bool isDryRun, int keycode_ix = -1)
        {
            string[] vals = data.Split(',');
            if (vals.Length != datalen)
                return false;
            byte[] msg = new byte[64];
            msg[1] = (byte)cmd;
            msg[2] = (byte)datalen;
            for (int i = 0; i < datalen; i++)
            {
                bool parseGood = false;
                int val;
                if (i == keycode_ix)
                    parseGood = kbdHandler.ParseCodeNameC(vals[i].Trim(), out val);
                else
                    parseGood = int.TryParse(vals[i].Trim(), out val);
                if (!parseGood)
                    return false;
                msg[i + 3] = (byte)val;
            }
            if (!isDryRun)
            {
                HidReport report = new HidReport(64, new HidDeviceData(msg, HidDeviceData.ReadStatus.Success));
                kbdDevice.WriteReport(report);
                ShowLoadProgress();
                // hack to refresh the screen as we are blocking the UI
                Action EmptyDelegate = delegate () { };
                keyData.Dispatcher.Invoke(DispatcherPriority.Render, EmptyDelegate); ;
            }
            return true;
        }

        private bool ParseSettingFile(string [] lines, bool isDryRun)
        {
            foreach (string line in lines)
            {
                line.Trim();
                if (line.StartsWith("#") || line.Length == 0)
                    continue;
                string[] tokens = line.Split('=');
                if (tokens.Length != 2)
                    return false;
                string settingName = tokens[0];
                settingName.TrimEnd();
                bool res = false;
                switch (settingName)
                {
                    case "Key":
                        res = ParseSettingLine(tokens[1], MSG_SET_KEY, 8, isDryRun, 1);
                        break;

                    case "JogData":
                        res = ParseSettingLine(tokens[1], MSG_SET_JOG_DATA, 7, isDryRun, 3);
                        break;

                    case "JogType":
                        res = ParseSettingLine(tokens[1], MSG_SET_JOG_TYPE, 2, isDryRun);
                        break;

                    default:
                        return false;
                }
                if (res == false)
                    return false;
            }
            return true;
        }

        private bool ReadSettings(string filename)
        {
            batchmode = true;
            string[] lines;
            try
            {
                lines = File.ReadAllLines(filename);
            }
            catch
            {
                lastError = "Unable to open file " + filename;
                return false;
            }

            if (ParseSettingFile(lines, true) == false)
            {
                lastError = "Invalid or bad file format";
                return false;
            }

            if (ParseSettingFile(lines, false) == false)
            {
                lastError = "Unable to program device";
                return false;
            }

            return true;
        }

        private void ButtSave_Click(object sender, RoutedEventArgs e)
        {
            if (batchmode)
                return;

            if (!deviceActive)
            {
                MessageBox.Show("Device not connected, aborting", "Error");
                return;
            }
            isCstyle = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            if (saveFileDlg.ShowDialog() == true)
            {
                buttSave.IsEnabled = false;
                saveFileName = saveFileDlg.FileName;
                Thread trd = new Thread(new ParameterizedThreadStart(ReportReader));
                trd.Start(this);
            }
        }

        private void ComboJogMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (deviceActive)
                UsbGetShuttleMode(WheelJogMode);
        }

        private void ButtLoad_Click(object sender, RoutedEventArgs e)
        {
            if (batchmode)
                return;

            if (!deviceActive)
            {
                MessageBox.Show("Device not connected, aborting", "Error");
                return;
            }
            if (openFileDlg.ShowDialog() == true)
            {
                buttLoad.IsEnabled = false;
                bool res = ReadSettings(openFileDlg.FileName);
                if (res == true)
                        MessageBox.Show("Settings loaded and device programmed successfuly", "Success");
                    else
                        MessageBox.Show(lastError, "Error");
                buttLoad.IsEnabled = true;
                keyData.Content = "";
                batchmode = false;
            }
        }

        private void ButtPlus_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}

