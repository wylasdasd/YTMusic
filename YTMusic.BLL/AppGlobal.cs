namespace YTMusic.BLL;

/// <summary>
/// BLL 层全局常量与进程内共享状态入口。
/// 业务运行时状态仍由 DI 单例服务承载；此处集中魔法值与跨模块复用的配置。
/// </summary>
public static class AppGlobal
{
    /// <summary>SQLite 数据库文件名。</summary>
    public static class Database
    {
        /// <summary>收藏夹数据库文件名。</summary>
        public const string FavoritesFileName = "YTMusicFavorites.db3";

        /// <summary>本地下载记录数据库文件名。</summary>
        public const string DownloadsFileName = "YTMusicDownloads.db3";

        /// <summary>播放历史数据库文件名。</summary>
        public const string PlaybackHistoryFileName = "YTMusicPlaybackHistory.db3";
    }

    /// <summary>收藏夹业务默认值。</summary>
    public static class Favorites
    {
        /// <summary>首次初始化时自动创建的默认收藏夹名称。</summary>
        public const string DefaultFolderName = "默认收藏夹";
    }

    /// <summary>下载/上传任务队列相关阈值。</summary>
    public static class Transfers
    {
        /// <summary>已完成任务在内存中最多保留条数，超出后按最旧优先裁剪。</summary>
        public const int MaxRetainedTasks = 100;

        /// <summary>播放历史在数据库中最多保留条数。</summary>
        public const int MaxPlaybackHistoryItems = 50;

        /// <summary>上传进行中进度上限（不含 1.0，完成时才记为 100%）。</summary>
        public const double MaxInProgressUploadProgress = 0.99;

        /// <summary>进度变化低于此阈值时不触发 UI 通知，减少刷新频率。</summary>
        public const double ProgressNotifyThreshold = 0.005;
    }

    /// <summary>AList 上传/远程浏览相关常量。</summary>
    public static class AList
    {
        /// <summary>单次内存直传的最大文件字节数，超出则走流式上传。</summary>
        public const long MaxInMemoryUploadBytes = 512L * 1024 * 1024;

        /// <summary>上传失败后的最大重试次数。</summary>
        public const int UploadMaxAttempts = 3;

        /// <summary>上传完成后校验远程文件的最大重试次数。</summary>
        public const int UploadVerifyMaxAttempts = 5;

        /// <summary>远程曲目在本地播放列表中的 ID 前缀，用于区分 YouTube 曲目。</summary>
        public const string RemoteTrackIdPrefix = "alist:";

        /// <summary>元数据缺失时的默认艺术家占位名。</summary>
        public const string UnknownArtist = "Unknown Artist";

        /// <summary>远程曲目的默认作者/来源显示名。</summary>
        public const string RemoteAuthor = "AList";

        /// <summary>MAUI Preferences 中 AList 连接配置键名。</summary>
        public static class PreferenceKeys
        {
            /// <summary>AList 服务 Base URL。</summary>
            public const string BaseUrl = "alist.upload.baseUrl";

            /// <summary>AList 访问 Token。</summary>
            public const string Token = "alist.upload.token";

            /// <summary>AList 远程默认目录路径。</summary>
            public const string RemoteDirectory = "alist.upload.remoteDirectory";
        }
    }

    /// <summary>网络错误提示相关常量。</summary>
    public static class Network
    {
        /// <summary>同类网络错误通知的最短间隔，避免短时间内重复弹窗。</summary>
        public static readonly TimeSpan ErrorNotifyCooldown = TimeSpan.FromSeconds(10);

        /// <summary>无法连接 YouTube 时的默认用户提示文案。</summary>
        public const string DefaultConnectionError = "无法连接到 YouTube 服务器";

        /// <summary>连接失败时附带的 VPN 切换建议。</summary>
        public const string VpnRetryHint = "可尝试切换 VPN 节点后重试。";
    }

    /// <summary>进程内可变共享状态（无 DI 场景或冷却窗口等）。</summary>
    public static class Runtime
    {
        /// <summary>
        /// 上一次弹出网络错误通知的 UTC 时间。
        /// 与 <see cref="Network.ErrorNotifyCooldown"/> 配合做冷却判断。
        /// </summary>
        internal static DateTime LastNetworkErrorNotifiedUtc = DateTime.MinValue;

        /// <summary>保护 <see cref="LastNetworkErrorNotifiedUtc"/> 读写的进程内锁。</summary>
        internal static readonly object NetworkErrorNotifyLock = new();
    }
}

