@echo off

set ConfigurationName=%~1
set ThisCmdPath=%~dp0

rem echo %ConfigurationName%
rem echo %ThisCmdPath%

set SolutionDir=%ThisCmdPath%\..
set TargetDir=%SolutionDir%\RegExpressWPF\bin\x64\%ConfigurationName%

rem echo %SolutionDir%
rem echo %TargetDir%


rem -- ICU --

set Prj=%SolutionDir%\RegexEngines\Icu\IcuRegexInterop

xcopy /D /R /Y "%Prj%\ICU-min\bin64\*.dll" "%TargetDir%\ICU-min\bin64\*"
xcopy /D /R /Y "%Prj%\ICU-min\bin64\*.dll" "%SolutionDir%\x64\%ConfigurationName%\ICU-min\bin64\*"
xcopy /D /R /Y "%SolutionDir%\x64\%ConfigurationName%\IcuClient.exe" "%TargetDir%\*.bin"

rem -- Perl --

set Prj=%SolutionDir%\RegexEngines\Perl\PerlRegexEngine

xcopy /E /D /R /Y "%Prj%\Perl-min\*" "%TargetDir%\Perl-min\*"
 

rem -- Python --

set Prj=%SolutionDir%\RegexEngines\Python\PythonRegexEngine

xcopy /E /D /R /Y "%Prj%\Python-embed\*" "%TargetDir%\Python-embed\*"


rem -- Rust --

set Prj=%SolutionDir%\RegexEngines\Rust\RustClient

xcopy /D /R /Y "%Prj%\target\release\RustClient.exe" "%TargetDir%\*.bin"


rem -- D --

set Prj=%SolutionDir%\RegexEngines\D\DClient

xcopy /D /R /Y "%Prj%\DClient.exe" "%TargetDir%\*.bin"

