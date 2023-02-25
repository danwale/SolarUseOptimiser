namespace SolarUseOptimiser.Models.AlphaESS
{
    public class GetLastPowerData
    {
        public int code { get; set; }

        public string info { get; set; }

        public GetPowerDataData data { get; set; }
    }

    public class GetPowerDataData
    {
        public double ppv1 { get; set; }
        public double ppv2 { get; set; }
        public double ppv3 { get; set; }
        public double ppv4 { get; set; }
        public double preal_l1 { get; set; }
        public double preal_l2 { get; set; }
        public double preal_l3 { get; set; }
        public double pmeter_l1 { get; set; }
        public double pmeter_l2 { get; set; }
        public double pmeter_l3 { get; set; }
        public double pmeter_dc { get; set; }
        public double soc { get; set; }
        public double pbat { get; set; }
        public int ev1_power { get; set; }
        public int ev2_power { get; set; }
        public int ev3_power { get; set; }
        public int ev4_power { get; set; }
        public string createtime { get; set; }
        public int ups_model { get; set; }
        public string ppv_slave { get; set; }
    }
}