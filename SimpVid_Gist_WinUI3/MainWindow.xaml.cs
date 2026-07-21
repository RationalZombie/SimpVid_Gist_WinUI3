using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.Storage;
using Windows.Storage.Pickers;
using YoutubeExplode;

namespace SimpVid_Gist_WinUI3
{
    public sealed partial class MainWindow : Window
    {
        private readonly YoutubeClient _youtubeClient = new YoutubeClient();
        private readonly HttpClient _httpClient = new HttpClient();

        public MainWindow()
        {
            this.InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            // 将无按钮的 DragAreaGrid 传给 SetTitleBar，避免按钮点击失效
            SetTitleBar(DragAreaGrid);

            this.AppWindow.Changed += AppWindow_Changed;
            CustomTitleBar.Loaded += CustomTitleBar_Loaded;
            CustomTitleBar.Unloaded += CustomTitleBar_Unloaded;

            LoadFromAppData();
        }
        private void CustomTitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            if (CustomTitleBar.XamlRoot != null)
            {
                CustomTitleBar.XamlRoot.Changed += XamlRoot_Changed;

                // 根据当前屏幕 DPI 正确设置窗口大小
                double scale = CustomTitleBar.XamlRoot.RasterizationScale;
                this.AppWindow.Resize(new Windows.Graphics.SizeInt32(
                    (int)(450 * scale),
                    (int)(680 * scale)
                ));
            }
            UpdateTitleBarLayout();
        }

        private void CustomTitleBar_Unloaded(object sender, RoutedEventArgs e)
        {
            if (CustomTitleBar.XamlRoot != null)
            {
                CustomTitleBar.XamlRoot.Changed -= XamlRoot_Changed;
            }
        }
        private void AppWindow_Changed(Microsoft.UI.Windowing.AppWindow sender, Microsoft.UI.Windowing.AppWindowChangedEventArgs args)
        {
            if (args.DidPresenterChange || args.DidSizeChange || args.DidPositionChange)
            {
                UpdateTitleBarLayout();
            }
        }
        private async void ExtractButton_Click(object sender, RoutedEventArgs e)
        {
            string videoInput = UrlTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(videoInput))
            {
                await ShowMessageDialogAsync("Invalid Video Input", "Please enter a valid YouTube Video URL or ID");
                return;
            }

            ExtractButton.IsEnabled = false;
            TranscriptTextBox.Text = "Fetching transcript tracks...";

            try
            {
                var trackManifest = await _youtubeClient.Videos.ClosedCaptions.GetManifestAsync(videoInput);
                var trackInfo = trackManifest.GetByLanguage("en") ?? (trackManifest.Tracks.Count > 0 ? trackManifest.Tracks[0] : null);

                if (trackInfo != null)
                {
                    TranscriptTextBox.Text = "Downloading transcript...";
                    var closedCaptionTrack = await _youtubeClient.Videos.ClosedCaptions.GetAsync(trackInfo);

                    var transcriptBuilder = new StringBuilder();
                    foreach (var caption in closedCaptionTrack.Captions)
                    {
                        if (!string.IsNullOrWhiteSpace(caption.Text))
                        {
                            transcriptBuilder.AppendLine(caption.Text);
                        }
                    }

                    TranscriptTextBox.Text = transcriptBuilder.ToString();
                    SummarizeButton.IsEnabled = true;
                }
                else
                {
                    TranscriptTextBox.Text = "No transcript or captions found for this video.";
                }
            }
            catch (Exception ex)
            {
                TranscriptTextBox.Text = $"Error retrieving transcript: {ex.Message}";
            }
            finally
            {
                ExtractButton.IsEnabled = true;
            }
        }

        private async void SummarizeButton_Click(object sender, RoutedEventArgs e)
        {
            string apiKey = ApiKeyTextBox.Password.Trim();
            string transcript = TranscriptTextBox.Text.Trim();
            string apiUrl = BaseUrlTextBox.Text.Trim();
            string modelName = ModelTextBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(apiKey))
            {
                await ShowMessageDialogAsync("AI API key Required", "Please enter your AI API key.");
                return;
            }
            if (string.IsNullOrWhiteSpace(transcript) || transcript.StartsWith("Error"))
            {
                await ShowMessageDialogAsync("Transcript Empty", "Please fetch a valid video transcript before summarizing.");
                return;
            }
            if (string.IsNullOrWhiteSpace(apiUrl))
            {
                await ShowMessageDialogAsync("Input Required", "Please enter the AI Base URL.");
                return;
            }
            if (string.IsNullOrWhiteSpace(modelName))
            {
                await ShowMessageDialogAsync("Input Required", "Please enter the Model Name.");
                return;
            }

            SummarizeButton.IsEnabled = false;
            ExtractButton.IsEnabled = false;
            SummaryTextBox.Text = "Analyzing transcript & generating summary...";

            try
            {
                string summaryResult = await CallAiApiAsync(apiKey, transcript, apiUrl, modelName);
                SummaryTextBox.Text = summaryResult;
            }
            catch (Exception ex)
            {
                SummaryTextBox.Text = $"AI Error: {ex.Message}";
            }
            finally
            {
                SummarizeButton.IsEnabled = true;
                ExtractButton.IsEnabled = true;
            }
        }

        private async Task<string> CallAiApiAsync(string apiKey, string transcriptContent, string apiUrl, string modelName)
        {
            var requestBody = new
            {
                model = modelName,
                messages = new[]
                {
                    new { role = "system", content = "You are an expert assistant. Summarize the following YouTube transcript into clear, structured paragraphs. You may use key bullet points." },
                    new { role = "user", content = transcriptContent }
                },
                temperature = 0.5
            };

            string jsonPayload = JsonSerializer.Serialize(requestBody);

            using (var request = new HttpRequestMessage(HttpMethod.Post, apiUrl))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                HttpResponseMessage response = await _httpClient.SendAsync(request);
                string responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"({response.StatusCode}) Details: {responseString}");
                }

                using (JsonDocument doc = JsonDocument.Parse(responseString))
                {
                    JsonElement root = doc.RootElement;
                    string rawSummary = root.GetProperty("choices")[0]
                                            .GetProperty("message")
                                            .GetProperty("content")
                                            .GetString();
                    return rawSummary?.Trim() ?? "AI returned an empty response.";
                }
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string dataToSave = BaseUrlTextBox.Text + "\n" + ModelTextBox.Text;
            await SaveToAppDataAsync("userdata.txt", dataToSave);
        }

        private async Task SaveToAppDataAsync(string fileName, string content)
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string myAppFolder = Path.Combine(appDataPath, "SimpVid Gist");

                if (!Directory.Exists(myAppFolder))
                {
                    Directory.CreateDirectory(myAppFolder);
                }

                string filePath = Path.Combine(myAppFolder, fileName);
                await File.WriteAllTextAsync(filePath, content, Encoding.UTF8);

                await ShowMessageDialogAsync("Success", $"Successfully saved.\nAt: {filePath}\nThe next time you open SimpVid Gist, your data will be automatically read.");
            }
            catch (Exception ex)
            {
                await ShowMessageDialogAsync("Error", $"保存失败: {ex.Message}");
            }
        }

        private void LoadFromAppData()
        {
            try
            {
                string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string filePath = Path.Combine(appDataPath, "SimpVid Gist", "userdata.txt");

                if (File.Exists(filePath))
                {
                    string[] lines = File.ReadAllLines(filePath, Encoding.UTF8);

                    if (lines.Length >= 1)
                    {
                        BaseUrlTextBox.Text = lines[0];
                    }
                    if (lines.Length >= 2)
                    {
                        ModelTextBox.Text = lines[1];
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load save data: {ex.Message}");
            }
        }

        private async void Button_Save_Click(object sender, RoutedEventArgs e)
        {
            if (!SummarizeButton.IsEnabled)
            {
                await ShowMessageDialogAsync("Nothing Saved", "Please extract transcript first");
            }
            else
            {
                await WriteInContentAsync();
            }
        }

        private async Task WriteInContentAsync()
        {
            string transcript = TranscriptTextBox.Text.Trim();
            string summary = SummaryTextBox.Text.Trim();

            var savePicker = new FileSavePicker();

            // 绑定窗口句柄
            IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, windowHandle);

            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("Text File", new List<string>() { ".txt" });
            savePicker.SuggestedFileName = "SimpVid_Export_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");

            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                try
                {
                    StringBuilder fileContent = new StringBuilder();
                    fileContent.AppendLine("========================================");
                    fileContent.AppendLine("           YOUTUBE TRANSCRIPT           ");
                    fileContent.AppendLine("========================================");
                    fileContent.AppendLine(string.IsNullOrWhiteSpace(transcript) ? "[No Transcript Available]" : transcript);

                    if (!string.IsNullOrEmpty(summary))
                    {
                        fileContent.AppendLine();
                        fileContent.AppendLine("========================================");
                        fileContent.AppendLine("               AI SUMMARY               ");
                        fileContent.AppendLine("========================================");
                        fileContent.AppendLine(string.IsNullOrWhiteSpace(summary) ? "[No Summary Generated]" : summary);
                    }

                    await FileIO.WriteTextAsync(file, fileContent.ToString());
                    await ShowMessageDialogAsync("Success", $"File Successfully Saved to\n{file.Path}");
                }
                catch (Exception ex)
                {
                    await ShowMessageDialogAsync("Export Error", $"Failed to export file: {ex.Message}");
                }
            }
        }

        private async Task ShowMessageDialogAsync(string title, string content)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private void RightControlsPanel_Loaded(object sender, RoutedEventArgs e)
        {
            // 绑定 XamlRoot 的 Changed 事件：当跨显示器移动或系统缩放比例变更时触发
            if (CustomTitleBar.XamlRoot != null)
            {
                CustomTitleBar.XamlRoot.Changed += XamlRoot_Changed;
            }

            // 初始化布局计算
            UpdateTitleBarLayout();
        }
        private void XamlRoot_Changed(Microsoft.UI.Xaml.XamlRoot sender, Microsoft.UI.Xaml.XamlRootChangedEventArgs args)
        {
            // DPI 缩放比例发生改变时自动更新
            UpdateTitleBarLayout();
        }

        /// <summary>
        /// 核心：动态 DPI 换算与安全边距更新
        /// </summary>
        private void UpdateTitleBarLayout()
        {
            if (CustomTitleBar?.XamlRoot == null || AppWindow == null) return;

            // 1. 获取当前窗口屏幕的 DPI 缩放因子 (如 1.0, 1.25, 1.5, 2.0)
            double scale = CustomTitleBar.XamlRoot.RasterizationScale;

            // 2. 将系统原生物理像素 (Pixels) 转换为 XAML 的设备独立像素 (DIPs)
            double rightInsetDip = AppWindow.TitleBar.RightInset / scale;

            // 3. 实时应用 Margin 避开右上角原生控制按钮
            RightControlsPanel.Margin = new Thickness(0, 0, rightInsetDip + 10, 0);
        }
    }
}