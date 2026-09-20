@echo off
REM =====================================================
REM  本地文件服务器 - 一键构建发布脚本
REM  使用用户目录安装的 .NET 10 SDK 编译并发布单文件 exe
REM =====================================================
setlocal

set "DOTNET_ROOT=C:\Users\Entruv\.dotnet"
set "PATH=%DOTNET_ROOT%;%PATH%"

echo [1/3] 还原并编译...
call "%DOTNET_ROOT%\dotnet.exe" build -c Release -p:UseWPF=true || goto :err
echo.
echo [2/3] 发布单文件...
call "%DOTNET_ROOT%\dotnet.exe" publish -c Release -r win-x64 --self-contained false -o "%~dp0publish" || goto :err
echo.
echo [3/3] 完成!
echo 输出目录: %~dp0publish
dir /b "%~dp0publish\*.exe" 2>nul
goto :eof

:err
echo.
echo 构建失败，请检查上面的错误信息。
exit /b 1
