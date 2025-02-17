﻿using MinerPluginToolkitV1;
using MinerPluginToolkitV1.Configs;
using NHM.Common.Algorithm;
using NHM.Common.Device;
using NHM.Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using static MinerPluginToolkitV1.Checkers;

namespace CryptoDredge
{
    public class CryptoDredgePlugin : PluginBase
    {
        public CryptoDredgePlugin()
        {
            MinerOptionsPackage = PluginInternalSettings.MinerOptionsPackage;
            // https://github.com/technobyl/CryptoDredge/releases | https://cryptodredge.org/ | https://bitcointalk.org/index.php?topic=4807821.0 current 0.21.0
            MinersBinsUrlsSettings = new MinersBinsUrlsSettings
            {
                BinVersion = "0.21.0",
                ExePath = new List<string> { "CryptoDredge_0.21.0", "CryptoDredge.exe" },
                Urls = new List<string>
                {
                    "https://github.com/technobyl/CryptoDredge/releases/download/v0.21.0/CryptoDredge_0.21.0_cuda_10.1_windows.zip", // original source
                }
            };
            PluginMetaInfo = new PluginMetaInfo
            {
                PluginDescription = "Simple in use and highly optimized cryptocurrency mining software with stable power consumption.",
                SupportedDevicesAlgorithms = new Dictionary<DeviceType, List<AlgorithmType>>
                {
                    { DeviceType.NVIDIA, new List<AlgorithmType>{ AlgorithmType.Lyra2REv3, AlgorithmType.X16R, AlgorithmType.MTP } }
                }
            };
        }

        public override Version Version => new Version(3, 0);
        public override string Name => "CryptoDredge";

        public override string Author => "domen.kirnkrefl@nicehash.com";

        public override string PluginUUID => "d9c2e620-7236-11e9-b20c-f9f12eb6d835";

        public override Dictionary<BaseDevice, IReadOnlyList<Algorithm>> GetSupportedAlgorithms(IEnumerable<BaseDevice> devices)
        {
            var supported = new Dictionary<BaseDevice, IReadOnlyList<Algorithm>>();

            var isDriverCompatible = Checkers.IsCudaCompatibleDriver(CudaVersion.CUDA_10_1_105, CUDADevice.INSTALLED_NVIDIA_DRIVERS);
            if (!isDriverCompatible) return supported;

            var cudaGpus = devices.Where(dev => dev is CUDADevice cuda && cuda.SM_major >= 5).Cast<CUDADevice>();

            foreach (var gpu in cudaGpus)
            {
                var algos = GetSupportedAlgorithms(gpu);
                if (algos.Count > 0) supported.Add(gpu, algos);
            }

            return supported;
        }

        IReadOnlyList<Algorithm> GetSupportedAlgorithms(CUDADevice gpu)
        {
            var algorithms = new List<Algorithm>
            {
                new Algorithm(PluginUUID, AlgorithmType.Lyra2REv3),
                new Algorithm(PluginUUID, AlgorithmType.X16R),
                new Algorithm(PluginUUID, AlgorithmType.MTP) { Enabled = false }
            };
            var filteredAlgorithms = Filters.FilterInsufficientRamAlgorithmsList(gpu.GpuRam, algorithms);
            return filteredAlgorithms;
        }

        protected override MinerBase CreateMinerBase()
        {
            return new CryptoDredge(PluginUUID);
        }

        public override IEnumerable<string> CheckBinaryPackageMissingFiles()
        {
            var pluginRootBinsPath = GetBinAndCwdPaths().Item2;
            return BinaryPackageMissingFilesCheckerHelpers.ReturnMissingFiles(pluginRootBinsPath, new List<string> { "CryptoDredge.exe" });
        }

        public override bool ShouldReBenchmarkAlgorithmOnDevice(BaseDevice device, Version benchmarkedPluginVersion, params AlgorithmType[] ids)
        {
            //no new version available
            return false;
        }

        // Since the API doesn't work for this one set the default value to false
        public override bool IsGetApiMaxTimeoutEnabled => MinerApiMaxTimeoutSetting.ParseIsEnabled(false, GetApiMaxTimeoutConfig);
    }
}
