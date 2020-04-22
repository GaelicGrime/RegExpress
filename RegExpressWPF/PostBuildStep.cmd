@echo off

set ConfigurationName=%~1
set PlatformName=%~2
set TargetDir=%~3
set ThisCmdPath=%~dp0

echo %ConfigurationName%
echo %PlatformName%
echo %ThisCmdPath%

set MainProjectPath=%ThisCmdPath%
echo %MainProjectPath%


rem -- ICU --

set Prj=%MainProjectPath%\..\RegexEngines\Icu\IcuRegexInterop

xcopy /d "%Prj%\ICU-min\bin64\*.dll" "%TargetDir%\ICU-min\bin64\*"


rem -- Perl 5 --

set Prj=%MainProjectPath%\..\RegexEngines\Perl5\Perl5RegexEngine

xcopy /E /D "%Prj%\Perl5-min\*" "%TargetDir%\Perl5-min\*"
 