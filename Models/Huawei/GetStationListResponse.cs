using Newtonsoft.Json;

namespace SolarUseOptimiser.Models.Huawei
{
    public class GetStationListResponse : BaseResponse
    {
        public IList<StationInfo> data { get;set;}

        [JsonProperty("params")]
        public StationInfoParams parameters { get;set;}
    }
}