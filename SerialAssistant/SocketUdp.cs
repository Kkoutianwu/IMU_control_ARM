using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

namespace SerialAssistant
{
    class SocketUdp
    {
        public delegate void AsyncDelegate(DateTime curTime, string eqIpAdrr, int iPort, string dataStr);
        private AsyncDelegate dlgt;
        private AsyncDelegate dlgtShow;
        private Socket Lisen_sock;
        private int iLocalPort = 1399;//本机(电脑)侦听端口
        private int iPcPort = 9250;//上位机侦听端口
        private string strLocalIp;

        private byte bStatu = 1;//状态 0=接收 1=暂停接收
        private byte viewType = 0;//0=当前数据  1=数据记录

        private object lockList = new object();

        private System.Collections.Hashtable EqWifiIpKeyList;
        private List<WiFiInFo> WiFiList;
        private ListView lstView;

        public string Udp_date;


        public void StartAccept()
        {
            bStatu = 0;
        }
        public void StopAccept()
        {
            bStatu = 1;
        }
        /// <summary>
        /// 显示类型
        /// </summary>
        /// <param name="vType"> 0=当前数据  1=数据记录</param>


        /// <summary>
        /// 处理数据
        /// </summary>
        /// <param name="curTime">接收时间</param>
        /// <param name="eqIpAdrr">IP地址</param>
        /// <param name="iPort">端口</param>
        /// <param name="dataStr">数据</param>

        private void ShowDataViewTh()
        {
            while (true)
            {
                for (int i = 0; i < WiFiList.Count; i++)
                {
                    if (WiFiList[i].data.Length > 0)
                    {
                        dlgtShow.BeginInvoke(WiFiList[i].LastTime, WiFiList[i].eqIpAdrr, 0, WiFiList[i].data, null, null);
                        WiFiList[i].data = "";
                    }
                }

                System.Threading.Thread.Sleep(5);
            }
        }



        /// <summary>
        /// 发送UDP
        /// </summary>
        public bool sendUdp(string sIp, string sendTxt)
        {
            bool bRet = false;

            byte[] sendByte = Encoding.UTF8.GetBytes(sendTxt);
            bRet = sendUdp(sIp, iPcPort, sendByte);

            return bRet;
        }

        public void Set_Udp_Watting(string sIp)
        {
            if (EqWifiIpKeyList.ContainsKey(sIp))
            {
                int WiFi_Index = (int)EqWifiIpKeyList[sIp];
                WiFiList[WiFi_Index].SendCommandStatu = 1;
            }
        }

        public bool Get_Return_OK(string sIp)
        {
            bool bRet = false;
            if (EqWifiIpKeyList.ContainsKey(sIp))
            {
                int WiFi_Index = (int)EqWifiIpKeyList[sIp];
                if (WiFiList[WiFi_Index].SendCommandStatu == 2)
                {
                    bRet = true;
                }
            }
            return bRet;
        }

        /// <summary>
        /// 发送UDP 16进制
        /// </summary>
        public bool sendUdp_Hex(string sIp,string sEquimpNo,string strHexData)
        {
            bool bRet = false;
            Console.WriteLine("{0} 开始发送命令", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            byte[] sendByte = Get_Sendbuffer(sEquimpNo, hexToByte(strHexData));

            bRet = sendUdp(sIp, iPcPort, sendByte);


            Console.WriteLine("{0} 结束发送命令", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            return bRet;
        }

        public bool sendUdp_HexTest(string sIp, string sEquimpNo, string strHexData)
        {
            bool bRet = false;
            Console.WriteLine(string.Format("{0} 开始发送命令", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")));
            byte[] sendByte = Get_Sendbuffer(sEquimpNo, hexToByte(strHexData));
            bRet = sendUdp(sIp, iPcPort, sendByte);
            Console.WriteLine(string.Format("{0} 结束发送命令", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")));
            return bRet;
        }

        /// <summary>
        /// 16进制转字节数组
        /// </summary>
        /// <param name="strHex">分隔符为空格如：AA 0B CC</param>
        /// <param name="Delimiter">分隔符</param>
        /// <returns>字节数组</returns>
        private static byte[] hexToByte(string strHex, char Delimiter = ' ')
        {
            byte[] b = new byte[1];
            try
            {
                if (strHex.Length > 0)
                {
                    List<string> hexList = new List<string>();
                    if (Delimiter.ToString().Length > 0)
                    {
                        string[] sl = strHex.Split(Delimiter);

                        foreach (string s in sl)
                        {
                            if (s.Trim().Length > 0)
                            {
                                hexList.Add(s.Trim());
                            }
                        }
                    }
                    else
                    {
                        string str = strHex.Replace(" ", "");
                        while (str.Length > 0)
                        {
                            if (str.Length > 2)
                            {
                                hexList.Add(str.Substring(0, 2));
                                str = str.Substring(2);
                            }
                            else
                            {
                                hexList.Add(str);
                                str = "";
                            }
                        }
                    }
                    b = new byte[hexList.Count];
                    for (int i = 0; i < b.Length; i++)
                    {
                        b[i] = byte.Parse(hexList[i], System.Globalization.NumberStyles.HexNumber);
                    }
                    hexList = null;
                }
            }
            catch (Exception)
            {
                ;
            }
            return b;
        }

        private byte[] Get_Sendbuffer(string sEquipmentNo, byte[] HexByte, string command = "02")
        {
            byte[] tmp = Encoding.UTF8.GetBytes(sEquipmentNo + command);
            int len = tmp.Length + HexByte.Length + 2;
            byte[] buffer = new byte[len];
            tmp.CopyTo(buffer, 0);
            if (HexByte.Length > 0) { HexByte.CopyTo(buffer, tmp.Length); }
            buffer[len - 2] = 13;
            buffer[len - 1] = 10;
            return buffer;
        }

        /// <summary>
        /// 发送UDP
        /// </summary>
        private bool sendUdp(string sIp, int iPort, byte[] sendByte)
        {
            bool bRet = false;
            try
            {
                System.Net.IPAddress ip = System.Net.IPAddress.Parse(sIp);
                System.Net.IPEndPoint serverIP = new System.Net.IPEndPoint(ip, iPort);

                System.Net.Sockets.Socket udpClient = new System.Net.Sockets.Socket(System.Net.Sockets.AddressFamily.InterNetwork, System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp);
                udpClient.SendTo(sendByte, System.Net.Sockets.SocketFlags.None, serverIP);
                udpClient.Close();
                bRet = true;
            }
            catch (Exception ex)
            {
                Console.WriteLine("发送UDP出错:" + ex.Message);
            }
            return bRet;
        }

        /// <summary>
        /// 广播数据(本机地址和端口)
        /// </summary>
        public void Ad_SendAddress()
        {
            try
            {
                IPEndPoint iep = new IPEndPoint(IPAddress.Broadcast, iPcPort);//9050
                byte[] data = Encoding.ASCII.GetBytes("WIT" + strLocalIp + "\r\n");
                Socket Ad_Socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                Ad_Socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, 1);
                Ad_Socket.SendTo(data, iep);
                //Ad_Socket.Close();
            }
            catch (Exception ex)
            {

                ;
            }

        }
        /// <summary>
        /// 开始启动服务
        /// </summary>
        public void StartService()
        {
            strLocalIp = "";
            string strHostName = Dns.GetHostName();
            IPAddress[] ipadrlist = Dns.GetHostAddresses(strHostName);
            foreach (IPAddress ipa in ipadrlist)
            {
                if (ipa.AddressFamily == AddressFamily.InterNetwork)
                {
                    strLocalIp = ipa.ToString();
                }
            }

            Thread thread = new Thread(Accept_Data);

            thread.IsBackground = true;

            thread.Start();

            Thread ad = new Thread(Time_Broadcast_Address);
            ad.IsBackground = true;
            ad.Start();
           
        }
        /// <summary>
        /// 定时广播地址 
        /// </summary>
        private void Time_Broadcast_Address()
        {
            while (true)
            {
                Ad_SendAddress();
                Thread.Sleep(5000);
            }
        }

        private void Accept_Data()
        {

            IPAddress ip;
            Console.WriteLine("OK2");
            if (strLocalIp.Length == 0)
            {
                ip = IPAddress.Any;
            }
            else
            {
                ip = IPAddress.Parse(strLocalIp);
            }
            Lisen_sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            IPEndPoint iep = new IPEndPoint(ip, iLocalPort);
            Lisen_sock.Bind(iep);
            EndPoint ep = (EndPoint)iep;
            Lisen_sock.ReceiveBufferSize = 200;
            int length;
            string strWords = ""; ;
            //string sstrTxt = "";
            string sIpAddress;
            int iPort;
            DateTime curTime;
            object lockobj = new object();
            while (true)
            {
                //通信用socket
                byte[] data = new byte[1024 * 20];

                    length = Lisen_sock.ReceiveFrom(data, ref ep);//接受来自服务器的数据 
                if (length > 0)
                {
                    curTime = DateTime.Now;

                    //strWords = Encoding.UTF8.GetString(data).Replace(((char)0).ToString(), "");
                    strWords = Encoding.Default.GetString(data, 0, length);

                    sIpAddress = ((IPEndPoint)ep).Address.ToString();
                    iPort = ((IPEndPoint)ep).Port;

                    this.Udp_date = strWords;
                   
                    // Console.WriteLine(Udp_date);
                }
            }
            //WriteDATA.ErrLog("接收:" + sstrTxt);
        }

        public void CloseSock()
        {
            try
            {
                Lisen_sock.Close();
                dlgt = null;
                dlgtShow = null;
            }
            catch (Exception)
            {

                ;
            }

        }
    }
}
