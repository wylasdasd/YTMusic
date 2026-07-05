namespace YTMusic;

/// <summary>
/// UI/MAUI 层全局常量与进程内共享状态入口。
/// 平台相关常量、偏好键、播放诊断标识等集中于此。
/// </summary>
public static class AppGlobal
{
    /// <summary>本地文件存储相关常量。</summary>
    public static class Storage
    {
        /// <summary>本地下载音乐根目录名（位于应用数据目录下）。</summary>
        public const string DownloadedMusicFolderName = "DownloadedMusic";
    }

    /// <summary>界面偏好与主题相关常量。</summary>
    public static class Ui
    {
        /// <summary>未保存偏好时的默认主题索引。</summary>
        public const int DefaultThemeIndex = 0;

        /// <summary>MAUI Preferences 中 UI/播放相关键名。</summary>
        public static class PreferenceKeys
        {
            /// <summary>收藏卡片是否显示封面图。</summary>
            public const string ShowFavoriteCardImages = "ui.showFavoriteCardImages";

            /// <summary>媒体标题是否允许两行显示。</summary>
            public const string MediaTitleTwoLines = "ui.mediaTitleTwoLines";

            /// <summary>当前选中主题索引。</summary>
            public const string ThemeIndex = "ui.themeIndex";

            /// <summary>是否预检远程视频流（后台探测可播性）。</summary>
            public const string PrefetchRemoteVideo = "playback.prefetchRemoteVideo";

            /// <summary>远程视频分离流画质偏好。</summary>
            public const string RemoteVideoStreamQuality = "playback.remoteVideoStreamQuality";
        }
    }

    /// <summary>播放器诊断与缓存相关常量。</summary>
    public static class Playback
    {
        /// <summary>播放诊断日志的系统 Debug 标签。</summary>
        public const string DiagnosticsTag = "YTMusic";

        /// <summary>播放管线统一日志前缀，与 JS 端 audioPlayer.js 保持一致。</summary>
        public const string LogPrefix = "[YTMusic:Playback]";

        /// <summary>远程视频预取结果在内存中的有效时长，过期后需重新探测。</summary>
        public static readonly TimeSpan RemoteVideoPrefetchTtl = TimeSpan.FromMinutes(30);
    }

    /// <summary>上传页 MudTabs 各标签页索引。</summary>
    public static class UploadPage
    {
        /// <summary>本地上传标签页。</summary>
        public const int LocalTabIndex = 0;

        /// <summary>AList 远程浏览/下载标签页。</summary>
        public const int RemoteTabIndex = 1;

        /// <summary>AList 连接设置标签页。</summary>
        public const int SettingsTabIndex = 2;
    }

    /// <summary>进程内可变共享状态（Service Locator、平台桥接等）。</summary>
    public static class Runtime
    {
        /// <summary>
        /// 应用启动后由 <see cref="MauiProgram"/> 注入的根服务容器。
        /// Android 前台服务等无 DI 构造场景通过此入口解析 <see cref="Services.MusicPlayerService"/>。
        /// </summary>
        public static IServiceProvider? Services { get; set; }
    }
}
