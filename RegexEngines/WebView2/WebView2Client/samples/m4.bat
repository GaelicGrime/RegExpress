@echo off
set myparams=m "?error" "" "xyz"
echo Params: %myparams%
..\x64\Debug\WebView2Client.exe %myparams% 1>o 2>e & type o & echo --- & type e

