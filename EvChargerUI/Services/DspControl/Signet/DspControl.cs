using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EvChargerUI.Services.DspControl.Signet;

namespace EvChargerUI.Services.DspControl.Signet
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

        private ushort _RunCount = 0;

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
        }

        public void Close()
        {
            _isRunning = false;
            _readQueue.Clear();
            _writeQueue.Clear();
            _modbus.Close();
            _modbus = null;
            
        }

        private void ReadTaskProduceAction()
        {
            while (_isRunning)
            {
                if (_readQueue.Count < _maxChannelCount * 2)
                {
                    _readQueue.Enqueue(_requestReadActionChannel);
                    _requestReadActionChannel = (_requestReadActionChannel + 1) % _maxChannelCount;
                }
                Thread.Sleep(200);
            }

        }
        private void ConsumerAction()
        {
            while (_isRunning)
            {
                if (_writeQueue.Count > 0)
                {
                    TxData data = _writeQueue.Dequeue();
                    WriteRegister(data);
                }
                if (_readQueue.Count > 0)
                {
                    int channel = _readQueue.Dequeue();
                    ReadRegister(channel);
                }
            }

        }

#if true
        private void ReadRegister(int channel)
        {
            var client = _modbus;
            if (client == null) return;

            try
            {
                if (!client.SendFc4(_channelIds[channel], 400, 41))
                {
                    IsConnected = false;
                    return;
                }

                Thread.Sleep(200);
                byte[] idnum = new byte[2];
                ushort[] values = new ushort[41];
                if (!client.ResponseF4F16(ref values, ref idnum))
                {
                    IsConnected = false;
                    return;
                }

                if ((int)idnum[0] == (int)this._channelIds[channel] && idnum[1] == 4)
                {
                    lock (_rxDatas[channel])
                    {
                        _rxDatas[channel].LoadFromRawData(values);
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
                Console.WriteLine("Signet DspControl ReadRegister Exception: " + ex.Message);
                Console.WriteLine("Signet Exception type: " + ex.GetType().Name);
                Console.WriteLine("Signet Exception message: " + ex.Message);
                IsConnected = false; // Other exception
                return;
            }
        }
#else
        private void ReadRegister(int channel)
        {
            _modbus.SendFc4(_channelIds[channel],400, 41 );

            Thread.Sleep(200);
            byte[] idnum = new byte[2];
            ushort[] values = new ushort[41];
            _modbus.ResponseF4F16(ref values, ref idnum);

            if ((int)idnum[0] == (int)this._channelIds[channel] && idnum[1] == 4)
            {
                lock (_rxDatas[channel])
                {
                    _rxDatas[channel].LoadFromRawData(values);
                }
            }
        }
#endif
        private void WriteRegister(TxData data)
        {
            try
            {
                data.RunCount = _RunCount++;
                ushort[] rawData = data.ToRawData();

                if (!_modbus.SendFc16(_channelIds[data.ChannelNo], 200, 22, rawData))
                {
                    IsConnected = false;
                    return;
                }
                Thread.Sleep(200);
                byte[] idnum = new byte[2];
                ushort[] values = new ushort[10];
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
                Console.WriteLine("Signet DspControl WriteRegister Exception: " + ex.Message);
                IsConnected = false;
            }
        }

        public void ClearWriteBuffer()
        {
            _writeQueue.Clear();
        }
        public void RequestWriteRegister(int channel)
        {
            lock (_txDatas[channel])
            {
                TxData txData = _txDatas[channel].Clone();
                txData.ChannelNo = channel;
                _writeQueue.Enqueue(txData);

            }
        }

        public RxData GetRxData(int channel)
        {
            lock(_rxDatas[channel])
                return _rxDatas[channel];
        }

        public TxData GetTxData(int channel)
        {
            return _txDatas[channel];
        }



    }
}
