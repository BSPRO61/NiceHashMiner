﻿using Newtonsoft.Json;
using NiceHashMiner.Configs;
using System;
using System.Collections.Generic;
using NiceHashMiner.Stats;

namespace NiceHashMiner
{
    internal class ExchangeRateApi
    {
#pragma warning disable IDE1006
        public class Result
        {
            public object algorithms { get; set; }
            public object servers { get; set; }
            public object idealratios { get; set; }
            public List<Dictionary<string, string>> exchanges { get; set; }
            public Dictionary<string, double> exchanges_fiat { get; set; }
        }

        public class ExchangeRateJson
        {
            public Result result { get; set; }
            public string method { get; set; }
        }
#pragma warning restore IDE1006

        private const string ApiUrl = "https://api.nicehash.com/api?method=nicehash.service.info";

        public static Dictionary<string, double> ExchangesFiat;
        public static double UsdBtcRate = -1;
        public static string ActiveDisplayCurrency = "USD";

        private static bool ConverterActive => ConfigManager.GeneralConfig.DisplayCurrency != "USD";


        public static double ConvertToActiveCurrency(double amount)
        {
            if (!ConverterActive)
            {
                return amount;
            }

            // if we are still null after an update something went wrong. just use USD hopefully itll update next tick
            if (ExchangesFiat == null || ActiveDisplayCurrency == "USD")
            {
                // Moved logging to update for berevity 
                return amount;
            }

            //Helpers.ConsolePrint("CurrencyConverter", "Current Currency: " + ConfigManager.Instance.GeneralConfig.DisplayCurrency);
            if (ExchangesFiat.TryGetValue(ActiveDisplayCurrency, out var usdExchangeRate))
                return amount * usdExchangeRate;
            Helpers.ConsolePrint("CurrencyConverter", "Unknown Currency Tag: " + ActiveDisplayCurrency + " falling back to USD rates");
            ActiveDisplayCurrency = "USD";
            return amount;
        }

        public static double GetUsdExchangeRate()
        {
            return UsdBtcRate > 0 ? UsdBtcRate : 0.0;
        }

        [Obsolete("UpdateApi is deprecated, use websocket method")]
        public static void UpdateApi(string worker)
        {
            var resp = NiceHashStats.GetNiceHashApiData(ApiUrl, worker);
            if (resp != null)
            {
                try
                {
                    var lastResponse = JsonConvert.DeserializeObject<ExchangeRateJson>(resp, Globals.JsonSettings);
                    // set that we have a response
                    if (lastResponse != null)
                    {
                        var lastResult = lastResponse.result;
                        ExchangesFiat = lastResult.exchanges_fiat;
                        if (ExchangesFiat == null)
                        {
                            Helpers.ConsolePrint("CurrencyConverter", "Unable to retrieve update, Falling back to USD");
                            ActiveDisplayCurrency = "USD";
                        }
                        else
                        {
                            ActiveDisplayCurrency = ConfigManager.GeneralConfig.DisplayCurrency;
                        }
                        // ActiveDisplayCurrency = "USD";
                        // check if currency avaliable and fill currency list
                        foreach (var pair in lastResult.exchanges)
                        {
                            if (pair.ContainsKey("USD") && pair.ContainsKey("coin") && pair["coin"] == "BTC" && pair["USD"] != null)
                            {
                                UsdBtcRate = Helpers.ParseDouble(pair["USD"]);
                                break;
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Helpers.ConsolePrint("ExchangeRateAPI", "UpdateAPI got Exception: " + e.Message);
                }
            }
            else
            {
                Helpers.ConsolePrint("ExchangeRateAPI", "UpdateAPI got NULL");
            }
        }
    }
}
