﻿using NHM.Common.Algorithm;
using NHM.Common.Device;
using NHM.Common.Enums;
using System;
using System.Linq;
using System.Collections.Generic;
using MinerPluginToolkitV1;
using MinerPluginToolkitV1.Configs;

namespace EWBF
{
    public class EwbfPlugin : PluginBase
    {
        public EwbfPlugin()
        {
            // set default internal settings
            MinerOptionsPackage = PluginInternalSettings.MinerOptionsPackage;
            // https://bitcointalk.org/index.php?topic=4466962.0 current v0.6
            MinersBinsUrlsSettings = new MinersBinsUrlsSettings
            {
                BinVersion = "v0.6",
                ExePath = new List<string> { "EWBF Equihash miner v0.6", "miner.exe" },
                Urls = new List<string>
                {
                    "https://github.com/nicehash/MinerDownloads/releases/download/1.9.1.5/EWBF.Equihash.miner.v0.6.7z",
                    "https://mega.nz/#F!fsAlmZQS!CwVgFfBDduQI-CbwVkUEpQ?Tlp22YKT" // original
                }
            };
            PluginMetaInfo = new PluginMetaInfo
            {
                PluginDescription = "EWBF is Cuda Equihash Miner.",
                SupportedDevicesAlgorithms = new Dictionary<DeviceType, List<AlgorithmType>>
                {
                    { DeviceType.NVIDIA, new List<AlgorithmType>{ AlgorithmType.ZHash } }
                }
            };
        }

        public override string PluginUUID => "f7d5dfa0-7236-11e9-b20c-f9f12eb6d835";

        public override Version Version => new Version(3, 0);

        public override string Name => "Ewbf";

        public override string Author => "stanko@nicehash.com";

        protected override MinerBase CreateMinerBase()
        {
            return new EwbfMiner(PluginUUID);
        }

        public override Dictionary<BaseDevice, IReadOnlyList<Algorithm>> GetSupportedAlgorithms(IEnumerable<BaseDevice> devices)
        {
            var supported = new Dictionary<BaseDevice, IReadOnlyList<Algorithm>>();
            //CUDA 9.1+: minimum drivers 391.29
            var minDrivers = new Version(391, 29);
            if (CUDADevice.INSTALLED_NVIDIA_DRIVERS < minDrivers) return supported;

            // we filter CUDA SM5.0+
            var cudaGpus = devices
                .Where(dev => dev is CUDADevice gpu && gpu.SM_major >= 5)
                .Cast<CUDADevice>();

            foreach (var gpu in cudaGpus)
            {
                var algorithms = GetSupportedAlgorithms(gpu);
                if (algorithms.Count > 0) supported.Add(gpu, algorithms);
            }

            return supported;
        }

        IReadOnlyList<Algorithm> GetSupportedAlgorithms(CUDADevice gpu)
        {
            var algorithms = new List<Algorithm>
            {
                new Algorithm(PluginUUID, AlgorithmType.ZHash),
            };
            var filteredAlgorithms = Filters.FilterInsufficientRamAlgorithmsList(gpu.GpuRam, algorithms);
            return filteredAlgorithms;
        }

        public override IEnumerable<string> CheckBinaryPackageMissingFiles()
        {
            var pluginRootBinsPath = GetBinAndCwdPaths().Item2;
            return BinaryPackageMissingFilesCheckerHelpers.ReturnMissingFiles(pluginRootBinsPath, new List<string> { "miner.exe", "cudart32_91.dll", "cudart64_91.dll" });
        }

        public override bool ShouldReBenchmarkAlgorithmOnDevice(BaseDevice device, Version benchmarkedPluginVersion, params AlgorithmType[] ids)
        {
            //no new version available
            return false;
        }
    }
}
