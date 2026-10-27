using System.Text;

namespace CommonHelp;

/// <summary>一帧 SSE：event 名与已拼接的 data 载荷。不解析 JSON。</summary>
public readonly record struct SseFrame(string EventName, string Data);

/// <summary>
/// 功能职责：按行组装 SSE 帧（<c>event:</c>、多行 <c>data:</c>、空行结束）。不识别业务 event 名、不解析 JSON。
/// 方法群组：喂入一行、冲刷未结束的尾帧。
/// </summary>
public sealed class SseFrameReader
{
    private readonly StringBuilder _data = new();
    private string _eventName = "message";

    /// <summary>
    /// 功能作用：喂入 SSE 一行。空行表示当前帧结束。
    /// </summary>
    /// <param name="line">流里的一行；不含换行符。</param>
    /// <param name="frame">完整帧；仅在返回 true 时有效。</param>
    /// <returns>本行结束了一帧则为 true（data 可为空）。</returns>
    public bool TryFeed(string? line, out SseFrame frame)
    {
        frame = default;
        if (line is null)
            return false;

        if (line.StartsWith("event:", StringComparison.OrdinalIgnoreCase))
        {
            _eventName = line["event:".Length..].Trim();
            return false;
        }

        if (line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            if (_data.Length > 0)
                _data.Append('\n');
            _data.Append(line["data:".Length..].TrimStart());
            return false;
        }

        if (line.Length != 0)
            return false;

        frame = Complete();
        return true;
    }

    /// <summary>
    /// 功能作用：流结束时取出尚未以空行结束、但已有 data 的尾帧。
    /// </summary>
    /// <param name="frame">尾帧。</param>
    /// <returns>有未结束的 data 则为 true。</returns>
    public bool TryFlush(out SseFrame frame)
    {
        if (_data.Length == 0)
        {
            frame = default;
            return false;
        }

        frame = Complete();
        return true;
    }

    private SseFrame Complete()
    {
        var frame = new SseFrame(_eventName, _data.ToString());
        _data.Clear();
        _eventName = "message";
        return frame;
    }
}
