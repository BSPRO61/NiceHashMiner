﻿using MinerPlugin;
using NHM.Common.Algorithm;
using NHM.Common.Device;
using NHM.Common.Enums;
using System;
using System.Linq;
using System.Collections.Generic;
using MinerPluginToolkitV1;
using MinerPluginToolkitV1.Configs;

namespace TeamRedMiner
{
    public class TeamRedMinerPlugin : PluginBase
    {
        public TeamRedMinerPlugin()
        {
            // set default internal settings
            MinerOptionsPackage = PluginInternalSettings.MinerOptionsPackage;
            MinerSystemEnvironmentVariables = PluginInternalSettings.MinerSystemEnvironmentVariables;
            // https://github.com/todxx/teamredminer/releases
            MinersBinsUrlsSettings = new MinersBinsUrlsSettings
            {
                BinVersion = "0.5.9",
                ExePath = new List<string> { "teamredminer-v0.5.9-win", "teamredminer.exe" },
                Urls = new List<string>
                {
                    "https://github.com/todxx/teamredminer/releases/download/0.5.9/teamredminer-v0.5.9-win.zip", // original
                }
            };
            PluginMetaInfo = new PluginMetaInfo
            {
                PluginDescription = "Miner for AMD gpus.",
                SupportedDevicesAlgorithms = new Dictionary<DeviceType, List<AlgorithmType>>
                {
                    { DeviceType.AMD, new List<AlgorithmType>{ AlgorithmType.CryptoNightR, AlgorithmType.Lyra2REv3, AlgorithmType.Lyra2Z, AlgorithmType.X16R, AlgorithmType.GrinCuckatoo31, AlgorithmType.GrinCuckarood29, AlgorithmType.MTP } }
                }
            };
        }

        public override string PluginUUID => "abc3e2a0-7237-11e9-b20c-f9f12eb6d835";

        public override Version Version => new Version(3, 0);

        public override string Name => "TeamRedMiner";

        public override string Author => "stanko@nicehash.com";

        public override bool CanGroup(MiningPair a, MiningPair b)
        {
            var canGroup = base.CanGroup(a, b);
            if (a.Device is AMDDevice aDev && b.Device is AMDDevice bDev && aDev.OpenCLPlatformID != bDev.OpenCLPlatformID)
            {
                // OpenCLPlatorm IDs must match
                return false;
            }
            return canGroup;
        }

        protected override MinerBase CreateMinerBase()
        {
            return new TeamRedMiner(PluginUUID);
        }

        public override Dictionary<BaseDevice, IReadOnlyList<Algorithm>> GetSupportedAlgorithms(IEnumerable<BaseDevice> devices)
        {
            var supported = new Dictionary<BaseDevice, IReadOnlyList<Algorithm>>();
            // Get AMD GCN4+
            var amdGpus = devices.Where(dev => dev is AMDDevice gpu && Checkers.IsGcn4(gpu)).Cast<AMDDevice>();

            foreach (var gpu in amdGpus)
            {
                var algorithms = GetSupportedAlgorithms(gpu);
                if (algorithms.Count > 0) supported.Add(gpu, algorithms);
            }

            return supported;
        }

        IReadOnlyList<Algorithm> GetSupportedAlgorithms(AMDDevice gpu)
        {
            var algorithms = new List<Algorithm> {
                new Algorithm(PluginUUID, AlgorithmType.CryptoNightR),
                new Algorithm(PluginUUID, AlgorithmType.Lyra2REv3),
                new Algorithm(PluginUUID, AlgorithmType.Lyra2Z),
                new Algorithm(PluginUUID, AlgorithmType.X16R),
                new Algorithm(PluginUUID, AlgorithmType.GrinCuckatoo31),
                new Algorithm(PluginUUID, AlgorithmType.MTP) { Enabled = false },
                new Algorithm(PluginUUID, AlgorithmType.GrinCuckarood29),
                new Algorithm(PluginUUID, AlgorithmType.X16Rv2)
            };

            var filteredAlgorithms = Filters.FilterInsufficientRamAlgorithmsList(gpu.GpuRam, algorithms);
            return filteredAlgorithms;
        }

        public override IEnumerable<string> CheckBinaryPackageMissingFiles()
        {
            var pluginRootBinsPath = GetBinAndCwdPaths().Item2;
            return BinaryPackageMissingFilesCheckerHelpers.ReturnMissingFiles(pluginRootBinsPath, new List<string> { "teamredminer.exe" });
        }

        public override bool ShouldReBenchmarkAlgorithmOnDevice(BaseDevice device, Version benchmarkedPluginVersion, params AlgorithmType[] ids)
        {
            //no improvements for algorithm speeds in the new version - just stability improvements
            return false;
        }
    }
}
