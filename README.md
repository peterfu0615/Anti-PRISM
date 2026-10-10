# Anti-PRISM

**Anti-PRISM** 是面向 Windows 10/11 的企业级隐身摄像头防御与进程猎杀系统。通过「前台隐身托盘 + 后台 SYSTEM 服务」的双进程解耦架构，实现开机登录前防御、驱动级硬件禁用、独占捕获锁死、亚 500ms 进程黑名单猎杀以及高隐蔽性的“假亮度条”伪装控制手势。

---

## ⚡ 一键极速安装（在任何一台 Windows 上）

只需打开 **CMD** 或 **PowerShell**（会自动请求管理员 UAC 提权），粘贴并运行以下一行命令即可全自动下载、安装并启动防护：

### 方式 1：PowerShell（推荐）
```powershell
irm https://raw.githubusercontent.com/peterfu0615/Anti-PRISM/main/scripts/install.ps1 | iex
```

### 方式 2：CMD（命令提示符）
```cmd
powershell -ExecutionPolicy Bypass -NoProfile -Command "irm https://raw.githubusercontent.com/peterfu0615/Anti-PRISM/main/scripts/install.ps1 | iex"
```

> **自动完成事项**：
> 1. 自动从 GitHub Release 下载最新的 `AntiPrism_Setup.exe`。
> 2. 静默安装到 `C:\Program Files\Anti-PRISM\`。
> 3. 注册并立即以 SYSTEM 权限在 Session 0 启动 `AntiPrismSvc` 守护服务。
> 4. 创建开机自启计划任务并在当前桌面会话启动伪装托盘 `BrightnessCtl.exe`。
> 5. 提示安装成功，摄像头全面受保护。

---

## 架构概览

```
Installer (Inno Setup, Admin)
└─ C:\Program Files\Anti-PRISM\
   ├─ AntiPrism.Svc.exe   # Windows Service, SYSTEM, Automatic, Session 0 无 UI
   │   └─ 职责: 枚举 / 独占占用 / SetupDi 禁用摄像头 + 5s 看门狗 + 进程黑名单猎杀 + 日志
   ├─ BrightnessCtl.exe   # 用户登录后启动, WPF, 隐蔽托盘 (中性命名)
   │   └─ 职责: 假亮度条 + 61 震荡手势识别 + 应急热键 + 真控制面板 + Pipe 交互
   └─ AntiPrism.Core.dll  # 设备控制 / MF / SetupDi / DPAPI 加密 / IPC 公共库
```

- **IPC 通信**: 命名管道 `\\.\pipe\AntiPrism`，双向长度前缀 JSON 协议，跨会话 ACL 权限控制（SYSTEM + Administrators + Users）。
- **配置持久化**: `C:\ProgramData\Anti-PRISM\config.json`，采用 Windows DPAPI (`DataProtectionScope.LocalMachine`) 机器级加密。
- **运行日志**: `C:\ProgramData\Anti-PRISM\logs\` 滚动日志文件 + Windows EventLog。
- **开机自启**:
  - `AntiPrism.Svc`: Windows 服务 (`start= auto`)。
  - `BrightnessCtl`: Task Scheduler 计划任务 (`At logon of any user`, `Run with highest privileges`, `--hidden`)。

---

## 核心防御机制

### M1: 摄像头独占占用（轻模式）
- 基于 `Windows.Media.Capture.MediaCapture` (`SharingMode = ExclusiveControl`)。
- 初始化并在后台独占视频捕获通道，不渲染大画面，零资源开销。
- 第三方应用程序（系统相机、OBS、Zoom 等）尝试打开摄像头时将直接触发 `0xC00D3704` (`MF_E_HW_MFT_FAILED_CREATE_DUE_TO_ALREADY_RESERVED` / 设备正忙)。
- 5 秒看门狗自动监听拔插与句柄丢弃，10 秒内自动重新夺取独占控制权。

### M2: 驱动级设备禁用（深度模式）
- 通过 `setupapi.dll` P/Invoke 直接向内核 PnP 驱动栈发送 `DIF_PROPERTYCHANGE` (`DICS_DISABLE` / `DICS_ENABLE`)。
- 设备管理器中摄像头直接显示为向下箭头禁用状态，任何软件物理不可见。
- **安全防变砖机制**：卸载程序（Inno Setup）及管理面板均包含 **EMERGENCY UNBRICK** 逻辑，强制重新枚举并唤醒所有被禁用的摄像头设备。

### M3: 登录前保护
- Windows Service 在 `NT AUTHORITY\SYSTEM` 权限下开机自启（Session 0）。
- 绕过登录阶段，在用户输入锁屏密码前即执行 M2 驱动禁用或 M1 占用，彻底杜绝开机瞬间的摄像头窥探。

### M5: 进程黑名单猎杀引擎
- **实时捕获**：基于 WMI `Win32_ProcessStartTrace` 事件通知，亚 500 毫秒内拦截黑名单进程启动。
- **兜底轮询**：1 秒后台进程快照兜底，防止极端高频启动漏判。
- **猎杀规则**：
  - Win32 进程：优先绝对路径完全匹配，次级文件名不区分大小写匹配，直接调用 `Process.Kill(entireProcessTree: true)` 连同子进程树一同抹除。
  - UWP 商店应用：自动写入注册表 `HKCU\...\CapabilityAccessManager\ConsentStore\webcam\<PackageFamilyName>` 设置为 `Deny`。

### M6: 伪装亮度条与 61 号手势识别
- 托盘图标伪装为常规系统太阳图标，Tooltip 为 `Display Brightness`。
- 左键点击弹出仿 Windows 11 Fluent 风格的假亮度条（0~100 滑动条），拖动仅改变界面数字，绝不调用任何 WMI/DDC 真实背光接口。
- **触发算法**：
  - 10 秒时间窗口内，滑动条进入 `61`（沿触发，即从非 61 划入 61）累计满 `6` 次，立即唤出真正的 Anti-PRISM 控制面板。
  - 停留在 61 不动或滑动到 60/62 均不触发。
- **应急逃生门**：注册全局热键 `Ctrl + Alt + Shift + F12`，防止手势操作意外锁死。

---

## 编译与测试说明

### 开发与编译
- 解决方案：`Anti-PRISM.sln`（.NET 8 SDK，Windows 10.0.19041+，x64）。
- 本地编译命令（在 Windows 环境或 GitHub Actions 中）：
  ```powershell
  dotnet restore Anti-PRISM.sln
  dotnet build Anti-PRISM.sln -c Release
  dotnet test Anti-PRISM.sln -c Release
  ```

### 自包含单文件发布
```powershell
# 发布 Service
dotnet publish src/AntiPrism.Svc/AntiPrism.Svc.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist/publish

# 发布 Tray (BrightnessCtl)
dotnet publish src/AntiPrism.Tray/AntiPrism.Tray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist/publish
```

### 安装包制作
安装 Inno Setup 6 后执行：
```powershell
iscc installer/install.iss
```
生成安装包位于 `dist/installer/AntiPrism_Setup.exe`。

---

## 验证与测试矩阵

| 测试项 | 验证步骤 | 预期结果 |
| :--- | :--- | :--- |
| **M1 独占占用** | 在真面板选择相机并置为 Occupy 模式；打开 OBS / 系统相机 | 报忙 `0xC00D3704`，黑屏无法开流；拔插摄像头 10s 内自动恢复锁死 |
| **M2 硬件禁用** | 在真面板置为 Disable 模式；打开设备管理器 | 摄像头显示向下禁用箭头；任何软件均提示找不到摄像头硬件 |
| **M3 登录前生效** | 重启 Windows 11 VM，停留在锁屏登录界面 | 远程通过 `sc query AntiPrismSvc` 显示 RUNNING；摄像头处于禁用/被占状态 |
| **M5 进程猎杀** | 黑名单添加 `notepad.exe` 或 `obs64.exe` 并启动 | 500ms 内进程树被强制终止；后台服务日志记录猎杀 PID 与时间戳 |
| **M6 假条与手势** | 点击托盘弹出假亮度条；在 61 附近反复划入 6 次 | 6 次划入瞬间立即弹出真控制面板；停留在 61 或划动到 60/62 不开 |
| **逃生热键** | 按下 `Ctrl + Alt + Shift + F12` | 无论滑动条处于何种状态，立刻强制唤出控制面板 |
| **卸载防变砖** | 将摄像头置于 Disable 模式，运行卸载程序 | 卸载脚本调用 `--unbrick`，设备管理器中所有摄像头均恢复为正常启用状态 |
