using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace SignApp
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }



        private bool systemErrorOccurred = false;
        private bool signErrorOccurred = false;

        private Timer timer = new Timer();

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            int interval = Settings.Default.Interval;
            if (interval > 0)
                interval *= 1000;
            else
                interval = 2000;

            timer.Interval = interval;
            timer.Elapsed += timer_Elapsed;
            timer.Start();
        }

        private void SetDiag(string systemError, string signError)
        {
            if (!string.IsNullOrWhiteSpace(systemError))
            {
                lblError.Text = "System error: "+systemError; systemErrorOccurred = true;
            }
            if (!string.IsNullOrWhiteSpace(signError))
            {
                if (!signErrorOccurred)
                    txtSingle.Text = "Sign error: " + signError + "\n";
                else
                    txtSingle.Text += signError + "\n";

                signErrorOccurred = true;
            }
            if (systemErrorOccurred)
            {
                lblStatus.Content = "Error";
                lblStatus.Foreground = new SolidColorBrush(Colors.Red);
            }
            else if (signErrorOccurred && isRunning)
            {
                lblStatus.Content = "Working";
                lblStatus.Foreground = new SolidColorBrush(Colors.DarkKhaki);
            }
            else if (!isRunning)
            {
                lblStatus.Content = "Paused";
                lblStatus.Foreground = new SolidColorBrush(Colors.DarkKhaki);
            }
            else
            {
                lblStatus.Content = "Working";
                lblStatus.Foreground = new SolidColorBrush(Colors.Green);
            }
        }

        private void SetTime()
        {
            lblTime.Content = "Last run at " + DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
        }

        private void timer_Elapsed(object sender, ElapsedEventArgs e)
        {
            try
            {
                timer.Stop();
                SRVWebServiceClient client = new SRVWebServiceClient();

                List<SRV_DOCUMENT> documents = client.GetDocumentsForSystemSign();
                List<SRV_DOCUMENT> signedDocuments = new List<SRV_DOCUMENT>();
                string certName = Settings.Default.CertName;
                foreach (SRV_DOCUMENT document in documents)
                {
                    try
                    {
                        string signedXML = Signer.SignXML(ToString(document.XMLCONTENT), certName);
                        SRV_DOCUMENT signedDocument = new SRV_DOCUMENT()
                        {
                            DESCRIPTION = document.DESCRIPTION,
                            DOCUMENTID = document.DOCUMENTID,
                            SIGNATURECOUNT = 1,
                            SIGNEDDOCUMENTID = -1,
                            XMLCONTENT = ToByteArray(signedXML)
                        };
                        signedDocuments.Add(signedDocument);
                    }
                    catch (Exception ex)
                    {
                        this.Dispatcher.Invoke(new Action<string, string>(SetDiag), string.Empty
                            , /*ex.Message*/  ex.GetDescription());
                    }
                }

                client = new SRVWebServiceClient();
                client.SaveSystemSignedDocuments(signedDocuments);
                timer.Start();
                this.Dispatcher.Invoke(new Action(SetTime));
            }
            catch (Exception ex)
            {
                this.Dispatcher.Invoke(new Action<string, string>(SetDiag), ex.Message /* ex.GetDescription()*/, string.Empty);
                this.Dispatcher.Invoke(new Action<bool>(StartStop), false);
            }
        }

        private bool isRunning = true;
        private void StartStop(bool doStart)
        {
            isRunning = doStart;
            timer.Enabled = doStart;
            if (doStart)
            {
                btnControl.Content = "Pause";
                lblError.Text = "No system errors after restart";
                txtSingle.Text = "No sign errors after restart";
                signErrorOccurred = false;
                systemErrorOccurred = false;
            }
            else
            {
                btnControl.Content = "Start";
            }
            SetDiag(null, null);
        }


        private byte[] ToByteArray(string s)
        {
            return Encoding.GetEncoding(1251).GetBytes(s);
            /*byte[] byteArray = new byte[s.Length * sizeof(char)];
            Buffer.BlockCopy(s.ToCharArray(), 0, byteArray, 0, byteArray.Length);
            return byteArray;*/
        }

        private string ToString(byte[] bytes)
        {
            return Encoding.GetEncoding(1251).GetString(bytes);
            /*char[] charArray = new char[bytes.Length / sizeof(char)];
            Buffer.BlockCopy(bytes, 0, charArray, 0, bytes.Length);
            return new string(charArray);*/
        }

        private void btnControl_Click(object sender, RoutedEventArgs e)
        {
            StartStop(!isRunning);
        }
    }
}
