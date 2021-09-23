using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SpeedEditorProg
{
    /// <summary>
    /// Interaction logic for WUpdateCode.xaml
    /// </summary>
    public partial class WUpdateCode : Window
    {
        const int JOG_SELL_MASK = 0x0F;
        const int JOG_SELL_TEMP = 0x10;
        public const int JOG_MAX_MODES = 6;

        public KbdHandler kbdHandler;
        int _modifiers = 0;
        int _code = 0;
        int _altModifiers = 0;
        int _altCode = 0;
        int _group = 0;
        int _maxRate = 0;
        int _key = 0;
        bool captureMode = false;
        bool isJogWinMode = false;
        bool altMode = false;
        int capturedModifiers;
        Brush checkedBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFD1A7D1"));
        Brush uncheckedBrush;
        Brush errBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFE86767"));
        Brush txtBrush;
        public bool OkPressed = false;

        public WUpdateCode(KbdHandler khandler)
        {
            kbdHandler = khandler;
            InitializeComponent();
            uncheckedBrush = buttmod0.Background;
            txtBrush = textGroup.Foreground;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            if (IsVisible)
            {
                e.Cancel = true;
                Hide();
            }
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            OkPressed = false;
        }

        protected void UpdateModifierButton(int mods, int modid)
        {
            Button butt = (Button)FindName("buttmod" + modid.ToString());
            if ((mods & (1 << modid)) != 0)
                butt.Background = checkedBrush;
            else
                butt.Background = uncheckedBrush;
        }

        protected void UpdateModifiersButtons(int mods)
        {
            for (int i = 0; i < 8; i++)
                UpdateModifierButton(mods, i);
         }

        private void ModButtClick(object sender, RoutedEventArgs e)
        {
            Button butt = (Button)sender;
            string buttidstr = butt.Name.Substring(7);
            int buttid = int.Parse(buttidstr);
            int buttmask = 1 << buttid;
            if (altMode)
                _altModifiers ^= buttmask;
            else
                _modifiers ^= buttmask;
            UpdateCodeView();
        }

        protected override void OnPreviewKeyDown(KeyEventArgs e)
        {
            base.OnPreviewKeyDown(e);

            int wincode = (int)e.Key;
            if (e.Key == System.Windows.Input.Key.System)
                wincode = (int)e.SystemKey;
            //textGroup.Text = wincode.ToString();

            if (captureMode)
            {
                int winmod = kbdHandler.translateWinModifier(wincode);
                if (winmod >= 0)
                {
                    capturedModifiers |= 1 << winmod;
                    UpdateModifierButton(capturedModifiers, winmod);
                }
                else
                {
                    int kbdcode = kbdHandler.translateWinCode(wincode);
                    if (kbdcode >= 0)
                    {
                        if (altMode)
                        {
                            _altCode = kbdcode;
                            _altModifiers = capturedModifiers;
                        }
                        else
                        {
                            _code = kbdcode;
                            _modifiers = capturedModifiers;
                        }
                        UpdateCodeView();
                        captureMode = false;
                        UpdateOkButt();
                    }
                }
                e.Handled = true;
            }
            //labelKey.Content = ((int)e.Key).ToString();
        }

        protected override void OnPreviewKeyUp(KeyEventArgs e)
        {
            base.OnPreviewKeyUp(e);
            if (captureMode)
            {
                int wincode = (int)e.Key;
                int winmod = kbdHandler.translateWinModifier(wincode);
                if (winmod >= 0)
                {
                    capturedModifiers &= ~(1 << winmod);
                    UpdateModifierButton(capturedModifiers, winmod);
                }
                e.Handled = true;
            }
        }

        private void UpdateCodeView()
        {
            if (altMode)
            {
                labelKey.Content = kbdHandler.KeyCombinationName(_altCode, _altModifiers);
                UpdateModifiersButtons(_altModifiers);
                textCode.Text = _altCode.ToString();
            }
            else
            {
                labelKey.Content = kbdHandler.KeyCombinationName(_code, _modifiers);
                UpdateModifiersButtons(_modifiers);
                textCode.Text = _code.ToString();
            }
        }

        public int Modifiers
        {
            get { return _modifiers; }
            set
            {
                _modifiers = value;
                UpdateCodeView();
            }
        }

        public int Code
        {
            get { return _code; }
            set
            {
                _code = value;
                UpdateCodeView();
            }
        }

        public int AltModifiers
        {
            get { return _altModifiers; }
            set
            {
                _altModifiers = value;
                UpdateCodeView();
            }
        }

        public int AltCode
        {
            get { return _altCode; }
            set
            {
                _altCode = value;
                UpdateCodeView();
            }
        }

        public int Group
        {
            get { return _group; }
            set
            {
                _group = value;
                textGroup.Text = _group.ToString();
                UpdateButAlternate();
            }
        }

        public int MinRate // alias to group
        {
            get { return _group; }
            set { Group = value; }
        }

        public int MaxRate
        {
            get { return _maxRate; }
            set
            {
                _maxRate = value;
                textMaxRate.Text = _maxRate.ToString();
            }
        }


        public int Key
        {
            get { return _key; }
            set
            {
                _key = value;
                Title = "Reassign key: " + _key.ToString();
            }
        }

        public int AltType
        {
            get { return comboAlt.SelectedIndex; }
            set
            {
                comboAlt.SelectedIndex = value;
                UpdateButAlternate();
            }
        }

        public int JogMode
        {
            get
            {
                if (comboJogSel.SelectedIndex == 0)
                    return 0;
                int retval = comboJogMode.SelectedIndex + 1;
                if (comboJogSel.SelectedIndex == 2)
                    retval |= JOG_SELL_TEMP;
                return retval;
            }

            set
            {
                if (value == 0)
                    comboJogSel.SelectedIndex = 0;
                else
                {
                    if ((value & JOG_SELL_TEMP) != 0)
                        comboJogSel.SelectedIndex = 2;
                    else
                        comboJogSel.SelectedIndex = 1;
                    value = (value & JOG_SELL_MASK) - 1;
                    if (value < 0 || value >= JOG_MAX_MODES)
                        value = -1;
                    comboJogMode.SelectedIndex = value;
                }

            }
        }

        private void UpdateButAlternate()
        {
            if (!isJogWinMode && AltType != 0)
                buttAlternate.Visibility = Visibility.Visible;
            else
            {
                buttAlternate.Visibility = Visibility.Hidden;
                altMode = false;
                UpdateCodeView();
            }
        }

        public void UpdateKeyData(int key, int code, int mod, int grp, int altType, int altcode, int altmod, int jogmode)
        {
            isJogWinMode = false;
            Key = key;
            _code = code;
            _modifiers = mod;
            _altCode = altcode;
            _altModifiers = altmod;
            AltType = altType;
            JogMode = jogmode;
            Group = grp;
            labelGrpJog.Content = "Group:";
            labelGrpJog.FontSize = 14;
            textMaxRate.Visibility = Visibility.Hidden;
            labelMaxRate.Visibility = Visibility.Hidden;
            altMode = false;
            buttAlternate.Background = uncheckedBrush;
            //UpdateButAlternate();
            captureMode = false;
            UpdateCodeView();
            UpdateOkButt();
            UpdateJogModeButtons();
        }

        public void UpdateJogData(int dir, int level, int code, int mod, int min_rate, int max_rate, bool isShuttle)
        {
            isJogWinMode = true;
            Title = string.Format("Reassign {0} jog level {1}", dir == 0 ? "right" : "left", level + 1);
            _code = code;
            _modifiers = mod;
            MinRate = min_rate;
            MaxRate = max_rate;
            labelGrpJog.FontSize = 12;
            labelGrpJog.Content = isShuttle ? "Min speed:" : "Speed thr:";
            labelMaxRate.Content = isShuttle ? "Max speed:" : "Angle:";
            textMaxRate.Visibility = Visibility.Visible;
            labelMaxRate.Visibility = Visibility.Visible;
            UpdateButAlternate();
            UpdateCodeView();
            captureMode = false;
            UpdateOkButt();
            UpdateJogModeButtons();
        }

        private void TextGroup_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(textGroup.Text, out _group))
            {
                if (_group < 0 || _group > 255)
                    _group = -1;
            }
            else
                _group = -1;
            textGroup.Foreground = _group < 0 ? errBrush : txtBrush;
            UpdateOkButt();
            //UpdateButAlternate();
        }

        private void TextCode_TextChanged(object sender, TextChangedEventArgs e)
        {
            int tcode;
            if (int.TryParse(textCode.Text, out tcode))
            {
                if (tcode < 0 || tcode > 255)
                    tcode = -1;
            }
            else
                tcode = -1;
            if (altMode)
                _altCode = tcode;
            else
                _code = tcode;
            textCode.Foreground = tcode < 0 ? errBrush : txtBrush;
            if (tcode >= 0)
                UpdateCodeView();
            UpdateOkButt();
        }

        private void UpdateOkButt()
        {
            buttOk.IsEnabled = _code >= 0 && _group >= 0 && _maxRate >= 0 && _altCode >= 0 && captureMode == false;
        }

        private void LabelKey_MouseDown(object sender, MouseButtonEventArgs e)
        {
            captureMode = true;
            capturedModifiers = 0;
            UpdateModifiersButtons(capturedModifiers);
            labelKey.Content = "_";
            UpdateOkButt();
        }

        private void ButtOk_Click(object sender, RoutedEventArgs e)
        {
            OkPressed = true;
            if (AltType == 0)
            {
                _altCode = 0;
                _altModifiers = 0;
            }
            Hide();
        }

        private void ButtCancel_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void ButtAlternate_Click(object sender, RoutedEventArgs e)
        {
            altMode = !altMode;
            buttAlternate.Background = altMode ? checkedBrush : uncheckedBrush;
            UpdateCodeView();
        }

        private void TextMaxRate_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(textMaxRate.Text, out _maxRate))
            {
                if (_maxRate < 0 || _maxRate > 255)
                    _maxRate = -1;
            }
            else
                _maxRate = -1;
            textMaxRate.Foreground = _maxRate < 0 ? errBrush : txtBrush;
            UpdateOkButt();
        }

        private void UpdateJogModeButtons()
        {
            if (isJogWinMode)
            {
                comboJogSel.Visibility = labelJogMode.Visibility = comboJogMode.Visibility = Visibility.Hidden;
                comboAlt.Visibility = Visibility.Hidden;
            }
            else
            {
                comboJogSel.Visibility = Visibility.Visible;
                comboAlt.Visibility = Visibility.Visible;
                bool isVisible = comboJogSel.SelectedIndex > 0;
                labelJogMode.Visibility = comboJogMode.Visibility = isVisible ? Visibility.Visible : Visibility.Hidden;
            }

        }

        private void ComboAlt_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateButAlternate();
        }

        private void ComboJogSel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateJogModeButtons();
        }
    }
}
