using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace PatchClientWebservices
{
    /// <summary>
    /// Interaction logic for DiffWindow.xaml
    /// </summary>
    public partial class DiffWindow : Window
    {
        private string serviceFolder;
        private string wcfFolder;

        public string ServiceFolder
        {
            get { return serviceFolder; }
            set { serviceFolder = value; this.txServiceFolder.Text = value; }
        }

        public string WCFFolder
        {
            get { return wcfFolder; }
            set { wcfFolder = value; this.txWCFFolder.Text = value; }
        }

        private MainWindow mainWindow;

        public DiffWindow(string defaultServiceFolder, string defaultWCFFolder, MainWindow mainWindow)
        {
            InitializeComponent();
            ServiceFolder = defaultServiceFolder;
            WCFFolder = defaultWCFFolder;
            this.mainWindow = mainWindow;
        }

        private void btnSelectService_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog fg = new FolderBrowserDialog())
            {
                fg.SelectedPath = ServiceFolder;
                if (fg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    ServiceFolder = fg.SelectedPath;
                }
            }
        }

        private void btnSelectWCF_Click(object sender, RoutedEventArgs e)
        {
            using (FolderBrowserDialog fg = new FolderBrowserDialog())
            {
                fg.SelectedPath = WCFFolder;
                if (fg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    WCFFolder = fg.SelectedPath;
                }
            }
        }

        // First, parts of proxy classes source code are saved to selected folders
        // Then saved parts of WCF proxy classes are compared and [Transient] attribute placement is checked
        private void runDiff_Click(object sender, RoutedEventArgs e)
        {
            if (mainWindow.cbServices.SelectedItem != null)
            {
                mainWindow.SaveParts(mainWindow.FolderName + "\\Service References\\" + mainWindow.cbServices.SelectedItem.ToString() + "\\Reference.cs", ServiceFolder, true);
            }

            bool doClean = true;

            foreach (string commonEntitiesFile in mainWindow.CommonEntityFiles)
            {
                mainWindow.SaveParts(mainWindow.CommonFolderName + "\\Entities\\" + commonEntitiesFile, WCFFolder, doClean);
                doClean = false;
            }

            foreach (string entitiesFile in mainWindow.EntityFiles)
            {
                mainWindow.SaveParts(mainWindow.FolderName + "\\Entities\\" + entitiesFile, WCFFolder, doClean);
                doClean = false;
            }

            Process p = new Process();
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.FileName = "diff";
            p.StartInfo.Arguments = "-i --ignore-file-name-case -w -d -B -r "+ServiceFolder+" "+WCFFolder;
            p.Start();
            string output = p.StandardOutput.ReadToEnd();
            p.WaitForExit();

            tbDiff.Text = output;

            using (StreamWriter writer = new StreamWriter(ServiceFolder + "//..//wcf.diff", false, Encoding.GetEncoding(1251)))
            { writer.Write(output); }

            tbTransient.Text = "";
            string mode = "";
            string line = "";
            string buf = "";

            foreach (string file in Directory.GetFiles(WCFFolder))
            {
                mode = "";
                buf = "";
                using (StreamReader reader = new StreamReader(file))
                {
                    while ((line = reader.ReadLine()) != null)
                    {
                        if (line.IndexOf("[Transient]") > -1)
                        {
                            buf += line + "\n";
                            mode = "Transient"; 
                        }
                        else if (line.IndexOf("ExtensionData") > -1)
                        {
                            buf += line + "\n";
                        }
                        else if (mode == "Transient" && (line.IndexOf("public") > -1))
                        {
                            buf += "----------\n";
                            mode = ""; 
                        }                            
                    }
                    if (buf != "")
                    {
                        tbTransient.Text += file + "\n" + buf;
                    }
                }                
            }
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            this.DataContext = this;
            ServiceFolder = ServiceFolder;
            WCFFolder = WCFFolder;
        }
    }
}
