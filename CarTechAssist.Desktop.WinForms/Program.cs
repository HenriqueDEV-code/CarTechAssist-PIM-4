using CarTechAssist.Desktop.WinForms.Forms;
using CarTechAssist.Desktop.WinForms.Helpers;
using CarTechAssist.Desktop.WinForms.Services;

namespace CarTechAssist.Desktop.WinForms
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Sempre iniciar no LoginForm
            Application.Run(new LoginForm());
        }
    }
}
