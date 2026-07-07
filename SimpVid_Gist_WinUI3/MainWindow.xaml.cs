using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using YoutubeExplode;
using YoutubeExplode.Videos.ClosedCaptions;

namespace SimpVid_Gist_WinUI3
{
    public partial class MainWindow : Window
    {
        private readonly YoutubeClient _youtubeClient = new YoutubeClient();
        private readonly HttpClient _httpClient = new HttpClient();

        public MainWindow()
        {
            this.InitializeComponent();

            // 设置 WinUI 3 窗口初始大小
            SetWindowSize(600, 800);
        }

        /// <summary>
        /// 调整 WinUI 3 窗口大小的辅助方法
        /// </summary>
        private void SetWindowSize(int width, int height)
        {
            var hWnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                appWindow.Resize(new Windows.Graphics.SizeInt32(width, height));
            }
        }

        /// <summary>
        /// 替代 WPF MessageBox 的 WinUI 3 弹窗方法
        /// </summary>
        private async Task ShowMessageDialogAsync(string title, string content)
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "OK",
                XamlRoot = this.Content.XamlRoot // WinUI 3 必须指定 XamlRoot
            };
            await dialog.ShowAsync();
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
                var trackInfo = trackManifest.GetByLanguage("en") ?? trackManifest.Tracks[0];

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
                await ShowMessageDialogAsync("AI API key Required", "Please enter your AI API key first");
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
    }
}