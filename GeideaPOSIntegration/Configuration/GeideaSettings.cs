namespace GeideaPOSIntegration.Configuration
{
    public class GeideaSettings
    {
        public const string SectionName = "GeideaSettings";

        public string MerchantId { get; set; } = string.Empty;
        public string TerminalId { get; set; } = string.Empty;
        public string ConnectionType { get; set; } = "USB";
        public SerialPortSettings SerialPort { get; set; } = new();
        public NetworkSettings Network { get; set; } = new();
    }

    public class SerialPortSettings
    {
        public int BaudRate { get; set; } = 9600;
        public int DataBits { get; set; } = 8;
        public string Parity { get; set; } = "None";
        public string StopBits { get; set; } = "One";
        public int TimeoutMs { get; set; } = 30000;
    }

    public class NetworkSettings
    {
        public string BaseUrl { get; set; } = "http://192.168.1.100:8080";
        public int TimeoutMs { get; set; } = 30000;
    }
}