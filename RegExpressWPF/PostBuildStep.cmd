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


rem -- Perl 5 --

set Prj=%MainProjectPath%\..\RegexEngines\Perl5\Perl5RegexEngine

xcopy /E /D /R /Y "%Prj%\Perl5-min\*" "%TargetDir%\Perl5-min\*"
 