@echo off
set myparams=m "(?<na>a)(?<nb>b)?(?<nc>c)" "i" "ac"
echo Params: %myparams%
..\x64\Debug\WebView2Client.exe %myparams% 1>o 2>e & type o & echo --- & type e

