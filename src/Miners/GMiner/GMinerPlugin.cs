using NHM.Common;
using NHM.Common.Algorithm;
using NHM.Common.Device;
using NHM.Common.Enums;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using MinerPluginToolkitV1;
using MinerPluginToolkitV1.Configs;
using MinerPluginToolkitV1.Interfaces;

namespace GMinerPlugin
{
    public class GMinerPlugin : PluginBase, IDevicesCrossReference
    {
        public GMinerPlugin()
        {
            // set default internal settings
            MinerOptionsPackage = PluginInternalSettings.MinerOptionsPackage;
            GetApiMaxTimeoutConfig = PluginInternalSettings.GetApiMaxTimeoutConfig;
            DefaultTimeout = PluginInternalSettings.DefaultTimeout;
            // https://bitcointalk.org/index.php?topic=5034735.0 | https://github.com/develsoftware/GMinerRelease/releases current v1.66
            MinersBinsUrlsSettings = new MinersBinsUrlsSettings
            {
                BinVersion = "1.66",
                ExePath = new List<string> { "miner.exe" },
                Urls = new List<string>
                {
                    "https://github.com/develsoftware/GMinerRelease/releases/download/1.66/gminer_1_66_windows64.zip", // original
                }
            };
            PluginMetaInfo = new PluginMetaInfo
            {
                PluginDescription = "GMiner - High-performance miner for AMD/Nvidia GPUs.",
                SupportedDevicesAlgorithms = new Dictionary<DeviceType, List<AlgorithmType>>
                {
                    { DeviceType.NVIDIA, new List<AlgorithmType>{ AlgorithmType.ZHash, AlgorithmType.GrinCuckatoo31, AlgorithmType.CuckooCycle, AlgorithmType.GrinCuckarood29, AlgorithmType.BeamV2 } },
                    { DeviceType.AMD, new List<AlgorithmType>{ AlgorithmType.CuckooCycle, AlgorithmType.BeamV2 } }
                }
            };
        }

        public override string PluginUUID => "1b7019d0-7237-11e9-b20c-f9f12eb6d835";

        public override Version Version => new Version(3, 0);

        public override string Name => "GMinerCuda9.0+";

        public override string Author => "stanko@nicehash.com";

        protected readonly Dictionary<string, int> _mappedDeviceIds = new Dictionary<string, int>();

        protected override MinerBase CreateMinerBase()
        {
            return new GMiner(PluginUUID, _mappedDeviceIds);
        }


        // Supported algoritms:
        //   - Cuckaroo29/Cuckatoo31 (Grin)
        //   - Cuckoo29 (Aeternity)
        //   - Equihash 96,5 (MinexCoin)
        //   - Equihash 144,5 (Bitcoin Gold, BitcoinZ, SnowGem, SafeCoin, Litecoin Z) // ZHash
        //   - Equihash 150,5 (BEAM)
        //   - Equihash 192,7 (Zero, Genesis)
        //   - Equihash 210,9 (Aion)

        // Requirements:
        //   - CUDA compute compability 5.0+ #1
        //   - Cuckaroo29 ~ 5.6GB VRAM
        //   - Cuckatoo31 ~ 7.4GB VRAM
        //   - Cuckoo29 ~ 5.6GB VRAM
        //   - Equihash 96,5 ~0.75GB VRAM
        //   - Equihash 144,5 ~1.75GB VRAM
        //   - Equihash 150,5 ~2.9GB VRAM
        //   - Equihash 192,7 ~2.75GB VRAM
        //   - Equihash 210,9 ~1GB VRAM
        //   - CUDA 9.0+ 

        public override Dictionary<BaseDevice, IReadOnlyList<Algorithm>> GetSupportedAlgorithms(IEnumerable<BaseDevice> devices)
        {
            var supported = new Dictionary<BaseDevice, IReadOnlyList<Algorithm>>();

            var gpus = devices
                .Where(dev => IsSupportedAMDDevice(dev) || IsSupportedNVIDIADevice(dev))
                .Where(dev => dev is IGpuDevice)
                .Cast<IGpuDevice>()
                .OrderBy(gpu => gpu.PCIeBusID);

            var pcieId = 0; // GMiner sortes devices by PCIe
            foreach (var gpu in gpus)
            {
                _mappedDeviceIds[gpu.UUID] = pcieId;
                ++pcieId;
                if (gpu is AMDDevice amd)
                {
                    var algorithms = GetAMDSupportedAlgorithms(amd).ToList();
                    if (algorithms.Count > 0) supported.Add(amd, algorithms);
                }
                // TODO we don't check GMiner minimum driver version
                if (gpu is CUDADevice cuda)
                {
                    var algorithms = GetCUDASupportedAlgorithms(cuda);
                    if (algorithms.Count > 0) supported.Add(cuda, algorithms);
                }
            }

            return supported;
        }

        IReadOnlyList<Algorithm> GetCUDASupportedAlgorithms(CUDADevice gpu) {
            var algorithms = new List<Algorithm>
            {
                new Algorithm(PluginUUID, AlgorithmType.ZHash),
                new Algorithm(PluginUUID, AlgorithmType.GrinCuckatoo31),
                new Algorithm(PluginUUID, AlgorithmType.CuckooCycle) {Enabled = false }, //~5% of invalid nonce shares,
                new Algorithm(PluginUUID, AlgorithmType.GrinCuckarood29),
                new Algorithm(PluginUUID, AlgorithmType.BeamV2),
            };
            var filteredAlgorithms = Filters.FilterInsufficientRamAlgorithmsList(gpu.GpuRam, algorithms);
            return filteredAlgorithms;
        }

        IReadOnlyList<Algorithm> GetAMDSupportedAlgorithms(AMDDevice gpu)
        {
            var algorithms = new List<Algorithm>
            {
                new Algorithm(PluginUUID, AlgorithmType.CuckooCycle) {Enabled = false }, //~5% of invalid nonce shares
                new Algorithm(PluginUUID, AlgorithmType.BeamV2),
            };
            var filteredAlgorithms = Filters.FilterInsufficientRamAlgorithmsList(gpu.GpuRam, algorithms);
            return filteredAlgorithms;
        }

        private static bool IsSupportedAMDDevice(BaseDevice dev)
        {
            var isSupported = dev is AMDDevice gpu && Checkers.IsGcn4(gpu);
            return isSupported;
        }

        private static bool IsSupportedNVIDIADevice(BaseDevice dev)
        {
            //CUDA 9.0+: minimum drivers 384.xx
            var minDrivers = new Version(384, 0);
            var isDriverSupported = CUDADevice.INSTALLED_NVIDIA_DRIVERS >= minDrivers;
            var isSupported = dev is CUDADevice gpu && gpu.SM_major >= 5;
            return isSupported && isDriverSupported;
        }

        public async Task DevicesCrossReference(IEnumerable<BaseDevice> devices)
        {
            if (_mappedDeviceIds.Count == 0) return;
            var minerBinPath = GetBinAndCwdPaths().Item1;
            var output = await DevicesCrossReferenceHelpers.MinerOutput(minerBinPath, "--list_devices");
            var mappedDevs = DevicesListParser.ParseGMinerOutput(output, devices.ToList());

            foreach (var kvp in mappedDevs)
            {
                var uuid = kvp.Key;
                var indexID = kvp.Value;
                _mappedDeviceIds[uuid] = indexID;
            }
        }

        public override IEnumerable<string> CheckBinaryPackageMissingFiles()
        {
            var pluginRootBinsPath = GetBinAndCwdPaths().Item2;
            return BinaryPackageMissingFilesCheckerHelpers.ReturnMissingFiles(pluginRootBinsPath, new List<string> { "miner.exe" });
        }

        public override bool ShouldReBenchmarkAlgorithmOnDevice(BaseDevice device, Version benchmarkedPluginVersion, params AlgorithmType[] ids)
        {
            try
            {
                if (ids.Count() == 0) return false;
                if (benchmarkedPluginVersion.Major == 2 && benchmarkedPluginVersion.Minor < 8)
                {
                    // improved performance for ZHash for nvidia cards
                    if (device.DeviceType == DeviceType.NVIDIA && ids.FirstOrDefault() == AlgorithmType.GrinCuckarood29) return true;
                }
                if (benchmarkedPluginVersion.Major == 2 && benchmarkedPluginVersion.Minor < 7)
                {
                    // improved performance for ZHash for nvidia cards
                    if (device.DeviceType == DeviceType.NVIDIA && ids.FirstOrDefault() == AlgorithmType.ZHash) return true;
                }
                if (benchmarkedPluginVersion.Major == 2 && benchmarkedPluginVersion.Minor < 6)
                {
                    // improved performance for BEAM2 for nvidia cards
                    if (device.DeviceType == DeviceType.NVIDIA && ids.FirstOrDefault() == AlgorithmType.BeamV2) return true;
                }
                return false;
            }
            catch (Exception e)
            {
                Logger.Error(PluginUUID, $"ShouldReBenchmarkAlgorithmOnDevice {e.Message}");
            }
            return false;
        }
    }
}
