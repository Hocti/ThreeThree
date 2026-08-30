using System;
using System.Threading;
using System.Windows.Forms;

namespace Q9CS
{
    static class Program
    {

        [STAThread]
        static void Main()
        {
            const string appName = "Q9CS";
            bool createdNew;
            var mutex = new Mutex(true, appName, out createdNew);
            if (!createdNew)
            {
                //app is already running! Exiting the application
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Form f = new Q9Form();
            f.TopMost = true;

            InterceptKeys.init(ref f,((Q9Form)f).handleKey);
            //Application.Run(f);
        }
    }
}