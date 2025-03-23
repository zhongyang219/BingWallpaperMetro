using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

// “空白页”项模板在 http://go.microsoft.com/fwlink/?LinkId=234238 上有介绍

namespace BingWallpaper
{
    /// <summary>
    /// 可用于自身或导航至 Frame 内部的空白页。
    /// </summary>
    public sealed partial class Settings : Page
    {
        private Popup parentPopup;
        MainPage mainPage;

        private void SettingsToUI(SettingsData settingsData)
        {
            showSechbox.IsOn = settingsData.showSearchBox;
            wallpaperSizeCombo.SelectedIndex = (int)settingsData.wallpaperSize;
        }

        private SettingsData SettingsFromUI()
        {
            SettingsData settingsData;
            settingsData.showSearchBox = showSechbox.IsOn;
            settingsData.wallpaperSize = (SettingsData.WallpaperSize)wallpaperSizeCombo.SelectedIndex;
            return settingsData;
        }

        public Settings(MainPage _mainPage)
        {
            mainPage = _mainPage;
            this.InitializeComponent();
            SettingsToUI(mainPage.GetSettingsData());
        }

        // 设置父 Popup
        public void SetParentPopup(Popup _parentPopup)
        {
            parentPopup = _parentPopup;
            parentPopup.Closed += OnPopupClosed; // 监听 Popup 关闭事件
        }

        // 返回按钮点击事件
        private void BackButton_Click(object sender, RoutedEventArgs e)
        {
            if (parentPopup != null)
            {
                parentPopup.IsOpen = false; // 关闭 Popup
            }
        }

        // Popup 关闭时保存设置
        private void OnPopupClosed(object sender, object e)
        {
            mainPage.ApplySettings(SettingsFromUI());
            mainPage.SaveSettings();
        }

        private void OnShowSearchBoxToggled(object sender, RoutedEventArgs e)
        {
            mainPage.ApplySettings(SettingsFromUI());
        }
    }
}
