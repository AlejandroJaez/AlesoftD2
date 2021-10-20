using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using libzkfpcsharp;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Threading;
using RestSharp;
using RestSharp.Deserializers;
using Newtonsoft.Json;
using System.Runtime.InteropServices;

namespace AlesoftD2
{
    /// <summary>
    /// Lógica de interacción para MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        IntPtr mDevHandle = IntPtr.Zero;
        IntPtr mDBHandle = IntPtr.Zero;
        IntPtr FormHandle = IntPtr.Zero;
        bool bIsTimeToDie = false;
        bool IsRegister = false;
        bool bIdentify = true;
        byte[] FPBuffer;
        int RegisterCount = 0;
        String strShows;
        const int REGISTER_FINGER_COUNT = 3;

        byte[][] RegTmps = new byte[3][];
        byte[] RegTmp = new byte[2048];
        byte[] CapTmp = new byte[2048];
        byte[] paramValue1 = new byte[4];

        int cbCapTmp = 2048;
        int cbRegTmp = 1;
        int iFid = 1;
        int profileid = 0;
        List<Fingerprint> users = get_hashes();

        private int mfpWidth = 0;
        private int mfpHeight = 0;
        private int mfpDpi = 0;
        const int MESSAGE_CAPTURED_OK = 0x0400 + 6;

        Mutex myMutex;

        private void AppOnStartup(object sender, StartupEventArgs e)
        {
            bool aIsNewInstance = false;
            myMutex = new Mutex(true, "AlesoftD2", out aIsNewInstance);
            if (!aIsNewInstance)
            {
                MessageBox.Show("Already an instance is running...");
                App.Current.Shutdown();
            }
        }


        /// <summary>Brings main window to foreground.</summary>
        public void BringToForeground()
        {
            if (this.WindowState == WindowState.Minimized || this.Visibility == Visibility.Hidden)
            {
                this.Show();
                this.WindowState = WindowState.Normal;
            }

            // According to some sources these steps gurantee that an app will be brought to foreground.
            this.Activate();
            this.Topmost = true;
            this.Topmost = false;
            this.Focus();
        }

        public MainWindow()
        {
            InitializeComponent();
            Task.Run(() => {
                Set_available();
            });
            Task.Run(() => {
                DoCapture();
            });
        }

        private void DoCapture()
        {
            while (!bIsTimeToDie)
            {
                cbCapTmp = 2048;
                int ret = zkfp2.AcquireFingerprint(mDevHandle, FPBuffer, CapTmp, ref cbCapTmp);

                zkfp.Int2ByteArray(1, paramValue1);
                zkfp2.SetParameters(mDevHandle, 101, paramValue1, 4);
                if (ret == zkfp.ZKFP_ERR_OK)
                {
                    this.Dispatcher.Invoke(() =>
                    {
                        leftstatusBar.Text = "captura echa";
                    });
                    identifyFingerprint();
                }
                Thread.Sleep(200);
            }
        }

        private void identifyFingerprint()
        {
            int ret = zkfp.ZKFP_ERR_OK;
            int fid = 0, score = 0;
            ret = zkfp2.DBIdentify(mDBHandle, CapTmp, ref fid, ref score);

            if (zkfp.ZKFP_ERR_OK == ret)
            {
                this.Dispatcher.Invoke(() =>
                {
                    leftstatusBar.Text = "Registro encontrado.";
                });
                foreach (Fingerprint fingerprint in users)
                {
                    if (fid == fingerprint.id)
                    {
                        requestCheckin(fingerprint.user);
                    }
                }
                zkfp.Int2ByteArray(1, paramValue1);
                zkfp2.SetParameters(mDevHandle, 102, paramValue1, 4);
                Thread.Sleep(50);
                return;
            }
            else
            {
                this.Dispatcher.Invoke(() =>
                {
                    leftstatusBar.Text = "Error: " + ret ;
                });


                zkfp.Int2ByteArray(1, paramValue1);
                zkfp2.SetParameters(mDevHandle, 103, paramValue1, 4);
                Thread.Sleep(50);
                return;
            }
            
        }

        private void requestCheckin(int fid)
        {
            var client = new RestClient("https://rh.celfun.com/api/v1/check/");
            var request = new RestRequest("", Method.POST, RestSharp.DataFormat.Json);
            request.AddJsonBody(new { worker_id = fid });
            var response = client.Execute<Check>(request);
            var checkin = response.Data;
            this.Dispatcher.Invoke(() =>
            {
                try
                {
                    if (checkin.worker.photo != null)
                    {
                        profilePic.Source = new BitmapImage(new Uri(checkin.worker.photo.ToString(), UriKind.RelativeOrAbsolute));
                    }
            
                    nameLabel.Content = checkin.worker.first_name + " " + checkin.worker.last_name;
                    datetimeLabel.Content = checkin.timestamp;
                    statusLabel.Content = checkin.status;
                }
                catch {
                    nameLabel.Content = "Error";
                    datetimeLabel.Content = "ya existe un registro.";
                    statusLabel.Content = "";
                }
            });
            Thread.Sleep(2000);
            clear_ui();
        }

        public class Worker
        {
            public int pk { get; set; }
            public object photo { get; set; }
            public string first_name { get; set; }
            public string last_name { get; set; }
            public string salary { get; set; }
        }

        public class Check
        {
            public int id { get; set; }
            public Worker worker { get; set; }
            public string status { get; set; }
            public string date { get; set; }
            public DateTime timestamp { get; set; }
        }

        private void BnInitMethod()
        {
            int ret = zkfperrdef.ZKFP_ERR_OK;
            if ((ret = zkfp2.Init()) == zkfperrdef.ZKFP_ERR_OK)
            {
                int nCount = zkfp2.GetDeviceCount();
                if (nCount > 0)
                {
                    for (int i = 0; i < nCount; i++)
                    {
                    }
                }
                else
                {
                    zkfp2.Terminate();
                    leftstatusBar.Text = ("Sensor no encontrado.");

                }
            }
            else
            {
                //MessageBox.Show("Initialize fail, ret=" + ret + " !");
                //this.statusStrip1.Text = ("Initialize fail, ret=" + ret + " !");
                leftstatusBar.Text = ("Error al iniciar: " + ret + " !");
            }

            if (IntPtr.Zero == (mDevHandle = zkfp2.OpenDevice(0)))
            {
                //MessageBox.Show("OpenDevice fail");
                leftstatusBar.Text = ("Lector de huella desconectado");
                return;
            }
            if (IntPtr.Zero == (mDBHandle = zkfp2.DBInit()))
            {
                rightstatusBar.Text = ("Error al iniciar la base de datos");
                zkfp2.CloseDevice(mDevHandle);
                mDevHandle = IntPtr.Zero;
                return;
            }
            RegisterCount = 0;
            cbRegTmp = 1;
            iFid = 1;
            for (int i = 0; i < 3; i++)
            {
                RegTmps[i] = new byte[2048];
            }
            byte[] paramValue = new byte[4];
            int size = 4;
            zkfp2.GetParameters(mDevHandle, 1, paramValue, ref size);
            zkfp2.ByteArray2Int(paramValue, ref mfpWidth);

            size = 4;
            zkfp2.GetParameters(mDevHandle, 2, paramValue, ref size);
            zkfp2.ByteArray2Int(paramValue, ref mfpHeight);

            FPBuffer = new byte[mfpWidth * mfpHeight];

            size = 4;
            zkfp2.GetParameters(mDevHandle, 3, paramValue, ref size);
            zkfp2.ByteArray2Int(paramValue, ref mfpDpi);


            bIsTimeToDie = false;
            leftstatusBar.Text = ("Lector de huella listo");
            //zkfp2.SetParameters(mDevHandle, 101, paramValue1, 4);
            users_to_db();
        }




        public static List<Fingerprint> get_hashes()
        {
            RestClient client = new RestClient("https://rh.celfun.com/api/v1/hash/");
            RestRequest request = new RestRequest("", Method.GET, RestSharp.DataFormat.Json);
            var response = client.Execute<List<Fingerprint>>(request);
            //List m = JsonConvert.DeserializeObject<List<Profile>>(response.Content);
            //foreach (Profile profile in response.Data)
            //{
            //    textBox.AppendText(profile.id.ToString() + " " + profile.user.ToString() + " " + profile.hash.ToString());
            //}
            return response.Data;
        }

        private void users_to_db()
        {
            int ret = zkfp.ZKFP_ERR_OK;
            foreach (Fingerprint fingerprint in users)
            {
                byte[] hash = Convert.FromBase64String(fingerprint.hash.Trim()); ;
                ret = zkfp2.DBAdd(mDBHandle, fingerprint.id, hash);
                //rightstatusBar.Text = ret.ToString();
            }
        }


        private void Set_available() {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render,
            new Action(() =>
            {
                BnInitMethod();
                profilePic.Source = new BitmapImage(new Uri(@"images/dummy-profile-pic-300x300-1.png", UriKind.RelativeOrAbsolute));
                nameLabel.Content = "COLOQUE SU DEDO EN EL SENSOR";
                datetimeLabel.Content = "";
                statusLabel.Content = "";
                users_to_db();
            }));
        }

        private void clear_ui()
        {
            Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Render,
            new Action(() =>
            {
                BnInitMethod();
                profilePic.Source = new BitmapImage(new Uri(@"images/dummy-profile-pic-300x300-1.png", UriKind.RelativeOrAbsolute));
                nameLabel.Content = "COLOQUE SU DEDO EN EL SENSOR";
                datetimeLabel.Content = "";
                statusLabel.Content = "";
            }));
        }


        public class Fingerprint
        {
            [JsonProperty("id")]
            public int id { get; set; }
            [JsonProperty("user")]
            public int user { get; set; }
            [JsonProperty("hash")]
            public string hash { get; set; }
        }



    }
}
