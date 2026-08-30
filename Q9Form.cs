using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Text;
using System.IO;
using System.Drawing.Text;
using System.Linq;

namespace Q9CS
{

    public enum q9command
    {
        cancel,
        prev,
        next,
        homo,
        openclose,
        relate,
        shortcut,
        sc,
    }

    public class IniFile
    {
        string filePath;

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        [DllImport("kernel32", CharSet = CharSet.Unicode)]
        static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        public IniFile(string filePath)
        {
            //var dir = Path.GetDirectoryName(filePath);
            var dir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir ?? throw new InvalidOperationException("Invalid file path"));
            }

            this.filePath = System.IO.Path.Combine(dir ,filePath);
        }

        public bool makeExcite()
        {

            // Check if the file exists, and if not, create an empty file
            if (!File.Exists(filePath))
            {
                File.Create(filePath).Close();
                return false;
            }
            return true;
        }

        public void Write(string section, string key, int value)
        {
            long result = WritePrivateProfileString(section, key, value.ToString(), this.filePath);
            Debug.WriteLine($"{result},{this.filePath},{key}");
        }

        public int ReadInt(string section, string key, int defaultValue)
        {
            StringBuilder SB = new StringBuilder(255);
            int i = GetPrivateProfileString(section, key, defaultValue.ToString(), SB, 255, this.filePath);
            return int.TryParse(SB.ToString(), out i) ? i : defaultValue;
        }

        public string ReadStr(string section, string key, string defaultValue)
        {
            StringBuilder SB = new StringBuilder(255);
            int i = GetPrivateProfileString(section, key, defaultValue.ToString(), SB, 255, this.filePath);
            return SB.ToString();
        }
    }


    public partial class Q9Form : Form
    {

        private static readonly string StartupKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        private static readonly string StartupValue = "Q9CS";


        private Q9Core core;
        private List<Button> buttons = new List<Button> { };

        private Image[] images = new Image[120];

        private bool active = true;
        private bool sc_output = false;
        private bool use_numpad = true;

        private IniFile ini = new IniFile("tq9_settings.ini");
        private Dictionary<string, int> Keys = new Dictionary<string, int>();
        private Dictionary<string, string> KeysStr = new Dictionary<string, string>();
        private Dictionary<string, int> altKeys = new Dictionary<string, int>();

        public void Q9Form_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Save size and position
            ini.Write("Window", "Left", this.Left);
            ini.Write("Window", "Top", this.Top);
            ini.Write("Window", "BoxSize", currBoxSize);
        }

        //

        public Q9Form()
        {
            InitializeComponent();
            this.StartPosition = FormStartPosition.Manual;
            this.ControlBox = false;

            //preload image
            for (int i = 0; i <= 10; i++)
            {
                for (int j = 1; j <= 9; j++)
                {
                    images[i * 10 + j] = Image.FromFile($"files/img/{i}_{j}.png");
                }
            }
            for (int j = 1; j <= 9; j++)
            {
                images[110 + j] = SetOpacity(images[j], 0.5f);
            }

            string fontName = "Microsoft Sans Serif";
            InstalledFontCollection installedFontCollection = new InstalledFontCollection();
            FontFamily[] fontFamilies = installedFontCollection.Families;
            string[] fontTargets = ("Noto Sans HK Medium,Noto Sans HK,Noto Sans HK Black,Noto Sans HK Light,Noto Sans HK Thin,Noto Sans TC,Noto Sans TC Black,Noto Sans TC Light,Noto Sans TC Medium,Noto Sans TC Regular,Noto Sans TC Thin,Noto Serif CJK TC Black,Noto Serif CJK TC Medium,Noto Sans CJK JP,Noto Sans CJK TC Black,Noto Sans CJK TC Bold,Noto Sans CJK TC Medium,Noto Sans CJK TC Regular,Noto Sans CJK DemiLight,Microsoft JhengHei").Split(',');


            foreach (string currfontName in fontTargets)
            {

                bool found = false;
                foreach (FontFamily fontFamily in fontFamilies)
                {
                    if (fontFamily.Name == currfontName)
                    {
                        fontName = currfontName;
                        found = true;
                        Console.WriteLine(fontName);
                        break;
                    }
                }
                if (found)
                {
                    break;
                }
            }

            //init buttons
            for (int i = 0; i < 11; i++)
            {
                Button b = new Button();

                //Arial
                //Microsoft Sans Serif
                //Debug.WriteLine(b.Font.FontFamily);
                b.Font = new Font(new FontFamily(fontName), 10);

                b.Text = i.ToString();

                this.Controls.Add(b);
                buttons.Add(b);
                b.BackgroundImageLayout = ImageLayout.Zoom;
                int j = i;
                if (i < 10)
                {
                    b.Click += new System.EventHandler((object Sender, EventArgs e) => pressKey(j));
                }
                else
                {
                    b.Click += new System.EventHandler((object Sender, EventArgs e) => commandInput(q9command.cancel));
                }
            }

            //----------------


            Rectangle screenSize = Screen.PrimaryScreen.Bounds;
            core = new Q9Core();

            //load ini------------------------




            var keys = "num1,num2,num3,num4,num5,num6,num7,num8,num9,num0,cancel".Split(',');
            var extraKeys = "relate,prev,shortcut,homo,openclose".Split(',');
            if (!ini.makeExcite())
            {
                currBoxSize = 60;
                this.Left = screenSize.Width - currBoxSize * 3 + 20;
                this.Top = 100;

                ini.Write("Window", "Left", this.Left);
                ini.Write("Window", "Top", this.Top);
                ini.Write("Window", "BoxSize", currBoxSize);

                ini.Write("system", "sc_output", 0);
                ini.Write("system", "use_numpad", 1);

                ini.Write("AltKey", "num1", (int)'X');
                ini.Write("AltKey", "num2", (int)'C');
                ini.Write("AltKey", "num3", (int)'V');
                ini.Write("AltKey", "num4", (int)'S');
                ini.Write("AltKey", "num5", (int)'D');
                ini.Write("AltKey", "num6", (int)'F');
                ini.Write("AltKey", "num7", (int)'W');
                ini.Write("AltKey", "num8", (int)'E');
                ini.Write("AltKey", "num9", (int)'R');

                ini.Write("AltKey", "num0", (int)'Z');
                ini.Write("AltKey", "cancel", (int)'B');

                ini.Write("AltKey", "relate", (int)'G');
                ini.Write("AltKey", "prev", (int)'A');
                ini.Write("AltKey", "shortcut", (int)'A');
                ini.Write("AltKey", "homo", (int)'T');
                ini.Write("AltKey", "openclose", (int)'Q');

                //
                ini.Write("Key", "relate", 107);
                ini.Write("Key", "prev", 109);
                ini.Write("Key", "shortcut", 109);
                ini.Write("Key", "homo", 106);
                ini.Write("Key", "openclose", 111);

                //

                ini.Write("Key", "switch", 121);
                ini.Write("Key", "position", 120);
                ini.Write("Key", "size", 119);
            }
            else
            {
                currBoxSize = ini.ReadInt("Window", "BoxSize", this.Width);
                sc_output = Convert.ToBoolean(ini.ReadInt("system", "sc_output", 0));
                use_numpad = Convert.ToBoolean(ini.ReadInt("system", "use_numpad", 1));

            }

            //------------------------

            for (int k = 0; k < keys.Length; k++)
            {
                string keyName = keys[k];
                altKeys[keyName] = ini.ReadInt("AltKey", keyName, 0);
            }
            for (int k = 0; k < extraKeys.Length; k++)
            {
                string keyName = extraKeys[k];
                altKeys[keyName] = ini.ReadInt("AltKey", keyName, 0);
                Keys[keyName] = ini.ReadInt("Key", keyName, 0);
            }

            Keys["switch"] = ini.ReadInt("Key", "switch", 121);
            Keys["position"] = ini.ReadInt("Key", "position", 120);
            Keys["size"] = ini.ReadInt("Key", "size", 119);

            KeysStr["switch"] = ini.ReadStr("Key", "switch", "121");
            KeysStr["position"] = ini.ReadStr("Key", "position", "120");
            KeysStr["size"] = ini.ReadStr("Key", "size", "119");



            //this.MinimumSize= new Size(120, 160);
            this.FormBorderStyle = FormBorderStyle.Sizable;
            //this.AutoSizeMode = 0;
            this.Resize += Form1_Resize;
            //this.ClientSize = new Size(currBoxSize*3, currBoxSize * 4);
            setNewWidth(currBoxSize * 3);

            this.Left = Math.Min(screenSize.Width - this.Width, ini.ReadInt("Window", "Left", this.Left));
            this.Top = Math.Min(screenSize.Height - this.Height, ini.ReadInt("Window", "Top", this.Top));

            //Debug.WriteLine($"{this.Left},{screenSize.Width},{screenSize.Left},{screenSize.X}");


            this.FormClosing += Q9Form_FormClosing;

            cancel();

            //tray---------------

            var components1 = new System.ComponentModel.Container();
            var contextMenu1 = new System.Windows.Forms.ContextMenu();



            //how2use
            MenuItem malert= new System.Windows.Forms.MenuItem();
            contextMenu1.MenuItems.AddRange(
                        new System.Windows.Forms.MenuItem[] { malert });
            malert.Text = "使用方法";
            malert.Click += new System.EventHandler((object Sender, EventArgs e) => MessageBox.Show(@"
《九万輸入法》使用方法:
需要開啟numLock

.           取消，特別強調0和.都不會因為要入關聯字或標點而改變用途
+           選擇關聯字 (在打下一個字前，都可隨時進入)
-           (選字時)上一頁
            (首頁) 快速選字 (碼表 id 1000)
            (首頁中按1~9後) 快速選字，共九種 (碼表 id 1001~1009)
*           同音字(打字後不出字，會進入同音字選字表)
/           「」等開關標點
0           首頁按0會進入普通標黜

scrollLock  開/關輸入法
F9          改變位置
F8          改變大小

可以自行修改`file/dataset.db`自行修改碼表，
例如調前常用字，加入emoji等，
推薦`DB Browser for SQLite`來修改



--------------------------------------

如果沒有num pad的電腦(如notebook)
可以選擇`不使用num pad`
那就會改為以n至/、j示至l、u至o來輸入

如想自行修改，可以開啓`tq9_settings.ini`
進入[AltKey]部份，自行修改 key code
google查`keycode online`隨便一個結果，都會有查key code的網頁
"));


            //sc
            var menuItemSC = new System.Windows.Forms.MenuItem();
            menuItemSC.Index = 0;
            menuItemSC.Text = "輸出簡體";
            menuItemSC.Click += new System.EventHandler((object Sender, EventArgs e) => {
                sc_output = !sc_output;
                ((System.Windows.Forms.MenuItem)Sender).Checked = sc_output;
                ini.Write("system", "sc_output", sc_output ? 1 : 0);
            });
            menuItemSC.Checked = sc_output;
            contextMenu1.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] { menuItemSC });


            //startup
            RegistryKey key = Registry.CurrentUser.OpenSubKey(StartupKey, true);
            MenuItem startupItem = new MenuItem("開機自動開啟", new EventHandler((sender, e) => {
                if (key.GetValue(StartupValue) == null)
                {
                    key.SetValue(StartupValue, Application.ExecutablePath.ToString());
                    ((MenuItem)sender).Checked = true;
                }
                else
                {
                    key.DeleteValue(StartupValue);
                    ((MenuItem)sender).Checked = false;
                }
            }));
            if (key.GetValue(StartupValue) != null)
            {
                startupItem.Checked = true;
            }
            //contextMenu1.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] { startupItem });




            //alt key
            MenuItem menuAltkey = new MenuItem("不使用num pad", new EventHandler((sender, e) => {
                use_numpad = !use_numpad;
                ((MenuItem)sender).Checked = !use_numpad;
                ini.Write("system", "use_numpad", use_numpad?1:0);
            }));
            menuAltkey.Checked = !use_numpad;
            contextMenu1.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] { menuAltkey });



            // exit
            var menuItem1 = new System.Windows.Forms.MenuItem();
            contextMenu1.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] { menuItem1 });
            //this.menuItem1.Index = 1;
            menuItem1.Text = "離開";
            menuItem1.Click += new System.EventHandler((object Sender, EventArgs e) => this.Close());



            // Create the NotifyIcon.
            var notifyIcon1 = new System.Windows.Forms.NotifyIcon(components1);
            notifyIcon1.Icon = new Icon("i.ico");
            notifyIcon1.ContextMenu = contextMenu1;
            notifyIcon1.Text = "TQ9";
            notifyIcon1.Visible = true;
        }
        private Image SetOpacity(Image image, float opacity)
        {
            var colorMatrix = new ColorMatrix();
            colorMatrix.Matrix33 = opacity;
            var imageAttributes = new ImageAttributes();
            imageAttributes.SetColorMatrix(
                colorMatrix,
                ColorMatrixFlag.Default,
                ColorAdjustType.Bitmap);
            var output = new Bitmap(image.Width, image.Height);
            using (var gfx = Graphics.FromImage(output))
            {
                gfx.SmoothingMode = SmoothingMode.AntiAlias;
                gfx.DrawImage(
                    image,
                    new Rectangle(0, 0, image.Width, image.Height),
                    0,
                    0,
                    image.Width,
                    image.Height,
                    GraphicsUnit.Pixel,
                    imageAttributes);
            }
            return output;
        }

        public static int Clamp(int val, int min, int max)
        {
            return Math.Max(Math.Min(val, max), min);
        }

        private int[] lastSize = new int[] { 0, 0 };
        private void Form1_Resize(object sender, System.EventArgs e)
        {
            Control control = (Control)sender;

            int[] newSize = new int[] { control.ClientSize.Width, control.ClientSize.Height };
            //Debug.WriteLine("WH {0}x{1}", control.Size.Height, control.Size.Width);
            if (control.ClientSize.Width != lastSize[0] && lastSize[0] != 0)
            {                
                newSize[0] = Clamp(control.ClientSize.Width, 90, 450);
            }

            /*
            Debug.Write(this.Width);
            Debug.Write("_");
            Debug.WriteLine(control.ClientSize.Width);
            */

            //Debug.WriteLine("{0}->{1}->{2}", control.Size.Width, control.ClientSize.Width,control.PreferredSize.Width);
            //Debug.WriteLine("{0}->{1}", lastSize[0], newSize[0]);
            setNewWidth(newSize[0]);
        }
        private void setNewWidth(int _width)
        {
            Control control = (Control)this;
            control.ClientSize = new Size(_width, (int)Math.Round(_width* 1.333));
            ResizeAllButton(control.ClientSize.Width / 3);
            lastSize[0] = control.ClientSize.Width;
            lastSize[1] = control.ClientSize.Height;
        }

        private int currBoxSize;

        public void ResizeAllButton(int boxSize)
        {
            currBoxSize = boxSize;
            for (int i = 0; i < 11; i++)
            {
                buttons[i].Width = buttons[i].Height = boxSize;

            }
            for (int i = 9, y = 0; i >= 3; i -= 3, y++)
            {
                for (int j = i, x = 2; j >= i - 2; j--, x--)
                {
                    buttons[j].Left = x * boxSize;
                    buttons[j].Top = y * boxSize;
                }
            }
            buttons[0].Top = buttons[10].Top = 3 * boxSize;
            buttons[0].Width = buttons[10].Left = 2 * boxSize;
            renewFontsize();
        }

        public void sendOpenClose(string openclose)
        {
            // $"「」"
            SendKeys.Send(openclose);
            SendKeys.SendWait("{Left}");
        }
        
        private void sendText(string text)
        {
            if (sc_output)
            {
                SendKeys.Send(core.tcsc(text));
            }
            else
            {
                SendKeys.Send(text);
            }
        }

        private bool[] downKeys = new bool[256];
        public bool handleKey(int keyCode,bool down)
        {
            if (!down)
            {
                downKeys[keyCode]=false;
                return false;
            }
            else
            {
                downKeys[keyCode] = true;
            }
            string keydownStr = "";
            int downCount = 0;
            for (int i = 0; i < 256; i++)
            {
                if (downKeys[i])
                {
                    downCount++;
                    keydownStr += i+" ";
                }
            }
            keydownStr = keydownStr.TrimEnd().Replace(" ", "+");
            
            if (downCount > 1)
            {
                Debug.WriteLine(keydownStr, KeysStr["position"]);
            }

            //scroll lock
            if ((KeysStr["switch"].IndexOf('+')<0 && keyCode == Keys["switch"]) || keydownStr == KeysStr["switch"])
            {
                active = !active;
                if (!active)
                {
                    this.Hide();
                }
                else
                {
                    this.Show();
                    this.TopMost = true;

                    Rectangle screenSize = Screen.PrimaryScreen.Bounds;
                    this.Left = Clamp(this.Left, 0, screenSize.Width - this.Width);
                    this.Top = Clamp(this.Top, 0, screenSize.Height - this.Height);
                }
                return false;
            }

            //exit if special case
            if (!active)
            {
                return false;
            }

            if ((KeysStr["position"].IndexOf('+') < 0 && keyCode == Keys["position"]) || keydownStr == KeysStr["position"])
            {

                this.Show();
                this.TopMost = true;
                Rectangle screenSize = Screen.PrimaryScreen.Bounds;

                this.Left = screenSize.Width - this.Width - 30;

                this.Top = this.Top > (screenSize.Height - this.Height) / 2 ? 30 : screenSize.Height - this.Height - 65;
                return false;
            }

            if ((KeysStr["size"].IndexOf('+') < 0 && keyCode == Keys["size"]) || keydownStr == KeysStr["size"])
                {
                int newSize = currBoxSize;
                if (newSize == 40)
                {
                    newSize = 70;
                }else if (newSize == 70)
                {
                    newSize = 100;
                }else if (newSize == 100)
                {
                    newSize = 40;
                }
                else
                {
                    newSize = 70;
                }
                setNewWidth(newSize * 3);
                return false;
            }

            if (!use_numpad)
            {
                for (int i = 0; i <= 9; i++)
                {
                    if (keyCode == altKeys[$"num{i}"])
                    {
                        pressKey(i);
                        return true;
                    }
                }

                if (keyCode == altKeys["cancel"])
                {
                    commandInput(q9command.cancel);
                }
                else if (keyCode == altKeys["relate"])
                {
                    commandInput(q9command.relate);
                }
                else if (keyCode == altKeys["homo"])
                {
                    commandInput(q9command.homo);
                }
                else if (keyCode == altKeys["openclose"])
                {
                    commandInput(q9command.openclose);
                }
                else if (keyCode == altKeys["shortcut"] && !selectMode)
                {
                    commandInput(q9command.shortcut);
                }
                else if (keyCode == altKeys["prev"] && selectMode)
                {
                    commandInput(q9command.prev);
                }
                else if(keyCode >= 65 && keyCode <=90)
                {
                    return true;
                }
                else
                {
                    return false;
                }
                return true;
            }

            //if num lock====
            if (keyCode >= 96 && keyCode <= 111)
            {
                if (keyCode >= 96 && keyCode <= 105)
                {
                    int inputInt = keyCode - 96;//0~9
                    pressKey(inputInt);
                    return true;
                }
                else
                {
                    if (keyCode == 110)
                    {
                        commandInput(q9command.cancel);
                    }
                    else if (keyCode == Keys["relate"])
                    {
                        commandInput(q9command.relate);
                    }
                    else if (keyCode == Keys["homo"])
                    {
                        commandInput(q9command.homo);
                    }
                    else if (keyCode == Keys["openclose"])
                    {
                        commandInput(q9command.openclose);
                    }
                    else if (keyCode == Keys["shortcut"] && !selectMode)
                    {
                        commandInput(q9command.shortcut);
                    }
                    else if (keyCode == Keys["prev"] && selectMode)
                    {
                        commandInput(q9command.prev);
                    }
                    else
                    {
                        return false;
                    }
                    return true;
                }
            }
            return false;
        }

        //==================================================================================================

        public void setButtonImg(int type)//0 1~9 10
        {
            for (int i = 1; i <= 9; i++)
            {
                int num = (type == 10 ? 11 : type) * 10 + i;
                //SetOpacity(images[num], type == 10 ? 0.5f : 1f);
                buttons[i].BackgroundImage = images[num];
                buttons[i].Text = "";
                buttons[i].BackColor = Color.White;

            }

            if (type == 0)
            {
                setText(0, "標點");
            }
            else if (type <= 9)
            {
                setText(0, "姓氏");
            }
            else if (type == 10)
            {
                setText(0, "選字");
            }

            setText(10, "取消");
        }

        public void setZeroWords(string[] words)//
        {
            for (int i = 1; i <= 9; i++)
            {
                buttons[i].BackgroundImage = images[100 + i];
                buttons[i].BackColor = Color.White;
                if (i > words.Length || words[i - 1] == "*")
                {
                    buttons[i].Text = "";
                }
                else
                {
                    setText(i, words[i-1], true);
                }
            }
            setText(0, "標點");//
        }

        public void setButtonsText(string[] words)
        {
            for (int i = 1; i <= 9; i++)
            {
                buttons[i].BackgroundImage = null;

                if (i >= words.Length || words[i] == "*")
                {
                    buttons[i].BackColor = Color.Gray;
                    buttons[i].Text = "";
                }
                else
                {
                    setText(i, words[i]);
                }
            }
            //setText(0, buttons[0].Text);
            setText(10, "取消");
        }

        private void setText(int i, string s, bool relate = false)
        {
            buttons[i].BackColor = Color.White;
            buttons[i].Text = s;

            int fontsize = 0;
            if (!relate)
            {
                float scale = i==10?0.5f:0.46f;
                fontsize = (int)((currBoxSize - 6) / Math.Max(1, s.Length) * scale * (i == 0 ? 1.8f : 1));
                //Debug.WriteLine(s, fontsize, currBoxSize);
                //Debug.WriteLine($"non relate:{s},{fontsize},{currBoxSize}");//"s, fontsize, currBoxSize);
                buttons[i].TextAlign = ContentAlignment.MiddleCenter;
                buttons[i].ForeColor = Color.Black;
            }
            else
            {
                float scale = i == 10 ? 0.5f : 0.28f;
                fontsize = (int)((currBoxSize - 6) / Math.Max(1, s.Length) * scale);
                if(i>0 && i < 10)
                {
                    buttons[i].TextAlign = ContentAlignment.TopLeft;
                    buttons[i].ForeColor = Color.Gray;
                }
                //Debug.WriteLine(s, fontsize, currBoxSize);
                //Debug.WriteLine($"relate:{s},{fontsize},{currBoxSize}");//"s, fontsize, currBoxSize);
            }
            buttons[i].Font = new Font(buttons[i].Font.FontFamily, fontsize);
        }

        private void renewFontsize()
        {
            for (int i = 0; i <= 10; i++)
            {
                if (buttons[i].Text != "")
                {
                    setText(i, buttons[i].Text, buttons[i].TextAlign == ContentAlignment.TopLeft);
                }
            }
        }

        //====================================================





        private string currCode = "";

        private bool homo = false;
        private bool afterHomo = false;
        private bool openclose = false;
        private string lastWord = "";


        private void pressKey(int inputInt)//0~9
        {
            string inputStr = inputInt.ToString();

            if (this.selectMode)
            {
                if (inputInt == 0)
                {
                    commandInput(q9command.next);
                }
                else
                {
                    selectWord(inputInt);
                }
            }
            else
            {
                currCode += inputStr;
                setStatusPrefix(currCode);
                updateStatus();
                if (inputInt == 0)
                {
                    processResult(core.keyInput(Convert.ToInt32(currCode)));
                }
                else
                {
                    if (currCode.Length == 3)
                    {
                        processResult(core.keyInput(Convert.ToInt32(currCode)));
                    }
                    else if (currCode.Length == 1)
                    {
                        setButtonImg(inputInt);
                    }
                    else
                    {
                        setButtonImg(10);
                    }
                }
            }
            //SendKeys.Send("中");
        }


        private void commandInput(q9command command)
        {
            if (command == q9command.cancel)
            {
                cancel();
            }
            else if (command == q9command.openclose)
            {
                homo = false;
                afterHomo = false;
                openclose = true;

                string opencloseStr = String.Join("",core.keyInput(1));
                string[] opencloseArr = new string[(int)(opencloseStr.Length / 2.0)];
                for (int i = 0; i < opencloseStr.Length; i += 2)
                {
                    opencloseArr[i/2]=opencloseStr.Substring(i, 2);
                }
                setStatusPrefix("「」");
                startSelectWord(opencloseArr);
            }
            else if (command == q9command.homo)
            {
                homo = !homo;
                renewStatus();
            }
            else if (command == q9command.shortcut && selectMode==false)
            {
                if (currCode.Length == 0)
                {
                    setStatusPrefix("速選");
                    startSelectWord(core.keyInput(1000));
                }
                else if (currCode.Length == 1)
                {
                    setStatusPrefix($"速選{Convert.ToInt32(currCode)}");
                    startSelectWord(core.keyInput(1000+Convert.ToInt32(currCode)));
                }
                
            }
            else if (command == q9command.relate)
            {
                if (lastWord.Length==1)
                {
                    homo = false;
                    setStatusPrefix($"[{lastWord}]關聯");
                    startSelectWord(core.getRelate(lastWord));
                }
            }
            else if (command == q9command.prev && selectMode)
            {
                addPage(-1);
            }
            else if (command == q9command.next && selectMode)
            {
                addPage(1);
            }


            //core.commandInput(command);
            //prev,next shortcut reset-position 0 related
        }

        private void cancel(bool cleanRelate = true)
        {
            this.selectMode = false;
            homo = false;
            afterHomo = false;
            openclose = false;
            currCode = "";

            this.currPage = 0;
            this.selectWords = new string[0];

            setStatusPrefix();
            updateStatus();

            if (cleanRelate)
            {
                this.setButtonImg(0);
            }
        }

        public void processResult(string[] words)
        {
            if (words==null || words.Length==0)
            {
                //* 
                cancel();
                return;
            }
            startSelectWord(words);
        }

        //===================================================================================

        private bool selectMode = false;
        private string[] selectWords = new string[0];
        private int currPage = 0;
        private int totalPage = 0;

        public void addPage(int addNum)
        {
            if(currPage + addNum < 0)
            {
                showPage(totalPage-1);
            }
            else if (currPage + addNum >=totalPage)
            {
                showPage(0);
            }
            else
            {
                showPage(currPage + addNum);
            }
        }

        public void startSelectWord(string[] words)
        {
            if(words==null || words.Length==0)return;

            selectWords= words;
            totalPage = (int)Math.Ceiling(words.Length / 9.0);
            selectMode = true;
            currCode = "";
            showPage(0);
            setText(10, "取消");

            if (totalPage > 1)
            {
                setText(0, "下頁");
            }
            else
            {
                setText(0, "");
            }
        }

        public void showPage(int showPage)
        {
            currPage = showPage;
            string[] words = new string[10];
            for (int i = 1; i <= 9; i++)
            {
                int p = currPage * 9 + i - 1;
                if (p >= selectWords.Length || selectWords[p] == "*")
                {
                    words[i] = "";
                }
                else
                {
                    words[i] = selectWords[p];
                }
            }
            setButtonsText(words);

            updateStatus(totalPage > 1 ? $"{currPage + 1}/{totalPage}頁" : "");
        }

        public void selectWord(int inputInt)
        {
            int key = currPage * 9 + inputInt - 1;
            if(key>= selectWords.Length)
            {
                return;
            }
            string typeWord = selectWords[key];
            if (homo)
            {
                homo = false;
                afterHomo = true;
                setStatusPrefix($"同音[{typeWord}]");
                startSelectWord(core.getHomo(typeWord));
                return;
            }
            else if (openclose)
            {
                openclose = false;
                sendOpenClose(typeWord);
                cancel();
                return;
            }
            sendText(typeWord);
            string[] relates=new string[0];
            if (typeWord.Length == 1 )
            {
                lastWord = typeWord;
                relates = core.getRelate(typeWord);
            }
            else
            {
                lastWord = "";
            }
            bool lastafterHomo = afterHomo;
            if (relates!=null && relates.Length > 0)
            {
                setZeroWords(relates);
                cancel(false);
            }
            else
            {
                cancel();
            }

            Debug.WriteLine($"lastafterHomo:{lastafterHomo},{typeWord}");
            if (lastafterHomo)
            {
                afterHomo = false;
                int[] nums=core.getCode(typeWord);
                string numsstr = string.Join(",", nums);
                //Debug.WriteLine(numsstr, nums,nums[0]);
                setStatusPrefix($"{typeWord}key:{numsstr}");
            }
        }

        //===================================================================================
        private string statusPrefix;
        private string statusText;
        public void setStatusPrefix(string _prefix = "")
        {
            statusPrefix = _prefix;
            renewStatus();
        }
        public void updateStatus(string topText = "")
        {
            statusText = topText;
            renewStatus();
        }
        public void renewStatus()
        {
            this.Text = "九万 " + (homo ? "[同音] " : "") + statusPrefix + " " + statusText;
        }
    }
}