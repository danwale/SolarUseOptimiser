namespace SolarUseOptimiser.Models.Configuration
{
    public class GrowattSettings
    {
        public string BaseURI
        {
            get; set;
        }

        public string Username
        {
            get; set;
        }

        public string Password
        {
            get; set;
        }

        public double PollRate
        {
            get; set;
        } = 5;

        public bool UseBatteryData
        {
            get; set;
        } = true;
    }
}