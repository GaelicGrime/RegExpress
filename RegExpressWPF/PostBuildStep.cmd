@echo off

set ConfigurationName=%~1
set PlatformName=%~2
set TargetDir=%~3
set ThisCmdPath=%~dp0

rem echo %ConfigurationName%
rem echo %PlatformName%
rem echo %ThisCmdPath%

set MainProjectPath=%ThisCmdPath%
rem echo %MainProjectPath%


rem -- ICU --

set Prj=%MainProjectPath%\..\RegexEngines\Icu\IcuRegexInterop

xcopy /D /R /Y "%Prj%\ICU-min\bin64\*.dll" "%TargetDir%\ICU-min\bin64\*"


rem -- Perl --

set Prj=%MainProjectPath%\..\RegexEngines\Perl\PerlRegexEngine

xcopy /E /D /R /Y "%Prj%\Perl-min\*" "%TargetDir%\Perl-min\*"
 

rem -- Python --

set Prj=%MainProjectPath%\..\RegexEngines\Python\PythonRegexEngine

xcopy /E /D /R /Y "%Prj%\Python-embed\*" "%TargetDir%\Python-embed\*"


rem -- Rust --

set Prj=%MainProjectPath%\..\RegexEngines\Rust\RustClient

xcopy /D /R /Y "%Prj%\target\debug\RustClient.exe" "%TargetDir%\*.bin"
