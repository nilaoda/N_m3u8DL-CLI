using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace N_m3u8DL_CLI
{
    internal class MyOptions
    {
        [Value(0, Hidden = true, MetaName = "Input Source", HelpText = "Help_input", ResourceType = typeof(strings))]
        public string Input { get; set; }

        [Option("workDir", HelpText = "Help_workDir", ResourceType = typeof(strings))]
        public string WorkDir { get; set; }

        [Option("saveName", HelpText = "Help_saveName", ResourceType = typeof(strings))]
        public string SaveName { get; set; } = "";

        [Option("baseUrl", HelpText = "Help_baseUrl", ResourceType = typeof(strings))]
        public string BaseUrl { get; set; }

        [Option("headers", HelpText = "Help_headers", ResourceType = typeof(strings))]
        public string Headers { get; set; } = "";

        [Option("maxThreads", Default = 32U, HelpText = "Help_maxThreads", ResourceType = typeof(strings))]
        public uint MaxThreads { get; set; }

        [Option("minThreads", Default = 16U, HelpText = "Help_minThreads", ResourceType = typeof(strings))]
        public uint MinThreads { get; set; }

        [Option("retryCount", Default = 15U, HelpText = "Help_retryCount", ResourceType = typeof(strings))]
        public uint RetryCount { get; set; }

        [Option("timeOut", Default = 10U, HelpText = "Help_timeOut", ResourceType = typeof(strings))]
        public uint TimeOut { get; set; }

        [Option("muxSetJson", HelpText = "Help_muxSetJson", ResourceType = typeof(strings))]
        public string MuxSetJson { get; set; }

        [Option("useKeyFile", HelpText = "Help_useKeyFile", ResourceType = typeof(strings))]
        public string UseKeyFile { get; set; }

        [Option("useKeyBase64", HelpText = "Help_useKeyBase64", ResourceType = typeof(strings))]
        public string UseKeyBase64 { get; set; }

        [Option("useKeyIV", HelpText = "Help_useKeyIV", ResourceType = typeof(strings))]
        public string UseKeyIV { get; set; }

        [Option("downloadRange", HelpText = "Help_downloadRange", ResourceType = typeof(strings))]
        public string DownloadRange { get; set; }

        [Option("liveRecDur", HelpText = "Help_liveRecDur", ResourceType = typeof(strings))]
        public string LiveRecDur { get; set; }

        [Option("stopSpeed", HelpText = "Help_stopSpeed", ResourceType = typeof(strings))]
        public long StopSpeed { get; set; } = 0L;

        [Option("maxSpeed", HelpText = "Help_maxSpeed", ResourceType = typeof(strings))]
        public long MaxSpeed { get; set; } = 0L;

        [Option("proxyAddress", HelpText = "Help_proxyAddress", ResourceType = typeof(strings))]
        public string ProxyAddress { get; set; }

        [Option("enableDelAfterDone", HelpText = "Help_enableDelAfterDone", ResourceType = typeof(strings))]
        public bool EnableDelAfterDone { get; set; }

        [Option("enableMuxFastStart", HelpText = "Help_enableMuxFastStart", ResourceType = typeof(strings))]
        public bool EnableMuxFastStart { get; set; }

        [Option("enableBinaryMerge", HelpText = "Help_enableBinaryMerge", ResourceType = typeof(strings))]
        public bool EnableBinaryMerge { get; set; }

        [Option("enableParseOnly", HelpText = "Help_enableParseOnly", ResourceType = typeof(strings))]
        public bool EnableParseOnly { get; set; }

        [Option("enableAudioOnly", HelpText = "Help_enableAudioOnly", ResourceType = typeof(strings))]
        public bool EnableAudioOnly { get; set; }

        [Option("disableDateInfo", HelpText = "Help_disableDateInfo", ResourceType = typeof(strings))]
        public bool DisableDateInfo { get; set; }

        [Option("disableIntegrityCheck", HelpText = "Help_disableIntegrityCheck", ResourceType = typeof(strings))]
        public bool DisableIntegrityCheck { get; set; }

        [Option("noMerge", HelpText = "Help_noMerge", ResourceType = typeof(strings))]
        public bool NoMerge { get; set; }

        [Option("noProxy", HelpText = "Help_noProxy", ResourceType = typeof(strings))]
        public bool NoProxy { get; set; }

        [Option("registerUrlProtocol", HelpText = "Help_registerUrlProtocol", ResourceType = typeof(strings))]
        public bool RegisterUrlProtocol { get; set; }

        [Option("unregisterUrlProtocol", HelpText = "Help_unregisterUrlProtocol", ResourceType = typeof(strings))]
        public bool UnregisterUrlProtocol { get; set; }

        [Option("enableChaCha20", HelpText = "enableChaCha20")]
        public bool EnableChaCha20 { get; set; }

        [Option("chaCha20KeyBase64", HelpText = "ChaCha20KeyBase64")]
        public string ChaCha20KeyBase64 { get; set; }

        [Option("chaCha20NonceBase64", HelpText = "ChaCha20NonceBase64")]
        public string ChaCha20NonceBase64 { get; set; }

    }
}
