using EssAppComponentsCommon.Common.Interfaces;
using Newtonsoft.Json;
using System.IO.Ports;

namespace EssDeviceComponents.Classes
{
    public class EssSerialPortConfig : IEssConfig
    {
        private readonly int MIN_TIMEOUT = 250;
        private readonly int MAX_TIMEOUT = 10000;
        private readonly int[] VALID_BAUD_RATES = [110, 300, 600, 1200, 2400, 4800, 9600, 14400, 19200, 38400, 57600, 115200, 128000, 256000];
        private readonly int[] VALID_DATA_BITS = [5, 6, 7, 8];

        private int _readTimeout = 1000;
        private int _baudRate = 9600;
        private int _dataBits = 8;

        public string Name { get; set; } = null!;
        public int PortNumber { get; set; }
        public int BaudRate
        {
            get => ValidBaudRate(_baudRate);
            set => _baudRate = ValidBaudRate(value);
        }
        public Parity Parity { get; set; }
        public StopBits StopBits { get; set; }
        public int DataBits
        {
            get => ValidDataBits(_dataBits);
            set => _dataBits = ValidDataBits(value);
        }
        public Handshake Handshake { get; set; }
        public bool RtsEnable { get; set; }
        public bool DtrEnable { get; set; }

        public int ReadTimeout
        {
            get => ValidInterval(_readTimeout);
            init => _readTimeout = ValidInterval(value);
        }

        [JsonIgnore]
        public string PortName => $"COM{PortNumber}";

        public bool IsValid(out string error)
        {
            error = string.Empty;

            if (PortNumber < 1 || PortNumber > 99)
            {
                error = "Invalid Port Number";
            }

            return string.IsNullOrWhiteSpace(error);

        }

        //If the value is less than 10, it is in seconds, otherwise it is in milliseconds
        private static int ToMilliSeconds(int value) => value < 10 ? value * 1000 : value;

        // The interval must be between MIN_INTERVAL and MAX_INTERVAL milliseconds
        private int ValidInterval(int value) => Math.Clamp(ToMilliSeconds(value), MIN_TIMEOUT, MAX_TIMEOUT);

        private int ValidBaudRate(int value) => VALID_BAUD_RATES.Contains((short)value) ? (short)value : 9600;

        private int ValidDataBits(int value) => VALID_DATA_BITS.Contains((short)value) ? (short)value : 8;

    }

}
