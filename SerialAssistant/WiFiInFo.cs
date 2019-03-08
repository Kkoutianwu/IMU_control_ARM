using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SerialAssistant
{
    public class WiFiInFo
    {
        public int SendCommandStatu { get; set; }// 1=发命令  2=返回成功 
        public DateTime LastTime { get; set; }
        public string data { get; set; }
        public string eqIpAdrr { get; set; }

        public WiFiInFo(string eqIpAdrr)
        {
            this.SendCommandStatu = 0;
            this.data = "";
            this.eqIpAdrr = eqIpAdrr;
        }
    }
}
