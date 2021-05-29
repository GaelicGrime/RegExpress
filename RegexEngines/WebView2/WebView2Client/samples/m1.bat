@echo off
set myparams=m "(.)(.)(?<n1>.)(?<n2>.)" "i" "abcd"
echo Params: %myparams%
..\x64\Debug\WebView2Client.exe %myparams% 1>o 2>e & type o & echo --- & type e

