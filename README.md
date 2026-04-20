# SaveGame Folder Backup

**Helps to create a scheduled or manual backup of common Save Game Folder locations**

You may use this application at your own risk. This is a personal project.

---

## Overview

**Local Folder Backup Manager** is a **.NET 10 WPF application** that lets users schedule or manually trigger backups of important folders—ideal for saving game progress, documents, or any data you don't want to lose.
This is a successor to the old CLI backup utlity Save_Game_Backup_Manager: https://github.com/supersharp/SaveGame_Folder_Backup 

<img width="1100" height="700" alt="Dashboard" src="https://github.com/user-attachments/assets/b1d38d84-92f7-4383-90e1-7315ef3c2984" />

---

## Features

- **WPF Desktop Interface:** Easy-to-use UI built with modern .NET 10 and WPF.
- **Flexible Scheduling:** Create one-time or recurring backup schedules using the Windows Task Manager/Task Scheduler.
- **Manual Backups:** Instantly back up important folders at any time.
- **Customizable Filters:** Include/exclude specific file types or subfolders as needed.
- **Resource Friendly:** No persistent services or tray apps—tasks wake the app only when needed.
- **Secure and Reliable:** Runs only on demand via Task Scheduler triggers.

---

## How It Works

1. **Configure Backup Tasks:**  
   - Select one or more folders.
   - Optionally set up filters to include/exclude particular folders.

2. **Schedule or Manual:**  
   - Create backup schedules with flexible triggers: daily, weekly, on login, etc.
   - Or, run a backup task manually at any time.

3. **Windows Task Scheduler Integration:**  
   - Each scheduled backup is registered as a separate Windows Task.
   - Tasks trigger the backup app with the relevant task name and settings—no need for background processes.

4. **Zero Background Services:**  
   - Unlike most backup tools, this app does not run any background or system tray services.
   - The .exe is executed only as a result of Task Scheduler events or direct user action.
5. **No telemetry - No internet access required.:**  

---

## Application previews:

[Check the screenshots folder](https://github.com/supersharp/LocalFolderBackupManager/tree/master/Screenshots) 

---
## Getting Started

### Prerequisites

- Windows 10 or higher
- [.NET 10 Runtime](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) it's already bundled in the EXE, so seperate install is not required.
- Requires Admin permissionns

### Installation

1. **Download** the latest release from [Releases](https://github.com/supersharp/LocalFolderBackupManager/releases).
2. Move the EXE to the location you'll like to keep it, since the tasks created by the app will refer to the .EXE location.
3. **Run** `LocalFolderBackupManager`.

### Usage

1. **Add folders** you want to back up.
2. **Choose filters** (optional) to include/exclude files.
3. **Create a schedule** for automated backups or run a backup manually.
4. **Scheduled backups** will appear in the Windows Task Scheduler and execute as per your defined triggers.

---

## FAQ

**Q:** Does this app require administrator privileges?  
**A:** Only for tasks that need elevated permissions, or to access protected folders. Basic use may not require admin rights.

**Q:** Are there any background services?  
**A:** No. Backups are performed only when triggered—there’s nothing running constantly.

**Q:** How does restoring backups work?  
**A:** You can press the restore now button in the app dashboard.

---

## License

[MIT](LICENSE)

---

## Contributing

PRs are welcome! Please open an issue to discuss new features or report bugs.

---

## Credits

Created by [supersharp](https)
