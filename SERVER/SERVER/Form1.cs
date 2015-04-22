using System;
using System.IO;
using System.IO.Ports;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using Emgu.CV;
using Emgu.CV.Structure;
using Emgu.Util;
using System.Timers;

namespace SERVER
{
    public partial class Form1 : Form
    {
        //İnternet haberleşmesi için gerekli soketler ve data gelmesi
        //halinde kullanılacak olan byte dizisi
        private Socket m_mainSocket;
        Socket workerSocket;
        public byte[] dataBuffer = new byte[1024];
        
        //Görüntü çekmek ve işlemek için Emgu.cv sınıfı objeleri
        private Capture capture;
        Image<Bgr, Byte> ImageFrame;
        Image<Gray, Byte> ImageFrameProcessed;

        //Görüntü gönderme aralığını belirleyen timer
        private static System.Timers.Timer aTimer;

        //Cross-Thread problemi oluşması durumunda kullanılacak delege
        public delegate void UpdateLblCallback(string text);

        public Form1()
        {
            InitializeComponent();

            //Serverin IP'si labela yazdırılıyor
            lblIP.Text = "Server's IP: " + GetIP();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            //WebCam'den görüntü çekme işlemi başlatılıyor
            capture = new Capture();
        }

        public void OnClientConnect(IAsyncResult asyn)
        {
            try
            {
                //mainSockete bağlanan Client referansı workerSockete aktarılıyor
                //dinleme işlemi sona erdiriliyor
                workerSocket = m_mainSocket.EndAccept(asyn);

                //Labella kullanıcı bilgilendiriliyor
                CrossThreadUpdateLbl("A Client connected !");

                //Bağlanan Client için daha sonraki işlemleri workerSockete bırakıyor
                WaitForData(workerSocket);

                //Labella kullanıcı bilgilendiriliyor
                string str = "A Client connected !\n" + "Waiting for data from client and sending image stream";
                CrossThreadUpdateLbl(str);

                //Serbest kalan mainSocket başka clientların gelebilecek bağlantısını dinlemeye geri dönüyor
                m_mainSocket.BeginAccept(new AsyncCallback(OnClientConnect), null);
            }
            catch (ObjectDisposedException)
            {
                System.Diagnostics.Debugger.Log(0, "1", "\n OnClientConnection: Socket has been closed\n");
            }
            catch (SocketException se)
            {
                MessageBox.Show(se.Message);
            }
        }

        public void WaitForData(System.Net.Sockets.Socket soc)
        {
            try
            {
                //Asenkron olarak ilgili soketten bilgi bekleniyor
                soc.BeginReceive(dataBuffer, 0, dataBuffer.Length, SocketFlags.None, OnDataReceived, null);

                //Bilgi beklerken bir yandan da 200ms'de bir görüntü işleniyor
                //ve soket üzerinden gönderiliyor
                aTimer = new System.Timers.Timer(200);
                aTimer.Elapsed += new ElapsedEventHandler(ProcessFrame);
                aTimer.Enabled = true;
            }
            catch (SocketException se)
            {
                MessageBox.Show(se.Message);
            }
        }

        public void OnDataReceived(IAsyncResult asyn)
        {
            try
            {                
                //Clienttan gelen bilgi integera dönüştürülüp labela yazdırılıyor
                workerSocket.EndReceive(asyn);
                int data= BitConverter.ToInt32(dataBuffer, 0);
                if (data >= 10 && data <= 19)
                {
                    data = data - 10;
                    string str = lblInfo.Text + "\nReceived Value For First Motor = " + data;
                    CrossThreadUpdateLbl(str); 
                }

                else if (data >= 20 && data <= 29)
                {
                    data = data - 20;
                    string str = lblInfo.Text + "\nRecevied Value For Second Motor = " + data;
                    CrossThreadUpdateLbl(str);
                }

                else
                {
                    string str = lblInfo.Text + "\nFrame Per Second Value = " + data;
                    CrossThreadUpdateLbl(str);
                }
                              
                //soket yeni bilgiler için dinlemeye devam ediyor
                WaitForData(workerSocket);
            }
            catch (ObjectDisposedException)
            {
                System.Diagnostics.Debugger.Log(0, "1", "\nOnDataReceived: Socket has been closed\n");
            }
            catch (SocketException se)
            {
                if (se.ErrorCode == 10054) // Error code for Connection reset by peer
                {
                    string msg = lblInfo.Text + "\n" + "Client Disconnected" + "\n";
                    CrossThreadUpdateLbl(msg);
                }
                else
                {
                    MessageBox.Show(se.Message);
                }
            }
        }

        private void ProcessFrame(object sender, ElapsedEventArgs arg)
        {
            //WebCam'den çekilen görüntüler Emgu.cv image objesine atanıyor
            ImageFrame = capture.QueryFrame().Resize(320, 240, Emgu.CV.CvEnum.INTER.CV_INTER_LINEAR).Copy();

            //Görüntünün rengi gri tonlarına döndürülüyor
            ImageFrameProcessed = ImageFrame.Convert<Gray, Byte>();

            //memorystream yoluyla önce jpege dönüştürülüyor
            //daha sonra byte dizisine dönüştürülüyor
            //sonra soket üzerinden clienta gönderiliyor
            MemoryStream ms = new MemoryStream();
            ImageFrameProcessed.Bitmap.Save(ms, ImageFormat.Jpeg);
            byte[] bytes = new byte[20000000];
            bytes = ms.ToArray();
            try
            {
                workerSocket.BeginSend(bytes, 0, bytes.Length, SocketFlags.None, new AsyncCallback(onSend), null);
            }
            catch (SocketException ex)
            {
                m_mainSocket.BeginAccept(new AsyncCallback(OnClientConnect), null);
            }
        }

        void onSend(IAsyncResult ar)
        {
            //gönderme işlemi asenkron olarak tamamlanıyor
            workerSocket.EndSend(ar);
        }

        private void CrossThreadUpdateLbl(string msg)
        {
            // Check to see if this method is called from a thread 
            // other than the one created the control
            if (InvokeRequired)
            {
                object[] pList = { msg };
                lblInfo.BeginInvoke(new UpdateLblCallback(OnUpdateLbl), pList); // We cannot update the GUI on this thread.
                                                                                // All GUI controls are to be updated by the main (GUI) thread.
                                                                                // Hence we will use the invoke method on the control which will
                                                                                // be called when the Main thread is free
                                                                                // Do UI update on UI thread
            }
            else
            {
                OnUpdateLbl(msg);   // This is the main thread which created this control, hence update it directly 
            }
        }

        private void OnUpdateLbl(string msg)
        {
            lblInfo.Text = msg;
        }

        String GetIP()
        {
            String strHostName = Dns.GetHostName();

            // Find host by name
            IPHostEntry iphostentry = Dns.GetHostByName(strHostName);

            // Grab the first IP addresses
            String IPStr = "";
            foreach (IPAddress ipaddress in iphostentry.AddressList)
            {
                IPStr = ipaddress.ToString();
                return IPStr;
            }
            return IPStr;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            try
            {
                //mainSocket objesi tanımlanıyor ve gelen bağlantılar asenkron olarak dinlenmeye başlanıyor
                //Bağlanan Client olması durumunda OnClientConnect() metodu geriçağrılıyor
                m_mainSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                IPEndPoint ipLocal = new IPEndPoint(IPAddress.Any, 8000);
                m_mainSocket.Bind(ipLocal);
                m_mainSocket.Listen(4);
                m_mainSocket.BeginAccept(new AsyncCallback(OnClientConnect), null);
                lblInfo.Text = "Server started to listen any\nincoming client connections from port 8000";
                button1.Enabled = true;
            }
            catch (SocketException se)
            {
                MessageBox.Show(se.Message);
            }
        }

    }
}
