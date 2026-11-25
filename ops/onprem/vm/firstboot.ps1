# First boot of the "on-prem" lab VM (runs once, at the unattend autologon, as Administrator).
# Lab/KVM-specific: turns on OpenSSH with key auth for lab-host and trims background load.
# Application provisioning lives in ops/windows/ and does not depend on this file.
$ErrorActionPreference = 'Continue'
$setup = 'C:\td-setup'
Start-Transcript -Path "$setup\firstboot.log" -Append | Out-Null

function Step($msg) { Write-Output ("[{0:u}] {1}" -f (Get-Date), $msg) }

# --- OpenSSH Server (in-box on Windows Server 2025; capability install as a fallback)
Step 'OpenSSH Server'
if (-not (Get-Service sshd -ErrorAction SilentlyContinue)) {
    $cap = Get-WindowsCapability -Online -Name 'OpenSSH.Server*' | Select-Object -First 1
    if ($cap -and $cap.State -ne 'Installed') { Add-WindowsCapability -Online -Name $cap.Name | Out-Null }
}
Set-Service sshd -StartupType Automatic
Start-Service sshd          # first start creates C:\ProgramData\ssh and the host keys
if (-not (Get-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -ErrorAction SilentlyContinue)) {
    New-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -DisplayName 'OpenSSH Server (sshd)' -Enabled True `
        -Direction Inbound -Protocol TCP -Action Allow -LocalPort 22 | Out-Null
}
# The in-box rule is Private-profile only and slirp's network comes up as Public: allow ssh on every
# profile and mark the lab network Private.
Set-NetFirewallRule -Name 'OpenSSH-Server-In-TCP' -Enabled True -Profile Any
Get-NetConnectionProfile | Set-NetConnectionProfile -NetworkCategory Private
$auth = 'C:\ProgramData\ssh\administrators_authorized_keys'
Copy-Item "$setup\admin_key.pub" $auth -Force
icacls.exe $auth /inheritance:r /grant '*S-1-5-32-544:F' /grant '*S-1-5-18:F' | Out-Null
New-Item -Path 'HKLM:\SOFTWARE\OpenSSH' -Force | Out-Null
New-ItemProperty -Path 'HKLM:\SOFTWARE\OpenSSH' -Name DefaultShell -PropertyType String -Force `
    -Value 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' | Out-Null
Restart-Service sshd

# --- Clean ACPI shutdown from the host (QMP system_powerdown) even with nobody logged on
Step 'Power button'
New-ItemProperty -Path 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System' `
    -Name ShutdownWithoutLogon -PropertyType DWord -Value 1 -Force | Out-Null
powercfg.exe /setacvalueindex SCHEME_CURRENT SUB_BUTTONS PBUTTONACTION 3
powercfg.exe /setactive SCHEME_CURRENT

# --- Lab-only load reduction (this VM may run without KVM, i.e. under TCG emulation):
#     no automatic Windows Update installs, no Defender real-time scanning, no sconfig auto-launch.
Step 'Lab tuning'
$au = 'HKLM:\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU'
New-Item -Path $au -Force | Out-Null
New-ItemProperty -Path $au -Name NoAutoUpdate -PropertyType DWord -Value 1 -Force | Out-Null
try { Set-MpPreference -DisableRealtimeMonitoring $true -ErrorAction Stop } catch { Step "Set-MpPreference: $_" }
try { Set-SConfig -AutoLaunch $false -ErrorAction Stop } catch { Step "Set-SConfig: $_" }

# --- Evaluation activation (starts the 180-day clock if not already started)
Step 'Activation'
cscript.exe //nologo C:\Windows\System32\slmgr.vbs /ato
cscript.exe //nologo C:\Windows\System32\slmgr.vbs /dlv | Out-File "$setup\slmgr-dlv.txt" -Encoding utf8

Step 'Remove Windows Defender feature (lab only), then reboot'
Set-Content -Path "$setup\FIRSTBOOT_DONE" -Value (Get-Date).ToUniversalTime().ToString('u')
Stop-Transcript | Out-Null
try { Uninstall-WindowsFeature -Name Windows-Defender -ErrorAction Stop | Out-File "$setup\defender-removal.txt" } catch { $_ | Out-File "$setup\defender-removal.txt" }
shutdown.exe /r /t 15 /c "td-setup firstboot done"
