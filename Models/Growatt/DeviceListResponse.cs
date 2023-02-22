namespace SolarUseOptimiser.Models.Growatt
{
    public class DeviceListResponse
    {
        public int result { get; set; }

        public DeviceListObjResponse obj { get; set; }
    }

    public class DeviceListObjResponse
    {
        public int currPage { get; set; }
        public int pages { get; set; }

        public int pageSize { get; set; }

        public int count { get; set; }

        public int ind { get; set; }

        public IList<DeviceListObjDatasResponse> datas { get; set; }

        public bool notPager { get; set; }
    }

    public class DeviceListObjDatasResponse
    {
        public string ptoStatus { get; set; }

        public string timeServer { get; set; }

        public string accountName { get; set; }

        public string timezone { get; set; }

        public string bctMode { get; set; }

        public string bdcStatus { get; set; }

        public string eMonth { get; set; }

        public string dtc { get; set; }
        public string pac { get; set; }

        public string batSysRateEnergy { get; set; }

        public string datalogSn { get; set; }

        public string alias  { get; set; }

        public string sn { get; set; }

        public string deviceType { get; set; }

        public string plantId { get; set; }

        public string deviceTypeName  { get; set; }

        public string nominalPower { get; set; }

        public string eTotay { get; set; }
        public string datalogTypeTest { get; set; }
        public string eTotal { get; set; }

        public string showDeviceModel { get; set; }
        public string location { get; set; }

        public string deviceModel { get; set; }

        public string plantName { get; set; }

        public string status { get; set; }
        public string lastUpdateTime { get; set; }
    }
}