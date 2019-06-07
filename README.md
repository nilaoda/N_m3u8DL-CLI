# N_m3u8DL-CLI
一个m3u8下载器
## 基本用法
双击exe，然后输入m3u8链接或拖入m3u8文件或拖入本程序生成的json文件按下回车键。

## 高级用法
使用命令行参数
```
N_m3u8DL-CLI.exe <URL|JSON> [OPTIONS]  

    --workDir    Directory  设定程序工作目录
    --saveName   Filename   设定存储文件名(不包括后缀)
    --baseUrl    BaseUrl    设定Baseurl
    --headers    headers    设定请求头，格式 key:value 使用|分割不同的key&value
    --maxThreads Thread     设定程序的最大线程数(默认为32)
    --minThreads Thread     设定程序的最小线程数(默认为16)
    --retryCount Count      设定程序的重试次数(默认为15)
    --muxSetJson File       使用外部json文件定义混流选项
    --enableDelAfterDone    开启下载后删除临时文件夹的功能
    --enableMuxFastStart    开启混流mp4的FastStart特性
    --enableBinaryMerge     开启二进制合并分片
    --enableParseOnly       开启仅解析模式(程序只进行到meta.json)
    --disableDateInfo       关闭混流中的日期写入

```
附录：一个典型的混流Json文件应该长这个样子：
```
    {
        "muxFormat": "mp4", 
        "fastStart": "True", 
        "poster": "文件路径，\需写为\\", 
        "audioName": "", 
        "title": "", 
        "copyright": "", 
        "comment": ""
    }
```
