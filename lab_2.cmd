@ECHO OFF
SETLOCAL ENABLEDELAYEDEXPANSION

REM --- 1. Призначення аргументів командного рядка ---
SET "LOG_FILE=%1"
SET "SOURCE_DIR=%2"
SET "PROCESS_NAME=%3"
SET "ARCHIVE_DIR=%4"
SET "REMOTE_IP=%5"
SET "MAX_LOG_SIZE_BYTES=%6"

REM === КОНФІГУРАЦІЯ EMAIL ===
IF NOT EXIST "admin.txt" (
    ECHO.
    ECHO ERROR: Configuration file admin.txt not found!
    ECHO Script aborted.
    ECHO.
    EXIT /B 1
)

FOR /F "tokens=1* delims==" %%A IN (admin.txt) DO (
    IF "%%A"=="SMTP_SERVER" SET "SMTP_SERVER=%%B"
    IF "%%A"=="SENDER_EMAIL" SET "SENDER_EMAIL=%%B"
    IF "%%A"=="SENDER_PASSWORD" SET "SENDER_PASSWORD=%%B"
    IF "%%A"=="ADMIN_EMAIL" SET "ADMIN_EMAIL=%%B"
)
REM =================================================

REM --- 2, 3. Перевірка, створення та початковий запис у log-файл ---
CALL :LOG "--- Script Start ---"
IF NOT EXIST "%LOG_FILE%" (
    CALL :LOG "Log file %LOG_FILE% not found. Creating new."
    ECHO. > "%LOG_FILE%"
) ELSE (
    CALL :LOG "Log file %LOG_FILE% opened."
)

REM --- 4. Синхронізація часу з NTP ---
CALL :LOG "Attempting time sync with NTP-server (pool.ntp.org)..."
w32tm /config /manualpeerlist:"pool.ntp.org" /syncfromflags:manual /update
w32tm /resync /force
CALL :LOG "Time sync command executed."
CALL :LOG "Updated time: %DATE% %TIME%"

REM --- 5. Список запущених процесів ---
CALL :LOG "List of running processes:"
CALL :LOG "--- Processes List Start ---"
tasklist >> "%LOG_FILE%"
CALL :LOG "--- Processes List End ---"

REM --- 6. Завершення процесу ---
CALL :LOG "Attempting to terminate process: %PROCESS_NAME%..."
taskkill /F /IM "%PROCESS_NAME%" /T
IF !ERRORLEVEL! EQU 0 (
    CALL :LOG "Process %PROCESS_NAME% and its children successfully terminated."
) ELSE (
    CALL :LOG "Error: Failed to terminate process %PROCESS_NAME% (maybe not running)."
)

REM --- 7. Видалення тимчасових файлів ---
SET "DEL_COUNT=0"
CALL :LOG "Deleting files *.TMP and temp*.* from %SOURCE_DIR%..."
FOR %%F IN ("%SOURCE_DIR%\*.TMP") DO (
    DEL /F /Q "%%F"
    SET /A DEL_COUNT+=1
)
FOR %%F IN ("%SOURCE_DIR%\temp*.*") DO (
    IF EXIST "%%F" (
        DEL /F /Q "%%F"
        IF !ERRORLEVEL! EQU 0 SET /A DEL_COUNT+=1
    )
)
CALL :LOG "Deleted temporary files: !DEL_COUNT!."

REM --- 8, 9. Стиснення та переміщення архіву ---
FOR /F "usebackq" %%I IN (`powershell -NoProfile -Command "Get-Date -Format 'yyyy-MM-dd_HH-mm-ss'"`) DO SET "TIMESTAMP=%%I"
SET "ARCHIVE_NAME=%TIMESTAMP%.zip"
SET "TEMP_ARCHIVE_PATH=%TEMP%\%ARCHIVE_NAME%"

CALL :LOG "Creating archive from %SOURCE_DIR%..."
powershell.exe -NoProfile -Command "Compress-Archive -Path '%SOURCE_DIR%\*' -DestinationPath '%TEMP_ARCHIVE_PATH%' -Force"

IF !ERRORLEVEL! EQU 0 (
    CALL :LOG "Archive %ARCHIVE_NAME% successfully created in temp folder."
    CALL :LOG "Moving archive to %ARCHIVE_DIR%..."
    powershell.exe -NoProfile -Command "Move-Item -Path '%TEMP_ARCHIVE_PATH%' -Destination '%ARCHIVE_DIR%'"
    IF !ERRORLEVEL! EQU 0 (
        CALL :LOG "Archive successfully moved to %ARCHIVE_DIR%."
    ) ELSE (
        CALL :LOG "ERROR: Failed to move archive to %ARCHIVE_DIR%."
    )
) ELSE (
    CALL :LOG "ERROR: Failed to create archive."
)

REM --- 10, 11. Перевірка архіву за минулий день ---
CALL :LOG "Checking for yesterday's archive..."
FOR /F %%a IN ('powershell -c "(Get-Date).AddDays(-1).ToString('yyyy-MM-dd')"') DO SET "YESTERDAY=%%a"

CALL :LOG "Yesterday's date: %YESTERDAY%."
IF EXIST "%ARCHIVE_DIR%\*%YESTERDAY%*.zip" (
    CALL :LOG "Archive for %YESTERDAY% found."
) ELSE (
    CALL :LOG "WARNING: Archive for %YESTERDAY% not found."
    CALL :LOG "Sending email notification..."
    powershell.exe -NoProfile -Command "$Cred = New-Object System.Management.Automation.PSCredential('%SENDER_EMAIL%', (ConvertTo-SecureString '%SENDER_PASSWORD%' -AsPlainText -Force)); Send-MailMessage -From '%SENDER_EMAIL%' -To '%ADMIN_EMAIL%' -Subject 'Warning: Missing Yesterday Archive' -Body 'Script failed to find archive for %YESTERDAY% in %ARCHIVE_DIR%.' -SmtpServer '%SMTP_SERVER%' -UseSsl -Port 587 -Credential $Cred -Encoding 'UTF8'"
    IF !ERRORLEVEL! EQU 0 (
        CALL :LOG "Email notification sent."
    ) ELSE (
        CALL :LOG "ERROR: Failed to send email."
    )
)

REM --- 12. Видалення архівів, старших 30 днів ---
CALL :LOG "Deleting archives older than 30 days from %ARCHIVE_DIR%..."
forfiles /P "%ARCHIVE_DIR%" /M "*.zip" /D -30 /C "cmd /c CALL :LOG \"Deleting old archive @file...\" && DEL @path"
CALL :LOG "Old archives cleanup finished."

REM --- 13. Перевірка підключення до Internet ---
CALL :LOG "Checking Internet connection (ping 8.8.8.8)..."
ping -n 1 8.8.8.8 | FIND "TTL=" > NUL
IF !ERRORLEVEL! EQU 0 (
    CALL :LOG "Internet connection is OK."
) ELSE (
    CALL :LOG "Internet connection is MISSING."
)

REM --- 14. Перевірка та вимкнення віддаленого ПК ---
CALL :LOG "Checking remote PC status %REMOTE_IP%..."
ping -n 1 %REMOTE_IP% | FIND "TTL=" > NUL
IF !ERRORLEVEL! EQU 0 (
    CALL :LOG "PC %REMOTE_IP% is ONLINE. Sending shutdown command..."
    shutdown /m \\%REMOTE_IP% /s /t 60 /c "Planned shutdown by administrator"
    CALL :LOG "Shutdown command sent to %REMOTE_IP%."
) ELSE (
    CALL :LOG "PC %REMOTE_IP% is OFFLINE (no ping response)."
)

REM --- 15. Список комп'ютерів в мережі ---
CALL :LOG "Getting network computers list (net view):"
CALL :LOG "--- Net View List Start ---"
net view >> "%LOG_FILE%"
CALL :LOG "--- Net View List End ---"

REM --- 16. Перевірка IP з файлу ipon.txt ---
SET "IP_FILE=ipon.txt"
CALL :LOG "Checking IP addresses from %IP_FILE%..."
IF NOT EXIST "%IP_FILE%" (
    CALL :LOG "ERROR: File %IP_FILE% not found. Check skipped."
) ELSE (
    SET "OFFLINE_IPS="
    FOR /F "tokens=*" %%I IN (%IP_FILE%) DO (
        ping -n 1 %%I | FIND "TTL=" > NUL
        IF !ERRORLEVEL! NEQ 0 (
            CALL :LOG "WARNING: IP address %%I from %IP_FILE% is OFFLINE."
            SET "OFFLINE_IPS=!OFFLINE_IPS! %%I"
        ) ELSE (
            CALL :LOG "IP address %%I from %IP_FILE% is ONLINE."
        )
    )
    
    IF NOT "!OFFLINE_IPS!"=="" (
        CALL :LOG "Sending email notification about OFFLINE IPs..."
        powershell.exe -NoProfile -Command "$Cred = New-Object System.Management.Automation.PSCredential('%SENDER_EMAIL%', (ConvertTo-SecureString '%SENDER_PASSWORD%' -AsPlainText -Force)); Send-MailMessage -From '%SENDER_EMAIL%' -To '%ADMIN_EMAIL%' -Subject 'Warning: Offline IP Addresses' -Body 'The following IP addresses from %IP_FILE% are offline:!OFFLINE_IPS!' -SmtpServer '%SMTP_SERVER%' -UseSsl -Port 587 -Credential $Cred -Encoding 'UTF8'"
        IF !ERRORLEVEL! EQU 0 (
            CALL :LOG "Email notification about OFFLINE IPs sent."
        ) ELSE (
            CALL :LOG "ERROR: Failed to send email about OFFLINE IPs."
        )
    )
)

REM --- 17. Перевірка розміру log-файлу ---
CALL :LOG "Checking log file size..."
FOR %%F IN ("%LOG_FILE%") DO SET "LOG_SIZE=%%~zF"
CALL :LOG "Current %LOG_FILE% size: %LOG_SIZE% bytes. Max: %MAX_LOG_SIZE_BYTES% bytes."

IF "%LOG_SIZE%" GTR "%MAX_LOG_SIZE_BYTES%" (
    CALL :LOG "WARNING: Log file size exceeds limit (%MAX_LOG_SIZE_BYTES% bytes)."
    powershell.exe -NoProfile -Command "$Cred = New-Object System.Management.Automation.PSCredential('%SENDER_EMAIL%', (ConvertTo-SecureString '%SENDER_PASSWORD%' -AsPlainText -Force)); Send-MailMessage -From '%SENDER_EMAIL%' -To '%ADMIN_EMAIL%' -Subject 'Warning: Log File Overflow' -Body 'File %LOG_FILE% exceeded limit. Current size: %LOG_SIZE% bytes.' -SmtpServer '%SMTP_SERVER%' -UseSsl -Port 587 -Credential $Cred -Encoding 'UTF8'"
    IF !ERRORLEVEL! EQU 0 (
        CALL :LOG "Email notification about log file size sent."
    ) ELSE (
        CALL :LOG "ERROR: Failed to send email about log file size."
    )
) ELSE (
    CALL :LOG "Log file size is within limits."
)

REM --- 18. Перевірка вільного місця на дисках ---
CALL :LOG "Free and total disk space info on local drives:"
CALL :LOG "--- Disk Info (PowerShell) Start ---"
powershell -NoProfile -Command "Get-WmiObject -Class Win32_LogicalDisk -Filter 'DriveType=3' | Format-Table DeviceID, @{Name='FreeSpace (GB)'; Expression={[int]($_.FreeSpace / 1GB)}}, @{Name='Size (GB)'; Expression={[int]($_.Size / 1GB)}} -AutoSize" >> "%LOG_FILE%"
CALL :LOG "--- Disk Info (PowerShell) End ---"

REM --- 19. Збереження systeminfo ---
SET "SYSINFO_FILE=systeminfo_%TIMESTAMP%.txt"
CALL :LOG "Saving system information to %SYSINFO_FILE%..."
systeminfo > "%SYSINFO_FILE%"
CALL :LOG "System information saved."

CALL :LOG "--- Script Finished ---"
ECHO. >> "%LOG_FILE%"

ENDLOCAL
GOTO :EOF

REM === ПІДПРОГРАМА ЛОГУВАННЯ ===
:LOG
@ECHO OFF
FOR /F "usebackq" %%I IN (`powershell -NoProfile -Command "Get-Date -Format 'yyyy-MM-dd HH:mm:ss'"`) DO SET "CURRENT_TIMESTAMP=%%I"

REM Виведення в консоль та запис у файл
ECHO [%CURRENT_TIMESTAMP%] %~1
ECHO [%CURRENT_TIMESTAMP%] %~1 >> "%LOG_FILE%"
GOTO :EOF