using System;
using System.IO.Ports;
using System.Windows;
using EvChargerUI.Commons.Util;

namespace EvChargerUI.Services.DspControl.Evsis
{

    public class DspModbus
    {
        public SerialPort spd = new SerialPort();
        private byte[] response = new byte[1024];
        private int responesize = 8;
        private FileLogger _logger = ((App)Application.Current).DspLogger;

        public bool IsOpen
        {
            get
            {
                return spd != null && spd.IsOpen;
            }
        }
        public bool Open(string portName, int baudRate)
        {
            if (!spd.IsOpen)
            {
                spd.PortName = portName;
                spd.BaudRate = baudRate;
                spd.DataBits = 8;
                spd.Parity = Parity.None;
                spd.StopBits = StopBits.One;
                spd.ReadTimeout = 500;
                spd.WriteTimeout = 500;

                try
                {
                    spd.Open();
                }
                catch (Exception ex)
                {
                    _logger.Error("Error opening " + portName + ": " + ex.Message);
                    return false;
                }
                _logger.Info(portName + " opened successfully");
                return true;
            }
            _logger.Error(portName + " already opened");
            return false;
        }

        public bool Close()
        {
            if (spd.IsOpen)
            {
                try
                {
                    spd.Close();
                    _logger.Info(spd.PortName + " closed successfully");
                    return true;
                }
                catch (Exception ex)
                {
                    _logger.Error("Error closing " + spd.PortName + ": " + ex.Message);
                    return false;
                }
            }
            _logger.Error(spd.PortName + " is not open");
            return false;
        }

        private void GetCRC(byte[] message, int buffsize, ref byte[] CRC)
        {
            ushort num1 = 0xFFFF;
            for (int i = 0; i < buffsize - 2; ++i)
            {
                num1 ^= message[i];
                for (int j = 0; j < 8; ++j)
                {
                    bool lsb = (num1 & 0x0001) != 0;
                    num1 >>= 1;
                    if (lsb)
                        num1 ^= 0xA001;
                }
            }
            CRC[0] = (byte)(num1 & 0xFF);
            CRC[1] = (byte)(num1 >> 8);
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
            if (spd.IsOpen)
            {
                spd.DiscardOutBuffer();
                spd.DiscardInBuffer();
                byte[] message = new byte[9 + 2 * registers];
                responesize = 8;
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
            if (spd.IsOpen)
            {
                spd.DiscardOutBuffer();
                spd.DiscardInBuffer();
                byte[] message = new byte[8];
                responesize = 5 + 2 * registers;
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
                spd.Write(message, 0, message.Length);
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
            if (!spd.IsOpen)
            {
                _logger.Error("Serial port not open");
                return false;
            }

            try
            {
                for (int i = 0; i < responesize; ++i)
                {
                    response[i] = (byte)spd.ReadByte();
                }

                _logger.Debug("[RECV] " + BitConverter.ToString(response, 0, responesize));

                byte[] CRC = new byte[2];
                GetCRC(response, responesize, ref CRC);
                if (response[responesize - 2] != CRC[0] || response[responesize - 1] != CRC[1])
                {
                    _logger.Error("CRC error");
                    return false;
                }

                idnum[0] = response[0];
                idnum[1] = response[1];

                for (int i = 0; i < (responesize - 5) / 2; ++i)
                {
                    values[i] = (ushort)(response[3 + 2 * i] << 8 | response[4 + 2 * i]);
                }

                _logger.Info("Read successful");
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