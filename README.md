# SimpVid Gist (WinUI 3 Port)

> [!NOTE]
> Semi-Vibecoded with Gemini
>
> Synced with [SimpVid Gist WPF](https://github.com/RationalZombie/SimpVid_Gist_WPF) v1.3.3



> [!IMPORTANT]
> This WinUI 3 port has not been updating for several months. Use [SimpVid Gist WPF](https://github.com/RationalZombie/SimpVid_Gist_WPF) instead for the latest improvements.

> [!TIP]
> You have to prepare your own AI API key for summarization.

## TL;DR
A lightweight YouTube video transcript extraction and AI-powered summarization tool developed on the **WinUI 3 (Windows App SDK)** platform. **Seamlessly migrated from a classic WPF architecture**, this project blends the modern Windows 11 design language with smooth, asynchronous network processing. For more information, visit [the SimpVid Gist WPF official repository](https://github.com/RationalZombie/SimpVid_Gist_WPF)

---

## 🚀 Features

* **Efficient Transcript Extraction**: Powered by the `YoutubeExplode` library, you can fetch English transcripts (or the video's default transcript track) with one click simply by entering a YouTube video URL or ID.
* **Universal AI Summarization**: Built-in OpenAI-compatible universal HTTP request wrapper. It supports custom **AI Base URLs** and **Model Names**, allowing perfect integration with mainstream large language model APIs such as DeepSeek, Alibaba Cloud Tongyi Qwen (DashScope), and OpenAI.
* **Modern UI Layout**: Utilizes native WinUI 3 controls with adaptive grids and compact `Spacing` typography, delivering a smoother interactive experience that aligns with Windows 11 visual design guidelines.
* **Robust Input Validation**: Features automated regex filtering tailored for hidden "invisible special characters" (such as zero-width spaces and non-ASCII characters) often introduced when copying and pasting API Keys. This completely eliminates the `Request headers must contain only ASCII characters` authentication error.

---

## 🛠️ Tech Stack & Dependencies

* **Framework**: .NET 8 / Windows App SDK (WinUI 3)
* **Core Dependencies**:
* `YoutubeExplode` (For parsing YouTube videos and transcript streams)
* `System.Text.Json` (For high-performance parsing and streaming deserialization of AI JSON responses)
* `Microsoft.UI.Xaml` (The core WinUI 3 UI component library)

---

## 📦 Getting Started

### 1. Prerequisites

* **OS**: Windows 10, version 1809 (Build 17763) or higher / Windows 11
* **IDE**: Visual Studio 2026 (Requires the ".NET Desktop Development" and "Universal Windows Platform Development" workloads)

### 2. AI API Configuration

This tool supports any API endpoint that complies with the standard OpenAI format.

---

## 🔧 Key Notes on Migrating from WPF to WinUI 3

If you are maintaining or doing secondary development based on the original WPF version, please pay close attention to the following underlying platform changes:

1. **Window Sizing and Title Configuration**:
WinUI 3 removes the ability to define `Width` and `Height` directly within XAML. This project achieves precise control over the `AppWindow` dimensions by calling the `WinRT.Interop` interface in the `MainWindow.xaml.cs` constructor to retrieve the underlying `WindowHandle`.
2. **Modern Popups Replacing Traditional MessageBox**:
WPF's `MessageBox.Show` is no longer available in WinUI 3. This project encapsulates an asynchronous `ContentDialog` helper method named `ShowMessageDialogAsync`, which explicitly sets `XamlRoot = this.Content.XamlRoot` to comply with modern WinUI 3 modal dialog standards.
3. **XAML Color Brush Specifications**:
WinUI 3 cannot directly resolve literal color strings like `SkyBlue` or `Violet`. All button backgrounds on the interface have been refactored to use standard Hex color codes (e.g., Sky Blue `#87CEEB`, Violet `#EE82EE`).


## Special Thanks
[Linux.do](https://linux.do/)
