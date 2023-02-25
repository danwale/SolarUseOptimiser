namespace SolarUseOptimiser.Models.AlphaESS
{
    public class GetCustomerDataResponse
    {
        public int code { get; set;}

        public string info { get; set; }

        public GetCustomerDataResponseData data { get; set; }
    }

    public class GetCustomerDataResponseData
    {
        public string sys_sn { get; set; } //system serial number

        public string sys_name { get; set; }

        public double popv { get; set; }

        public string minv { get; set; }

        public double poinv { get; set; }

        public double cobat { get; set; }

        public string mbat { get; set; }

        public double surpluscobat { get; set; }

        public double uscapacity { get; set; }

        public string ems_status { get; set; }

        public int trans_frequency { get; set; }

        public int parallel_en { get; set; }

        public int parallel_mode { get; set; }
    }
}