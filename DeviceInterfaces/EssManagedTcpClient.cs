using DotNetty.Buffers;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using EssAppComponentsCommon.Common.Classes;
using EssAppComponentsCommon.Logging.Interfaces;
using EssDeviceComponents.Classes;
using EssDeviceComponents.Interfaces;
using System.Text;


namespace EssDeviceComponents.DeviceInterfaces
{
    public class EssManagedTcpClient(EssTcpClientConfig config, IEssAppLogger? logger, string? endOfLine = "\r\n")
        : EssManagedDeviceInterface(logger, endOfLine), IDeviceInterface
    {

        private readonly EssTcpClientConfig _config = config;

        private IChannel _clientChannel = null!;
        private Bootstrap _bootstrap = null!;
        private MultithreadEventLoopGroup _group = null!;
        private const int reconnectDelay = 1000;

        public bool IsConnected
        {
            get
            {
                lock (LockObject)
                {
                    return _clientChannel != null && _clientChannel.Active;
                }
            }

        }

        public event EventHandler<string>? DataReceived;
        public event EventHandler<string>? ErrorReceived;

        public async Task ConnectAsync()
        {

            CancelTokenSource = new();

            _group = new MultithreadEventLoopGroup();
            _bootstrap = new Bootstrap();
            _bootstrap
                .Group(_group)
                .Channel<TcpSocketChannel>()
                .Option(ChannelOption.TcpNodelay, true)
                .Handler(new ActionChannelInitializer<ISocketChannel>(channel =>
                {
                    channel.Pipeline.AddLast(new ClientHandler(this));
                }));

            await ReconnectAsync();
        }

        private async Task ReconnectAsync()
        {
            while (CancelTokenSource.IsCancellationRequested == false && IsConnected == false)
            {
                try
                {
                    Logger?.Debug("Connect to the server.");
                    IChannel clientChannel;
                    clientChannel = await _bootstrap.ConnectAsync(_config.ServerAddress, _config.PortNumber);
                    lock (LockObject)
                    {
                        _clientChannel = clientChannel;
                    }
                    Logger?.Debug("Connected to the server.");
                }
                catch (Exception ex)
                {
                    Logger?.Debug($"Connection error: {ex.Message}");
                }

                // Wait before starting a new attempt (back-off strategy)
                await EssTask.DelayAsync(reconnectDelay, CancelTokenSource.Token);
            }
        }

        public async Task DisconnectAsync()
        {
            CancelTokenSource.Cancel();

            lock (LockObject)
            {
                _clientChannel.CloseAsync().Wait();
            }

            await _group.ShutdownGracefullyAsync(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(1));

            Logger?.Debug("Client stopped.");
        }

        public Task WriteDataAsync(string data, uint waitMilliseconds = 0)
        {
            if (IsConnected == false)
            {
                Logger?.Debug("Cannot send a message: No connection to the server.");
                return Task.CompletedTask;
            }

            var realData = StripEndOfLineCharacter(data);

            byte[] messageBytes = Encoding.UTF8.GetBytes(realData);
            IByteBuffer buffer = Unpooled.WrappedBuffer(messageBytes);

            lock (LockObject)
            {
                _clientChannel.WriteAndFlushAsync(buffer).Wait();
            }

            if (waitMilliseconds > 0)
            {
                WaitForResponse(waitMilliseconds);
            }

            return Task.CompletedTask;
        }

        private class ClientHandler(EssManagedTcpClient client) : SimpleChannelInboundHandler<IByteBuffer>
        {
            private readonly EssManagedTcpClient _client = client;

            protected override void ChannelRead0(IChannelHandlerContext ctx, IByteBuffer msg)
            {
                string received = msg.ToString(Encoding.UTF8);

                // Remove first the new line character from the data
                received = _client.StripEndOfLineCharacter(received);


                if (string.IsNullOrWhiteSpace(received) == false
                    && _client.IsWaitingPeriodExpired == false
                    && _client.CancelTokenSource.IsCancellationRequested == false)
                {
                    _client.DataReceived?.Invoke(_client, received);
                    _client.Logger?.Debug("Received: " + received);
                }
            }

            public override void ChannelInactive(IChannelHandlerContext context)
            {
                _client.Logger?.Debug("Connection lost. Trying to reconnect ...");
                _client.ReconnectAsync().Wait(); // This restarts the reconnect logic.
            }

            public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
            {
                if (_client.CancelTokenSource.IsCancellationRequested == false)
                {
                    _client.Logger?.Debug("Error: " + exception);
                    _client.ErrorReceived?.Invoke(_client, exception.Message);
                    context.CloseAsync();
                }
            }
        }
    }
}
