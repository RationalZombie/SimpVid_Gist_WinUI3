# SimpVid Gist (WinUI 3 Port)

> [!NOTE]
> Both humans and Generative AI were involved in the development of this software.
>
> Synced with [SimpVid Gist WPF](https://github.com/RationalZombie/SimpVid_Gist_WPF) v1.4

Here is a sample of this app running on Windows 11:

<img width="392" height="1025" alt="A single page app with textboxes regarding the video link, extracted transcript and the summary." src="https://github.com/user-attachments/assets/8d0cd0cd-0d7e-4a44-808a-b254e1593a0f" />


> [!TIP]
> You have to prepare your own AI API key for summarization.

## TL;DR
A lightweight YouTube video transcript extraction and AI-powered summarization tool developed on the **WinUI 3 (Windows App SDK)** platform. **Seamlessly migrated from a classic WPF architecture**, this project blends the modern Windows 11 design language with smooth, asynchronous network processing. For more information, visit [the SimpVid Gist WPF official repository](https://github.com/RationalZombie/SimpVid_Gist_WPF)

---

## 🚀 Features

* **Efficient Transcript Extraction**: Powered by the `YoutubeExplode` library, you can select a subtitle language (or use the video's first available transcript track) with one click simply by entering a YouTube video URL or ID.
* **Universal AI Summarization**: Built-in OpenAI-compatible universal HTTP request wrapper. It supports custom **AI Base URLs** and **Model Names**, allowing perfect integration with mainstream large language model APIs such as DeepSeek, Alibaba Cloud Tongyi Qwen (DashScope), and OpenAI.
* **Modern UI Layout**: Utilizes native WinUI 3 controls with adaptive grids and compact `Spacing` typography, delivering a smoother interactive experience that aligns with Windows 11 visual design guidelines.
* **Topic-based Text Export**: The AI summary begins with a short video topic, which becomes the suggested `.txt` filename and the first line of the exported file. Transcript-only exports use a timestamped filename.
* **Repeat Summarization**: After summarization, the button displays “Re-summarize Transcript”.
* **Native Window Controls**: Uses WinUI 3's system close, minimize, and maximize buttons.

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



## Special Thanks
[Linux.do](https://linux.do/)
