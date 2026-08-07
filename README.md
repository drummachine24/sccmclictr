# Client Center for Configuration Manager

## Project Description
The tool is designed for IT Professionals to troubleshoot ConfigMgr Agent related Issues. The Client Center for Configuration Manager provides a quick and easy overview of client settings, including running services and Agent settings in a good, easy to use user interface.

[![paypal](https://www.paypalobjects.com/en_US/CH/i/btn/btn_donateCC_LG.gif)](https://www.paypal.com/cgi-bin/webscr?cmd=_s-xclick&hosted_button_id=TLTFJHYA69VHU)

## Downloads
### offline Installer
https://github.com/drummachine24/sccmclictr/releases

### Build from source
```bash
git clone --recurse-submodules https://github.com/drummachine24/sccmclictr.git
dotnet restore SCCMCliCtrWPF/SCCMCliCtrWPF.sln
dotnet build SCCMCliCtrWPF/SCCMCliCtrWPF.sln -c Release
```

Self-contained release packaging (Windows): `tools/Build-Release.ps1`

## Documentation
https://github.com/rzander/sccmclictr/wiki

## Requirements
* Windows Remote Management (WinRM) must be enabled and configured on all target computers. (Run `winrm quickconfig` in a command prompt.)
* **.NET 10 Desktop Runtime / SDK** on the computer running the tool (see `global.json`; target TFM is `net10.0-windows`)
* Configuration Manager Agent on the target computer
* Admin rights on the target computer
* PowerShell remoting on the target computer (Windows PowerShell 5.1 is the typical WinRM endpoint)

## Tested on:
* Windows 10 / Windows 11 x64
* Windows Server 2016+
