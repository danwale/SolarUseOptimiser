namespace SolarUseOptimiser.Models.Growatt
{
    public class MixStatusResponse
    {
        public int result { get; set; }
        public MixStatusObjResponse obj { get; set; }
    }

    public class MixStatusObjResponse
    {
        public string pdisCharge1 { get; set; } // BatteryDischargePower
        public string uwSysWorkMode { get; set; }
        public double pactouser { get; set; } // PowerImportFromGrid
        public string vBat { get; set; }
        public string vAc1 { get; set; }
        public string priorityChoose { get; set; }
        public string lost { get; set; }
        public double pactogrid { get; set; } // PowerOutputTotal
        public double pLocalLoad { get; set; } // LocalLoadPower
        public string vPv2 { get; set; }
        public string deviceType { get; set; }
        public double pex { get; set; } // PowerOutputExport
        public double chargePower { get; set; } // BatteryChargePower
        public string vPv1 { get; set; }
        public string upsVac1 { get; set; }
        public string SOC { get; set; } // BatteryChargePercent
        public string wBatteryType { get; set; }
        public string pPv2 { get; set; }
        public string fAc { get; set; }
        public string vac1 { get; set; }
        public string pPv1 { get; set; }
        public string storagePpv { get; set; }
        public string upsFac { get; set; }
        public double ppv { get; set; } // PVPowerTotal
        public string status { get; set; }
    }
}