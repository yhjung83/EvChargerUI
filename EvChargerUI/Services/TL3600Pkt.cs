using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EvChargerUI.Services
{
    public class TL3600Pkt
    {
        public const byte SOH = 1;
        public const byte STX = 2;
        public const byte ETX = 3;
        public const byte EOT = 4;
        public const byte ENQ = 5;
        public const byte ACK = 6;
        public const byte SYN = 22;
        public const byte CR = 13;
        public const byte LF = 10;
        public const byte NACK = 21;
        public const byte CMD_TX_TREMCHACK = 65;
        public const byte CMD_TX_PAY = 66;
        public const byte CMD_TX_PAY_G = 71;
        public const byte CMD_TX_PAYCANCEL = 67;
        public const byte CMD_TX_SEARCH = 68;
        public const byte CMD_TX_WAITNG = 69;
        public const byte CMD_TX_UID = 70;
        public const byte CMD_TX_RESET = 82;
        public const byte CMD_TX_CONFIG = 88;
        public const byte CMD_TX_WRITING = 75;
        public const byte CMD_RX_TREMCHACK = 97;
        public const byte CMD_RX_PAY = 98;
        public const byte CMD_RX_PAY_G = 103;
        public const byte CMD_RX_PAYCANCEL = 99;
        public const byte CMD_RX_SEARCH = 100;
        public const byte CMD_RX_WAITNG = 101;
        public const byte CMD_RX_UID = 102;
        public const byte CMD_RX_RESET = 114;
        public const byte CMD_RX_EVENT = 64;
        public const byte CMD_RX_CONFIG = 120;
        public const byte CMD_RX_WRITING = 107;
        public const int HEADER_SIZE = 35;
        public string termID;
        public string dateTime;
        public byte jodCode = 0;
        public byte respCode = 0;
        public int dataLength = 0;
        public int retry = 0;
        public byte[] data = (byte[])null;

        public TL3600Pkt()
        {
        }

        public TL3600Pkt(string tid, byte jcode, DateTime now)
        {
            this.termID = tid;
            this.dateTime = now.ToString("yyyyMMddHHmmss");
            this.jodCode = jcode;
            this.respCode = (byte)0;
        }
    }
}
