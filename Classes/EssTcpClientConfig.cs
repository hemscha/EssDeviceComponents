namespace EssDeviceComponents.Classes
{
    public class EssTcpClientConfig
    {
        public string ServerAddress { get; set; } = null!;
        public int PortNumber { get; set; }

        public bool IsValid(out string error)
        {
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(ServerAddress))
            {
                error = $"The {nameof(ServerAddress)} is not set";
            }
            else if (PortNumber <= 0)
            {
                error = $"The {nameof(PortNumber)} is not set";
            }

            return string.IsNullOrWhiteSpace(error);

        }
    }
}
