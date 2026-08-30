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

namespace TT
{

    public enum ttcommand
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


    public partial class TTForm : Form
    {

        private static readonly string StartupKey = "SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Run";
        private static readonly string StartupValue = "TT";


        private TTCore core;
        private TTFreq freq;
        private List<Button> buttons = new List<Button> { };

        private Image[] images = new Image[120];

        //九宮格筆劃圖：一幅 sheet，橫 SHEET_COLS 格 x 直 SHEET_ROWS 格。
        //每格幾大唔緊要 (900x1000、810x1000、1800x2000 都得)，一律由 sheet 尺寸除返出嚟。
        private const string SHEET_FILE = "files/default90.png";
        private const int SHEET_COLS = 9;
        private const int SHEET_ROWS = 10;
        //10_x 那套：原圖原大擺喺一個大 1/0.5714 的畫布右下角，等佢睇落縮細咗
        private const float SMALL_IMG_SCALE = 100f / 175f;

        private bool active = true;
        private bool sc_output = false;
        private bool use_numpad = true;
        private bool long_press = true;
        private bool freq_sort = true;

        private IniFile ini = new IniFile("tt_settings.ini");
        private Dictionary<string, int> Keys = new Dictionary<string, int>();
        private Dictionary<string, string> KeysStr = new Dictionary<string, string>();
        private Dictionary<string, int> altKeys = new Dictionary<string, int>();

        public void TTForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Save size and position
            ini.Write("Window", "Left", this.Left);
            ini.Write("Window", "Top", this.Top);
            ini.Write("Window", "BoxSize", currBoxSize);
        }

        //

        public TTForm()
        {
            InitializeComponent();
            this.StartPosition = FormStartPosition.Manual;
            this.ControlBox = false;

            //preload image：切開 files/default90.png (橫 9 直 10 格)
            //  第 0 行     -> images[1 ~ 9]      (原本 0_1 ~ 0_9)
            //  第 1~9 行   -> images[10i + j]    (原本 1_1 ~ 9_9)
            //  images[100 + j] = 第 0 行縮細擺右下角 (原本 10_1 ~ 10_9)
            //  images[110 + j] = 第 0 行半透明
            using (Bitmap sheet = new Bitmap(SHEET_FILE))
            {
                //用格邊界算，唔用「格闊 x 第幾格」，咁 sheet 尺寸唔啱除都唔會漏 pixel
                for (int i = 0; i < SHEET_ROWS; i++)
                {
                    int y0 = i * sheet.Height / SHEET_ROWS;
                    int y1 = (i + 1) * sheet.Height / SHEET_ROWS;
                    for (int j = 1; j <= SHEET_COLS; j++)
                    {
                        int x0 = (j - 1) * sheet.Width / SHEET_COLS;
                        int x1 = j * sheet.Width / SHEET_COLS;
                        images[i * 10 + j] = sheet.Clone(
                            new Rectangle(x0, y0, x1 - x0, y1 - y0),
                            PixelFormat.Format32bppArgb);
                    }
                }

                for (int j = 1; j <= SHEET_COLS; j++)
                {
                    int tw = images[j].Width;
                    int th = images[j].Height;
                    int cw = (int)Math.Round(tw / SMALL_IMG_SCALE);
                    int ch = (int)Math.Round(th / SMALL_IMG_SCALE);

                    Bitmap small = new Bitmap(cw, ch, PixelFormat.Format32bppArgb);
                    using (Graphics g = Graphics.FromImage(small))
                    {
                        g.Clear(Color.White);
                        //SourceCopy：原圖連 alpha 直接蓋落去，唔好同白底溝埋。
                        //要寫明 dest rect，唔用 DrawImageUnscaled — 佢會跟圖片 DPI 縮放
                        g.CompositingMode = CompositingMode.SourceCopy;
                        g.DrawImage(images[j], new Rectangle(cw - tw, ch - th, tw, th),
                            new Rectangle(0, 0, tw, th), GraphicsUnit.Pixel);
                    }
                    images[100 + j] = small;
                    images[110 + j] = SetOpacity(images[j], 0.5f);
                }
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
                    b.Click += new System.EventHandler((object Sender, EventArgs e) => commandInput(ttcommand.cancel));
                }
            }

            //----------------


            Rectangle screenSize = Screen.PrimaryScreen.Bounds;
            core = new TTCore();
            freq = new TTFreq();

            longPressTimer = new System.Windows.Forms.Timer();
            longPressTimer.Interval = LONG_PRESS_MS;
            longPressTimer.Tick += longPressTick;

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
                ini.Write("system", "long_press", 1);
                ini.Write("system", "freq_sort", 1);

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
                long_press = Convert.ToBoolean(ini.ReadInt("system", "long_press", 1));
                freq_sort = Convert.ToBoolean(ini.ReadInt("system", "freq_sort", 1));

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


            this.FormClosing += TTForm_FormClosing;

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
《三三正體中文輸入法》使用方法:
需要開啟numLock

.           取消，特別強調0和.都不會因為要入關聯字或標點而改變用途
+           選擇關聯字 (在打下一個字前，都可隨時進入)
-           (選字時)上一頁
            (首頁) 快速選字 (碼表 id 1000)
            (首頁中按1~9後) 快速選字，共九種 (碼表 id 1001~1009)
*           同音字(打字後不出字，會進入同音字選字表)
/           「」等開關標點
0           首頁按0會進入普通標黜

長按 (0.4秒，可在tray menu開關):
   0~9 和 . 長按都不會變成連續輸入
   (選字時) 長按 1~9 = 入該字的同音表   等於 *
   (首頁未打code) 長按 0   = 開關符號表   等於 /
   (首頁未打code) 長按 1~9 = 1~9號速選表   等於 -
   (首頁未打code) 長按 .   = 關聯(候選)字表 等於 +

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

如想自行修改，可以開啓`tt_settings.ini`
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


            //long press
            MenuItem menuLongPress = new MenuItem("長按功能", new EventHandler((sender, e) => {
                long_press = !long_press;
                ((MenuItem)sender).Checked = long_press;
                ini.Write("system", "long_press", long_press ? 1 : 0);
                longPressClear();
            }));
            menuLongPress.Checked = long_press;
            contextMenu1.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] { menuLongPress });


            //frequently used words first
            MenuItem menuFreqSort = new MenuItem("常用字調前", new EventHandler((sender, e) => {
                freq_sort = !freq_sort;
                ((MenuItem)sender).Checked = freq_sort;
                ini.Write("system", "freq_sort", freq_sort ? 1 : 0);
            }));
            menuFreqSort.Checked = freq_sort;
            contextMenu1.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] { menuFreqSort });


            //clear the frequency database
            MenuItem menuFreqClear = new MenuItem("清除常用字記錄", new EventHandler((sender, e) => {
                if (MessageBox.Show("確定清除所有常用字/連打記錄?", "三三正體中文輸入法",
                        MessageBoxButtons.OKCancel, MessageBoxIcon.Question) == DialogResult.OK)
                {
                    freq.clear();
                }
            }));
            contextMenu1.MenuItems.AddRange(new System.Windows.Forms.MenuItem[] { menuFreqClear });



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
            notifyIcon1.Text = "三三";
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
                longPressUp(keyCode);
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
                longPressClear();
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
                        return keyDown(i, keyCode);
                    }
                }

                if (keyCode != altKeys["cancel"])
                {
                    longPressFlush();//仲㩒住數字掣就去㩒指令掣，先結算返佢
                }

                if (keyCode == altKeys["cancel"])
                {
                    keyDown(CANCEL_INPUT, keyCode);
                }
                else if (keyCode == altKeys["relate"])
                {
                    commandInput(ttcommand.relate);
                }
                else if (keyCode == altKeys["homo"])
                {
                    commandInput(ttcommand.homo);
                }
                else if (keyCode == altKeys["openclose"])
                {
                    commandInput(ttcommand.openclose);
                }
                else if (keyCode == altKeys["shortcut"] && !selectMode)
                {
                    commandInput(ttcommand.shortcut);
                }
                else if (keyCode == altKeys["prev"] && selectMode)
                {
                    commandInput(ttcommand.prev);
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
                    return keyDown(inputInt, keyCode);
                }
                else
                {
                    if (keyCode != 110)
                    {
                        longPressFlush();//仲㩒住數字掣就去㩒指令掣，先結算返佢
                    }

                    if (keyCode == 110)
                    {
                        keyDown(CANCEL_INPUT, keyCode);
                    }
                    else if (keyCode == Keys["relate"])
                    {
                        commandInput(ttcommand.relate);
                    }
                    else if (keyCode == Keys["homo"])
                    {
                        commandInput(ttcommand.homo);
                    }
                    else if (keyCode == Keys["openclose"])
                    {
                        commandInput(ttcommand.openclose);
                    }
                    else if (keyCode == Keys["shortcut"] && !selectMode)
                    {
                        commandInput(ttcommand.shortcut);
                    }
                    else if (keyCode == Keys["prev"] && selectMode)
                    {
                        commandInput(ttcommand.prev);
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

        //=========================================== 長按 =================================================

        // 0~9 用 0~9，「取消」(numpad .) 用 10
        private const int CANCEL_INPUT = 10;
        private const int LONG_PRESS_MS = 400;

        private enum longAction
        {
            none,
            homo,       // 選字時長按 1~9 -> 該字的同音表   (等於 numpad *)
            openclose,  // 首頁長按 0     -> 開關符號表     (等於 numpad /)
            shortcut,   // 首頁長按 1~9   -> 1~9號速選表    (等於 numpad -)
            relate,     // 首頁長按 .     -> 關聯(候選)字表 (等於 numpad +)
        }

        private System.Windows.Forms.Timer longPressTimer;
        private longAction pendingLong = longAction.none;
        private int lpKeyCode = -1;
        private int lpInput = -1;
        private bool lpFired = false;
        private bool lpDeferred = false;

        /// <summary>按下去嗰刻，睇下現時狀態有冇長按功能可以用。</summary>
        private longAction pickLongAction(int input)
        {
            if (selectMode)
            {
                //開關符號表冇同音可言，多過一個字的項目 getHomo 亦查唔到
                if (!openclose && input >= 1 && input <= 9)
                {
                    int key = currPage * 9 + input - 1;
                    if (key < selectWords.Length && selectWords[key] != "*"
                        && TTFreq.isSingleWord(selectWords[key]))
                    {
                        return longAction.homo;
                    }
                }
                return longAction.none;
            }
            if (currCode.Length == 0)
            {
                if (input == 0) return longAction.openclose;
                if (input >= 1 && input <= 9) return longAction.shortcut;
                if (input == CANCEL_INPUT) return longAction.relate;
            }
            return longAction.none;
        }

        private void shortAction(int input)
        {
            if (input == CANCEL_INPUT)
            {
                commandInput(ttcommand.cancel);
            }
            else
            {
                pressKey(input);
            }
        }

        /// <summary>
        /// 0~9 和「取消」的 key down。
        /// 長按功能開著時，同一個鍵的 auto repeat 會被食走，
        /// 所以長按永遠唔會變連續輸入。
        /// </summary>
        private bool keyDown(int input, int keyCode)
        {
            if (!long_press)
            {
                shortAction(input);
                return true;
            }

            if (lpKeyCode == keyCode)
            {
                return true;//auto repeat，食咗佢
            }

            longPressFlush();//上一個鍵未放手就按下一個 (roll over)

            longAction la = pickLongAction(input);

            lpKeyCode = keyCode;
            lpInput = input;
            lpFired = false;
            // 選字時短按會即刻出字，收唔返，所以要放手先做；
            // 其餘情況短按只係改介面，長按時直接蓋過就得，可以即刻有反應。
            lpDeferred = (la == longAction.homo);
            pendingLong = la;

            if (!lpDeferred)
            {
                shortAction(input);
            }
            if (la != longAction.none)
            {
                longPressTimer.Start();
            }
            return true;
        }

        private void longPressUp(int keyCode)
        {
            if (lpKeyCode != keyCode) return;
            longPressFlush();
        }

        /// <summary>放手／被打斷：未夠時間就當普通短按。</summary>
        private void longPressFlush()
        {
            if (lpKeyCode == -1) return;
            longPressTimer.Stop();
            bool run = !lpFired && lpDeferred;
            int input = lpInput;
            longPressClear();
            if (run)
            {
                shortAction(input);
            }
        }

        private void longPressClear()
        {
            if (longPressTimer != null) longPressTimer.Stop();
            lpKeyCode = -1;
            lpInput = -1;
            lpFired = false;
            lpDeferred = false;
            pendingLong = longAction.none;
        }

        private void longPressTick(object sender, EventArgs e)
        {
            longPressTimer.Stop();
            if (lpKeyCode == -1 || lpFired) return;
            lpFired = true;

            switch (pendingLong)
            {
                case longAction.homo:
                    homo = true;
                    selectWord(lpInput);
                    break;
                case longAction.openclose:
                    commandInput(ttcommand.openclose);
                    break;
                case longAction.shortcut:
                    commandInput(ttcommand.shortcut);
                    break;
                case longAction.relate:
                    commandInput(ttcommand.relate);
                    break;
            }
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
        private string prevTypedWord = "";


        private void pressKey(int inputInt)//0~9
        {
            string inputStr = inputInt.ToString();

            if (this.selectMode)
            {
                if (inputInt == 0)
                {
                    commandInput(ttcommand.next);
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


        private void commandInput(ttcommand command)
        {
            if (command == ttcommand.cancel)
            {
                cancel();
            }
            else if (command == ttcommand.openclose)
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
                startSelectWord(opencloseArr, false);
            }
            else if (command == ttcommand.homo)
            {
                homo = !homo;
                renewStatus();
            }
            else if (command == ttcommand.shortcut && selectMode==false)
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
            else if (command == ttcommand.relate)
            {
                if (lastWord.Length==1)
                {
                    homo = false;
                    setStatusPrefix($"[{lastWord}]關聯");
                    //關聯字唔用 sortByFreq，getRelateList 已經按連打次數排好，而且第一頁都可以動
                    startSelectWord(getRelateList(lastWord), false);
                }
            }
            else if (command == ttcommand.prev && selectMode)
            {
                addPage(-1);
            }
            else if (command == ttcommand.next && selectMode)
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

        public void startSelectWord(string[] words, bool useFreqSort = true)
        {
            if(words==null || words.Length==0)return;

            if (useFreqSort)
            {
                words = sortByFreq(words);
            }

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
            //"*" 係碼表用嚟留空位，showPage 唔會 display，撳落去亦當冇撳過
            if (key >= selectWords.Length || selectWords[key] == "*")
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

            freq.record(prevTypedWord, typeWord);
            prevTypedWord = TTFreq.isSingleWord(typeWord) ? typeWord : "";

            string[] relates=new string[0];
            if (typeWord.Length == 1 )
            {
                lastWord = typeWord;
                relates = getRelateList(typeWord);
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

        //========================================= 常用字 ===================================

        /// <summary>
        /// 選字表：第一頁 (頭9個) 的次序原封不動，
        /// 第二頁之後先按打過的次數調前。
        /// 碼表入面用嘅 "*" (留空位) 唔會被搬走。
        /// </summary>
        private string[] sortByFreq(string[] words)
        {
            if (!freq_sort || words == null || words.Length <= 10) return words;

            var slots = new List<int>();
            var tail = new List<string>();
            for (int i = 9; i < words.Length; i++)
            {
                if (words[i] == "*") continue;
                slots.Add(i);
                tail.Add(words[i]);
            }
            if (tail.Count < 2) return words;

            var counts = freq.getWordCounts(tail);
            if (counts.Count == 0) return words;

            //OrderByDescending 係 stable 排序，同次數的字保持碼表原本次序
            var sorted = tail.Where(w => counts.ContainsKey(w))
                             .OrderByDescending(w => counts[w])
                             .Concat(tail.Where(w => !counts.ContainsKey(w)))
                             .ToList();

            var result = (string[])words.Clone();
            for (int i = 0; i < slots.Count; i++)
            {
                result[slots[i]] = sorted[i];
            }
            return result;
        }

        /// <summary>
        /// 關聯(候選)字表：連住打過超過兩次的字排最前，次數多排前，
        /// 就算本身唔係關聯字表入面都會加入去。第一頁次序都會動。
        /// </summary>
        private string[] getRelateList(string word)
        {
            string[] baseArr = core.getRelate(word);
            if (baseArr == null) baseArr = new string[0];
            if (!freq_sort) return baseArr;

            var hot = freq.getFollowers(word);
            if (hot.Count == 0) return baseArr;

            var result = new List<string>();
            foreach (string w in hot)
            {
                if (!result.Contains(w)) result.Add(w);
            }
            foreach (string w in baseArr)
            {
                if (w == "*" || !result.Contains(w)) result.Add(w);
            }
            return result.ToArray();
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
            this.Text = "三三 " + (homo ? "[同音] " : "") + statusPrefix + " " + statusText;
        }
    }
}