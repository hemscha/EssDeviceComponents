namespace EssDeviceComponents.Interfaces
{
    public interface IDeviceInterface
    {
        Task ConnectAsync();
        Task DisconnectAsync();

        public bool IsConnected { get; }
        Task WriteDataAsync(string data, uint waitMilliseconds = 0);

        public event EventHandler<string>? DataReceived;
        public event EventHandler<string>? ErrorReceived;

    }
}
