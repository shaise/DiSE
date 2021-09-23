using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SpeedEditorProg
{
    public class KbdHandler
    {
        class cKeyMap
        {
            public string name;
            public int kbdCode;
            public int winCode;
            public cKeyMap (string name, int kbdc, int winc)
            {
                this.name = name;
                kbdCode = kbdc;
                winCode = winc;
            }
        }

        string[] keyModifiers = new string[] {
             "L-Ctrl", // = 0x01,
             "L-Shift", // = 0x02,
             "L-Alt", // = 0x04,
             "L-Gui", // = 0x08,
             "R-Ctrl", // = 0x10,
             "R-Shift", // = 0x20,
             "R-Alt", // = 0x40,
             "R-Gui", // = 0x80,
        };

        int[] winModifiers = new int[]
        {
            118,   //  L-Ctrl
            116,   //  L-Shift
            120,   //  L-Alt
            70,    //  L-Gui
            119,   //  R-Ctrl
            117,   //  R-Shift
            121,   //  R-Alt - same as left
            72,   //  R-Gui
        };

        Dictionary<int, cKeyMap> keyDictByKbdCode;
        Dictionary<int, cKeyMap> keyDictByWinCode;
        Dictionary<int, int> winModifiersDict;
        Dictionary<string, cKeyMap> keyDictByName;

        public KbdHandler()
        {
            keyDictByKbdCode = new Dictionary<int, cKeyMap>();
            keyDictByWinCode = new Dictionary<int, cKeyMap>();
            keyDictByName = new Dictionary<string, cKeyMap>();
            winModifiersDict = new Dictionary<int, int>();
            ReadKeyMap();
            for (int i = 0; i < winModifiers.Length; i++)
            {
                int wcode = winModifiers[i];
                winModifiersDict[wcode] = i;
            }
        }

        protected void ReadKeyMap()
        {
            string mapFile = Properties.Resources.KeyMap_txt;
            string[] lines = mapFile.Split(new char[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            foreach(string line in lines)
            {
                string [] vars = line.Split(new char[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries);
                string name = vars[0];
                int kbdcode = int.Parse(vars[1]);
                int wincode = int.Parse(vars[2]);
                cKeyMap km = new cKeyMap(name, kbdcode, wincode);
                keyDictByKbdCode[kbdcode] = km;
                keyDictByWinCode[wincode] = km;
                keyDictByName[name] = km;

            }
        }

        public string CodeName(int code)
        {
            if (code == 0)
                return "";
            if (keyDictByKbdCode.ContainsKey(code))
            {
                string codestr = keyDictByKbdCode[code].name;
                return codestr;
            }
            return "";
        }

        public string CodeNameC(int code)
        {
            if (code == 0)
                return "0";
            if (keyDictByKbdCode.ContainsKey(code))
            {
                string codestr = keyDictByKbdCode[code].name;
                if (codestr.StartsWith("KP-"))
                    codestr = "KP_" + codestr.Substring(3);
                else
                    codestr = "KEY_" + codestr;
                return codestr;
            }
            return code.ToString();
        }

        public bool ParseCodeNameC(string name, out int code)
        {
            if (name.StartsWith("KP_"))
                name = "KP-" + name.Substring(3);
            else if (name.StartsWith("KEY_"))
                name = name.Substring(4);
            if (keyDictByName.ContainsKey(name))
            {
                code = keyDictByName[name].kbdCode;
                return true;
            }
            return int.TryParse(name, out code);
        }

        public string KeyCombinationName(int code, int modifiers)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < 8; i++)
            {
                if ((modifiers & (1 << i)) != 0)
                {
                    if (sb.Length != 0)
                        sb.Append(" + ");
                    sb.Append(keyModifiers[i]);
                }
            }
            string codestr = CodeName(code);
            if (codestr.Length > 0)
            {
                if (sb.Length != 0)
                    sb.Append(" + ");
                sb.Append(codestr);
            }
            return sb.ToString();
        }

        public int translateWinModifier(int wincode)
        {
            if (winModifiersDict.ContainsKey(wincode))
                return winModifiersDict[wincode];
            return -1;
        }

        public int translateWinCode(int wincode)
        {
            if (keyDictByWinCode.ContainsKey(wincode))
                return keyDictByWinCode[wincode].kbdCode;
            return -1;
        }

    }
}
