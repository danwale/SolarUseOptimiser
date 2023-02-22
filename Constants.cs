using System;

namespace SolarUseOptimiser
{
    public static class Constants
    {
        public const string HUAWEI_CONFIG_SECTION = "Huawei";
        public const string CHARGE_HQ_CONFIG_SECION = "ChargeHQ";

        public const string ROUTING_SECTION = "Routing";

        public const string ROUTE_DATASOURCE_HUAWEI = "Huawei";
        public const string ROUTE_DATASOURCE_GROWWATT = "GrowWatt";
        public const string ROUTE_TARGET_CHARGEHQ = "ChargeHQ";
        
        // need to login every 30 minutes
        public const string LOGIN_URI = "thirdData/login";

        public const string STATION_LIST_URI = "thirdData/getStationList";

        public const string STATION_LIST_NEW_URI = "thirdData/stations";

        // can't call this more than 10 times a minute
        public const string DEV_LIST_URI = "thirdData/getDevList";

        // can't call this more than once every 5 minutes
        public const string DEV_REAL_KPI_URI = "thirdData/getDevRealKpi";
    }
}