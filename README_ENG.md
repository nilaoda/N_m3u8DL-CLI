```

███╗   ██╗        ███╗   ███╗██████╗ ██╗   ██╗ █████╗ ██████╗ ██╗       ██████╗██╗     ██╗
████╗  ██║        ████╗ ████║╚════██╗██║   ██║██╔══██╗██╔══██╗██║      ██╔════╝██║     ██║
██╔██╗ ██║        ██╔████╔██║ █████╔╝██║   ██║╚█████╔╝██║  ██║██║█████╗██║     ██║     ██║
██║╚██╗██║        ██║╚██╔╝██║ ╚═══██╗██║   ██║██╔══██╗██║  ██║██║╚════╝██║     ██║     ██║
██║ ╚████║███████╗██║ ╚═╝ ██║██████╔╝╚██████╔╝╚█████╔╝██████╔╝███████╗ ╚██████╗███████╗██║
╚═╝  ╚═══╝╚══════╝╚═╝     ╚═╝╚═════╝  ╚═════╝  ╚════╝ ╚═════╝ ╚══════╝  ╚═════╝╚══════╝╚═╝
                                                                                          
```
This is an m3u8 downloader.  
## Summary
Supports: 
  * Auto decrypt for `AES-128-CBC`
  * `Master List`
  * Live stream recording(`BETA`)
  * Customize HTTP headers
  * Auto merge clips(Binary or ffmpeg)
  * Select save clip by `time code` or `index`
  * Network driver on Windows OS
  * Alternative audio/video track
  * Mux without video track
  * Custom HTTP proxy or Use system proxy
  * Optimization for Chinese streaming platforms
  
  ![ScreenShot](https://nilaoda.github.io/N_m3u8DL-CLI/source/images/%E7%9B%B4%E6%8E%A5%E4%BD%BF%E7%94%A8.gif)  
  
## GUI
  * Easy-to-use `GUI`
  
## Options
```
N_m3u8DL-CLI

USAGE:

  N_m3u8DL-CLI <URL|JSON|FILE> [OPTIONS]

OPTIONS:

  --workDir                  Set work dir (Video will be here)
  --saveName                 Set save name(Exclude extention)
  --baseUrl                  Set Baseurl
  --headers                  Set HTTP headers，format: key:value use | split all
                             key&value
  --maxThreads               (Default: 32) Set max thread
  --minThreads               (Default: 16) Set min thread
  --retryCount               (Default: 15) Set retry times
  --timeOut                  (Default: 10) Set timeout for http request(second)
  --muxSetJson               Set a json file for mux
  --useKeyFile               Use 16 bytes file as KEY for AES-128 decryption
  --useKeyBase64             Use Base64 String as KEY for AES-128 decryption
  --useKeyIV                 Use HEX String as IV for AES-128 decryption
  --downloadRange            Set range for a video
  --liveRecDur               When the live recording reaches this length, the
                             software will exit automatically(HH:MM:SS)
  --stopSpeed                Speed below this, retry(KB/s)
  --maxSpeed                 Set max download speed(KB/s)
  --proxyAddress             Set HTTP/SOCKS5 Proxy, like http://127.0.0.1:8080
  --enableDelAfterDone       Enable delete clips after download completed
  --enableMuxFastStart       Enable fast start for mp4
  --enableBinaryMerge        Enable use binary merge instead of ffmpeg
  --enableParseOnly          Enable parse only mode
  --enableAudioOnly          Enable only audio track when mux use ffmpeg
  --disableDateInfo          Disable write date info when mux use ffmpeg
  --disableIntegrityCheck    Disable integrity check
  --noMerge                  Disable auto merge
  --noProxy                  Disable use system proxy
  --registerUrlProtocol      Register m3u8dl URL protocol
  --unregisterUrlProtocol    Unregister m3u8dl URL protocol
  --enableChaCha20           enableChaCha20
  --chaCha20KeyBase64        ChaCha20KeyBase64
  --chaCha20NonceBase64      ChaCha20NonceBase64
  --help                     Display this help screen.
  --version                  Display version information.
```

## About `m3u8dl://`
New commandline options：
```
--registerUrlProtocol          Register m3u8dl URL protocol
--unregisterUrlProtocol        Unregister m3u8dl URL protocol
```

URI Format：
```
m3u8dl://<base64 encoded params>
```

URI Example：
```
m3u8dl://Imh0dHBzOi8vZXhhbXBsZS5jb20vYWJjLm0zdTgiIC0td29ya0RpciAiJVVTRVJQUk9GSUxFJVxEb3dubG9hZHNcbTN1OGRsIiAtLXNhdmVOYW1lICJhYmMiIC0tZW5hYmxlRGVsQWZ0ZXJEb25lIC0tZGlzYWJsZURhdGVJbmZvIC0tbm9Qcm94eQ==
```

URI Decode Result：
```
"https://example.com/abc.m3u8" --workDir "%USERPROFILE%\Downloads\m3u8dl" --saveName "abc" --enableDelAfterDone --disableDateInfo --noProxy
```

## Document
https://nilaoda.github.io/N_m3u8DL-CLI/

## Chit-chat
https://discord.gg/RscAJZv3Yq
