# N_m3u8DL-CLI
一个m3u8下载器
## 基本用法
双击exe输入m3u8链接或拖入m3u8文件按下回车键。

## 高级用法
使用命令行参数
```
N_m3u8DL-CLI.exe <URL> [OPTIONS]  

    --workDir    Directory  设定程序工作目录
    --saveName   Filename   设定存储文件名(不包括后缀)
    --baseUrl    BaseUrl    设定Baseurl
    --maxThreads Thread     设定程序的最大线程数(默认为32)
    --minThreads Thread     设定程序的最小线程数(默认为16)
    --retryCount Count      设定程序的重试次数(默认为15)
    --enableDelAfterDone    开启下载后删除临时文件夹的功能
    --enableMuxFastStart    开启混流mp4的FastStart特性
    --muxSetJson File       使用外部json文件定义混流选项
```
