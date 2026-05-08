using System;
using System.IO.Ports;
using System.Text;
using System.Windows;
using EvChargerUI.Commons.Util;

namespace EvChargerUI.Services.DspControl.Chaevi
{
    public class DspModbus
    {
        private SerialPort _sp = new SerialPort();
        private byte[] _response = new byte[1024];
        private byte[] _responseHex = new byte[512];
        private FileLogger _logger = ((App)Application.Current).DspLogger;

        private static ushort[] wCRCTable = new ushort[256] 
        {
          (ushort) 0,
          (ushort) 4129,
          (ushort) 8258,
          (ushort) 12387,
          (ushort) 16516,
          (ushort) 20645,
          (ushort) 24774,
          (ushort) 28903,
          (ushort) 33032,
          (ushort) 37161,
          (ushort) 41290,
          (ushort) 45419,
          (ushort) 49548,
          (ushort) 53677,
          (ushort) 57806,
          (ushort) 61935,
          (ushort) 4657,
          (ushort) 528,
          (ushort) 12915,
          (ushort) 8786,
          (ushort) 21173,
          (ushort) 17044,
          (ushort) 29431,
          (ushort) 25302,
          (ushort) 37689,
          (ushort) 33560,
          (ushort) 45947,
          (ushort) 41818,
          (ushort) 54205,
          (ushort) 50076,
          (ushort) 62463,
          (ushort) 58334,
          (ushort) 9314,
          (ushort) 13379,
          (ushort) 1056,
          (ushort) 5121,
          (ushort) 25830,
          (ushort) 29895,
          (ushort) 17572,
          (ushort) 21637,
          (ushort) 42346,
          (ushort) 46411,
          (ushort) 34088,
          (ushort) 38153,
          (ushort) 58862,
          (ushort) 62927,
          (ushort) 50604,
          (ushort) 54669,
          (ushort) 13907,
          (ushort) 9842,
          (ushort) 5649,
          (ushort) 1584,
          (ushort) 30423,
          (ushort) 26358,
          (ushort) 22165,
          (ushort) 18100,
          (ushort) 46939,
          (ushort) 42874,
          (ushort) 38681,
          (ushort) 34616,
          (ushort) 63455,
          (ushort) 59390,
          (ushort) 55197,
          (ushort) 51132,
          (ushort) 18628,
          (ushort) 22757,
          (ushort) 26758,
          (ushort) 30887,
          (ushort) 2112,
          (ushort) 6241,
          (ushort) 10242,
          (ushort) 14371,
          (ushort) 51660,
          (ushort) 55789,
          (ushort) 59790,
          (ushort) 63919,
          (ushort) 35144,
          (ushort) 39273,
          (ushort) 43274,
          (ushort) 47403,
          (ushort) 23285,
          (ushort) 19156,
          (ushort) 31415,
          (ushort) 27286,
          (ushort) 6769,
          (ushort) 2640,
          (ushort) 14899,
          (ushort) 10770,
          (ushort) 56317,
          (ushort) 52188,
          (ushort) 64447,
          (ushort) 60318,
          (ushort) 39801,
          (ushort) 35672,
          (ushort) 47931,
          (ushort) 43802,
          (ushort) 27814,
          (ushort) 31879,
          (ushort) 19684,
          (ushort) 23749,
          (ushort) 11298,
          (ushort) 15363,
          (ushort) 3168,
          (ushort) 7233,
          (ushort) 60846,
          (ushort) 64911,
          (ushort) 52716,
          (ushort) 56781,
          (ushort) 44330,
          (ushort) 48395,
          (ushort) 36200,
          (ushort) 40265,
          (ushort) 32407,
          (ushort) 28342,
          (ushort) 24277,
          (ushort) 20212,
          (ushort) 15891,
          (ushort) 11826,
          (ushort) 7761,
          (ushort) 3696,
          (ushort) 65439,
          (ushort) 61374,
          (ushort) 57309,
          (ushort) 53244,
          (ushort) 48923,
          (ushort) 44858,
          (ushort) 40793,
          (ushort) 36728,
          (ushort) 37256,
          (ushort) 33193,
          (ushort) 45514,
          (ushort) 41451,
          (ushort) 53516,
          (ushort) 49453,
          (ushort) 61774,
          (ushort) 57711,
          (ushort) 4224,
          (ushort) 161,
          (ushort) 12482,
          (ushort) 8419,
          (ushort) 20484,
          (ushort) 16421,
          (ushort) 28742,
          (ushort) 24679,
          (ushort) 33721,
          (ushort) 37784,
          (ushort) 41979,
          (ushort) 46042,
          (ushort) 49981,
          (ushort) 54044,
          (ushort) 58239,
          (ushort) 62302,
          (ushort) 689,
          (ushort) 4752,
          (ushort) 8947,
          (ushort) 13010,
          (ushort) 16949,
          (ushort) 21012,
          (ushort) 25207,
          (ushort) 29270,
          (ushort) 46570,
          (ushort) 42443,
          (ushort) 38312,
          (ushort) 34185,
          (ushort) 62830,
          (ushort) 58703,
          (ushort) 54572,
          (ushort) 50445,
          (ushort) 13538,
          (ushort) 9411,
          (ushort) 5280,
          (ushort) 1153,
          (ushort) 29798,
          (ushort) 25671,
          (ushort) 21540,
          (ushort) 17413,
          (ushort) 42971,
          (ushort) 47098,
          (ushort) 34713,
          (ushort) 38840,
          (ushort) 59231,
          (ushort) 63358,
          (ushort) 50973,
          (ushort) 55100,
          (ushort) 9939,
          (ushort) 14066,
          (ushort) 1681,
          (ushort) 5808,
          (ushort) 26199,
          (ushort) 30326,
          (ushort) 17941,
          (ushort) 22068,
          (ushort) 55628,
          (ushort) 51565,
          (ushort) 63758,
          (ushort) 59695,
          (ushort) 39368,
          (ushort) 35305,
          (ushort) 47498,
          (ushort) 43435,
          (ushort) 22596,
          (ushort) 18533,
          (ushort) 30726,
          (ushort) 26663,
          (ushort) 6336,
          (ushort) 2273,
          (ushort) 14466,
          (ushort) 10403,
          (ushort) 52093,
          (ushort) 56156,
          (ushort) 60223,
          (ushort) 64286,
          (ushort) 35833,
          (ushort) 39896,
          (ushort) 43963,
          (ushort) 48026,
          (ushort) 19061,
          (ushort) 23124,
          (ushort) 27191,
          (ushort) 31254,
          (ushort) 2801,
          (ushort) 6864,
          (ushort) 10931,
          (ushort) 14994,
          (ushort) 64814,
          (ushort) 60687,
          (ushort) 56684,
          (ushort) 52557,
          (ushort) 48554,
          (ushort) 44427,
          (ushort) 40424,
          (ushort) 36297,
          (ushort) 31782,
          (ushort) 27655,
          (ushort) 23652,
          (ushort) 19525,
          (ushort) 15522,
          (ushort) 11395,
          (ushort) 7392,
          (ushort) 3265,
          (ushort) 61215,
          (ushort) 65342,
          (ushort) 53085,
          (ushort) 57212,
          (ushort) 44955,
          (ushort) 49082,
          (ushort) 36825,
          (ushort) 40952,
          (ushort) 28183,
          (ushort) 32310,
          (ushort) 20053,
          (ushort) 24180,
          (ushort) 11923,
          (ushort) 16050,
          (ushort) 3793,
          (ushort) 7920
        };

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
            _logger.Error(_sp.PortName + " is not open");
            return false;
        }


        public bool SendFc16(byte address, ushort start, ushort registers, ushort[] values)
        {
            if (_sp.IsOpen)
            {
                _sp.DiscardOutBuffer();
                _sp.DiscardInBuffer();
                int size = 8 + 2 * (int)registers + 2;
                byte[] src_hex = new byte[size];
                byte[] messageArray = new byte[size * 2];
                byte[] numArray1 = new byte[size * 2 + 2];
                src_hex[0] = address;
                src_hex[1] = (byte)16;
                src_hex[2] = (byte)((uint)start >> 8);
                src_hex[3] = (byte)start;
                src_hex[4] = (byte)((uint)registers >> 8);
                src_hex[5] = (byte)registers;
                src_hex[6] = (byte)0;
                src_hex[7] = (byte)((uint)registers * 4U);
                for (int index = 0; index < (int)registers; ++index)
                {
                    src_hex[8 + 2 * index] = (byte)((uint)values[index] >> 8);
                    src_hex[9 + 2 * index] = (byte)values[index];
                }
                Hex_2_Ascii_Conversion(src_hex, messageArray, size);
                byte[] crc = this.calculateCRC(ref messageArray, size * 2 - 4);
                byte[] numArray2 = new byte[4];
                byte[] dest_asc = numArray2;
                Hex_2_Ascii_Conversion(crc, dest_asc, 2);
                numArray1[0] = (byte)2;
                Buffer.BlockCopy((Array)messageArray, 0, (Array)numArray1, 1, size * 2 - 4);
                numArray1[size * 2 - 4 + 1] = numArray2[0];
                numArray1[size * 2 - 4 + 2] = numArray2[1];
                numArray1[size * 2 - 4 + 3] = numArray2[2];
                numArray1[size * 2 - 4 + 4] = numArray2[3];
                numArray1[size * 2 - 4 + 5] = (byte)13;
                return Send(numArray1);
            }
            _logger.Error("Serial port not open");
            return false;
        }

        public bool SendFc3(byte address, ushort start, ushort registers)
        {
            if (_sp.IsOpen)
            {
                byte[] src_hex = new byte[8];
                byte[] messageArray = new byte[16];
                byte[] numArray1 = new byte[18];
                src_hex[0] = address;
                src_hex[1] = (byte)3;
                src_hex[2] = (byte)((uint)start >> 8);
                src_hex[3] = (byte)start;
                src_hex[4] = (byte)((uint)registers >> 8);
                src_hex[5] = (byte)registers;
                Hex_2_Ascii_Conversion(src_hex, messageArray, 8);
                byte[] crc = this.calculateCRC(ref messageArray, 12);
                byte[] numArray2 = new byte[4];
                byte[] dest_asc = numArray2;
                Hex_2_Ascii_Conversion(crc, dest_asc, 2);
                numArray1[0] = (byte)2;
                Buffer.BlockCopy((Array)messageArray, 0, (Array)numArray1, 1, 12);
                numArray1[13] = numArray2[0];
                numArray1[14] = numArray2[1];
                numArray1[15] = numArray2[2];
                numArray1[16] = numArray2[3];
                numArray1[17] = (byte)13;
                return Send(numArray1);
            }
            _logger.Error("Serial port not open");
            return false;
        }

        private bool Send(byte[] message)
        {
            try
            {
                _logger.Debug("[SEND] " + Encoding.ASCII.GetString(message, 0,message.Length) );
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
                int responseSize = _sp.BytesToRead - 2;

                _sp.ReadByte();
                for (int i = 0; i < responseSize; ++i)
                {
                    _response[i] = (byte)_sp.ReadByte();
                }
                _sp.ReadByte();

                _logger.Debug("[RECV] " + Encoding.ASCII.GetString(_response, 0, responseSize));

                if (_response[2] == (byte)48 && _response[3] == (byte)51)
                {
                    if (this.checkCRC(ref _response, responseSize) || true)
                    {
                        Ascii_2_Hex_Conversion(_response, _responseHex, responseSize);
                    }
                    else
                    {
                        _logger.Error("Command03 CRC error");
                        return false;
                    }

                }
                else if (_response[2] == (byte)49 && _response[3] == (byte)48)
                {
                    if (this.checkCRC(ref _response, responseSize) || true)
                    {
                        Ascii_2_Hex_Conversion(_response, _responseHex, responseSize);
                    }
                    else
                    {
                        _logger.Error("Command16 CRC error");
                        return false;
                    }
                }
                if (_responseHex[1] == (byte)3)
                {
                    idnum[0] = _responseHex[0];
                    idnum[1] = _responseHex[1];
                    int num = (int)_responseHex[3] / 4;
                    for (int index = 0; index < num; ++index)
                    {
                        values[index] = (ushort)_responseHex[4 + 2 * index];
                        values[index] <<= 8;
                        values[index] += (ushort)_responseHex[5 + 2 * index];
                    }
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


        private byte[] calculateCRC(ref byte[] messageArray, int dataLength)
        {
            ushort num = ushort.MaxValue;
            for (int index = 0; index < dataLength; ++index)
                num = (ushort)((uint)num << 8 ^ (uint)wCRCTable[(int)num >> 8 ^ (int)byte.MaxValue & (int)messageArray[index]]);
            return new byte[2]
            {
                (byte) ((uint) num >> 8),
                (byte) ((uint) num & (uint) byte.MaxValue)
            };
        }

        private bool checkCRC(ref byte[] messageToCheck, int numberOfBytes)
        {
            byte[] dest_asc = new byte[4];
            Hex_2_Ascii_Conversion(this.calculateCRC(ref messageToCheck, numberOfBytes), dest_asc, 2);
            return (int)dest_asc[0] == (int)messageToCheck[numberOfBytes] && (int)dest_asc[1] == (int)messageToCheck[numberOfBytes + 1];
        }
        private static void Hex_2_Ascii_Conversion(byte[] src_hex, byte[] dest_asc, int size)
        {
            int num1 = 0;
            for (int index1 = 0; index1 < size; ++index1)
            {
                int num2;
                if (((int)src_hex[index1] >> 4 & 15) > 9)
                {
                    byte[] numArray = dest_asc;
                    int index2 = num1;
                    num2 = index2 + 1;
                    int num3 = (int)(byte)(((int)src_hex[index1] >> 4 & 15) + 55);
                    numArray[index2] = (byte)num3;
                }
                else
                {
                    byte[] numArray = dest_asc;
                    int index3 = num1;
                    num2 = index3 + 1;
                    int num4 = (int)(byte)(((int)src_hex[index1] >> 4 & 15) + 48);
                    numArray[index3] = (byte)num4;
                }
                if (((int)src_hex[index1] & 15) > 9)
                {
                    byte[] numArray = dest_asc;
                    int index4 = num2;
                    num1 = index4 + 1;
                    int num5 = (int)(byte)(((int)src_hex[index1] & 15) + 55);
                    numArray[index4] = (byte)num5;
                }
                else
                {
                    byte[] numArray = dest_asc;
                    int index5 = num2;
                    num1 = index5 + 1;
                    int num6 = (int)(byte)(((int)src_hex[index1] & 15) + 48);
                    numArray[index5] = (byte)num6;
                }
            }
        }

        private static void Ascii_2_Hex_Conversion(byte[] src_asc, byte[] dest_hex, int size)
        {
            int num1 = 0;
            byte num2 = 0;
            for (int index = 0; index < size; ++index)
            {
                byte num3 = src_asc[index] <= (byte)57 ? (byte)((uint)src_asc[index] - 48U) : (byte)((uint)src_asc[index] - 55U);
                if (index % 2 == 0)
                    num2 = num3;
                else
                    dest_hex[num1++] = (byte)(((uint)num2 << 4) + (uint)num3);
            }
        }
    }
}