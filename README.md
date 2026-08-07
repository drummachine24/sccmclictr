# Client Center for Configuration Manager

## Project Description
The tool is designed for IT Professionals to troubleshoot ConfigMgr Agent related Issues. The Client Center for Configuration Manager provides a quick and easy overview of client settings, including running services and Agent settings in a good, easy to use user interface.

[![paypal](https://www.paypalobjects.com/en_US/CH/i/btn/btn_donateCC_LG.gif)](https://www.paypal.com/ncp/payment/VEUDU4YDUB3KQ)

## Downloads
### GitHub Releases (recommended)
https://github.com/drummachine24/sccmclictr/releases

Each release includes:
- **Setup.exe** — interactive installer (Inno Setup)
- **MSI** — enterprise / silent install (`msiexec /i ... /qn`)
- **Portable ZIP** — extract and run, or use `Install.cmd`

All packages are self-contained (no separate .NET install required).

### Build from source
```bash
git clone --recurse-submodules https://github.com/drummachine24/sccmclictr.git
dotnet restore SCCMCliCtrWPF/SCCMCliCtrWPF.sln
dotnet build SCCMCliCtrWPF/SCCMCliCtrWPF.sln -c Release
```

Release packaging on Windows (ZIP + MSI + Setup.exe):

```powershell
.\tools\Build-Release.ps1 -Version 1.1.6
```

Requires [WiX Toolset](https://wixtoolset.org/) CLI (`dotnet tool install -g wix`) and [Inno Setup 6](https://jrsoftware.org/isinfo.php) for the MSI/EXE outputs.

## Documentation
https://github.com/rzander/sccmclictr/wiki

## Requirements
* Windows Remote Management (WinRM) must be enabled and configured on all target computers. (Run `winrm quickconfig` in a command prompt.)
* **Self-contained release:** no separate .NET install required on the Client Center machine
* **Framework-dependent / source builds:** .NET 10 Desktop Runtime / SDK (see `global.json`; TFM `net10.0-windows`)
* Configuration Manager Agent on the target computer
* Admin rights on the target computer
* PowerShell remoting on the target computer (Windows PowerShell 5.1 is the typical WinRM endpoint)

## Tested on:
* Windows 10 / Windows 11 x64
* Windows Server 2016+
