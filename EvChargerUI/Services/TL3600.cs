using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EvChargerUI.Services
{
    public class TL3600
    {
        public string TERMID = "7303502001000000";
        public string tlserStatus;
        public string tlserStatusText = string.Empty;
        private const int RECV_BUF_SIZE = 1024;
        private const int SEND_BUF_SIZE = 512;
        private const int MAX_RETRY_COUNT = 3;
        private TL3600.DbgLogEvent dbgLogEventFunc = (TL3600.DbgLogEvent)null;
        private TL3600.ResponseCallback responseCallback = (TL3600.ResponseCallback)null;
        private SerialPort serialPort = new SerialPort();
        private byte[] recvData = new byte[1024];
        private byte[] sendData = new byte[512];
        private int recvPos = 0;
        private object lockSend = new object();
        private bool isPrePayReq = false;
        public Dictionary<string, string> prePayInfo = (Dictionary<string, string>)null;
        public Dictionary<string, string> lastPayInfo = (Dictionary<string, string>)null;
        public Dictionary<string, string> prePayInfo_sub = (Dictionary<string, string>)null;
        private TL3600.State curState = TL3600.State.None;
        private Timer retryTimer = (Timer)null;
        private TL3600Pkt retryPacket = (TL3600Pkt)null;
        public static bool checkcancel = false;

        public TL3600(string tid)
        {
            this.TERMID = tid;
            this.retryTimer = new Timer();
            this.retryTimer.Interval = 3000;
            this.retryTimer.Tick += new EventHandler(this.RetrySendPktTimeout);
            this.retryTimer.Enabled = false;
        }

        public void SetDbgLogEvent(TL3600.DbgLogEvent eventFunc) => this.dbgLogEventFunc = eventFunc;

        public void SetResponseCallback(TL3600.ResponseCallback callFunc)
        {
            this.responseCallback = callFunc;
        }

        public bool Open(string port, int baudRate)
        {
            this.serialPort.PortName = port;
            this.serialPort.BaudRate = baudRate;
            this.serialPort.DataBits = 8;
            this.serialPort.ReadTimeout = 500;
            this.serialPort.WriteTimeout = 500;
            this.serialPort.DataReceived += new SerialDataReceivedEventHandler(this.SerialDataReceived);
            try
            {
                this.serialPort.Open();
            }
            catch (Exception ex)
            {
                this.DbgLog("TL3600 Open Error:" + ex.ToString());
                this.tlserStatus = "TL3600 Open Error:";
                return false;
            }
            this.DbgLog("TL3600 Opened:" + port);
            this.tlserStatus = "TL3600 Opened:" + port;
            this.SetState(TL3600.State.Ready);
            return true;
        }

        private void SerialDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                int bytesToRead = this.serialPort.BytesToRead;
                this.DbgLog("RAW READ:" + (object)bytesToRead);
                if (bytesToRead + this.recvPos > 1024)
                {
                    this.DbgLog("Error Buff!!");
                    this.tlserStatus = "Error Buff!!!";
                    this.recvPos = 0;
                }
                else
                {
                    int length = this.serialPort.Read(this.recvData, this.recvPos, bytesToRead);
                    this.recvPos += length;
                    this.DbgLog("<< RECV_RAW: " + BitConverter.ToString(this.recvData, 0, length));
                    do
                    {
                        for (; this.recvPos > 0 && this.recvData[0] != (byte)2; --this.recvPos)
                        {
                            if (this.recvData[0] == (byte)6)
                                this.OnAck();
                            if (this.recvPos > 1)
                                Array.Copy((Array)this.recvData, 1, (Array)this.recvData, 0, this.recvPos - 1);
                        }
                        if (this.recvPos > 35 && this.recvData[0] == (byte)2)
                        {
                            int dataSize = (int)this.recvData[34] << 8 & 65280 | (int)this.recvData[33];
                            int num = dataSize + 35 + 2;
                            if (this.recvPos >= num)
                            {
                                this.CheckData(this.recvData, dataSize, num);
                                if (this.recvPos > num)
                                    Array.Copy((Array)this.recvData, num, (Array)this.recvData, 0, this.recvPos - num);
                                this.recvPos -= num;
                            }
                            else
                                return;
                        }
                    }
                    while (this.recvPos > 35);
                }
            }
            catch (Exception ex)
            {
                // Prevent unhandled exceptions from killing the application
                this.DbgLog("SerialDataReceived Error: " + ex.ToString());
                this.tlserStatus = "SerialDataReceived Error";
                // reset buffer position to avoid repeated errors
                this.recvPos = 0;
            }
        }

        private void SendOneCommand(byte cmd)
        {
            this.SendToSerial(new byte[1] { cmd }, 0, 1);
            if (cmd == (byte)21)
            {
                this.DbgLog(">>SendNack>");
            }
            else
            {
                if (cmd != (byte)6)
                    return;
                this.DbgLog(">>SendACK>");
            }
        }

        private void CheckData(byte[] pkt, int dataSize, int size)
        {
            bool flag = true;
            this.DbgLog("<< RECV: " + BitConverter.ToString(pkt, 0, size));
            if (pkt[31] == (byte)64)
                flag = false;
            if (pkt[size - 2] != (byte)3)
            {
                if (!flag)
                    return;
                this.SendOneCommand((byte)21);
            }
            else
            {
                byte num = 0;
                for (int index = 0; index < size - 1; ++index)
                    num ^= pkt[index];
                if ((int)num != (int)pkt[size - 1])
                {
                    if (!flag)
                        return;
                    this.SendOneCommand((byte)21);
                }
                else
                {
                    if (flag)
                        this.SendOneCommand((byte)6);
                    this.ParsePacket(pkt, dataSize, size);
                }
            }
        }

        private void ParsePacket(byte[] pkt, int dataSize, int size)
        {
            TL3600Pkt pkt1 = new TL3600Pkt();
            int index1 = 1;
            pkt1.termID = Encoding.Default.GetString(pkt, index1, 16);
            int index2 = index1 + 16;
            pkt1.dateTime = Encoding.Default.GetString(pkt, index2, 14);
            int num1 = index2 + 14;
            TL3600Pkt tl3600Pkt1 = pkt1;
            byte[] numArray1 = pkt;
            int index3 = num1;
            int num2 = index3 + 1;
            int num3 = (int)numArray1[index3];
            tl3600Pkt1.jodCode = (byte)num3;
            TL3600Pkt tl3600Pkt2 = pkt1;
            byte[] numArray2 = pkt;
            int index4 = num2;
            int num4 = index4 + 1;
            int num5 = (int)numArray2[index4];
            tl3600Pkt2.respCode = (byte)num5;
            pkt1.dataLength = dataSize;
            int sourceIndex = num4 + 2;
            pkt1.data = new byte[dataSize];
            Array.Copy((Array)pkt, sourceIndex, (Array)pkt1.data, 0, dataSize);
            this.ProcessPacket(pkt1);
        }

        private void ProcessPacket(TL3600Pkt pkt)
        {
            switch (pkt.jodCode)
            {
                case 64:
                    this.OnEvent(pkt);
                    break;
                case 97:
                    this.OnTermCheck(pkt);
                    break;
                case 98:
                    this.OnPay(pkt);
                    break;
                case 99:
                    this.OnPayCancel(pkt);
                    break;
                case 100:
                    this.OnSearch(pkt);
                    break;
                case 101:
                    this.OnWaiting(pkt);
                    break;
                case 102:
                    this.OnUid(pkt);
                    break;
                case 103:
                    this.OnPay_G(pkt);
                    break;
                case 107:
                    this.TermResetReq(DateTime.Now);
                    break;
                case 120:
                    this.TermWritingReq(DateTime.Now);
                    break;
            }
        }

        private void OnAck()
        {
            this.retryTimer.Enabled = false;
            this.SetState(TL3600.State.Ready);
        }

        private void OnTermCheck(TL3600Pkt pkt)
        {
            Dictionary<string, string> retVal = new Dictionary<string, string>();
            Dictionary<string, string> dictionary1 = retVal;
            char ch = Convert.ToChar(pkt.data[0]);
            string str1 = ch.ToString() ?? "";
            dictionary1.Add("commStat", str1);
            Dictionary<string, string> dictionary2 = retVal;
            ch = Convert.ToChar(pkt.data[1]);
            string str2 = ch.ToString() ?? "";
            dictionary2.Add("rfModuleStat", str2);
            Dictionary<string, string> dictionary3 = retVal;
            ch = Convert.ToChar(pkt.data[2]);
            string str3 = ch.ToString() ?? "";
            dictionary3.Add("vanStat", str3);
            Dictionary<string, string> dictionary4 = retVal;
            ch = Convert.ToChar(pkt.data[3]);
            string str4 = ch.ToString() ?? "";
            dictionary4.Add("serverStat", str4);
            if (this.responseCallback == null)
                return;
            this.responseCallback(TL3600.ResponseType.Check, retVal);
        }

        private void OnSearch(TL3600Pkt pkt)
        {
            Dictionary<string, string> retVal = new Dictionary<string, string>();
            int num1 = 0;
            Dictionary<string, string> dictionary1 = retVal;
            byte[] data1 = pkt.data;
            int index1 = num1;
            int num2 = index1 + 1;
            char ch = Convert.ToChar(data1[index1]);
            string str1 = ch.ToString() ?? "";
            dictionary1.Add("card_type", str1);
            Dictionary<string, string> dictionary2 = retVal;
            byte[] data2 = pkt.data;
            int index2 = num2;
            int index3 = index2 + 1;
            ch = Convert.ToChar(data2[index2]);
            string str2 = ch.ToString() ?? "";
            dictionary2.Add("card", str2);
            retVal.Add("cardnum", Encoding.Default.GetString(pkt.data, index3, 20));
            int index4 = index3 + 20;
            retVal.Add("date", Encoding.Default.GetString(pkt.data, index4, 14));
            int index5 = index4 + 14;
            retVal.Add("price", Encoding.Default.GetString(pkt.data, index5, 8));
            int index6 = index5 + 8;
            retVal.Add("balance", Encoding.Default.GetString(pkt.data, index6, 8));
            int index7 = index6 + 8;
            Dictionary<string, string> dictionary3 = retVal;
            ch = Convert.ToChar(pkt.data[index7]);
            string str3 = ch.ToString() ?? "";
            dictionary3.Add("info", str3);
            if (this.responseCallback == null)
                return;
            this.responseCallback(TL3600.ResponseType.Search, retVal);
        }

        private void OnPay(TL3600Pkt pkt)
        {
            Dictionary<string, string> retVal = new Dictionary<string, string>();
            int num1 = 0;
            Dictionary<string, string> dictionary1 = retVal;
            byte[] data1 = pkt.data;
            int index1 = num1;
            int num2 = index1 + 1;
            char ch = Convert.ToChar(data1[index1]);
            string str1 = ch.ToString() ?? "";
            dictionary1.Add("payCode", str1);
            Dictionary<string, string> dictionary2 = retVal;
            byte[] data2 = pkt.data;
            int index2 = num2;
            int index3 = index2 + 1;
            ch = Convert.ToChar(data2[index2]);
            string str2 = ch.ToString() ?? "";
            dictionary2.Add("payType", str2);
            retVal.Add("cardNum", Encoding.Default.GetString(pkt.data, index3, 20));
            int index4 = index3 + 20;
            retVal.Add("totalCost", Encoding.Default.GetString(pkt.data, index4, 10));
            int index5 = index4 + 10;
            retVal.Add("tax", Encoding.Default.GetString(pkt.data, index5, 8));
            int index6 = index5 + 8;
            retVal.Add("service", Encoding.Default.GetString(pkt.data, index6, 8));
            int index7 = index6 + 8;
            retVal.Add("div", Encoding.Default.GetString(pkt.data, index7, 2));
            int index8 = index7 + 2;
            retVal.Add("authNum", Encoding.Default.GetString(pkt.data, index8, 12));
            int index9 = index8 + 12;
            retVal.Add("payDate", Encoding.Default.GetString(pkt.data, index9, 8));
            int index10 = index9 + 8;
            retVal.Add("payTime", Encoding.Default.GetString(pkt.data, index10, 6));
            int index11 = index10 + 6;
            retVal.Add("uniqueNum", Encoding.Default.GetString(pkt.data, index11, 12));
            int index12 = index11 + 12;
            retVal.Add("regNum", Encoding.Default.GetString(pkt.data, index12, 15));
            int index13 = index12 + 15;
            retVal.Add("termId", Encoding.Default.GetString(pkt.data, index13, 14));
            int num3 = index13 + 14;
            byte[] data3 = pkt.data;
            int index14 = num3;
            int index15 = index14 + 1;
            ch = Convert.ToChar(data3[index14]);
            if (ch.Equals((object)"X"))
            {
                retVal.Add("errMsg1", Encoding.Default.GetString(pkt.data, index15, 40));
                int num4 = index15 + 40;
            }
            if (this.isPrePayReq)
            {
                this.prePayInfo = retVal;
                this.isPrePayReq = false;
            }
            this.lastPayInfo = retVal;
            if (this.responseCallback == null)
                return;
            this.responseCallback(TL3600.ResponseType.Pay, retVal);
        }

        private void OnPay_G(TL3600Pkt pkt)
        {
            Dictionary<string, string> retVal = new Dictionary<string, string>();
            int num1 = 0;
            Dictionary<string, string> dictionary1 = retVal;
            byte[] data1 = pkt.data;
            int index1 = num1;
            int num2 = index1 + 1;
            char ch = Convert.ToChar(data1[index1]);
            string str1 = ch.ToString() ?? "";
            dictionary1.Add("payCode", str1);
            Dictionary<string, string> dictionary2 = retVal;
            byte[] data2 = pkt.data;
            int index2 = num2;
            int index3 = index2 + 1;
            ch = Convert.ToChar(data2[index2]);
            string str2 = ch.ToString() ?? "";
            dictionary2.Add("payType", str2);
            retVal.Add("cardNum", Encoding.Default.GetString(pkt.data, index3, 20));
            int index4 = index3 + 20;
            retVal.Add("totalCost", Encoding.Default.GetString(pkt.data, index4, 10));
            int index5 = index4 + 10;
            retVal.Add("tax", Encoding.Default.GetString(pkt.data, index5, 8));
            int index6 = index5 + 8;
            retVal.Add("service", Encoding.Default.GetString(pkt.data, index6, 8));
            int index7 = index6 + 8;
            retVal.Add("div", Encoding.Default.GetString(pkt.data, index7, 2));
            int index8 = index7 + 2;
            retVal.Add("authNum", Encoding.Default.GetString(pkt.data, index8, 12));
            int index9 = index8 + 12;
            retVal.Add("payDate", Encoding.Default.GetString(pkt.data, index9, 8));
            int index10 = index9 + 8;
            retVal.Add("payTime", Encoding.Default.GetString(pkt.data, index10, 6));
            int index11 = index10 + 6;
            retVal.Add("uniqueNum", Encoding.Default.GetString(pkt.data, index11, 12));
            int index12 = index11 + 12;
            retVal.Add("regNum", Encoding.Default.GetString(pkt.data, index12, 15));
            int index13 = index12 + 15;
            retVal.Add("termId", Encoding.Default.GetString(pkt.data, index13, 14));
            int index14 = index13 + 14;
            retVal.Add("errMsg1", Encoding.Default.GetString(pkt.data, index14, 40));
            int index15 = index14 + 40;
            retVal.Add("pgnum", Encoding.Default.GetString(pkt.data, index15, 30));
            int num3 = index15 + 30;
            if (this.isPrePayReq)
            {
                this.prePayInfo = retVal;
                this.isPrePayReq = false;
            }
            this.lastPayInfo = retVal;
            if (this.responseCallback == null)
                return;
            this.responseCallback(TL3600.ResponseType.Pay, retVal);
        }

        private void OnPayCancel(TL3600Pkt pkt)
        {
            Dictionary<string, string> retVal = new Dictionary<string, string>();
            int num1 = 0;
            Dictionary<string, string> dictionary1 = retVal;
            byte[] data1 = pkt.data;
            int index1 = num1;
            int num2 = index1 + 1;
            char ch = Convert.ToChar(data1[index1]);
            string str1 = ch.ToString() ?? "";
            dictionary1.Add("payCode", str1);
            Dictionary<string, string> dictionary2 = retVal;
            byte[] data2 = pkt.data;
            int index2 = num2;
            int index3 = index2 + 1;
            ch = Convert.ToChar(data2[index2]);
            string str2 = ch.ToString() ?? "";
            dictionary2.Add("payType", str2);
            retVal.Add("cardNum", Encoding.Default.GetString(pkt.data, index3, 20));
            int index4 = index3 + 20;
            retVal.Add("totalCost", Encoding.Default.GetString(pkt.data, index4, 10));
            int index5 = index4 + 10;
            retVal.Add("tax", Encoding.Default.GetString(pkt.data, index5, 8));
            int index6 = index5 + 8;
            retVal.Add("service", Encoding.Default.GetString(pkt.data, index6, 8));
            int index7 = index6 + 8;
            retVal.Add("div", Encoding.Default.GetString(pkt.data, index7, 2));
            int index8 = index7 + 2;
            retVal.Add("authNum", Encoding.Default.GetString(pkt.data, index8, 12));
            int index9 = index8 + 12;
            retVal.Add("payDate", Encoding.Default.GetString(pkt.data, index9, 8));
            int index10 = index9 + 8;
            retVal.Add("payTime", Encoding.Default.GetString(pkt.data, index10, 6));
            int index11 = index10 + 6;
            retVal.Add("uniqueNum", Encoding.Default.GetString(pkt.data, index11, 12));
            int index12 = index11 + 12;
            retVal.Add("regNum", Encoding.Default.GetString(pkt.data, index12, 15));
            int index13 = index12 + 15;
            retVal.Add("termId", Encoding.Default.GetString(pkt.data, index13, 14));
            int num3 = index13 + 14;
            byte[] data3 = pkt.data;
            int index14 = num3;
            int index15 = index14 + 1;
            ch = Convert.ToChar(data3[index14]);
            if (ch.Equals((object)"X"))
            {
                retVal.Add("errMsg1", Encoding.Default.GetString(pkt.data, index15, 40));
                int num4 = index15 + 40;
            }
            if (this.responseCallback == null)
                return;
            this.responseCallback(TL3600.ResponseType.CancelPay, retVal);
        }

        private void OnWaiting(TL3600Pkt pkt)
        {
            Dictionary<string, string> retVal = new Dictionary<string, string>();
            retVal.Add("type", "waiting");
            if (this.responseCallback == null)
                return;
            this.responseCallback(TL3600.ResponseType.Event, retVal);
        }

        private void OnUid(TL3600Pkt pkt)
        {
            Dictionary<string, string> retVal = new Dictionary<string, string>();
            retVal.Add("type", "uid");
            retVal.Add("uid", Encoding.Default.GetString(pkt.data, 0, 10));
            if (this.responseCallback == null)
                return;
            this.responseCallback(TL3600.ResponseType.Event, retVal);
        }

        private void OnEvent(TL3600Pkt pkt)
        {
            Dictionary<string, string> retVal = new Dictionary<string, string>();
            retVal.Add("type", "event");
            retVal.Add("event", Convert.ToChar(pkt.data[0]).ToString() ?? "");
            if (this.responseCallback == null)
                return;
            this.responseCallback(TL3600.ResponseType.Event, retVal);
        }

        private TL3600.State GetState() => this.curState;

        public void SetState(TL3600.State state)
        {
            this.curState = state;
            this.tlserStatus = "SetState : " + state.ToString();
            this.tlserStatusText = state.ToString();
        }

        private void SendToSerial(byte[] data, int start, int size)
        {
            lock (this.lockSend)
            {
                try
                {
                    this.serialPort.Write(data, start, size);
                }
                catch
                {
                }
            }
        }

        private void SendRequest(TL3600Pkt pkt)
        {
            if (this.curState != TL3600.State.Ready)
                return;
            this.SendRequestPacket(pkt);
            this.retryPacket = pkt;
            this.SetState(TL3600.State.WaitingAck);
            this.retryTimer.Enabled = true;
        }

        private void SendRequestPacket(TL3600Pkt pkt)
        {
            // 배열 초기화
            Array.Clear((Array)this.sendData, 0, this.sendData.Length);

            int num1 = 0;
            byte[] sendData1 = this.sendData;
            int index1 = num1;
            int destinationIndex1 = index1 + 1;
            sendData1[index1] = (byte)2;

            byte[] bytes1 = Encoding.Default.GetBytes(pkt.termID);
            byte[] array = Enumerable.Repeat<byte>((byte)32, 3).ToArray<byte>();
            byte[] numArray = new byte[bytes1.Length + array.Length];
            Array.Copy((Array)bytes1, 0, (Array)numArray, 0, bytes1.Length);
            Array.Copy((Array)array, 0, (Array)numArray, bytes1.Length, array.Length);
            Array.Copy((Array)numArray, 0, (Array)this.sendData, destinationIndex1, numArray.Length);
            int destinationIndex2 = destinationIndex1 + 16;
            byte[] bytes2 = Encoding.Default.GetBytes(pkt.dateTime);
            Array.Copy((Array)bytes2, 0, (Array)this.sendData, destinationIndex2, bytes2.Length);
            int num2 = destinationIndex2 + 14;
            byte[] sendData2 = this.sendData;
            int index2 = num2;
            int num3 = index2 + 1;
            int jodCode = (int)pkt.jodCode;
            sendData2[index2] = (byte)jodCode;
            byte[] sendData3 = this.sendData;
            int index3 = num3;
            int num4 = index3 + 1;
            sendData3[index3] = (byte)0;
            byte[] sendData4 = this.sendData;
            int index4 = num4;
            int num5 = index4 + 1;
            int num6 = (int)(byte)(pkt.dataLength & (int)byte.MaxValue);
            sendData4[index4] = (byte)num6;
            byte[] sendData5 = this.sendData;
            int index5 = num5;
            int destinationIndex3 = index5 + 1;
            int num7 = (int)(byte)(pkt.dataLength >> 8 & (int)byte.MaxValue);
            sendData5[index5] = (byte)num7;
            if (pkt.data != null)
                Array.Copy((Array)pkt.data, 0, (Array)this.sendData, destinationIndex3, pkt.dataLength);
            int num8 = destinationIndex3 + pkt.dataLength;
            byte[] sendData6 = this.sendData;
            int index6 = num8;
            int num9 = index6 + 1;
            sendData6[index6] = (byte)3;
            byte num10 = 0;
            for (int index7 = 0; index7 < num9; ++index7)
                num10 ^= this.sendData[index7];
            byte[] sendData7 = this.sendData;
            int index8 = num9;
            int num11 = index8 + 1;
            int num12 = (int)num10;
            sendData7[index8] = (byte)num12;
            this.SendToSerial(this.sendData, 0, num11);
            this.DbgLog(">> Send: " + BitConverter.ToString(this.sendData, 0, num11));
        }

        public void RetrySendPktTimeout(object sender, EventArgs e)
        {
            if (this.retryPacket == null)
            {
                this.retryTimer.Enabled = false;
                this.retryPacket = (TL3600Pkt)null;
                this.SetState(TL3600.State.Ready);
                Dictionary<string, string> retVal = new Dictionary<string, string>();
                retVal.Add("error", "nopacket");
                if (this.responseCallback == null)
                    return;
                this.responseCallback(TL3600.ResponseType.Error, retVal);
            }
            else
            {
                ++this.retryPacket.retry;
                if (this.retryPacket.retry >= 3)
                {
                    this.DbgLog("Max Retry!!. Packet Timeout Error");
                    this.retryTimer.Enabled = false;
                    this.retryPacket = (TL3600Pkt)null;
                    this.SetState(TL3600.State.Ready);
                    Dictionary<string, string> retVal = new Dictionary<string, string>();
                    retVal.Add("error", "nopacket");
                    if (this.responseCallback == null)
                        return;
                    this.responseCallback(TL3600.ResponseType.Error, retVal);
                }
                else
                {
                    this.SendRequestPacket(this.retryPacket);
                    this.DbgLog("Retry Packet:" + (object)this.retryPacket.retry);
                }
            }
        }

        public void TermCheckReq(DateTime now)
        {
            this.SendRequest(new TL3600Pkt(this.TERMID, (byte)65, now));
        }

        public void CardInfoReq(DateTime now)
        {
            this.SendRequest(new TL3600Pkt(this.TERMID, (byte)68, now));
        }

        public void PayReq(int cost, int tax, bool isPrePay, DateTime now)
        {
            TL3600Pkt pkt = new TL3600Pkt(this.TERMID, (byte)66, now);
            byte[] bytes1 = Encoding.Default.GetBytes((cost + tax).ToString("0000000000"));
            byte[] bytes2 = Encoding.Default.GetBytes(tax.ToString("00000000"));
            byte[] bytes3 = Encoding.Default.GetBytes("00000000");
            pkt.data = new byte[30];
            int num1 = 0;
            byte[] data1 = pkt.data;
            int index1 = num1;
            int destinationIndex1 = index1 + 1;
            data1[index1] = (byte)49;
            Array.Copy((Array)bytes1, 0, (Array)pkt.data, destinationIndex1, bytes1.Length);
            int destinationIndex2 = destinationIndex1 + 10;
            Array.Copy((Array)bytes2, 0, (Array)pkt.data, destinationIndex2, bytes2.Length);
            int destinationIndex3 = destinationIndex2 + 8;
            Array.Copy((Array)bytes3, 0, (Array)pkt.data, destinationIndex3, bytes3.Length);
            int num2 = destinationIndex3 + 8;
            byte[] data2 = pkt.data;
            int index2 = num2;
            int num3 = index2 + 1;
            data2[index2] = (byte)48;
            byte[] data3 = pkt.data;
            int index3 = num3;
            int num4 = index3 + 1;
            data3[index3] = (byte)48;
            byte[] data4 = pkt.data;
            int index4 = num4;
            int num5 = index4 + 1;
            data4[index4] = (byte)49;
            this.isPrePayReq = isPrePay;
            pkt.dataLength = pkt.data.Length;
            this.SendRequest(pkt);
        }

        public void Config(DateTime now)
        {
            TL3600Pkt pkt = new TL3600Pkt(this.TERMID, (byte)88, now);
            byte[] bytes1 = Encoding.Default.GetBytes("7303502001");
            byte[] bytes2 = Encoding.Default.GetBytes("211.33.136.2");
            byte[] bytes3 = Encoding.Default.GetBytes("19997");
            byte[] array1 = Enumerable.Repeat<byte>((byte)0, 16).ToArray<byte>();
            Enumerable.Repeat<byte>((byte)0, 16).ToArray<byte>();
            byte[] array2 = Enumerable.Repeat<byte>((byte)0, 16).ToArray<byte>();
            byte[] array3 = Enumerable.Repeat<byte>((byte)0, 16).ToArray<byte>();
            byte[] array4 = Enumerable.Repeat<byte>((byte)0, 16).ToArray<byte>();
            byte[] bytes4 = Encoding.Default.GetBytes("");
            byte[] bytes5 = Encoding.Default.GetBytes("");
            byte[] bytes6 = Encoding.Default.GetBytes("");
            byte[] bytes7 = Encoding.Default.GetBytes("");
            byte[] bytes8 = Encoding.Default.GetBytes("0");
            byte[] array5 = Enumerable.Repeat<byte>((byte)0, 16).ToArray<byte>();
            byte[] array6 = Enumerable.Repeat<byte>((byte)0, 16).ToArray<byte>();
            byte[] bytes9 = Encoding.Default.GetBytes("1");
            byte[] bytes10 = Encoding.Default.GetBytes("192.168.1.3");
            byte[] bytes11 = Encoding.Default.GetBytes("255.255.255.0");
            byte[] bytes12 = Encoding.Default.GetBytes("192.168.1.1");
            pkt.data = new byte[246];
            int destinationIndex1 = 0;
            Array.Copy((Array)bytes1, 0, (Array)pkt.data, destinationIndex1, bytes1.Length);
            int destinationIndex2 = destinationIndex1 + 16;
            Array.Copy((Array)bytes2, 0, (Array)pkt.data, destinationIndex2, bytes2.Length);
            int destinationIndex3 = destinationIndex2 + 16;
            Array.Copy((Array)bytes3, 0, (Array)pkt.data, destinationIndex3, bytes3.Length);
            int destinationIndex4 = destinationIndex3 + 16;
            Array.Copy((Array)array1, 0, (Array)pkt.data, destinationIndex4, array1.Length);
            int destinationIndex5 = destinationIndex4 + 16;
            Array.Copy((Array)array2, 0, (Array)pkt.data, destinationIndex5, array2.Length);
            int destinationIndex6 = destinationIndex5 + 16;
            Array.Copy((Array)array2, 0, (Array)pkt.data, destinationIndex6, array2.Length);
            int destinationIndex7 = destinationIndex6 + 16;
            Array.Copy((Array)bytes2, 0, (Array)pkt.data, destinationIndex7, bytes2.Length);
            int destinationIndex8 = destinationIndex7 + 16;
            Array.Copy((Array)bytes3, 0, (Array)pkt.data, destinationIndex8, bytes3.Length);
            int destinationIndex9 = destinationIndex8 + 16;
            Array.Copy((Array)array3, 0, (Array)pkt.data, destinationIndex9, array3.Length);
            int destinationIndex10 = destinationIndex9 + 16;
            Array.Copy((Array)array4, 0, (Array)pkt.data, destinationIndex10, array4.Length);
            int destinationIndex11 = destinationIndex10 + 16;
            Array.Copy((Array)bytes4, 0, (Array)pkt.data, destinationIndex11, bytes4.Length);
            int destinationIndex12 = destinationIndex11 + 1;
            Array.Copy((Array)bytes5, 0, (Array)pkt.data, destinationIndex12, bytes5.Length);
            int destinationIndex13 = destinationIndex12 + 1;
            Array.Copy((Array)bytes6, 0, (Array)pkt.data, destinationIndex13, bytes6.Length);
            int destinationIndex14 = destinationIndex13 + 1;
            Array.Copy((Array)bytes7, 0, (Array)pkt.data, destinationIndex14, bytes7.Length);
            int destinationIndex15 = destinationIndex14 + 1;
            Array.Copy((Array)bytes8, 0, (Array)pkt.data, destinationIndex15, bytes8.Length);
            int destinationIndex16 = destinationIndex15 + 1;
            Array.Copy((Array)array5, 0, (Array)pkt.data, destinationIndex16, array5.Length);
            int destinationIndex17 = destinationIndex16 + 16;
            Array.Copy((Array)array6, 0, (Array)pkt.data, destinationIndex17, array6.Length);
            int destinationIndex18 = destinationIndex17 + 16;
            Array.Copy((Array)bytes9, 0, (Array)pkt.data, destinationIndex18, bytes9.Length);
            int destinationIndex19 = destinationIndex18 + 1;
            Array.Copy((Array)bytes10, 0, (Array)pkt.data, destinationIndex19, bytes10.Length);
            int destinationIndex20 = destinationIndex19 + 16;
            Array.Copy((Array)bytes11, 0, (Array)pkt.data, destinationIndex20, bytes11.Length);
            int destinationIndex21 = destinationIndex20 + 16;
            Array.Copy((Array)bytes12, 0, (Array)pkt.data, destinationIndex21, bytes12.Length);
            int num = destinationIndex21 + 16;
            pkt.dataLength = pkt.data.Length;
            this.SendRequest(pkt);
            this.DbgLog("설정정보요청전문[X]");
        }

        public void PayReq_G(int cost, int tax, bool isPrePay, DateTime now, string csname)
        {
            TL3600Pkt pkt = new TL3600Pkt(this.TERMID, (byte)71, now);
            byte[] bytes1 = Encoding.Default.GetBytes(cost.ToString("0000000000"));
            byte[] bytes2 = Encoding.Default.GetBytes(tax.ToString("00000000"));
            byte[] bytes3 = Encoding.Default.GetBytes("00000000");
            byte[] array1 = Enumerable.Repeat<byte>((byte)32, 30).ToArray<byte>();
            byte[] array2 = Enumerable.Repeat<byte>((byte)32, 20).ToArray<byte>();
            byte[] array3 = Enumerable.Repeat<byte>((byte)32, 40).ToArray<byte>();
            byte[] array4 = Enumerable.Repeat<byte>((byte)32, 20).ToArray<byte>();
            byte[] array5 = Enumerable.Repeat<byte>((byte)32, 50).ToArray<byte>();
            byte[] array6 = Enumerable.Repeat<byte>((byte)32, 100).ToArray<byte>();
            byte[] array7 = Enumerable.Repeat<byte>((byte)32, 50).ToArray<byte>();
            pkt.data = new byte[339];
            int num1 = 0;
            byte[] data1 = pkt.data;
            int index1 = num1;
            int destinationIndex1 = index1 + 1;
            data1[index1] = (byte)49;
            Array.Copy((Array)bytes1, 0, (Array)pkt.data, destinationIndex1, bytes1.Length);
            int destinationIndex2 = destinationIndex1 + 10;
            Array.Copy((Array)bytes2, 0, (Array)pkt.data, destinationIndex2, bytes2.Length);
            int destinationIndex3 = destinationIndex2 + 8;
            Array.Copy((Array)bytes3, 0, (Array)pkt.data, destinationIndex3, bytes3.Length);
            int num2 = destinationIndex3 + 8;
            byte[] data2 = pkt.data;
            int index2 = num2;
            int num3 = index2 + 1;
            data2[index2] = (byte)48;
            byte[] data3 = pkt.data;
            int index3 = num3;
            int destinationIndex4 = index3 + 1;
            data3[index3] = (byte)48;
            Array.Copy((Array)array1, 0, (Array)pkt.data, destinationIndex4, array1.Length);
            int destinationIndex5 = destinationIndex4 + 30;
            Array.Copy((Array)array2, 0, (Array)pkt.data, destinationIndex5, array2.Length);
            int destinationIndex6 = destinationIndex5 + 20;
            Array.Copy((Array)array3, 0, (Array)pkt.data, destinationIndex6, array3.Length);
            int destinationIndex7 = destinationIndex6 + 40;
            Array.Copy((Array)array4, 0, (Array)pkt.data, destinationIndex7, array4.Length);
            int destinationIndex8 = destinationIndex7 + 20;
            Array.Copy((Array)array5, 0, (Array)pkt.data, destinationIndex8, array5.Length);
            int destinationIndex9 = destinationIndex8 + 50;
            Array.Copy((Array)array6, 0, (Array)pkt.data, destinationIndex9, array6.Length);
            int destinationIndex10 = destinationIndex9 + 100;
            Array.Copy((Array)array7, 0, (Array)pkt.data, destinationIndex10, array7.Length);
            int num4 = destinationIndex10 + 50;
            this.isPrePayReq = isPrePay;
            pkt.dataLength = pkt.data.Length;
            this.SendRequest(pkt);
            this.DbgLog("결제요청전문[G]");
        }

        public void CancelPay_G(
          Dictionary<string, string> payInfo,
          int cost,
          int tax,
          int cType,
          DateTime now,
          string csInfo)
        {
            TL3600Pkt pkt = new TL3600Pkt(this.TERMID, (byte)67, now);
            byte[] bytes1 = Encoding.Default.GetBytes(cost.ToString("0000000000"));
            byte[] bytes2 = Encoding.Default.GetBytes(tax.ToString("00000000"));
            byte[] bytes3 = Encoding.Default.GetBytes("00000000");
            pkt.data = new byte[89];
            int num1 = 0;

            // PG부분 취소 (4:무카드 취소) (5:부분취소)
            switch (cType)
            {
                case 4:
                    pkt.data[num1++] = (byte)52;
                    break;
                case 5:
                    pkt.data[num1++] = (byte)53;
                    break;
            }
            byte[] data1 = pkt.data;
            int index1 = num1;
            int destinationIndex1 = index1 + 1;

            //“2”[RF/MS 신용승인, 카카오페 이(신용)]  must be
            data1[index1] = (byte)50;

            // 실제 충전한 금액
            Array.Copy((Array)bytes1, 0, (Array)pkt.data, destinationIndex1, bytes1.Length);
            int destinationIndex2 = destinationIndex1 + 10;
            Array.Copy((Array)bytes2, 0, (Array)pkt.data, destinationIndex2, bytes2.Length);
            int destinationIndex3 = destinationIndex2 + 8;
            Array.Copy((Array)bytes3, 0, (Array)pkt.data, destinationIndex3, bytes3.Length);
            int num2 = destinationIndex3 + 8;

            // 할부
            byte[] data2 = pkt.data;
            int index2 = num2;
            int num3 = index2 + 1;
            data2[index2] = (byte)48;
            byte[] data3 = pkt.data;
            int index3 = num3;
            int num4 = index3 + 1;
            data3[index3] = (byte)48;

            // 비서명
            byte[] data4 = pkt.data;
            int index4 = num4;
            int destinationIndex4 = index4 + 1;
            data4[index4] = (byte)49;

            //승인번호
            byte[] bytes4 = Encoding.Default.GetBytes(payInfo["authNum"]);
            Array.Copy((Array)bytes4, 0, (Array)pkt.data, destinationIndex4, bytes4.Length);
            int destinationIndex5 = destinationIndex4 + 12;

            // 일자
            byte[] bytes5 = Encoding.Default.GetBytes(payInfo["payDate"]);
            Array.Copy((Array)bytes5, 0, (Array)pkt.data, destinationIndex5, bytes5.Length);
            int destinationIndex6 = destinationIndex5 + 8;


            // 시간(4:무카드 취소) (5:부분취소)
            switch (cType)
            {
                case 4: // 무카드 취소 시 거래일련번호 마지막 6자리
                    byte[] bytes6 = Encoding.Default.GetBytes(payInfo["pgnum"].Substring(25));
                    Array.Copy((Array)bytes6, 0, (Array)pkt.data, destinationIndex6, bytes6.Length);
                    break;
                case 5: // 승인 시 매출시간[hhmmss]
                    byte[] bytes7 = Encoding.Default.GetBytes(payInfo["payTime"]);
                    Array.Copy((Array)bytes7, 0, (Array)pkt.data, destinationIndex6, bytes7.Length);
                    break;
            }
            int num5 = destinationIndex6 + 6;
            byte[] data5 = pkt.data;
            int index5 = num5;
            int num6 = index5 + 1;
            data5[index5] = (byte)51;
            byte[] data6 = pkt.data;
            int index6 = num6;
            int destinationIndex7 = index6 + 1;
            data6[index6] = (byte)48;

            //부가정보
            byte[] bytes8 = Encoding.Default.GetBytes(payInfo["pgnum"]);
            Array.Copy((Array)bytes8, 0, (Array)pkt.data, destinationIndex7, bytes8.Length);

            int num7 = destinationIndex7 + 30;

            pkt.dataLength = pkt.data.Length;
            this.SendRequest(pkt);

            this.DbgLog("취소요청전문[C]");
        }

        public void CancelPay(Dictionary<string, string> payInfo, DateTime now)
        {
            TL3600Pkt pkt = new TL3600Pkt(this.TERMID, (byte)67, now);
            byte[] bytes1 = Encoding.Default.GetBytes(payInfo["totalCost"]);
            byte[] bytes2 = Encoding.Default.GetBytes(payInfo["tax"]);
            byte[] bytes3 = Encoding.Default.GetBytes(payInfo["service"]);
            pkt.data = new byte[57];
            int num1 = 0;
            byte[] data1 = pkt.data;
            int index1 = num1;
            int num2 = index1 + 1;
            data1[index1] = (byte)49;
            byte[] data2 = pkt.data;
            int index2 = num2;
            int destinationIndex1 = index2 + 1;
            data2[index2] = (byte)49;
            Array.Copy((Array)bytes1, 0, (Array)pkt.data, destinationIndex1, bytes1.Length);
            int destinationIndex2 = destinationIndex1 + 10;
            Array.Copy((Array)bytes2, 0, (Array)pkt.data, destinationIndex2, bytes2.Length);
            int destinationIndex3 = destinationIndex2 + 8;
            Array.Copy((Array)bytes3, 0, (Array)pkt.data, destinationIndex3, bytes3.Length);
            int num3 = destinationIndex3 + 8;
            byte[] data3 = pkt.data;
            int index3 = num3;
            int num4 = index3 + 1;
            data3[index3] = (byte)48;
            byte[] data4 = pkt.data;
            int index4 = num4;
            int num5 = index4 + 1;
            data4[index4] = (byte)48;
            byte[] data5 = pkt.data;
            int index5 = num5;
            int destinationIndex4 = index5 + 1;
            data5[index5] = (byte)49;
            byte[] bytes4 = Encoding.Default.GetBytes(payInfo["authNum"]);
            Array.Copy((Array)bytes4, 0, (Array)pkt.data, destinationIndex4, bytes4.Length);
            int destinationIndex5 = destinationIndex4 + 12;
            byte[] bytes5 = Encoding.Default.GetBytes(payInfo["payDate"]);
            Array.Copy((Array)bytes5, 0, (Array)pkt.data, destinationIndex5, bytes5.Length);
            int destinationIndex6 = destinationIndex5 + 8;
            byte[] bytes6 = Encoding.Default.GetBytes(payInfo["payTime"]);
            Array.Copy((Array)bytes6, 0, (Array)pkt.data, destinationIndex6, bytes6.Length);
            int num6 = destinationIndex6 + 6;
            pkt.dataLength = pkt.data.Length;
            this.SendRequest(pkt);
        }

        public void TermReadyReq(DateTime now)
        {
            this.SendRequest(new TL3600Pkt(this.TERMID, (byte)69, now));
        }

        public void TermResetReq(DateTime now)
        {
            this.SendRequest(new TL3600Pkt(this.TERMID, (byte)82, now));
        }

        public void TermWritingReq(DateTime now)
        {
            this.SendRequest(new TL3600Pkt(this.TERMID, (byte)75, now));
        }

        public void CancelPrePay(DateTime now) => this.CancelPay(this.prePayInfo, now);

        public void CancelLastPay(DateTime now) => this.CancelPay(this.lastPayInfo, now);

        public void CancelPayG(int cost, int tax, int cType, DateTime now, string csInfo)
        {
            if (TL3600.checkcancel)
                return;
            TL3600.checkcancel = true;
            this.CancelPay_G(this.lastPayInfo, cost, tax, cType, now, csInfo);
        }

        public void Close()
        {
            this.serialPort.Close();
            this.retryTimer.Enabled = false;
            this.SetState(TL3600.State.None);
        }

        public bool PortStatus() => this.serialPort.IsOpen;

        private void DbgLog(string str)
        {
            if (this.dbgLogEventFunc == null)
                return;
            this.dbgLogEventFunc(str);
        }

        public enum State
        {
            None,
            Ready,
            WaitingAck,
        }

        public enum ResponseType
        {
            Check,
            Pay,
            CancelPay,
            Error,
            Event,
            Search,
        }

        public delegate void DbgLogEvent(string text);

        public delegate void ResponseCallback(
          TL3600.ResponseType type,
          Dictionary<string, string> retVal);
    }
}
