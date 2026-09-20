# 本地文件服务器 (LocalFileServer)

一个 Windows 桌面小工具：在本机起一个 HTTP 文件服务器，局域网内任何设备（路由器、手机、其他电脑）通过浏览器即可 **浏览 / 下载 / 上传** 共享目录里的文件。

![.NET](https://img.shields.io/badge/.NET-10.0-blue) ![Platform](https://img.shields.io/badge/platform-Windows-lightgrey)

## 特性

- 绿色单文件 exe，免安装，双击即用
- 自动列出本机所有网卡 IP，一键选择
- 可选监听端口（默认 8848）与访问口令（token）
- 网页界面：文件列表、一键复制 `wget` 命令、拖拽上传、删除文件
- 口令模式下抓取命令自动附带 `?token=xxx`

## 快速开始

1. 从 [Releases](https://github.com/Entruv1/LocalFileServer/releases) 下载 `LocalFileServer-*-win-x64.exe`
2. 双击运行，选择 IP / 端口 / 共享目录，点 **启动服务**
3. 浏览器打开 `http://<本机IP>:8848/` 即可访问

### 路由器 / SSH 场景

```sh
wget -O /root/script.sh http://192.168.x.x:8848/script.sh
sh /root/script.sh
```

## 从源码构建

```sh
dotnet publish LocalFileServer/LocalFileServer.csproj -c Release -r win-x64 --self-contained false -o publish
```

需要 .NET 10 SDK，产物为 `publish/本地文件服务器.exe`。

也可以打 `v*` 标签推送，GitHub Actions 会自动构建并附到 Release。

## 技术栈

C# / WPF / .NET 10，自实现 HTTP 文件服务（`FileServer.cs`），网页界面内嵌（`WebResources.cs`）。

详细使用说明见 [本地文件服务器使用说明.md](本地文件服务器使用说明.md)。
