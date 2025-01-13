using EssAppComponentsCommon.Common.Classes;
using EssAppComponentsCommon.Logging.Interfaces;
using EssDeviceComponents.Classes;
using EssDeviceComponents.Interfaces;
using System.IO.Ports;

namespace EssDeviceComponents.DeviceInterfaces
{
    public class EssManagedSerialPort(EssSerialPortConfig config, IEssAppLogger? logger, string? endOfLine = "\r\n")
        : EssManagedDeviceInterface(logger, endOfLine), IDeviceInterface
    {
        protected readonly EssSerialPortConfig _config = config;
        private SerialPort _serialPort = null!;

        public event EventHandler<string>? DataReceived;
        public event EventHandler<string>? ErrorReceived;

        public bool IsConnected
        {
            get
            {
                lock (LockObject)
                {
                    return _serialPort?.IsOpen == true;
                }
            }
        }

        public static List<string> GetAvailablePorts()
        {
            return new List<string>(SerialPort.GetPortNames());
        }

        public static bool IsPortAvailable(string portName)
        {
            List<string> availablePorts = GetAvailablePorts();

            foreach (string port in availablePorts)
            {
                if (port.Equals(portName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        public Task ConnectAsync()
        {
            CancelTokenSource = new CancellationTokenSource();
            return Task.Run(() => MonitorConnection(CancelTokenSource.Token));
        }

        public Task DisconnectAsync()
        {
            lock (LockObject)
            {
                CancelTokenSource?.Cancel();
                DisposeSerialPort();
            }

            return Task.CompletedTask;
        }

        private void DisposeSerialPort()
        {
            if (_serialPort == null) return;

            try
            {
                _serialPort.DataReceived -= SerialPort_DataReceived;
                _serialPort.ErrorReceived -= SerialPort_ErrorReceived;
                _serialPort.Dispose();
                _serialPort = null!;
            }
            catch
            {
                // ignore
            }

        }

        private async void MonitorConnection(CancellationToken cancellationToken)
        {
            while (cancellationToken.IsCancellationRequested == false)
            {
                // Wait 1 second before checking again whether the connection is open
                int delayMilliseconds = 1000;

                if (IsConnected == false && TryReconnect() == false)
                {
                    // Wait 5 seconds before trying to establish the connection again
                    delayMilliseconds = 5000;
                }

                await EssTask.DelayAsync(delayMilliseconds, cancellationToken);

            }
        }

        private bool TryReconnect()
        {
            lock (LockObject)
            {
                DisposeSerialPort();

                if (IsPortAvailable(_config.PortName) == false)
                {
                    Logger?.Error($"Port '{_config.PortName}' does not exist.");
                    return false;
                }

                try
                {
                    _serialPort = new()
                    {
                        PortName = _config.PortName,
                        BaudRate = _config.BaudRate,
                        Parity = _config.Parity,
                        StopBits = _config.StopBits,
                        DataBits = _config.DataBits,
                        Handshake = _config.Handshake,
                        RtsEnable = _config.RtsEnable,
                        DtrEnable = _config.DtrEnable,
                        NewLine = EndOfLine,
                        ReadTimeout = _config.ReadTimeout <= 0 ? 500 : _config.ReadTimeout
                    };

                    if (string.IsNullOrWhiteSpace(EndOfLine) == false)
                    {
                        _serialPort.NewLine = EndOfLine;
                    }

                    _serialPort.Open();

                    _serialPort.DataReceived += SerialPort_DataReceived;
                    _serialPort.ErrorReceived += SerialPort_ErrorReceived;

                    Logger?.Debug("Connected to serial port.");

                }
                catch (Exception ex)
                {
                    Logger?.Error($"Failed to connect: {ex.Message}");
                }
            }

            return IsConnected;
        }

        public Task WriteDataAsync(string data, uint waitMilliseconds = 0)
        {
            if (IsConnected == false)
            {
                Logger?.Error("Serial port is not connected.");
                return Task.CompletedTask;
            }

            var realData = StripEndOfLineCharacter(data);

            // It is possible that realData is now empty
            if (string.IsNullOrWhiteSpace(realData))
            {
                Logger?.Debug("Data is empty.");
                return Task.CompletedTask;
            }

            lock (LockObject)
            {
                _serialPort.WriteLine(realData);
            }

            if (waitMilliseconds > 0)
            {
                Task.Run(() => WaitForResponse(waitMilliseconds));
            }

            return Task.CompletedTask;

        }

        private void SerialPort_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                string data;

                lock (LockObject)
                {
                    // SerialPort raised the DataReceived event, so there is data to read
                    // Read the data until the new line character is reached
                    // The ReadLine method will block until the new line character is reached
                    // or the ReadTimeout is reached
                    // Important!!!
                    // The ReadTimeout must always be set to a value > 0 milliseconds!!!!
                    if (_serialPort.ReadTimeout <= 0)
                    {
                        _serialPort.ReadTimeout = 500;
                    }
                    data = _serialPort.ReadLine();
                }

                HasReceivedResponse = true;

                // Remove first the new line character from the data
                var realData = StripEndOfLineCharacter(data);

                // It is possible that Realdata is now empty
                // Raise the event only if the data is not empty after removing the new line character
                if (string.IsNullOrWhiteSpace(realData) == false
                    && IsWaitingPeriodExpired == false
                    && CancelTokenSource.IsCancellationRequested == false)
                {
                    DataReceived?.Invoke(this, realData);
                }
            }
            catch (Exception ex)
            {
                Logger?.Error($"Error reading data: {ex.Message}");
            }
        }

        private void SerialPort_ErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            if (CancelTokenSource.IsCancellationRequested == false)
            {
                ErrorReceived?.Invoke(this, $"Communication error {e.EventType}");
            }
        }

    }
}
