using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Xaml.Media.Imaging;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Windows.Web.Http;
using Windows.UI.Notifications;
using Windows.Data.Xml.Dom;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.ApplicationSettings;
using Windows.UI.Popups;

// “空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=234238 上有介绍

namespace BingWallpaper
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class MainPage : Page
    {
        List<XmlDocument> tileNotifications = new List<XmlDocument>();
        private SettingsData settingsData;
        private const string KEY_SHOW_SEARCH_BOX = "showSearchBox";
        private const string KEY_SHOW_WALLPAPER_SIZE = "wallpaperSize";
        private const string KEY_LAST_UPDATE_DATE = "LastUpdateDate";
        WallpaperInfo wallpaperInfo;
        DateTime lastUpdateDate = new DateTime();
        private DispatcherTimer timer = new DispatcherTimer();

        public MainPage()
        {
            this.InitializeComponent();
            LoadSettings();

            // 添加设置
            SettingsCommand settingsCommand = new SettingsCommand("settingsCmd", "设置", new UICommandInvokedHandler(OnSettingsCmd));
            // 将设置命令添加到设置面板
            SettingsPane.GetForCurrentView().CommandsRequested += (sender, args) =>
            {
                args.Request.ApplicationCommands.Add(settingsCommand);
            };

            // 创建定时器
            timer.Interval = TimeSpan.FromMinutes(1); // 每 1 分钟触发一次
            timer.Tick += TimerTick; // 绑定事件处理程序

            // 启动定时器
            timer.Start();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            SetBingWallpaper();
        }

        private async void SetBingWallpaper()
        {
            // 清空之前的磁贴模板
            tileNotifications.Clear();

            // 获取壁纸信息
            wallpaperInfo = await GetCurrentBingWallpaper();

            // 获取磁贴更新器
            TileUpdater tileUpdater = TileUpdateManager.CreateTileUpdaterForApplication();
            // 启用队列通知
            tileUpdater.EnableNotificationQueue(true);
            // 清空之前的计划通知
            tileUpdater.Clear();

            // 下载图片
            StorageFile downloadedFile = await utilities.Common.DownloadImageAsync(wallpaperInfo.GetUrl(settingsData.wallpaperSize));
            if (downloadedFile != null)
            {
                // 调整图片尺寸为宽磁贴大小
                StorageFile resizedFileWide = await utilities.Common.ResizeImageAsync(downloadedFile, 558, 270);
                if (resizedFileWide != null)
                {
                    // 使用调整后的图片更新磁贴
                    string localImageUrl = "ms-appdata:///local/" + resizedFileWide.Name;
                    UpdateWideTileWithWallpaper(localImageUrl);
                    UpdateTileWallpaperAndTitle(localImageUrl, wallpaperInfo.title);
                }

                // 调整图片大小为方形磁贴大小
                StorageFile resizedFileSquare = await utilities.Common.ResizeImageAsync(downloadedFile, 558, 558);
                if (resizedFileSquare != null)
                {
                    string localImageUrl = "ms-appdata:///local/" + resizedFileSquare.Name;
                    UpdateSquareTileWithWallpaper(localImageUrl);
                    UpdateSquareTileWallpaperAndText(localImageUrl, wallpaperInfo.title, wallpaperInfo.copyright);
                }
            }

            try
            {
                // 异步加载壁纸
                BitmapImage bitmapImage = new BitmapImage();
                bitmapImage.UriSource = new Uri("ms-appdata:///local/" + utilities.Common.DOWNLOAD_FILE_NAME);
                curWallpaper.Source = bitmapImage;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("加载壁纸失败: " + ex.Message);
                // 设置默认壁纸
                curWallpaper.Source = new BitmapImage(new Uri("ms-appx:///Assets/Placeholder.jpg"));
            }

            // 设置标题和版权信息
            titleTextBlock.Text = wallpaperInfo.title;
            copyrightTextBlock.Text = wallpaperInfo.copyright;

            // 更新磁贴
            UpdateTileWithText(wallpaperInfo.title, wallpaperInfo.copyright); // 显示文本的磁贴

            try
            {
                // 添加磁贴通知到队列
                foreach (var tileXml in tileNotifications)
                {
                    TileNotification tileNotification = new TileNotification(tileXml);
                    tileUpdater.Update(tileNotification);
                }
                System.Diagnostics.Debug.WriteLine("队列通知已添加！");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("队列通知失败: " + ex.Message);
            }

            //记录更新时间
            lastUpdateDate = DateTime.Now.Date;
            SaveSettings();
        }

        private async Task<WallpaperInfo> GetCurrentBingWallpaper()
        {
            WallpaperInfo wallpaperInfo = new WallpaperInfo
            {
                url = "ms-appx:///Assets/Placeholder.jpg", // 默认壁纸
                title = "今日壁纸", // 默认标题
                copyright = "无网络连接" // 默认版权信息
            };

            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string apiUrl = "https://cn.bing.com/HPImageArchive.aspx?format=js&idx=0&n=1&mkt=zh-CN";
                    HttpResponseMessage response = await client.GetAsync(new Uri(apiUrl));
                    response.EnsureSuccessStatusCode(); // 确保请求成功

                    string jsonString = await response.Content.ReadAsStringAsync();
                    JObject json = JObject.Parse(jsonString);
                    JToken imageInfo = json["images"][0];

                    wallpaperInfo.url = "https://cn.bing.com" + imageInfo["url"].ToString();
                    // 截取url中从开头到".jpg"的部分
                    int jpgIndex = wallpaperInfo.url.IndexOf(".jpg");
                    if (jpgIndex != -1)
                    {
                        wallpaperInfo.url = wallpaperInfo.url.Substring(0, jpgIndex + 4);
                    }
                    wallpaperInfo.urlWithWaterMark = wallpaperInfo.url;
                    //获取1920x1200链接
                    if (wallpaperInfo.urlWithWaterMark.EndsWith("_1920x1080.jpg"))
                    {
                        wallpaperInfo.urlWithWaterMark = wallpaperInfo.urlWithWaterMark.Replace("1920x1080", "1920x1200");
                    }
                    wallpaperInfo.title = imageInfo["title"].ToString();
                    wallpaperInfo.copyright = imageInfo["copyright"].ToString();
                    wallpaperInfo.copyrightLink = imageInfo["copyrightlink"].ToString();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("发生异常: " + ex.Message);
                if (ex.InnerException != null)
                {
                    System.Diagnostics.Debug.WriteLine("内部异常: " + ex.InnerException.Message);
                }
            }

            return wallpaperInfo;
        }

        public SettingsData GetSettingsData()
        {
            return settingsData;
        }

        public void LoadSettings()
        {
            ApplicationDataContainer rootContainer = ApplicationData.Current.LocalSettings;
            settingsData.showSearchBox = (bool)utilities.Common.GetConfigValue(rootContainer.Values, KEY_SHOW_SEARCH_BOX, false);
            settingsData.wallpaperSize = (SettingsData.WallpaperSize)utilities.Common.GetConfigValue(rootContainer.Values, KEY_SHOW_WALLPAPER_SIZE, SettingsData.WallpaperSize.SIZE_1200);
            ApplySettings(settingsData);
            string dateString = utilities.Common.GetConfigValue(rootContainer.Values, KEY_LAST_UPDATE_DATE, "").ToString();
            DateTime.TryParse(dateString, out lastUpdateDate);
        }

        public void SaveSettings()
        {
            ApplicationDataContainer rootContainer = ApplicationData.Current.LocalSettings;
            rootContainer.Values[KEY_SHOW_SEARCH_BOX] = settingsData.showSearchBox;
            rootContainer.Values[KEY_SHOW_WALLPAPER_SIZE] = (int)settingsData.wallpaperSize;
            rootContainer.Values[KEY_LAST_UPDATE_DATE] = lastUpdateDate.ToString();
        }

        private void CheckAndUpdateTile()
        {
            DateTime currentDate = DateTime.Now.Date;

            // 如果上次更新日期为空或与当前日期不同，则更新磁贴
            if (lastUpdateDate != currentDate)
            {
                SetBingWallpaper();
            }
        }

        public void ApplySettings(SettingsData _settingsData)
        {
            settingsData = _settingsData;
            bingSearchBox.Visibility = settingsData.showSearchBox ? Visibility.Visible : Visibility.Collapsed;
        }

        private void Image_Tapped(object sender, Windows.UI.Xaml.Input.TappedRoutedEventArgs e)
        {
            // 切换标题和版权信息的可见性
            bool textVisible = textPanel.Visibility == Visibility.Collapsed;
            bool searchBoxVisible = textVisible && settingsData.showSearchBox;
            textPanel.Visibility = textVisible ? Visibility.Visible : Visibility.Collapsed;
            bingSearchBox.Visibility = searchBoxVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private XmlDocument CreateTileTemplate(TileTemplateType templateType, string imageUrl = null, string title = null, string copyright = null)
        {
            XmlDocument tileXml = TileUpdateManager.GetTemplateContent(templateType);

            switch (templateType)
            {
                // 宽磁贴仅图片
                case TileTemplateType.TileWide310x150Image:
                // 方磁贴仅图片
                case TileTemplateType.TileSquare150x150Image:
                // 大型方磁贴仅图片
                case TileTemplateType.TileSquare310x310Image:
                {
                    IXmlNode imageNode = tileXml.ChildNodes[0].ChildNodes[0].ChildNodes[0].ChildNodes[0];
                    imageNode.Attributes[1].NodeValue = imageUrl;
                }
                    break;

                // 宽磁贴仅文本
                case TileTemplateType.TileWide310x150Text09:
                // 方磁贴仅文本
                case TileTemplateType.TileSquare150x150Text02:
                {
                    IXmlNode bindingNode = tileXml.ChildNodes[0].ChildNodes[0].ChildNodes[0];
                    IXmlNode textNode1 = bindingNode.ChildNodes[0];
                    IXmlNode textNode2 = bindingNode.ChildNodes[1];
                    textNode1.InnerText = title;
                    textNode2.InnerText = copyright;
                }
                    break;

                // 宽磁贴图片加文本
                case TileTemplateType.TileWide310x150ImageAndText01:
                {
                    IXmlNode bindingNode2 = tileXml.ChildNodes[0].ChildNodes[0].ChildNodes[0];
                    IXmlNode imageNode2 = bindingNode2.ChildNodes[0];
                    IXmlNode textNode = bindingNode2.ChildNodes[1];
                    imageNode2.Attributes[1].NodeValue = imageUrl;
                    textNode.InnerText = title;
                }
                    break;

                //大型宽磁贴图片加文本
                case TileTemplateType.TileSquare310x310ImageAndText02:
                case TileTemplateType.TileSquare310x310ImageAndTextOverlay02:
                {
                    IXmlNode bindingNode = tileXml.ChildNodes[0].ChildNodes[0].ChildNodes[0];
                    IXmlNode imageNode = bindingNode.ChildNodes[0];
                    IXmlNode textNode1 = bindingNode.ChildNodes[1];
                    IXmlNode textNode2 = bindingNode.ChildNodes[2];
                    imageNode.Attributes[1].NodeValue = imageUrl;
                    textNode1.InnerText = title;
                    textNode2.InnerText = copyright;
                }
                    break;

            }

            return tileXml;
        }

        private void UpdateWideTileWithWallpaper(string imageUrl)
        {
            try
            {
                XmlDocument tileXml = CreateTileTemplate(TileTemplateType.TileWide310x150Image, imageUrl);
                tileNotifications.Add(tileXml);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("磁贴 XML 添加失败: " + ex.Message);
            }
        }

        private void UpdateSquareTileWithWallpaper(string imageUrl)
        {
            try
            {
                tileNotifications.Add(CreateTileTemplate(TileTemplateType.TileSquare150x150Image, imageUrl));
                tileNotifications.Add(CreateTileTemplate(TileTemplateType.TileSquare310x310Image, imageUrl));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("磁贴 XML 添加失败: " + ex.Message);
            }
        }

        private void UpdateTileWithText(string title, string copyright)
        {
            try
            {
                XmlDocument tileWideXml = CreateTileTemplate(TileTemplateType.TileWide310x150Text09, null, title, copyright);
                tileNotifications.Add(tileWideXml);
                XmlDocument tileSquareXml = CreateTileTemplate(TileTemplateType.TileSquare150x150Text02, null, title, copyright);
                tileNotifications.Add(tileSquareXml);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("磁贴 XML 添加失败: " + ex.Message);
            }
        }

        private void UpdateTileWallpaperAndTitle(string imageUrl, string title)
        {
            try
            {
                XmlDocument tileXml = CreateTileTemplate(TileTemplateType.TileWide310x150ImageAndText01, imageUrl, title);
                tileNotifications.Add(tileXml);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("磁贴 XML 添加失败: " + ex.Message);
            }
        }

        private void UpdateSquareTileWallpaperAndText(string imageUrl, string title, string copyright)
        {
            try
            {
                tileNotifications.Add(CreateTileTemplate(TileTemplateType.TileSquare310x310ImageAndText02, imageUrl, title, copyright));
                tileNotifications.Add(CreateTileTemplate(TileTemplateType.TileSquare310x310ImageAndTextOverlay02, imageUrl, title, copyright));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("磁贴 XML 添加失败: " + ex.Message);
            }

        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            SetBingWallpaper();
        }

        private async void SaveAsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 获取下载的图片文件
                StorageFile downloadedFile = await ApplicationData.Current.LocalFolder.GetFileAsync("DownloadedImage.jpg");

                if (downloadedFile != null)
                {
                    // 生成默认文件名
                    string defaultFileName = utilities.Common.GenerateFileName(copyrightTextBlock.Text);

                    // 弹出“另存为”对话框
                    FileSavePicker savePicker = new FileSavePicker();
                    savePicker.SuggestedStartLocation = PickerLocationId.PicturesLibrary;
                    savePicker.FileTypeChoices.Add("JPEG 图片", new List<string> { ".jpg" });
                    savePicker.SuggestedFileName = defaultFileName;

                    StorageFile savedFile = await savePicker.PickSaveFileAsync();

                    if (savedFile != null)
                    {
                        // 复制文件到指定位置
                        await downloadedFile.CopyAndReplaceAsync(savedFile);
                        System.Diagnostics.Debug.WriteLine("文件保存成功: " + savedFile.Path);
                    }
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("未找到下载的图片文件");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("保存文件失败: " + ex.Message);
            }
        }

        private void searchTextBox_KeyUp(object sender, KeyRoutedEventArgs e)
        {
            // 检查是否按下回车键
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                // 获取搜索关键词
                string searchKeyword = searchTextBox.Text;

                if (!string.IsNullOrEmpty(searchKeyword))
                {
                    // 打开必应搜索
                    utilities.Common.OpenBingSearch(searchKeyword);
                }
            }
        }

        private void OnSettingsCmd(IUICommand command)
        {
            // 创建自定义设置界面
            Settings settingsFlyout = new Settings(this);

            // 创建 Popup 用于显示设置界面
            Popup popup = new Popup
            {
                Child = settingsFlyout,
                IsLightDismissEnabled = true, // 点击外部关闭 Popup
            };

            // 设置 Settings 的宽度和高度
            settingsFlyout.Width = 346;
            settingsFlyout.Height = Window.Current.Bounds.Height;

            // 计算 Popup 的位置
            double screenWidth = Window.Current.Bounds.Width;
            popup.HorizontalOffset = screenWidth - 346; // 显示在屏幕右侧
            popup.VerticalOffset = 0; // 显示在屏幕顶部

            // 将 Popup 传递给 Settings 页面
            settingsFlyout.SetParentPopup(popup);

            // 显示 Popup
            popup.IsOpen = true;
        }

        private void OnCopyrightTapped(object sender, TappedRoutedEventArgs e)
        {
            if (wallpaperInfo.copyrightLink != null && wallpaperInfo.copyrightLink.Length > 0)
                utilities.Common.OpenUrl(wallpaperInfo.copyrightLink);
        }

        private void TimerTick(object sender, object e)
        {
            // 定时器触发时检查并更新磁贴
            CheckAndUpdateTile();
        }
    }

    public struct WallpaperInfo
    {
        public string url;              //壁纸的路径（1920x1080）
        public string urlWithWaterMark; //壁纸的路径（1920x1200，有必应水印）
        public string title;            //壁纸的标题
        public string copyright;        //版权
        public string copyrightLink;    //必应搜索链接
        public string searchWords;      //从copyrightLink提取的搜索关键字
        public string GetUrl(SettingsData.WallpaperSize wallpaperSize)
        {
            if (wallpaperSize == SettingsData.WallpaperSize.SIZE_1080)
                return url;
            else
                return urlWithWaterMark;
        }
    }

    public struct SettingsData
    {
        public bool showSearchBox;
        public enum WallpaperSize
        {
            SIZE_1080,
            SIZE_1200
        }
        public WallpaperSize wallpaperSize;
    }
}
