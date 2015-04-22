using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Timers;

namespace CLIENT
{
    public partial class Form1 : Form
    {
        //İnternet haberleşmesi için gerekli soketler ve data gelmesi
        //halinde kullanılacak olan byte dizisi
        public Socket mSocket;
        IPEndPoint mIpEndPoint;
        public byte[] dataBuffer = new byte[20000000];

        //Cross-Thread problemi oluşması durumunda kullanılacak delege
        public delegate void UpdateLblCallback(string text);

        int x=5;

        public Form1()
        {
            InitializeComponent();
        }

        public void ConnectToServer()
        {
            timer1.Stop();
            x = 5;
            label3.Text = "";

            //textboxtaki IP'li servera bağlanmayı dene
            String mIP = txtBxIP.Text;
            mIpEndPoint = new IPEndPoint(IPAddress.Parse(mIP), 8000);

            mSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);           
                try
                {
                    mSocket.Connect(mIpEndPoint);
                    lblInfo.Text = "Connected to Server that has " + mIP + " \nIP address through port 8000\nWaiting for incoming data !";
                    WaitForData();
                    btnSendData.Enabled = true;
                }

                //Bağlantı girişim başarısız olursa kullanıcıya
                //IP değiştirmeyi isteyip istemediğini sor
                catch (SocketException se)
                {
                    string str = se.Message +
                                "\nConnection failed to " + mIP + " addressed server\n" +
                                "Is the server running or is IP correct?\n" +
                                "Do you want to change IP ?";
                    switch (MessageBox.Show(str, "Connection failed", MessageBoxButtons.YesNoCancel,
                        MessageBoxIcon.Warning))
                    {
                            //programı kapat
                        case DialogResult.Cancel:
                            Environment.Exit(0);
                            break;

                            //bağlanmayı bir daha dene
                        case DialogResult.No:
                            ConnectToServer();
                            break;

                            //IP değiştirmek için formu aç
                        case DialogResult.Yes:
                            break;
                    }
                }                       
        }

        //txtboxIP texti değişirse timerı sıfırla
        public void txtChangedConnect(object sender, EventArgs e)
        {
            if (timer1.Enabled == true)
            {
                timer1.Dispose();                
            }
            x = 5;
            timer1.Start();
            label3.Text = "Connecting in\n" + x + "...";
            
        }

        //timer durumuna göre kullanıcıyı bilgilendir
        //süre sonunda servera bağlanmayı dene
        private void timer1_Tick(object sender, EventArgs e)
        {
            if (x == 0)
            {
                ConnectToServer();
            }
            else if (x == 1)
            {
                label3.Text = "Connection\nin progress...";
                x--;
            }
            else
            {
                x--;
                label3.Text = "Connecting in\n" + x + "...";
            }
        }

        public void WaitForData()
        {
            try
            {
                //Asenkron olarak ilgili soketten bilgi bekleniyor
                mSocket.BeginReceive(dataBuffer, 0, dataBuffer.Length, SocketFlags.None, OnDataReceived, null);
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
                //Bilgi alma işlemi sonlandırılıyor
                mSocket.EndReceive(asyn);

                //Gelen görüntü bilgisi memorystream 
                //vasıtasıyla pictureboxta gösteriliyor
                MemoryStream ms = new MemoryStream(dataBuffer);
                try
                {
                    pictureBox1.Image = Image.FromStream(ms, false, true);
                    pictureBox2.Image = Image.FromStream(ms, false, true);
                }
                catch (ArgumentException)
                { }

                //Asenkron olarak ilgili soketten bilgi beklenmeye devam ediliyor
                WaitForData();
            }
            catch (ObjectDisposedException)
            {
                System.Diagnostics.Debugger.Log(0, "1", "\nOnDataReceived: Socket has been closed\n");
            }
            catch (SocketException se)
            {
                CrossThreadUpdateLbl("\nServer has been closed !");
                pictureBox1.Image = null;
            }
        }

        private void btnSendData_Click(object sender, EventArgs e)
        {
            //Girilen hız bilgisi textboxtan çekiliyor
            //ve soket üzerinden senkron olarak servera iletiliyor
            try
            {
                if (txtBxData.Text != "")
                {
                    int data = Int32.Parse(txtBxData.Text);
                    data = data + 10;
                    if (data >= 10 && data <= 19)
                    {
                        byte[] buffer = BitConverter.GetBytes(data);
                        mSocket.Send(buffer);
                        data = data - 10;
                        lblInfo.Text += "\nSended Value For First Motor = " + data;
                    }
                    else
                    {
                        MessageBox.Show("Please Enter A Value Between 0 and 9");
                    }
                }
                txtBxData.Clear();
                txtBxData.Focus();
            }
            catch (SocketException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (FormatException ex)
            {
                MessageBox.Show("Wrong format has been entered !");
            }
        }

        private void btnSendData2_Click(object sender, EventArgs e)
        {
            //Girilen hız bilgisi textboxtan çekiliyor
            //ve soket üzerinden senkron olarak servera iletiliyor
            try
            {
                if (txtBxData2.Text != "")
                {
                    int data = Int32.Parse(txtBxData2.Text);
                    data = data + 20;
                    if (data >= 20 && data <= 29)
                    {
                        byte[] buffer = BitConverter.GetBytes(data);
                        mSocket.Send(buffer);
                        data = data - 20;
                        lblInfo.Text += "\nSended Value For Second Motor = " + data;
                    }
                    else
                    {
                        MessageBox.Show("Please Enter A Value Between 0 and 9");
                    }
                }
                txtBxData2.Clear();
                txtBxData2.Focus();
            }
            catch (SocketException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (FormatException ex)
            {
                MessageBox.Show("Wrong format has been entered !");
            }
        }

        private void txtBxData_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)13)
            {
                btnSendData_Click(null,null);
            }
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
            lblInfo.Text += "\n" + msg;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //Program açıldığında textboxta yazılı IP'li servera
            //bağlanmayı dene
            ConnectToServer();

            //eğer soket bağlı değilse hız bilgisi gönderme
            //butonu disabled olsun
            if (mSocket.Connected == false)
            {
                btnSendData.Enabled = false;
                btnSendData2.Enabled = false;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            try
            {
                if (textBox1.Text != "")
                {
                    int data = Int32.Parse(textBox1.Text);
                                    
                        byte[] buffer = BitConverter.GetBytes(data);
                        mSocket.Send(buffer);
                        lblInfo.Text += "\nFrame Per Second Value = " + data;
                   
                }
                textBox1.Clear();
                textBox1.Focus();
            }
            catch (SocketException ex)
            {
                MessageBox.Show(ex.Message);
            }
            catch (FormatException ex)
            {
                MessageBox.Show("Wrong format has been entered !");
            }

        }


        
    }
}
