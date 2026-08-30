using System;
using System.Threading;
using System.Windows.Forms;

namespace TT
{
    static class Program
    {

        [STAThread]
        static void Main()
        {
            const string appName = "TT";
            bool createdNew;
            var mutex = new Mutex(true, appName, out createdNew);
            if (!createdNew)
            {
                //app is already running! Exiting the application
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Form f = new TTForm();
            f.TopMost = true;

            InterceptKeys.init(ref f,((TTForm)f).handleKey);
            //Application.Run(f);
        }
    }
}