using Newtonsoft.Json;

namespace SolarUseOptimiser.Models.Huawei
{
    public class DeviceInfoResponse : BaseResponse
    {
        public IList<DeviceInfo> data { get; set; }

        [JsonProperty("params")]
        public DeviceInfoParams parameters { get; set;}
    }
}