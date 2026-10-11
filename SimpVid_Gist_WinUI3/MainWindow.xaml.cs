using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Pickers;
using YoutubeExplode;

namespace SimpVid_Gist_WinUI3
{
    public sealed partial class MainWindow : Window
    {
        private readonly YoutubeClient _youtubeClient = new YoutubeClient();
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly string _saveFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SimpVid Gist", "userdata.txt");
        private bool _isWindowSizeInitialized = false;
        // 引入原生的 GetDpiForWindow
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hwnd); public MainWindow()
        {
            InitializeComponent();

            ExtendsContentIntoTitleBar = true;
            SetTitleBar(DragAreaGrid);

            // 【新增】在窗口绘制前直接计算缩放并调整大小，彻底告别闪烁
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            uint dpi = GetDpiForWindow(hWnd);
            double scale = dpi / 96.0;

            int scaledWidth = (int)(450 * scale);
            int scaledHeight = (int)(750 * scale);
            AppWindow.Resize(new Windows.Graphics.SizeInt32(scaledWidth, scaledHeight));

            AppWindow.Changed += AppWindow_Changed;
            CustomTitleBar.Loaded += CustomTitleBar_Loaded;
            CustomTitleBar.Unloaded += CustomTitleBar_Unloaded;

            LoadFromAppData();
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
            ExtractButton.Content = "Fetching transcript tracks...";
            try
            {
                var trackManifest = await _youtubeClient.Videos.ClosedCaptions.GetManifestAsync(videoInput);

                string targetLangCode = (LanguageCodeComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "en";

                var trackInfo = trackManifest.GetByLanguage(targetLangCode)
                                ?? trackManifest.Tracks.FirstOrDefault();

                if (trackInfo != null)
                {
                    ExtractButton.Content = "Downloading transcript...";

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
                    TranscriptTextBox.Text = $"No transcript or captions found for this video in language {targetLangCode}.";
                }
            }
            catch (Exception ex)
            {
                TranscriptTextBox.Text = $"Error retrieving transcript: {ex.Message}";
            }
            finally
            {
                ExtractButton.IsEnabled = true;
                ExtractButton.Content = "Extract Transcript";
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
            SummarizeButton.Content = "Waiting for server to respond...";
            try
            {
                SummaryTextBox.Text = "";
                await foreach (string chunk in CallAiApiStreamAsync(apiKey, transcript, apiUrl, modelName))
                {
                    SummaryTextBox.Text += chunk;
                    SummaryTextBox.Select(SummaryTextBox.Text.Length, 0);
                }
            }
            catch (Exception ex)
            {
                SummaryTextBox.Text = $"AI Error: {ex.Message}";
            }
            finally
            {
                SummarizeButton.IsEnabled = true;
                ExtractButton.IsEnabled = true;
                SummarizeButton.Content = "Re-summarize Transcript";
            }
        }

        private async IAsyncEnumerable<string> CallAiApiStreamAsync(string apiKey, string transcriptContent, string apiUrl, string modelName)
        {
            var requestBody = new JsonObject
            {
                ["model"] = modelName,
                ["messages"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["role"] = "system",
                        ["content"] = "You are an expert AI summarizer. Summarize the following YouTube transcript into clear, structured paragraphs. You may use bullet points. IMPORTANT: The VERY FIRST line of your response MUST be a single short sentence (under 80 characters, no quotes, no colons) describing the overall topic of the video — this line will be used as the file name. Leave one blank line after it, then provide the detailed summary."
                    },
                    new JsonObject
                    {
                        ["role"] = "user",
                        ["content"] = transcriptContent
                    }
                },
                ["temperature"] = 0.5,
                ["stream"] = true
            };

            string jsonPayload = requestBody.ToJsonString();

            using (var request = new HttpRequestMessage(HttpMethod.Post, apiUrl))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

                using (HttpResponseMessage response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        string error = await response.Content.ReadAsStringAsync();
                        throw new Exception($"({response.StatusCode}) Details: {error}");
                    }

                    using (Stream stream = await response.Content.ReadAsStreamAsync())
                    using (StreamReader reader = new StreamReader(stream))
                    {
                        while (!reader.EndOfStream)
                        {
                            string line = await reader.ReadLineAsync();
                            if (string.IsNullOrWhiteSpace(line)) continue;

                            if (line.StartsWith("data:"))
                            {
                                string data = line.Substring(5).Trim();

                                if (data == "[DONE]") break;

                                JsonDocument doc;
                                try
                                {
                                    doc = JsonDocument.Parse(data);
                                }
                                catch
                                {
                                    continue;
                                }

                                using (doc)
                                {
                                    var choices = doc.RootElement.GetProperty("choices");
                                    if (choices.GetArrayLength() > 0)
                                    {
                                        var delta = choices[0].GetProperty("delta");

                                        if (delta.TryGetProperty("content", out JsonElement contentElement))
                                        {
                                            yield return contentElement.GetString();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string dataToSave = BaseUrlTextBox.Text + "\n" + ModelTextBox.Text;
            await SaveToAppDataAsync(dataToSave);
        }

        private async Task SaveToAppDataAsync(string content)
        {
            try
            {
                string directoryPath = Path.GetDirectoryName(_saveFilePath);
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                File.WriteAllText(_saveFilePath, content, Encoding.UTF8);
                await ShowMessageDialogAsync("", $"Successfully saved.\nAt: {_saveFilePath}\nThe next time you open SimpVid Gist, your data will be automatically read.");
            }
            catch (Exception ex)
            {
                await ShowMessageDialogAsync("Save Error", $"Failed to save: {ex.Message}");
            }
        }

        private async void LoadFromAppData()
        {
            try
            {
                if (File.Exists(_saveFilePath))
                {
                    string[] lines = File.ReadAllLines(_saveFilePath, Encoding.UTF8);

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
                await ShowMessageDialogAsync("Load Error", $"Failed to load save data:\n{ex.Message}");
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

            // Match WPF: the summary's first line is the overall video topic.
            string overallInfo = "";
            string detailedSummary = summary;
            if (!string.IsNullOrEmpty(summary))
            {
                string[] lines = summary.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
                overallInfo = lines[0].Trim();
                if (lines.Length > 1)
                {
                    detailedSummary = string.Join(Environment.NewLine, lines, 1, lines.Length - 1).Trim();
                }
            }

            string baseFileName = "SimpVid_Export_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            if (!string.IsNullOrWhiteSpace(overallInfo))
            {
                string safeName = overallInfo;
                foreach (char c in Path.GetInvalidFileNameChars())
                {
                    safeName = safeName.Replace(c, '_');
                }
                baseFileName = safeName;
            }

            var savePicker = new FileSavePicker();
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            WinRT.Interop.InitializeWithWindow.Initialize(savePicker, hWnd);

            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("Text Files", new List<string>() { ".txt" });
            savePicker.FileTypeChoices.Add("All Files", new List<string>() { "*" });
            savePicker.SuggestedFileName = baseFileName;

            StorageFile file = await savePicker.PickSaveFileAsync();
            if (file != null)
            {
                try
                {
                    StringBuilder fileContent = new StringBuilder();
                    fileContent.AppendLine(string.IsNullOrWhiteSpace(overallInfo) ? "[No Overall Info]" : overallInfo);
                    fileContent.AppendLine();
                    fileContent.AppendLine("========================================");
                    fileContent.AppendLine("           YOUTUBE TRANSCRIPT           ");
                    fileContent.AppendLine("========================================");
                    fileContent.AppendLine(string.IsNullOrWhiteSpace(transcript) ? "[No Transcript Available]" : transcript);

                    if (!string.IsNullOrEmpty(detailedSummary))
                    {
                        fileContent.AppendLine();
                        fileContent.AppendLine("========================================");
                        fileContent.AppendLine("               AI SUMMARY               ");
                        fileContent.AppendLine("========================================");
                        fileContent.AppendLine(string.IsNullOrWhiteSpace(detailedSummary) ? "[No Summary Generated]" : detailedSummary);
                    }

                    await FileIO.WriteTextAsync(file, fileContent.ToString());
                    await ShowMessageDialogAsync("", $"File Successfully Saved to\n{file.Path}");
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
                XamlRoot = Content.XamlRoot
            };

            await dialog.ShowAsync();
        }

        private void CustomTitleBar_Loaded(object sender, RoutedEventArgs e)
        {
            if (CustomTitleBar.XamlRoot != null)
            {
                CustomTitleBar.XamlRoot.Changed += XamlRoot_Changed;
                UpdateTitleBarLayout();

                // 利用代码中已实现的 DPI 缩放模式 (RasterizationScale) 调整初始窗口大小
                if (!_isWindowSizeInitialized)
                {
                    double scale = CustomTitleBar.XamlRoot.RasterizationScale;
                    int scaledWidth = (int)(450 * scale);
                    int scaledHeight = (int)(750 * scale);

                    // AppWindow.Resize 需要传入物理像素 (SizeInt32)
                    AppWindow.Resize(new Windows.Graphics.SizeInt32(scaledWidth, scaledHeight));
                    _isWindowSizeInitialized = true;
                }
            }
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
            if (args.DidSizeChange)
            {
                UpdateTitleBarLayout();
            }
        }

        private void XamlRoot_Changed(Microsoft.UI.Xaml.XamlRoot sender, Microsoft.UI.Xaml.XamlRootChangedEventArgs args)
        {
            UpdateTitleBarLayout();
        }

        private void UpdateTitleBarLayout()
        {
            if (CustomTitleBar?.XamlRoot == null || AppWindow == null) return;

            double scale = CustomTitleBar.XamlRoot.RasterizationScale;
            double rightInsetDip = AppWindow.TitleBar.RightInset / scale;
            double leftInsetDip = AppWindow.TitleBar.LeftInset / scale;

            CustomTitleBar.Margin = new Thickness(leftInsetDip, 0, rightInsetDip, 0);
        }
    }
}