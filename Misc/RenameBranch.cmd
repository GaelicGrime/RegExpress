@echo off
rem See: https://linuxize.com/post/how-to-rename-local-and-remote-git-branch

set OLDNAME=%1
set NEWNAME=%2

echo Renaming "%OLDNAME%" to "%NEWNAME%"
pause

@echo on

git checkout %OLDNAME%
@if ERRORLEVEL 1 goto error

git branch -m %NEWNAME%
@if ERRORLEVEL 1 goto error

git push origin -u %NEWNAME%
@if ERRORLEVEL 1 goto error

git push origin --delete %OLDNAME%
@if ERRORLEVEL 1 goto error

@goto:eof

:error
@echo AN ERROR OCCURED
pause
@goto:eof
