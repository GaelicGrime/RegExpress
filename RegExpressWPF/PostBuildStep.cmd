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

set Prj=%MainProjectPath%\..\RegexEngines\Perl5\Perl5RegexInterop

rem TODO: Exclude 'lib\CORE'
xcopy /E /D "%Prj%\Perl5-min\perl\lib\*.*" "%TargetDir%\Perl5-min\perl\lib\*"
xcopy /D "%Prj%\Perl5-min\perl\bin\*.dll" "%TargetDir%\Perl5-min\perl\bin\*"
 