namespace CommonHelp;

/// <summary>
/// 功能职责：文件超出预期大小限制时抛出的业务异常。不负责检测文件大小。
/// 方法群组：构造。
/// </summary>
public class FileBigException : Exception
{
    /// <summary>
    /// 功能作用：以错误消息创建异常。
    /// </summary>
    /// <param name="exMsg">错误消息。</param>
    public FileBigException(string exMsg) : base(exMsg)
    {
    }

    /// <summary>
    /// 功能作用：以错误消息与内部异常创建异常。
    /// </summary>
    /// <param name="exMsg">错误消息。</param>
    /// <param name="ex">内部异常。</param>
    public FileBigException(string exMsg, Exception ex) : base(exMsg, ex)
    {
    }
}
