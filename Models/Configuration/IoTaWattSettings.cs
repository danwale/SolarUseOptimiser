namespace SolarUseOptimiser.Models.Configuration
{
    public class IoTaWattSettings
    {
        public string IPAddress { get; set; }

        public int PollRate
        {
            get; set;
        } = 1;
    }
}