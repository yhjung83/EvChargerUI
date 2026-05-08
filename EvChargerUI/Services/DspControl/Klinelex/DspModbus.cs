using System;
using System.IO.Ports;
using System.Windows;
using EvChargerUI.Commons.Util;

namespace EvChargerUI.Services.DspControl.Klinelex
{
    public class DspModbus
    {
        private SerialPort _sp = new SerialPort();
        private byte[] _response = new byte[1024];
        private int _responeSize = 8;
        private FileLogger _logger = ((App)Application.Current).DspLogger;

        public bool IsOpen
        {
            get
            {
                return _sp != null && _sp.IsOpen;
            }
        }
        public bool Open(string portName, int baudRate)
        {
            if (!_sp.IsOpen)
            {
                _sp.PortName = portName;
                _sp.BaudRate = baudRate;
                _sp.DataBits = 8;
                _sp.Parity = Parity.None;
                _sp.StopBits = StopBits.One;
                _sp.ReadTimeout = 500;
                _sp.WriteTimeout = 500;

                try
                {
                    _sp.Open();
                }
                catch (Exception ex)
                {
                    _logger.Error("Error opening " + portName + ": " + ex.Message);
                    return false;
                }

                _logger.Info(portName + " opened successfully");
                return true;
            }
            _logger.Info(portName + " already opened");
 
            return false;
        }

        public bool Close()
        {
            if (_sp.IsOpen)
            {
                try
                {
                    _sp.Close();
         
                    _logger.Info(_sp.PortName + " closed successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.Error("Error closing " + _sp.PortName + ": " + ex.Message);
                    return false;
                }
            }
            _logger.Info(_sp.PortName + " is not open");

            return false;
        }

        private void GetCRC(byte[] message, int buffsize, ref byte[] CRC)
        {
            ushort num1 = ushort.MaxValue;
            byte num2 = byte.MaxValue;
            byte num3 = byte.MaxValue;
            for (int index1 = 0; index1 < buffsize - 2; ++index1)
            {
                num1 ^= (ushort)message[index1];
                for (int index2 = 0; index2 < 8; ++index2)
                {
                    char ch = (char)((uint)num1 & 1U);
                    num1 = (ushort)((int)num1 >> 1 & (int)short.MaxValue);
                    if (ch == '\u0001')
                        num1 ^= (ushort)40961;
                }
            }
            CRC[1] = num2 = (byte)((int)num1 >> 8 & (int)byte.MaxValue);
            CRC[0] = num3 = (byte)((uint)num1 & (uint)byte.MaxValue);
        }

        private void BuildMessage(byte address, byte type, ushort start, ushort registers, ref byte[] message)
        {
            byte[] CRC = new byte[2];
            message[0] = address;
            message[1] = type;
            message[2] = (byte)(start >> 8);
            message[3] = (byte)(start & 0xFF);
            message[4] = (byte)(registers >> 8);
            message[5] = (byte)(registers & 0xFF);
            GetCRC(message, message.Length, ref CRC);
            message[message.Length - 2] = CRC[0];
            message[message.Length - 1] = CRC[1];
        }

        public bool SendFc16(byte address, ushort start, ushort registers, ushort[] values)
        {
            if (_sp.IsOpen)
            {
                _sp.DiscardOutBuffer();
                _sp.DiscardInBuffer();
                byte[] message = new byte[9 + 2 * registers];
                _responeSize = 8;
                message[6] = (byte)(registers * 2);
                for (int i = 0; i < registers; ++i)
                {
                    message[7 + 2 * i] = (byte)(values[i] >> 8);
                    message[8 + 2 * i] = (byte)(values[i] & 0xFF);
                }
                BuildMessage(address, 16, start, registers, ref message);
                return Send(message);
            }
            _logger.Error("Serial port not open");
            return false;
        }

        public bool SendFc4(byte address, ushort start, ushort registers)
        {
            if (_sp.IsOpen)
            {
                _sp.DiscardOutBuffer();
                _sp.DiscardInBuffer();
                byte[] message = new byte[8];
                _responeSize = 5 + 2 * registers;
                BuildMessage(address, 4, start, registers, ref message);
                return Send(message);
            }
            _logger.Error("Serial port not open");
            return false;
        }

        private bool Send(byte[] message)
        {
            try
            {
                _logger.Debug("[SEND] " + BitConverter.ToString(message));
                _sp.Write(message, 0, message.Length);
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error("Transmission error: " + ex.Message);
                return false;
            }
        }

        public bool ResponseF4F16(ref ushort[] values, ref byte[] idnum)
        {
            if (!_sp.IsOpen)
            {
                _logger.Error("Serial port not open");
                return false;
            }

            try
            {
                for (int i = 0; i < _responeSize; ++i)
                {
                    _response[i] = (byte)_sp.ReadByte();
                }

                _logger.Debug("[RECV] " + BitConverter.ToString(_response, 0, _responeSize));

                if (!ValidateCRC(_response, _responeSize) && false )
                {
                    _logger.Error("CRC error");
                    return false;
                }

                idnum[0] = _response[0];
                idnum[1] = _response[1];

                for (int i = 0; i < (_responeSize - 5) / 2; ++i)
                {
                    values[i] = (ushort)(_response[3 + 2 * i] << 8 | _response[4 + 2 * i]);
                }

                return true;
            }
            //catch (System.TimeoutException tex)
            //{
            //    _logger.Error("Read timeout: " + tex.Message);
            //    return false;
            //}
            catch (System.TimeoutException tex)
            {
                // Throw our custom exception to be caught by the caller
                _logger.Error("Read timeout: " + tex.Message);
                throw new DspTimeoutException("DSP read operation timed out.", tex);
            }
            catch (Exception ex)
            {
                _logger.Error("Read error: " + ex.Message);
                return false;
            }
        }

        private bool ValidateCRC(byte[] buffer, int length)
        {
            byte[] calcCRC = new byte[2];
            GetCRC(buffer, length, ref calcCRC);
            return (buffer[length - 2] == calcCRC[0] && buffer[length - 1] == calcCRC[1]);
        }
    }
}