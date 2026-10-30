@echo off
rem Resets Vanilla Notes on this Windows account to the state of a first start: deletes the data
rem folder (every profile, note, attachment, and backup) and the machine folder (window position and
rem diagnostics log). Nothing deleted can be recovered.
rem
rem Usage: scripts\cmd\reset.cmd [--yes]
rem   --yes   Deletes without asking for confirmation.
rem
rem The keys of Windows Hello made for private profiles stay in Windows; a profile made later under
rem the same name replaces its key.

setlocal EnableExtensions

set "app_name=Vanilla Notes"
set "exe_name=VanillaNotes.exe"

set "assume_yes="
if "%~1"=="" goto :options_read
if /i "%~1"=="--yes" (
    set "assume_yes=1"
    goto :options_read
)
call :fail "unknown option '%~1'. Usage: reset.cmd [--yes]"
exit /b 1

:options_read
if not defined APPDATA (
    call :fail "APPDATA is not set."
    exit /b 1
)
if not defined LOCALAPPDATA (
    call :fail "LOCALAPPDATA is not set."
    exit /b 1
)

tasklist /FI "IMAGENAME eq %exe_name%" /NH | %SystemRoot%\System32\find.exe /i "%exe_name%" > nul
if not errorlevel 1 (
    call :fail "%app_name% is running. Close it and run the script again."
    exit /b 1
)

set "data_folder=%APPDATA%\%app_name%"
set "machine_folder=%LOCALAPPDATA%\%app_name%"

set "has_data="
set "has_machine="
if exist "%data_folder%\" set "has_data=1"
if exist "%machine_folder%\" set "has_machine=1"

if not defined has_data if not defined has_machine (
    echo Nothing to reset: %app_name% has no data on this account.
    exit /b 0
)

echo This deletes, with no way back:
if defined has_data echo   %data_folder%
if defined has_machine echo   %machine_folder%

if not defined assume_yes (
    set "answer="
    set /p "answer=Type 'reset' to continue: "
)
if not defined assume_yes (
    if not "%answer%"=="reset" (
        call :fail "cancelled; nothing was deleted."
        exit /b 1
    )
)

if defined has_data rd /s /q "%data_folder%"
if defined has_machine rd /s /q "%machine_folder%"

rem rd reports no failure through errorlevel: a folder still there is the sign of one
if exist "%data_folder%\" goto :not_deleted
if exist "%machine_folder%\" goto :not_deleted

echo %app_name% was reset. The next start shows the window of profiles, empty.
exit /b 0

:not_deleted
call :fail "some files could not be deleted. Close any program using them and run the script again."
exit /b 1

:fail
>&2 echo reset: %~1
exit /b 1
