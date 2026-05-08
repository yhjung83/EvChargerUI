using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using EvChargerUI.Commons.Util;
using EvChargerUI.Services.DspControl.Evsis;

namespace EvChargerUI.Services.DspControl.Evsis
{
    public class DspControl
    {
        private DspModbus _modbus;
        private byte[] _channelIds = new byte[6] { 1, 2, 3, 4, 5, 6 };
        private int _maxChannelCount = 1;
        private string _comPortNum = "5";
        private RxData[] _rxDatas;
        private TxData[] _txDatas;

        private Queue<int> _readQueue;
        private Queue<TxData> _writeQueue;

        private Thread _produceThread;
        private Thread _consumerThread;

        private int _requestReadActionChannel;

        private bool _isRunning;

        private ushort _runCount;

        private FileLogger _logger = ((App)Application.Current).DspLogger;
        public volatile bool IsConnected;

        public bool IsOpen
        {
            get
            {
                return _modbus != null && _modbus.IsOpen;
            }
        }
        public DspControl(int maxChannelCount)
        {
            _maxChannelCount = maxChannelCount;
            _rxDatas = new RxData[_maxChannelCount];
            _txDatas = new TxData[_maxChannelCount];

            for (int i = 0; i < _maxChannelCount; i++)
            {
                _rxDatas[i] = new RxData();
                _txDatas[i] = new TxData();
            }

            _runCount = 0;
            _modbus = null;
            _readQueue = new Queue<int>();
            _writeQueue = new Queue<TxData>();
            _requestReadActionChannel = 0;
            IsConnected = true;
        }

        public void Open(string comPort, int baudRate)
        {
            _comPortNum = comPort;

            _modbus = new DspModbus();
            _modbus.Open("COM" + _comPortNum, baudRate);
            _readQueue.Clear();
            _writeQueue.Clear();
            _isRunning = true;
            _consumerThread = new Thread(new ThreadStart(this.ConsumerAction));
            _consumerThread.Start();
            _produceThread = new Thread(new ThreadStart(this.ReadTaskProduceAction));
            _produceThread.Start();

            timercnt_servercheck = 30;
            _pmsWatchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _pmsWatchTimer.Tick += new EventHandler(this.timerStatusTick);
            _pmsWatchTimer.Start();

        }

        public void Close()
        {
            _isRunning = false;
            _readQueue.Clear();
            _writeQueue.Clear();
            _modbus.Close();
            _modbus = null;

            _pmsWatchTimer.Stop();
            ServerDisconnect();
            
        }

        private void ReadTaskProduceAction()
        {
            while (_isRunning)
            {
                lock (_readQueue)
                {
                    if (_readQueue.Count < _maxChannelCount * 2)
                    {
                        _readQueue.Enqueue(_requestReadActionChannel);
                        _requestReadActionChannel = (_requestReadActionChannel + 1) % _maxChannelCount;
                    }
                }
                Thread.Sleep(200);
            }

        }
        private void ConsumerAction()
        {
            while (_isRunning)
            {
                TxData data = null;
                lock (_writeQueue)
                {
                    if (_writeQueue.Count > 0)
                    {
                        data = _writeQueue.Dequeue();
                    }
                }
                
                if (data != null)
                {
                    WriteRegister(data);
                }
                
                int channel = -1;
                lock (_readQueue)
                {
                    if (_readQueue.Count > 0)
                    {
                        channel = _readQueue.Dequeue();
                    }
                }
                
                if (channel >= 0)
                {
                    ReadRegister(channel);
                }
                
                // CPU 부하 완화
                Thread.Sleep(10);
            }

        }

#if false
        private void ReadRegister(int channel)
        {
            var client = _modbus;
            if (client == null) return;

            try
            {
                client.SendFc4(_channelIds[channel], 400, 40);

                Thread.Sleep(200);
                byte[] idnum = new byte[2];
                ushort[] values = new ushort[40];
                client.ResponseF4F16(ref values, ref idnum);

                if ((int)idnum[0] == (int)this._channelIds[channel] && idnum[1] == 4)
                {
                    lock (_rxDatas[channel])
                    {
                        _rxDatas[channel].Decode(values);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Evsis DspControl ReadRegister Exception: " + ex.Message);
                Console.WriteLine("Evsis Exception type: " + ex.GetType().Name);
                Console.WriteLine("Evsis Exception message: " + ex.Message);
                return;
            }

        }
#else
        private void ReadRegister(int channel)
        {
            try
            {
                if (!_modbus.SendFc4(_channelIds[channel], 400, 40))
                {
                    IsConnected = false;
                    return;
                }

                Thread.Sleep(200);
                byte[] idnum = new byte[2];
                ushort[] values = new ushort[40];
                if (!_modbus.ResponseF4F16(ref values, ref idnum))
                {
                    IsConnected = false;
                    return;
                }

                if ((int)idnum[0] == (int)this._channelIds[channel] && idnum[1] == 4)
                {
                    lock (_rxDatas[channel])
                    {
                        _rxDatas[channel].Decode(values);
                    }
                    IsConnected = true; // Success
                }
            }
            catch (DspTimeoutException)
            {
                IsConnected = false; // Timeout occurred
            }
            catch (Exception ex)
            {
                Console.WriteLine("Evsis DspControl ReadRegister Exception: " + ex.Message);
                IsConnected = false;
            }
        }
#endif
        private void WriteRegister(TxData data)
        {
            try
            {
                //data.runCnt = _runCount++;
                ushort[] rawData = data.Encode();

                if (!_modbus.SendFc16(_channelIds[data.ChannelNo], 200, 31, rawData))
                {
                    IsConnected = false;
                    return;
                }
                Thread.Sleep(200);
                byte[] idnum = new byte[2];
                ushort[] values = new ushort[31];
                if (!_modbus.ResponseF4F16(ref values, ref idnum))
                {
                    IsConnected = false;
                    return;
                }
                IsConnected = true;
            }
            catch (DspTimeoutException)
            {
                IsConnected = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Evsis DspControl WriteRegister Exception: " + ex.Message);
                IsConnected = false;
            }
        }

        public void ClearWriteBuffer()
        {
            _writeQueue.Clear();
        }
        public void RequestWriteRegister(int channel)
        {
            lock (_writeQueue)
            {
                lock (_txDatas[channel])
                {
                    if (_txDatas[channel].runCnt < ushort.MaxValue)
                    {
                        _txDatas[channel].runCnt++;
                    }
                    else
                    {
                        _txDatas[channel].runCnt = 0;
                    }

                    _txDatas[channel].outputDcVol = _voltage;
                    _txDatas[channel].outputDcCur = _current;
                    _txDatas[channel].dcpowerMeter = (float)_powerMeterInKw;
                    // AC3 전용 전력량(가정): PMS 패킷의 5번 필드
                    _txDatas[channel].acpowerMeter = (float)_powerMeterInKwAc3;
                    _txDatas[channel].commError = !_isPmsConnected;

                    // 같은 채널의 오래된 데이터를 큐에서 제거하여 최신 데이터만 유지
                    var itemsToKeep = new Queue<TxData>();
                    while (_writeQueue.Count > 0)
                    {
                        TxData item = _writeQueue.Dequeue();
                        if (item.ChannelNo != channel)
                        {
                            itemsToKeep.Enqueue(item);
                        }
                    }
                    
                    // 다른 채널의 데이터들을 다시 넣고, 현재 채널의 최신 데이터 추가
                    while (itemsToKeep.Count > 0)
                    {
                        _writeQueue.Enqueue(itemsToKeep.Dequeue());
                    }

                    TxData txData = _txDatas[channel].Clone();
                    txData.ChannelNo = channel;
                    _writeQueue.Enqueue(txData);
                }
            }
        }

        public RxData GetRxData(int channel)
        {
            lock(_rxDatas[channel])
                return _rxDatas[channel];
        }

        public TxData GetTxData(int channel)
        {
            // TxData는 여러 스레드에서 읽기 위해 사용되므로 원본 반환 (값은 Init() 후 설정)
            // 주의: 직접 수정 시 RequestWriteRegister에서 lock 사용 필요
            return _txDatas[channel];
        }

        

        #region PowerMeter Comm.

        private bool _isPmsConnected;
        public bool IsPmsConnected => _isPmsConnected;
        private bool _isPmsEndFlag;
        private readonly string pmsIp = "127.0.0.1";
        private readonly int pmsPort = 9998;
        private IPEndPoint _pmsEndPoint;
        private Thread _pmsConnectThread;
        private Thread _pmsRecieveThread;
        private Socket _pmsSocket;
        private int _pmsConnChkResult;
        private int timercnt_servercheck = 30;
        private DispatcherTimer _pmsWatchTimer;

        private double _powerMeterInKw;
        // AC3 전용(가정): PMS 패킷의 5/7/8 필드가 AC3 값
        private double _powerMeterInKwAc3;
        private float _current;
        private float _voltage;
        private float _currentAc3;
        private float _voltageAc3;

        public double PowerMeterInKw => _powerMeterInKw;
        public double PowerMeterInKwAc3 => _powerMeterInKwAc3;
        public float Current => _current;
        public float Voltage => _voltage;
        public float CurrentAc3 => _currentAc3;
        public float VoltageAc3 => _voltageAc3;

        public bool ConnectToServer()
        {
            _isPmsConnected = false;
            _isPmsEndFlag = false;

            try
            {
                _pmsEndPoint = new IPEndPoint(IPAddress.Parse(pmsIp), pmsPort);
            }
            catch
            {
                _logger.Error("[PowerMeterComm] IP나 Port가 잘못되었습니다.");
                return false;
            }

            try
            {
                _pmsConnectThread = new Thread(new ThreadStart(this.ClientConnectThread));
                _pmsConnectThread.IsBackground = true;
                _pmsConnectThread.Start();
            }
            catch
            {
            }
            return true;
        }

        private void ClientConnectThread()
        {
            _logger.Info("[PowerMeterComm] 연결 쓰레드 시작");
            try
            {
                _pmsSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                _pmsSocket.Connect(_pmsEndPoint);
                Thread.Sleep(100);
                _pmsConnChkResult = this.ServerConnectRcheck();
            }
            catch
            {
                _pmsConnChkResult = 2;
            }
            this._pmsConnectThread.Abort();
        }

        private int ServerConnectRcheck()
        {
            if (!this._pmsSocket.Connected)
                return 0;
            _isPmsConnected = true;
            _pmsRecieveThread = new Thread(new ThreadStart(this.RecieveFromServer));
            _pmsRecieveThread.IsBackground = true;
            _pmsRecieveThread.Start();
            return 1;
        }

        private void RecieveFromServer()
        {
            while (true)
            {
                try
                {
                    byte[] numArray = new byte[9100];
                    ASCIIEncoding asciiEncoding = new ASCIIEncoding();
                    _pmsSocket.Receive(numArray);
                    string str1 = Encoding.UTF8.GetString(numArray, 0, numArray.Length);
                    _logger.Info("[PowerMeterComm] << RECV: " + str1);
                    
                    string[] strArray = str1.Split('|');

                    _powerMeterInKw = double.Parse(strArray[1]);
                    _voltage = float.Parse(strArray[3]);
                    _current = float.Parse(strArray[4]);

                    if (strArray.Length > 8)
                    {
                        double ac3Power;
                        float ac3Voltage;
                        float ac3Current;

                        if (double.TryParse(strArray[5], out ac3Power))
                            _powerMeterInKwAc3 = ac3Power;
                        if (float.TryParse(strArray[7], out ac3Voltage))
                            _voltageAc3 = ac3Voltage;
                        if (float.TryParse(strArray[8], out ac3Current))
                            _currentAc3 = ac3Current;
                    }
                    
                }
                catch (Exception)
                {
                    _isPmsConnected = false;
                    _pmsSocket.Close();
                    timercnt_servercheck = 30;
                    break;
                }
            }
        }

        private void ServerDisconnect()
        {
            _isPmsConnected = false;
            if (_pmsRecieveThread != null)
            {
                _pmsRecieveThread.Abort();
            }
 
            if (_pmsSocket != null)
            {
                if (_pmsSocket.Connected)
                {
                    try
                    {
                        _pmsSocket.Shutdown(SocketShutdown.Both);
                    }
                    catch
                    {
                    }
                }
                _pmsSocket.Close();
            }
        }
        private void timerStatusTick(object sender, EventArgs e)
        {
            if (this.timercnt_servercheck > 30)
            {
                this.timercnt_servercheck = 0;
                if (this._pmsRecieveThread == null)
                    this.ConnectToServer();
                else if (!this._pmsSocket.Connected)
                {
                    ServerDisconnect();
                    this.ConnectToServer();
                }
            }
            ++this.timercnt_servercheck;
        }
        #endregion

    }
}