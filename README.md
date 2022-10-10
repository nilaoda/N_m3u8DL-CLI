```

███╗   ██╗        ███╗   ███╗██████╗ ██╗   ██╗ █████╗ ██████╗ ██╗       ██████╗██╗     ██╗
████╗  ██║        ████╗ ████║╚════██╗██║   ██║██╔══██╗██╔══██╗██║      ██╔════╝██║     ██║
██╔██╗ ██║        ██╔████╔██║ █████╔╝██║   ██║╚█████╔╝██║  ██║██║█████╗██║     ██║     ██║
██║╚██╗██║        ██║╚██╔╝██║ ╚═══██╗██║   ██║██╔══██╗██║  ██║██║╚════╝██║     ██║     ██║
██║ ╚████║███████╗██║ ╚═╝ ██║██████╔╝╚██████╔╝╚█████╔╝██████╔╝███████╗ ╚██████╗███████╗██║
╚═╝  ╚═══╝╚══════╝╚═╝     ╚═╝╚═════╝  ╚═════╝  ╚════╝ ╚═════╝ ╚══════╝  ╚═════╝╚══════╝╚═╝
                                                                                          
```
---
[![img](https://img.shields.io/github/stars/nilaoda/N_m3u8DL-CLI?label=%E7%82%B9%E8%B5%9E)](https://github.com/nilaoda/N_m3u8DL-CLI)  [![img](https://img.shields.io/github/last-commit/nilaoda/N_m3u8DL-CLI?label=%E6%9C%80%E8%BF%91%E6%8F%90%E4%BA%A4)](https://github.com/nilaoda/N_m3u8DL-CLI)  [![img](https://img.shields.io/github/release/nilaoda/N_m3u8DL-CLI?label=%E6%9C%80%E6%96%B0%E7%89%88%E6%9C%AC)](https://github.com/nilaoda/N_m3u8DL-CLI/releases)  [![img](https://img.shields.io/github/license/nilaoda/N_m3u8DL-CLI?label=%E8%AE%B8%E5%8F%AF%E8%AF%81)](https://github.com/nilaoda/N_m3u8DL-CLI)  [![img](https://img.shields.io/badge/URL-%E7%94%A8%E6%88%B7%E6%96%87%E6%A1%A3-blue)](https://nilaoda.github.io/N_m3u8DL-CLI/)


# [ENGLISH VERSION](https://github.com/nilaoda/N_m3u8DL-CLI/blob/master/README_ENG.md)

# 下载使用
* 发行版: https://github.com/nilaoda/N_m3u8DL-CLI/releases
* 自动构建版`(供测试)`: https://github.com/nilaoda/N_m3u8DL-CLI/actions
 
# 关于开源
本项目已于2019年10月9日开源，采用MIT许可证，各取所需。

# 关于跨平台
* N_m3u8DL-CLI `(本项目)`: 基于 .NET Framework, 不具备跨平台能力. 目前已进入维护阶段.

* [N_m3u8DL-RE](https://github.com/nilaoda/N_m3u8DL-RE) : 抛弃历史包袱从零做起, 支持Win/Linux/Mac, 更丰富的功能会在这里出现 ...

# N_m3u8DL-CLI
一个**简单易用的**m3u8下载器，下载地址：https://github.com/nilaoda/N_m3u8DL-CLI/releases  

支持下载m3u8链接或文件为`mp4`或`ts`格式，并提供丰富的命令行选项。
  * **不支持**优酷视频解密
  * **不支持**气球云视频解密
  * 支持`AES-128-CBC`加密自动解密
  * 支持多线程下载
  * 支持下载限速
  * 支持断点续传
  * 支持`Master List`
  * 支持直播流录制(`BETA`)
  * 支持自定义`HTTP Headers`
  * 支持自动合并 (二进制合并或使用ffmpeg合并)
  * 支持选择下载`m3u8`中的指定时间段/分片内容
  * 支持下载路径为网络驱动器的情况
  * 支持下载外挂字幕轨道、音频轨道
  * 支持仅合并为音频
  * 支持设置特定http代理
  * 支持自动使用系统代理（默认行为, 可禁止）
  * 支持m3u8dl链接协议（通过web链接调用本机客户端）
  * 提供SimpleG简易的`GUI`生成常用参数



![运行截图](https://nilaoda.github.io/N_m3u8DL-CLI/source/images/%E7%9B%B4%E6%8E%A5%E4%BD%BF%E7%94%A8.gif)  

# 命令行选项
```
N_m3u8DL-CLI

USAGE:

  N_m3u8DL-CLI <URL|JSON|FILE> [OPTIONS]

OPTIONS:

  --workDir                  设定程序工作目录
  --saveName                 设定存储文件名(不包括后缀)
  --baseUrl                  设定Baseurl
  --headers                  设定请求头，格式 key:value 使用|分割不同的key&value
  --maxThreads               (Default: 32) 设定程序的最大线程数
  --minThreads               (Default: 16) 设定程序的最小线程数
  --retryCount               (Default: 15) 设定程序的重试次数
  --timeOut                  (Default: 10) 设定程序网络请求的超时时间(单位为秒)
  --muxSetJson               使用外部json文件定义混流选项
  --useKeyFile               使用外部16字节文件定义AES-128解密KEY
  --useKeyBase64             使用Base64字符串定义AES-128解密KEY
  --useKeyIV                 使用HEX字符串定义AES-128解密IV
  --downloadRange            仅下载视频的一部分分片或长度
  --liveRecDur               直播录制时，达到此长度自动退出软件(HH:MM:SS)
  --stopSpeed                当速度低于此值时，重试(单位为KB/s)
  --maxSpeed                 设置下载速度上限(单位为KB/s)
  --proxyAddress             设置HTTP/SOCKS5代理, 如 http://127.0.0.1:8080
  --enableDelAfterDone       开启下载后删除临时文件夹的功能
  --enableMuxFastStart       开启混流mp4的FastStart特性
  --enableBinaryMerge        开启二进制合并分片
  --enableParseOnly          开启仅解析模式(程序只进行到meta.json)
  --enableAudioOnly          合并时仅封装音频轨道
  --disableDateInfo          关闭混流中的日期写入
  --disableIntegrityCheck    不检测分片数量是否完整
  --noMerge                  禁用自动合并
  --noProxy                  不自动使用系统代理
  --registerUrlProtocol      注册m3u8dl链接协议
  --unregisterUrlProtocol    取消注册m3u8dl链接协议
  --enableChaCha20           enableChaCha20
  --chaCha20KeyBase64        ChaCha20KeyBase64
  --chaCha20NonceBase64      ChaCha20NonceBase64
  --help                     Display this help screen.
  --version                  Display version information.
```

# 关于`m3u8dl://`协议
新增命令行参数：
```
--registerUrlProtocol          注册m3u8dl链接协议
--unregisterUrlProtocol     取消注册m3u8dl链接协议
```

URI格式：
```
m3u8dl://<base64编码的客户端命令行文本>
```

URI示例：
```
m3u8dl://Imh0dHBzOi8vZXhhbXBsZS5jb20vYWJjLm0zdTgiIC0td29ya0RpciAiJVVTRVJQUk9GSUxFJVxEb3dubG9hZHNcbTN1OGRsIiAtLXNhdmVOYW1lICJhYmMiIC0tZW5hYmxlRGVsQWZ0ZXJEb25lIC0tZGlzYWJsZURhdGVJbmZvIC0tbm9Qcm94eQ==
```

URI解码结果：
```
"https://example.com/abc.m3u8" --workDir "%USERPROFILE%\Downloads\m3u8dl" --saveName "abc" --enableDelAfterDone --disableDateInfo --noProxy
```

# 用户文档
https://nilaoda.github.io/N_m3u8DL-CLI/

# 聊聊
https://discord.gg/SSGwKrjC44

# 赞赏
![Wow](https://nilaoda.github.io/N_m3u8DL-CLI/source/images/alipay.png)
