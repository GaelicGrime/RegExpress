@echo off
chcp 65001
set myparams=m "(?<�>�)" "" "�"
echo Params: %myparams%
..\x64\Debug\WebView2Client.exe %myparams% 1>o 2>e & type o & echo --- & type e

