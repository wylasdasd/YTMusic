# CommonHelp API 索引

> 命名空间：`CommonHelp`（file-scoped）
> 约定：公开类/方法均使用 XML doc 注释；可失败的工具方法返回 `Result<T>`（Ok/Fail）。
> 注：部分 API 为跨项目通用预留，当前解决方案内未必有直接调用。

---

## 目录

- [ResultFormat（请求响应结果格式）](#common)
- [ArrayHelps（数组与枚举）](#arrayhelps)
- [CalculationHelps（计算）](#calculationhelps)
- [EncodingHelps（编码）](#encodinghelps)
- [Exceptions（异常）](#exceptions)
- [FileHelps（文件）](#filehelps)
- [JsonHelps（JSON）](#jsonhelps)
- [LogHelps（Serilog 全局日志）](#loghelps)
- [NetHelps（网络）](#nethelps)
- [StringHelps（文本）](#stringhelps)
- [TaskHelps（异步重试）](#taskhelps)
- [TimeHelps（时间）](#timehelps)
- [WindowsHelps（Windows 平台）](#windowshelps)
- [McpHelps（MCP stdio 客户端）](#mchelps)

---

## ResultFormat

### Result 类型（统一返回结果）

库中以泛型 record 的形式提供了三种常用的结果封装：`Result<T>`、`HttpResult<T>` 与 `BLLResult<T>`。推荐优先使用 `Result<T>` 或 `HttpResult<T>`，`BLLResult<T>` 仅为兼容旧业务场景保留。

#### `Result<T>`  `sealed record`

通用的工具操作返回类型：成功时通过 `IsSuccess` 判断并在 `Value` 中携带结果，失败时在 `Error` 中携带用户可读的错误说明。

| 成员 | 说明 |
|:---|:---|
| `IsSuccess` (bool) | 操作是否成功 |
| `Value` (T?) | 成功时的返回值 |
| `Error` (string?) | 失败时的错误说明 |
| `static Ok(T value)` | 创建成功结果 |
| `static Fail(string error)` | 创建失败结果 |

#### `HttpResult<T>`  `sealed record`

用于表示带有 HTTP 状态码语义的返回：包含 `Code` 字段并以 200–299 范围判断为成功。

| 成员 | 说明 |
|:---|:---|
| `Code` (int) | HTTP 状态码 |
| `IsSuccess` (bool) | 根据 `Code`（200–299 为成功）计算得出 |
| `Value` (T?) | 成功时的返回值 |
| `Error` (string?) | 失败时的错误说明 |
| `static Ok(T value, int code = 200)` | 创建成功结果，默认 code = 200 |
| `static Fail(string error, int code = 500)` | 创建失败结果，默认 code = 500 |

#### `BLLResult<T>`  `sealed record`

为兼容部分业务代码保留的结果类型（不推荐新代码使用）。语义上假设 `Code == 0` 为成功，非 0 表示失败。

| 成员 | 说明 |
|:---|:---|
| `Code` (int) | 业务码（约定 0 为成功） |
| `IsSuccess` (bool) | `Code == 0` 表示成功 |
| `Value` (T?) | 成功时的返回值 |
| `Error` (string?) | 失败时的错误说明 |
| `static Ok(T value, int code = 0)` | 创建成功结果，默认 code = 0 |
| `static Fail(string error, int code = -1)` | 创建失败结果，默认 code = -1 |

---

## ArrayHelps

### `ArrayHelp`  `static class`

数组随机打乱工具。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `Shuffle` | `void Shuffle<T>(this T[] array)` | Fisher-Yates 洗牌算法，原地打乱数组 |
| `Shuffle` | `void Shuffle<T>(this IList<T> array)` | Fisher-Yates 洗牌算法，原地打乱 `IList<T>` |

### `EnumHelp`  `static class`

枚举扩展工具：通过 `DescriptionAttribute` 获取描述文本。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `GetDescription` | `string GetDescription(this Enum value)` | 获取枚举值的 `[Description]` 标注文本（结果缓存；无描述时返回枚举名） |
| `ToDictionary` | `Dictionary<int, string> ToDictionary<TEnum>()` | 将枚举所有值（int）与描述转为字典 |
| `GetList` | `List<EnumItemDetail> GetList<TEnum>()` | 返回枚举所有值的 `EnumItemDetail` 列表 |
| `GetEnumByDescription` | `TEnum? GetEnumByDescription<TEnum>(string description)` | 按描述文本（或字段名）反向查找枚举值 |

### `EnumItemDetail`  `class`

枚举项详情：`Value`（int 值）、`Name`（枚举名）、`Description`（描述）。

### `RandomHelp`  `static class`

随机抽取与 UUID 批量生成（静态方法）。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `GetRandomList<T>` | `List<T> GetRandomList<T>(IEnumerable<T> list, int count)` | 从序列随机抽取 count 个元素（拷贝后洗牌截断，count 自动钳制到 [0, n]） |
| `GenerateUuids` | `Result<UuidGenerateResult> GenerateUuids(int count, bool includeHyphens, bool upperCase)` | 批量生成 UUID/GUID 字符串 |

### `UuidGenerateResult`  `sealed record`

UUID 生成结果：`Output`（每行一个的 UUID 文本）。

### `ModelIdMatchHelp`  `static class`

键三段回退匹配（源自模型 id 查表场景，逻辑与 AI 无关，任何「带 `vendor/key` 形态键的查表」可复用）：精确 → 去掉最后一段 `vendor/` 前缀后的尾段 → 候选键以 `/key` 结尾；全部 OrdinalIgnoreCase。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `TryMatch` | `static TValue? TryMatch<TEntry, TValue>(IEnumerable<TEntry> entries, Func<TEntry, string> keySelector, string? key, Func<TEntry, TValue> valueSelector) where TValue : struct` | 按三段回退取第一个命中的值；空白键或都不命中返回 null |

---

## CalculationHelps

### `BitwiseHelp`  `static class`

位运算计算工具：支持 AND、OR、XOR、NOT 及左右移位。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `Compute` | `Result<BitwiseComputeResult> Compute(string operation, long operandA, long operandB, int operandC, int bitWidth, bool isSigned, long currentResult, bool shiftFromResult)` | 执行位运算（AND/OR/XOR/NOT/SHL/SHR_A/SHR_L）并按位宽归一化 |
| `ApplyShift` | `long ApplyShift(string operation, long operandA, long currentResult, int operandC, int bitWidth, bool isSigned, bool fromResult)` | 移位运算：被移位数可选操作数 A 或当前结果，移位量钳制在 0–bitWidth |
| `IsShiftOp` | `bool IsShiftOp(string op)` | 判断是否为移位操作 |
| `GetMask` | `long GetMask(int bits)` | 生成指定位宽的掩码 |
| `SignExtend` | `long SignExtend(long value, int originalBits)` | 将无符号值按原始位宽做符号扩展 |
| `NormalizeResult` | `long NormalizeResult(long res, int bitWidth, bool isSigned, bool signExtendWhenSigned = true)` | 将结果裁剪到指定位宽（有符号时可选符号扩展） |
| `FormatValue` | `string FormatValue(long value, int bitWidth, bool isSigned)` | 格式化值为十进制字符串（按有/无符号展示） |
| `FormatBinary` | `string FormatBinary(long value, int bitWidth)` | 格式化为二进制（左侧补零至目标位宽，每 4 位空格分隔） |
| `FormatHex` | `string FormatHex(long value, int bitWidth)` | 格式化为十六进制 |

### `BitwiseComputeResult`  `sealed record`

位运算结果：`Result`（按位宽归一化后的十进制 `long` 值）。

### `RadixHelp`  `static class`

进制转换工具：支持 2–128 进制互转。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `ConvertFrom` | `Result<RadixConvertResult> ConvertFrom(string? value, int fromBase)` | 将 `fromBase` 进制输入批量换算为所有常用进制（2/8/10/16/32/64/128） |
| `Parse` | `BigInteger Parse(string value, int radix)` | 将指定进制的字符串解析为 BigInteger（2/8/10/16 走内置快路径；暂不支持负数） |
| `Format` | `string Format(BigInteger value, int radix, bool upperCaseForHex = true)` | 将 BigInteger 格式化为指定进制字符串 |
| `GetDigitAlphabet` | `string GetDigitAlphabet(int radix)` | 获取指定进制使用的字符集（如 16 进制返回 `0-9A-F`） |
| `DigitAlphabetHint` | `string DigitAlphabetHint { get; }` | 进制字符表的人类可读提示（属性） |
| `ProgrammerBases` | `static IReadOnlyList<int>` | 常用进制列表（2/8/10/16/32/64/128） |
| `ClearRadixValues` | `RadixConvertResult ClearRadixValues()` | 清除/重置输入值（UI 辅助） |

### `RadixConvertResult`  `sealed record`

进制转换结果：`ValuesByRadix`（`IReadOnlyDictionary<int, string>`，各常用进制对应的字符串表示）。

### `ColorConvertHelp`  `static class`

颜色转换工具：HEX ↔ RGBA ↔ HSL。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `NormalizeHexDigits` | `string NormalizeHexDigits(string? value)` | 规范化 HEX 输入（去 `#`、截断到最多 8 位、转大写） |
| `FromHex` | `Result<ColorState> FromHex(string hexDigits)` | 解析 6 位 RGB 或 8 位 RGBA Hex；长度/格式非法时 Fail |
| `FromRgba` | `ColorState FromRgba(int r, int g, int b, int a)` | 从 RGBA 分量构建颜色状态（各分量钳制到 0–255） |
| `ClampByte` | `int ClampByte(int value)` | 将值裁剪到 0–255 范围 |
| `AlphaPercentToByte` | `int AlphaPercentToByte(int alphaPercent)` | 透明度百分比 (0–100) 转 Alpha 字节值 (0–255) |

### `ColorState`  `sealed record`

颜色状态：`HexDigits`、`R`/`G`/`B`/`A`（0–255）、`H`/`S`/`L`（H 0–360，S/L 0–100）。另有 `HexDisplay`（带 `#`）、`RgbaCss`、`AlphaPercent`。

---

## EncodingHelps

### `Base64Help`  `static class`

Base64 编解码，支持标准与 URL-safe 两种字母表。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `Encode` | `Result<string> Encode(string? plainText, bool useUrlSafe)` | 将 UTF-8 文本编码为 Base64（可选 URL-safe 字母表） |
| `Decode` | `Result<string> Decode(string? base64Text, bool useUrlSafe)` | 将 Base64 解码为 UTF-8 文本（URL-safe 时先还原字母表与填充） |

### `AesGcmCryptoHelp`  `static class`

AES-GCM 字符串加解密：密钥由上下文（如站点 Origin）+ 前缀派生，密文带版本前缀，适合浏览器存储等半可信环境。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `Encrypt` | `string Encrypt(string plaintext, string context, string keyMaterialPrefix = "")` | 加密为 `v1:Base64(nonce‖cipher‖tag)` |
| `TryDecrypt` | `bool TryDecrypt(string stored, string context, out string? plaintext, string keyMaterialPrefix = "")` | 尝试解密；前缀不符/载荷损坏/上下文不匹配返回 false |
| `EncryptedPrefix` | `const string` | 密文版本前缀 `v1:` |

### `EncodingHelp`  `static class`

编码辅助工具：常见编码获取与十六进制字节解析/格式化。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `TextHexEncodings` | `static IReadOnlyList<EncodingOption> TextHexEncodings` | 预定义编码列表（UTF-8、UTF-16、ASCII、Latin-1、GB2312/GBK/GB18030、Big5） |
| `GetEncoding` | `Encoding GetEncoding(string id)` | 按编码 Id（如 "gbk"）获取 Encoding；未知 Id 回退 UTF-8（静态构造注册 CodePages 提供程序） |
| `ParseHexBytes` | `byte[] ParseHexBytes(string input, int expectedSize = 0)` | 解析 HEX 字符串为字节数组（容忍 `0x`/`\x` 前缀、分隔符与 `\xNN` C 转义；`expectedSize > 0` 时校验字节数） |
| `FormatHexBytes` | `string FormatHexBytes(byte[] bytes, bool uppercaseHex = true)` | 字节数组格式化为空格分隔的 HEX 字符串（可选大小写） |

### `EncodingHelp.EncodingOption`  `sealed record`（嵌套）

编码选项：`Id`（编码 Id）、`Label`（展示名）、`CodePageName`（传给 `Encoding.GetEncoding` 的名称）。

### `TextHexHelp`  `static class`

文本与十六进制互转工具。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `EncodeTextToHex` | `Result<TextHexEncodeResult> EncodeTextToHex(string? textInput, string encodingId, string displayModeId, bool uppercaseHex, bool showUnicodeEscape, bool showCEscape)` | 文本按指定编码转 HEX，输出空格分隔 Hex 与附加展示块（连续 Hex、\xNN、\uXXXX） |
| `DecodeHexToText` | `Result<TextHexDecodeResult> DecodeHexToText(string? hexInput, string encodingId)` | HEX（容忍 0x/\x 前缀与分隔符）按指定编码解码为文本 |

### `TextHexEncodeResult`  `sealed record`

编码结果：`HexInput`（空格分隔 Hex）、`Output`（按展示模式组装的完整输出）。

### `TextHexDecodeResult`  `sealed record`

解码结果：`TextInput` 与 `Output`（解码文本，两字段一致便于双向绑定）。

### `MimeHelp`  `static class`

MIME 类型辅助工具：扩展名推断、Base64 图片魔数检测、Base64 校验与 Data URI 解析/构建。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `GuessContentType` | `string GuessContentType(string path)` | 按扩展名推断常见 MIME（png/jpeg/webp/gif/bmp/svg/json/csv/md 等）；未知返回 text/plain |
| `GuessImageMime` | `string GuessImageMime(string fileName, string contentType)` | contentType 以 image/ 开头时原样返回，否则按扩展名推断（缺省 image/jpeg） |
| `DetectImageMimeType` | `string DetectImageMimeType(string base64)` | 按 Base64 前缀魔数检测图片类型（png/jpeg/gif/svg/bmp/webp）；无法识别返回空字符串 |
| `IsValidBase64` | `bool IsValidBase64(string value)` | 校验字符串是否为合法 Base64 |
| `TryDecodeBase64` | `bool TryDecodeBase64(string? value, out byte[] bytes)` | 解码 Base64 为字节；空白或非法返回 false |
| `NormalizeImageMime` | `string NormalizeImageMime(string? mimeOrType)` | `image/jpg` → `image/jpeg`；裸类型 png/jpg/gif/webp 等补 `image/` 前缀 |
| `TryParseDataUrl` | `bool TryParseDataUrl(string input, out string dataUrl, out string base64Part, out string mimeType)` | 拆解 `data:<mime>;base64,<payload>`（RFC 2397）；非该形式返回 false |
| `BuildDataUrl` | `string BuildDataUrl(string mimeType, byte[] bytes)` | 由字节构建 `data:<mime>;base64,<payload>` |
| `BuildDataUrl` | `string BuildDataUrl(string mimeType, string base64)` | 由已有 Base64 字符串构建 Data URI（不再二次编码） |

### `JwtParseHelp`  `static class`

JWT（JSON Web Token）解析工具：解码 Header/Payload 并提取过期时间。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `Parse` | `Result<JwtParseResult> Parse(string? token)` | 解析 JWT，返回 Header/Payload JSON 与 exp 过期时间 |

### `JwtParseResult`  `sealed record`

JWT 解析结果：`HeaderJson`、`PayloadJson`（缩进 JSON）、`ExpiryUtc`、`ExpiryLocal`（过期时间文本，无 exp 时为空字符串）。

### `UrlCodecHelp`  `static class`

URL 编解码工具。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `Encode` | `Result<string> Encode(string? plainText, bool useEscapeDataString)` | URL 编码；`useEscapeDataString=true` 时用 RFC 3986（`Uri.EscapeDataString`），否则用 `WebUtility.UrlEncode` |
| `Decode` | `Result<string> Decode(string? encodedText, bool useEscapeDataString)` | URL 解码；`useEscapeDataString=true` 时用 `Uri.UnescapeDataString`，否则用 `WebUtility.UrlDecode` |

---

## Exceptions

### `FileBigException`  `class : Exception`

文件过大时抛出的自定义异常。

| 构造函数 | 说明 |
|:---|:---|
| `FileBigException(string message)` | 指定错误消息 |
| `FileBigException(string message, Exception inner)` | 指定错误消息与内部异常 |

---

## FileHelps

### `FileHelp`  `static class`

文件操作工具集：创建、读写、复制、删除、目录遍历等。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `EnsureDirectoryExists` | `void EnsureDirectoryExists(string path)` | 确保目录存在：路径以分隔符结尾时创建该目录，否则视为文件路径并创建其父目录 |
| `ReadAllBytesAsync` | `Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken ct)` | 将任意流完整读入内存并返回字节数组（可寻址流先重置到起始位置）；适用于上传文件/网络流 |
| `FormatFileSize` | `string FormatFileSize(long bytes)` | 字节数格式化为人类可读大小（如 `1.5 MB`），按 1024 进位，最多两位小数 |
| `CreateFile` | `bool CreateFile(string path)` | 文件不存在时创建（含目录）；返回是否实际创建 |
| `WriteAllText` | `void WriteAllText(string path, string content, Encoding? encoding = null)` | 同步写入全部文本（自动建目录，默认 UTF8，null 内容按空串） |
| `WriteAllTextAsync` | `Task WriteAllTextAsync(string path, string content, Encoding? encoding = null, CancellationToken ct = default)` | 异步写入全部文本（自动建目录，默认 UTF8） |
| `ReadAllText` | `string ReadAllText(string path, Encoding? encoding = null)` | 同步读取全部文本（文件缺失返回空字符串） |
| `ReadAllTextAsync` | `Task<string> ReadAllTextAsync(string path, Encoding? encoding = null, CancellationToken ct = default)` | 异步读取全部文本（文件缺失返回空字符串） |
| `DeleteIfExists` | `bool DeleteIfExists(string filePath)` | 文件存在时删除；返回是否实际删除 |
| `CopyFile` | `void CopyFile(string sourcePath, string destPath, bool overwrite = true)` | 复制文件（默认覆盖） |
| `GetFilesRecursively` | `IEnumerable<string> GetFilesRecursively(string directory, string? searchPattern = null)` | 惰性递归枚举目录下所有文件；目录不存在返回空 |
| `GetDirectorySize` | `long GetDirectorySize(string directory, string? searchPattern = null)` | 计算目录总大小（字节，忽略不可访问文件） |
| `SafeFileName` | `string SafeFileName(string name)` | 将文件名中的非法字符替换为下划线 |
| `CreateFileStream` | `FileStream CreateFileStream(string path, int bufferSize = 81920, bool overwrite = true)` | 创建异步写入文件流（Asynchronous | SequentialScan） |
| `WriteBytesAsync` | `Task WriteBytesAsync(string filePath, byte[] data, CancellationToken ct)` | 异步写入字节数组 |
| `WriteStreamAsync` | `Task WriteStreamAsync(string path, Stream source, bool overwrite = true, CancellationToken cancellationToken = default)` | 将源流从当前位置异步复制写入文件（不关闭源流） |
| `CreateEmptyFileDurable` | `void CreateEmptyFileDurable(string filePath)` | 原子性创建空文件（防止并发冲突） |
| `AreSamePath` | `bool AreSamePath(string? pathA, string? pathB)` | 判断是否同一位置：绝对化、统一分隔符、去掉末尾分隔符；Windows 忽略大小写；两个空白路径视为相同 |
| `NormalizePathForCompare` | `string NormalizePathForCompare(string? path)` | 整理成可比较路径（Trim / GetFullPath / 分隔符统一 / 去末尾分隔符）；非法路径不抛，退回斜杠统一 |
| `TryResolveUnderRoot` | `bool TryResolveUnderRoot(string? root, string? relativePath, out string fullPath)` | 把相对路径接到根目录下；拒绝空白、非法路径和 `..` 逃逸；已是绝对路径时仍须落在根内；失败不抛，`fullPath` 为空串 |

### `LockFileHelper`  `static class`

文件独占锁：进程内按规范化绝对路径排队，跨进程 `FileShare.None`。不解析内容，由调用方通过 `Action`/`Func` 自行处理 `FileStream`。

打开后 `Position == 0`。读到 EOF 后指针在末尾；覆盖整文件须先 `Seek(0, Begin)` 再写，写完用 `SetLength(Position)` 截掉旧尾部。不要 Dispose 回调里的 stream。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `WithExclusiveLock` | `static void WithExclusiveLock(string filePath, Action<FileStream> action)` | 独占打开（不存在则创建空文件），锁内执行 action |
| `WithExclusiveLock<TResult>` | `static TResult WithExclusiveLock<TResult>(string filePath, Func<FileStream, TResult> func)` | 同上，并返回 func 的结果 |

### `WorkspacePathSandbox`  `static class`

根目录与额外允许目录的路径边界：包含判定、可读/可写路径解析、相对路径归一，含软链接 / 目录联接真实目标防逃逸（逐级 `ResolveLinkTarget`）。不读取产品设置。纯 `System.IO`，无状态。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `IsWithinRoot` | `static bool IsWithinRoot(string path, string root)` | 路径是否位于 root 内（含根本身）且无指向 root 之外的链接逃逸 |
| `ResolveAllowedReadPath` | `static string? ResolveAllowedReadPath(string root, string? requestedPath, bool allowOutsideRoot, IEnumerable<string>? allowedDirectories)` | 相对路径与主根内绝对路径优先；`allowOutsideRoot` 为 true 时再查额外目录。越界或链接逃逸返回 null |
| `ResolveAllowedEditPath` | `static string? ResolveAllowedEditPath(string root, string requestedPath, IEnumerable<string>? allowedDirectories)` | 主根优先，否则在额外目录中解析可写路径；越界返回 null |
| `ResolveRelativePath` | `static string? ResolveRelativePath(string root, string relativePath)` | 只接受相对路径，归一为根内绝对路径；绝对路径、越界或链接逃逸返回 null |

---

## JsonHelps

### `JsonExtensions`  `static class`

`System.Text.Json` 序列化/反序列化扩展方法。序列化不限制泛型约束，支持值类型（如 `JsonElement`/`JsonNode`）；预置 `JsonSerializerOptions` 为静态只读实例，避免每次调用重建序列化元数据。

全小写 + 缩进写法：`obj.JsonSe(JsonExtensions.IndentedLowerCaseOptions)`；保留原属性名 + 缩进：`obj.JsonSe(JsonExtensions.IndentedRelaxedOptions)`；紧凑不缩进：`obj.JsonSe(JsonExtensions.CompactRelaxedOptions)`。容错反序列化：`json.JsonDe<T>(JsonExtensions.LenientDeserializeOptions)`。HTTP 请求体用 `obj.JsonSeUtf8(JsonExtensions.CompactRelaxedOptions)`。

| 成员 | 签名 | 说明 |
|:---|:---|:---|
| `LowerCaseNamingPolicyInstance` | `static readonly LowerCaseNamingPolicy` | 属性名全小写命名策略单例 |
| `IndentedLowerCaseOptions` | `static readonly JsonSerializerOptions` | 缩进 + 属性名全小写 + 非 ASCII 不转义 |
| `IndentedRelaxedOptions` | `static readonly JsonSerializerOptions` | 缩进 + 保留原属性名 + 非 ASCII 不转义 |
| `CompactRelaxedOptions` | `static readonly JsonSerializerOptions` | 不缩进 + 保留原属性名 + 非 ASCII 不转义（存储 / HTTP） |
| `LenientDeserializeOptions` | `static readonly JsonSerializerOptions` | 忽略属性名大小写、容忍尾逗号 |
| `JsonSe` | `string JsonSe(this JsonElement element, JsonSerializerOptions options)` | 将 `JsonElement` 序列化为 JSON 文本（基于 `Utf8JsonWriter` + `ArrayBufferWriter<byte>`，AOT 安全） |
| `JsonSe<T>` | `string JsonSe<T>(this T obj, JsonTypeInfo<T> typeInfo)` | 使用源生成 `JsonTypeInfo<T>` 序列化（AOT / Trim 安全） |
| `JsonDe<T>` | `T? JsonDe<T>(this string json, JsonTypeInfo<T> typeInfo)` | 使用源生成 `JsonTypeInfo<T>` 反序列化（AOT / Trim 安全） |
| `JsonSeUtf8<T>` | `byte[] JsonSeUtf8<T>(this T obj, JsonTypeInfo<T> typeInfo)` | 使用源生成 `JsonTypeInfo<T>` 序列化为 UTF-8 字节（AOT / Trim 安全） |
| `JsonSe<T>` | `string JsonSe<T>(this T obj)` | 使用 `JsonSerializer` 默认 Options 序列化 |
| `JsonSe<T>` | `string JsonSe<T>(this T obj, JsonSerializerOptions options)` | 使用指定 Options 序列化 |
| `JsonSeUtf8<T>` | `byte[] JsonSeUtf8<T>(this T obj, JsonSerializerOptions options)` | 序列化为 UTF-8 JSON 字节（请求体）；Options 决定缩进与转义 |
| `JsonDe<T>` | `T? JsonDe<T>(this string json) where T : class` | 使用默认 Options 反序列化 |
| `JsonDe<T>` | `T? JsonDe<T>(this string json, JsonSerializerOptions options) where T : class` | 使用指定 Options 反序列化 |

#### `LowerCaseNamingPolicy`  `class : JsonNamingPolicy`

属性名全小写命名策略（`ConvertName` → `name.ToLowerInvariant()`）。嵌套于 `JsonExtensions`。

### `MarkdownJsonHelp`  `static class`

Markdown 围栏 JSON 提取工具：剥离代码围栏并规范化 JSON 文本（LLM 输出清洗常用）。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `StripCodeFence` | `string StripCodeFence(string text)` | 剥离 ```/```json 围栏：命中时返回围栏内文本（已 Trim），否则返回原文本 Trim |
| `NormalizeToJson` | `string NormalizeToJson(string raw)` | 剥围栏后解析并重新序列化为缩进、非 ASCII 不转义的展示格式；输入为空或解析失败时抛出异常 |

### `FlexibleBoolConverter`  `class : JsonConverter<bool>`

容错 bool 转换器：接受 `true/false`、`"true"/"false"`、`"1"/"0"`、`1/0` 等多种 JSON 表示。

| 方法 | 说明 |
|:---|:---|
| `Read` | 读取并容错转换为 bool；无法识别时返回 false |
| `Write` | 写出标准 JSON 布尔值 |

### `JsonElementHelp`  `static class`

从 `JsonElement` 取常用标量与容器。不解析文档、不识别业务字段名。父节点不是对象时取值方法返回空/false，不会抛。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `TryGetProperty` | `bool TryGetProperty(JsonElement parent, string name, out JsonElement value)` | 取属性；父节点非对象或无名则为 false |
| `TryGetObject` | `bool TryGetObject(JsonElement parent, string name, out JsonElement value)` | 取 object 属性 |
| `TryGetArray` | `bool TryGetArray(JsonElement parent, string name, out JsonElement value)` | 取 array 属性 |
| `GetString` | `string? GetString(JsonElement parent, string name)` | 取字符串；缺失或类型不对为 null |
| `GetNonEmptyString` | `string? GetNonEmptyString(JsonElement parent, string name)` | Trim 后非空字符串；空白为 null |
| `GetNonEmptyString` | `string? GetNonEmptyString(JsonElement node)` | 节点本身是字符串则 Trim；空白为 null |
| `GetString` | `string? GetString(JsonElement node)` | 节点本身是字符串则取值；不 Trim |
| `GetStringList` | `IReadOnlyList<string> GetStringList(JsonElement parent, string name)` | 字符串数组，跳过非字符串与空白项 |
| `GetInt32` | `int? GetInt32(JsonElement parent, string name)` | 取 32 位整数；超出 int 范围钳到边界 |
| `GetInt32` | `int? GetInt32(JsonElement node)` | 把数字节点收成 32 位整数 |
| `GetPositiveInt` | `int? GetPositiveInt(JsonElement node)` | 正整数；数字走 GetInt32，字符串则尝试解析 |
| `GetPositiveInt` | `int? GetPositiveInt(JsonElement parent, string name)` | 取对象上的正整数属性 |
| `GetDouble` | `double? GetDouble(JsonElement parent, string name)` | 取双精度数字 |
| `GetBool` | `bool? GetBool(JsonElement parent, string name)` | 只认 JSON true/false |
| `GetDateTimeOffset` | `DateTimeOffset? GetDateTimeOffset(JsonElement parent, string name)` | 字符串属性解析为 DateTimeOffset |
| `ArrayContainsString` | `bool ArrayContainsString(JsonElement parent, string name, string value, StringComparison comparison = OrdinalIgnoreCase)` | 字符串数组是否含指定值 |
| `TryParse` | `bool TryParse(string? json, out JsonDocument? document)` | 解析 JSON 文本；空白或非法为 false，成功时调用方 Dispose |

---

## LogHelps

### `LogHelp`  `static class`

Serilog 全局日志装配：按天滚动文件为主，控制台 / Debug 可选，Microsoft / System 命名空间降级降噪；不负责业务埋点与 DI 注册（宿主自行 `builder.Logging.AddSerilog(...)`）。

| 成员 | 签名 | 说明 |
|:---|:---|:---|
| `LogOutputTemplate` | `const string` | 默认输出模板（本地时间 / 三位级别 / SourceContext / 消息 / 异常） |
| `CreateFileLogger` | `static Serilog.ILogger CreateFileLogger(string logDirectory, string applicationName, LogEventLevel minimumLevel = Information, LogEventLevel frameworkMinimumLevel = Warning, bool console = false, bool debug = false, int retainedFileCountLimit = 14, Action<LoggerConfiguration>? configure = null)` | 装配并返回 Logger（产出 `{dir}/{app}-yyyyMMdd.log`，目录自动创建）；不改静态 `Log.Logger`，由调用方赋值 |
| `CreateLoggerFactory` | `static ILoggerFactory CreateLoggerFactory(bool dispose = false)` | 把当前 `Log.Logger` 包成 MEL 工厂（无 DI 容器 / 测试用） |

典型接线（MAUI 宿主）：

```csharp
Log.Logger = LogHelp.CreateFileLogger(logDir, "airouter", debug: true);
builder.Logging.AddSerilog(dispose: true);   // 退出时随宿主释放并刷新文件
```

---

## NetHelps

### `HttpExtensions`  `static class`

`HttpRequestMessage` 扩展：将 HTTP 请求导出为等价 curl 命令。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `ToCurlStringAsync` | `Task<string> ToCurlStringAsync(this HttpRequestMessage request)` | 转换为 curl 命令字符串（含方法、URL、请求头、JSON/表单体） |

### `HttpErrorHelp`  `static class`

HTTP 错误响应消息提取工具：从 JSON 错误体提取用户可读消息。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `FormatFailure` | `string FormatFailure(string prefix, HttpStatusCode code, string responseText)` | 拼接用户可读失败消息：能提取到消息时为「prefix：HTTP 500，detail」，否则「prefix：HTTP 500。」 |
| `TryExtractMessage` | `string? TryExtractMessage(string responseText)` | 依次识别 `error`（字符串）、`error.message`（嵌套对象）、`message`（字符串）；解析失败或无匹配返回 null |
| `FormatExceptionChain` | `string FormatExceptionChain(Exception ex, string separator = " → ")` | 聚合异常链消息（跳过空消息与相邻重复），全部为空时返回异常类型名 |

### `SseLineHelp`  `static class`

剥 SSE 文本行的 `data:` 前缀。不解析 JSON、不识别 event/id 字段。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `TryGetDataPayload` | `bool TryGetDataPayload(string? line, out string payload)` | 取出 `data:` 后已 Trim 的载荷；空行、非 data、空 data、`[DONE]` 返回 false |

### `SseFrame`  `readonly record struct`

一帧 SSE：`EventName`（event 名）、`Data`（已拼接的 data 载荷）。不解析 JSON。

### `SseFrameReader`  `sealed class`

按行组装 SSE 帧：识别 `event:`、多行 `data:`，空行结束一帧。不识别业务 event 名、不解析 JSON。默认 event 名为 `message`。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `TryFeed` | `bool TryFeed(string? line, out SseFrame frame)` | 喂入一行；空行结束当前帧（data 可为空） |
| `TryFlush` | `bool TryFlush(out SseFrame frame)` | 流结束时取出尚未以空行结束、但已有 data 的尾帧 |

### `UriHelp`  `static class`

HTTP 根地址规范化与路径拼接。不发请求、不解析查询串。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `NormalizeRoot` | `string NormalizeRoot(string? url)` | 去掉首尾空白和末尾斜杠；空或空白则空串 |
| `Combine` | `Uri Combine(string? root, string path)` | 根地址与相对路径拼成绝对 URI；path 可带或不带前导斜杠 |

### `HttpTransportHelp`  `class`

HTTP JSON 传输工具：共享连接池的 `HttpClient` 工厂、JSON POST 构建、瞬时失败自动重试与响应体读取；适用于无 `IHttpClientFactory` 场景的高频 API 调用（如 LLM/OpenAI 风格接口）。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `HttpTransportHelp` | `HttpTransportHelp(HttpClient httpClient)` | 注入 `HttpClient` 创建传输服务；也可手动创建 |
| `CreateJsonContent<T>` | `ByteArrayContent CreateJsonContent<T>(T payload, JsonTypeInfo<T> typeInfo)` | 使用源生成 `JsonTypeInfo<T>` 序列化为 UTF-8 JSON 内容（AOT / Trim 安全） |
| `CreateJsonContent<T>` | `ByteArrayContent CreateJsonContent<T>(T payload)` | 序列化为 UTF-8 JSON 内容（`application/json; charset=utf-8`） |
| `CreateJsonPost` | `HttpRequestMessage CreateJsonPost(Uri uri, byte[] jsonBody)` | 构建 JSON POST 请求（HTTP/1.1 优先，允许降级） |
| `SendJsonPostWithRetryAsync` | `virtual Task<HttpResponseMessage> SendJsonPostWithRetryAsync(Uri, byte[], Action<HttpRequestMessage>?, CancellationToken)` | 发送 JSON POST，瞬时网络失败最多重试 3 次（退避 400ms×次数）；可在派生类中重写 |
| `ReadBodyAsync` | `virtual Task<string> ReadBodyAsync(HttpResponseMessage response, CancellationToken ct)` | 流式读取响应体为文本；可在派生类中重写 |
| `SendAsync` | `virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct = default)` | 发送原始 HTTP 请求；可在派生类中重写 |
| `SendAsync` | `virtual Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpCompletionOption completionOption, CancellationToken ct = default)` | 同上，可指定 `ResponseHeadersRead` 以便流式读 body |

### `EndianHexHelp`  `static class`

数值与十六进制字节互转：支持 int8–uint64、float32/float64 十种类型与大端/小端字节序。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `ConvertValueToHex` | `Result<EndianHexResult> ConvertValueToHex(string? input, string? dataTypeId, bool bigEndian, bool inputIsHex)` | 数值转 HEX 字节；同时输出二进制与十进制 |
| `ConvertHexToValue` | `Result<EndianHexResult> ConvertHexToValue(string? input, string? dataTypeId, bool bigEndian)` | HEX 字节转十进制数值 |
| `DataTypes` | `static IReadOnlyList<EndianHexDataType> DataTypes` | 全部支持的数据类型（int8/uint8/int16/…/float64） |

### `EndianHexDataType`  `sealed record`

数据类型定义：`Id`、`Label`、`ByteSize`、`IsFloat`。

### `EndianHexResult`  `sealed record`

端序转换结果：`HexOutput`（空格分隔）、`BinaryOutput`（每字节 8 位）、`DecimalOutput`。

### `IpCalculatorHelp`  `static class`

IPv4 子网计算器：由 IP + CIDR/掩码计算网络地址、广播地址、主机范围等。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `Calculate` | `Result<IpCalculatorResult> Calculate(string? input)` | 计算子网信息；支持 `192.168.1.10/24`、`192.168.1.10 255.255.255.0`、纯 IP（/32）三种输入 |

### `IpCalculatorResult`  `sealed record`

子网计算结果：`Address`、`IntegerValue`、`HexValue`、`SubnetMask`、`WildcardMask`、`NetworkAddress`、`BroadcastAddress`、`FirstHost`、`LastHost`、`HostCount`、`Cidr`。

### `HttpExec`  `abstract class`

HTTP 发送公共核心：组请求、超时发送、读响应。不负责提供 `HttpClient`（由子类实现 `GetClient()`），可选日志。无重试（瞬时重试见 `HttpTransportHelp`）。

`StringBodyHttpService`（`IHttpService`）接收已准备好的 JSON 字符串；`TypedBodyHttpService`（`ITypedHttpService`）在内部把对象序列化成 JSON。具体入口：`RequestHelp` / `HttpClientFactoryService`、`TypedRequestHelp` / `TypedHttpClientFactoryService`。

### `RequestHelp`  `class : StringBodyHttpService`

使用调用方提供的 `HttpClient` 的快捷请求工具；实现 `IHttpService`，不依赖 `IHttpClientFactory` 和日志服务。
请求体参数使用已准备好的 JSON 字符串或表单字典，泛型参数 `T` 表示响应类型而不是请求体类型。
所有发送方法为 `virtual`，支持继承重写，也可手动创建。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `RequestHelp` | `RequestHelp(HttpClient httpClient)` | 注入 `HttpClient` 创建请求服务 |
| `SendRequestAsync` | `virtual Task<int> SendRequestAsync(HttpMethod method, string url, string body, Action<HttpRequestHeaders>? action)` | 使用 JSON 请求体发送指定 HTTP 方法并返回状态码；可在派生类中重写 |
| `PostRequestAsync<T>` | `virtual Task<T?> PostRequestAsync<T>(string url, string body, int timeoutSeconds, Action<HttpRequestHeaders>? action) where T : class` | 发送 JSON POST 请求并将响应反序列化为 `T`；`T` 为 `string` 时直接返回响应文本 |
| `PutRequestAsync<T>` | `virtual Task<T?> PutRequestAsync<T>(string url, string body, int timeoutSeconds, Action<HttpRequestHeaders>? action) where T : class` | 发送 JSON PUT 请求并将响应反序列化为 `T` |
| `DeleteRequestAsync<T>` | `virtual Task<T?> DeleteRequestAsync<T>(string url, int timeoutSeconds, Action<HttpRequestHeaders>? action) where T : class` | 发送无请求体的 DELETE 请求并将响应反序列化为 `T` |
| `GetRequestAsync<T>` | `virtual Task<T?> GetRequestAsync<T>(string url, int timeoutSeconds, Action<HttpRequestHeaders>? action) where T : class` | 发送无请求体的 GET 请求并将响应反序列化为 `T` |
| `PostFormRequestAsync<T>` | `virtual Task<T?> PostFormRequestAsync<T>(string url, Dictionary<string, object> formData, Action<HttpRequestHeaders>? action, int timeoutSeconds) where T : class` | 发送 `application/x-www-form-urlencoded` 表单并将响应反序列化为 `T` |
| `SendAsync` | `virtual Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? body, string? mediaType, int timeoutSeconds, Action<HttpRequestHeaders>? action, CancellationToken cancellationToken)` | 发送原始 HTTP 请求并返回 `HttpResponseMessage`；异常直接抛出，响应由调用方读取并释放 |

### `TypedHttpClientFactoryService`  `class : TypedBodyHttpService`

强类型 JSON 请求工具。请求方法接收 `TRequest` 对象并在内部序列化，不需要调用方手动准备 `body` 字符串；响应使用 `TResponse` 反序列化。
通过 `IHttpClientFactory` 创建客户端，通过 `ILogger<ITypedHttpService>` 记录超时和异常。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `TypedHttpClientFactoryService` | `TypedHttpClientFactoryService(IHttpClientFactory httpClientFactory, ILogger<ITypedHttpService> logger)` | 注入 HTTP 客户端工厂和日志服务 |
| `SendRequestAsync<TRequest>` | `virtual Task<int> SendRequestAsync<TRequest>(HttpMethod method, string url, TRequest requestBody, Action<HttpRequestHeaders>? action)` | 使用强类型请求对象发送 JSON 请求，只返回 HTTP 状态码 |
| `PostRequestAsync<TRequest, TResponse>` | `virtual Task<TResponse?> PostRequestAsync<TRequest, TResponse>(string url, TRequest requestBody, int timeoutSeconds, Action<HttpRequestHeaders>? action) where TResponse : class` | 使用强类型请求对象发送 JSON POST，并将响应反序列化为 `TResponse` |
| `PutRequestAsync<TRequest, TResponse>` | `virtual Task<TResponse?> PutRequestAsync<TRequest, TResponse>(string url, TRequest requestBody, int timeoutSeconds, Action<HttpRequestHeaders>? action) where TResponse : class` | 使用强类型请求对象发送 JSON PUT，并将响应反序列化为 `TResponse` |
| `DeleteRequestAsync<TResponse>` | `virtual Task<TResponse?> DeleteRequestAsync<TResponse>(string url, int timeoutSeconds, Action<HttpRequestHeaders>? action) where TResponse : class` | 发送 DELETE 请求并将响应反序列化为 `TResponse` |
| `GetRequestAsync<TResponse>` | `virtual Task<TResponse?> GetRequestAsync<TResponse>(string url, int timeoutSeconds, Action<HttpRequestHeaders>? action) where TResponse : class` | 发送 GET 请求并将响应反序列化为 `TResponse` |
| `PostFormRequestAsync<TResponse>` | `virtual Task<TResponse?> PostFormRequestAsync<TResponse>(string url, Dictionary<string, object> formData, Action<HttpRequestHeaders>? action, int timeoutSeconds) where TResponse : class` | 发送 urlencoded 表单并将响应反序列化为 `TResponse` |
| `SendAsync<TRequest>` | `virtual Task<HttpResponseMessage> SendAsync<TRequest>(HttpMethod method, string url, TRequest? requestBody, string? mediaType, int timeoutSeconds, Action<HttpRequestHeaders>? action, CancellationToken cancellationToken)` | 使用强类型请求对象发送原始 HTTP 请求 |

### `TypedRequestHelp`  `class : TypedBodyHttpService`

强类型 JSON 请求工具。请求方法接收 `TRequest` 对象并在内部序列化，不需要调用方手动准备 `body` 字符串；响应使用 `TResponse` 反序列化。
使用构造函数注入的 `HttpClient`，适用于无需 `IHttpClientFactory` 和日志服务的简单场景。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `TypedRequestHelp` | `TypedRequestHelp(HttpClient httpClient)` | 注入 `HttpClient` 创建请求服务 |
| `SendRequestAsync<TRequest>` | `virtual Task<int> SendRequestAsync<TRequest>(HttpMethod method, string url, TRequest requestBody, Action<HttpRequestHeaders>? action)` | 使用强类型请求对象发送 JSON 请求，只返回 HTTP 状态码 |
| `PostRequestAsync<TRequest, TResponse>` | `virtual Task<TResponse?> PostRequestAsync<TRequest, TResponse>(string url, TRequest requestBody, int timeoutSeconds, Action<HttpRequestHeaders>? action) where TResponse : class` | 使用强类型请求对象发送 JSON POST，并将响应反序列化为 `TResponse` |
| `PutRequestAsync<TRequest, TResponse>` | `virtual Task<TResponse?> PutRequestAsync<TRequest, TResponse>(string url, TRequest requestBody, int timeoutSeconds, Action<HttpRequestHeaders>? action) where TResponse : class` | 使用强类型请求对象发送 JSON PUT，并将响应反序列化为 `TResponse` |
| `DeleteRequestAsync<TResponse>` | `virtual Task<TResponse?> DeleteRequestAsync<TResponse>(string url, int timeoutSeconds, Action<HttpRequestHeaders>? action) where TResponse : class` | 发送 DELETE 请求并将响应反序列化为 `TResponse` |
| `GetRequestAsync<TResponse>` | `virtual Task<TResponse?> GetRequestAsync<TResponse>(string url, int timeoutSeconds, Action<HttpRequestHeaders>? action) where TResponse : class` | 发送 GET 请求并将响应反序列化为 `TResponse` |
| `PostFormRequestAsync<TResponse>` | `virtual Task<TResponse?> PostFormRequestAsync<TResponse>(string url, Dictionary<string, object> formData, Action<HttpRequestHeaders>? action, int timeoutSeconds) where TResponse : class` | 发送 urlencoded 表单并将响应反序列化为 `TResponse` |
| `SendAsync<TRequest>` | `virtual Task<HttpResponseMessage> SendAsync<TRequest>(HttpMethod method, string url, TRequest? requestBody, string? mediaType, int timeoutSeconds, Action<HttpRequestHeaders>? action, CancellationToken cancellationToken)` | 使用强类型请求对象发送原始 HTTP 请求 |

### `ITypedHttpService`  `interface`

强类型 JSON 请求服务接口。`TRequest` 表示请求对象类型，`TResponse` 表示响应类型；实现类可使用 `IHttpClientFactory` 和日志服务，也可直接使用调用方提供的 `HttpClient`。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `SendRequestAsync<TRequest>` | `Task<int> SendRequestAsync<TRequest>(HttpMethod method, string url, TRequest requestBody, Action<HttpRequestHeaders>? action)` | 使用强类型请求对象发送请求，只返回 HTTP 状态码 |
| `PostRequestAsync<TRequest, TResponse>` | `Task<TResponse?> PostRequestAsync<TRequest, TResponse>(string url, TRequest requestBody, int timeoutSeconds, Action<HttpRequestHeaders>? action) where TResponse : class` | 使用强类型请求对象发送 JSON POST |
| `PutRequestAsync<TRequest, TResponse>` | `Task<TResponse?> PutRequestAsync<TRequest, TResponse>(string url, TRequest requestBody, int timeoutSeconds, Action<HttpRequestHeaders>? action) where TResponse : class` | 使用强类型请求对象发送 JSON PUT |
| `DeleteRequestAsync<TResponse>` | `Task<TResponse?> DeleteRequestAsync<TResponse>(string url, int timeoutSeconds, Action<HttpRequestHeaders>? action) where TResponse : class` | 发送 DELETE 请求并反序列化响应 |
| `GetRequestAsync<TResponse>` | `Task<TResponse?> GetRequestAsync<TResponse>(string url, int timeoutSeconds, Action<HttpRequestHeaders>? action) where TResponse : class` | 发送 GET 请求并反序列化响应 |
| `PostFormRequestAsync<TResponse>` | `Task<TResponse?> PostFormRequestAsync<TResponse>(string url, Dictionary<string, object> formData, Action<HttpRequestHeaders>? action, int timeoutSeconds) where TResponse : class` | 发送 urlencoded 表单并反序列化响应 |
| `SendAsync<TRequest>` | `Task<HttpResponseMessage> SendAsync<TRequest>(HttpMethod method, string url, TRequest? requestBody, string? mediaType, int timeoutSeconds, Action<HttpRequestHeaders>? action, CancellationToken cancellationToken)` | 使用强类型请求对象发送原始请求 |

### `IHttpService`  `interface`

HTTP 请求服务接口。`T` 表示响应类型；请求体由调用方以 JSON 字符串或表单字典提供。
`HttpClientFactoryService` 使用 `IHttpClientFactory`，`RequestHelp` 使用构造函数注入的 `HttpClient`。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `PostRequestAsync<T>` | `Task<T?> PostRequestAsync<T>(string url, string body, int timeoutSeconds, Action<HttpRequestHeaders>? action) where T : class` | POST JSON 请求并将响应反序列化为 `T` |
| `PutRequestAsync<T>` | `Task<T?> PutRequestAsync<T>(string url, string body, int timeoutSeconds, Action<HttpRequestHeaders>? action) where T : class` | PUT JSON 请求并将响应反序列化为 `T` |
| `DeleteRequestAsync<T>` | `Task<T?> DeleteRequestAsync<T>(string url, int timeoutSeconds, Action<HttpRequestHeaders>? action) where T : class` | DELETE 请求并将响应反序列化为 `T`；请求不包含请求体 |
| `GetRequestAsync<T>` | `Task<T?> GetRequestAsync<T>(string url, int timeoutSeconds, Action<HttpRequestHeaders>? action) where T : class` | GET 请求并将响应反序列化为 `T`；请求不包含请求体 |
| `PostFormRequestAsync<T>` | `Task<T?> PostFormRequestAsync<T>(string url, Dictionary<string, object> formData, Action<HttpRequestHeaders>? action, int timeoutSeconds) where T : class` | POST `application/x-www-form-urlencoded` 表单并将响应反序列化为 `T` |
| `SendAsync` | `Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string? body, string? mediaType, int timeoutSeconds, Action<HttpRequestHeaders>? action, CancellationToken cancellationToken)` | 发送原始请求返回 `HttpResponseMessage`：不吞异常、不反序列化，超时/网络错误直接抛出，响应由调用方释放 |
| `SendRequestAsync` | `Task<int> SendRequestAsync(HttpMethod method, string url, string body, Action<HttpRequestHeaders>? action)` | 使用 JSON 请求体发送指定 HTTP 方法，只返回状态码 |

### `HttpClientFactoryService`  `class : StringBodyHttpService`

`IHttpService` 的默认实现：封装 POST/PUT/DELETE/GET/表单 POST，超时或异常时返回 default 并记录日志。`T` 为 `string` 时直接返回响应文本。需要原始响应（错误体、状态码、流式读取）时用 `SendAsync`，异常直接抛出、不记录不吞。

### `WebPageClient`  `sealed class`

网页抓取并精简为纯文本：只取响应前 600 KB，按响应头 charset 解码（未知回退 UTF-8，静态构造注册 CodePages），HTML 走 `HtmlTextHelp.ToPlainText` / `ExtractTitle` 去标签压缩空白，非 HTML（JSON / 纯文本）原样返回。共享一个静态 HttpClient，也可注入自定义实例；30 秒超时以 `TaskCanceledException` 结束、HTTP 失败抛 `HttpRequestException`，由调用方转错误。

| 成员 | 签名 | 说明 |
|:---|:---|:---|
| `DefaultMaxCharacters` | `const int = 8_000` | 正文返回的默认字符上限 |
| `MaxCharactersLimit` | `const int = 40_000` | 正文返回的字符上限最大值 |
| `DefaultUserAgent` | `const string` | 未传入产品名时共享客户端使用的 User-Agent，不含具体产品名 |
| `FetchAsync` | `Task<WebPageContent> FetchAsync(string url, int maxCharacters = DefaultMaxCharacters, CancellationToken cancellationToken = default)` | 抓取网页；只接受绝对 http/https，否则抛 `ArgumentException`。构造传入的 `userAgent` 写到本次请求。正文收在 1–40000，超限截断并置 `Truncated` |

### `WebPageContent`  `sealed record`

网页抓取结果：`Url`（最终地址）、`Title`、`Text`（正文纯文本）、`Truncated`（是否截断）。

---

## StringHelps

### `StringHelp`  `static class`

文本统计工具：行数、字符数/字节数统计。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `CountLines` | `static int CountLines(string text, string? lineSeparator = null)` | 计算文本行数；默认识别 \r\n、\r、\n 逻辑行，可指定自定义分隔符 |
| `CountCharacters` | `static long CountCharacters(string text, Encoding? encoding)` | 计算字符数或字节数（encoding 为 null 返回字符数，否则返回字节数） |
| `CountFileLinesAndChars` | `static (long Lines, long Count) CountFileLinesAndChars(string filePath, Encoding? encoding)` | 读取文件并计算行数与字符/字节数 |
| `CountFileCharacters` | `static long CountFileCharacters(string filePath, Encoding? encoding)` | 计算文件的字符数或字节数 |
| `TruncateWithEllipsis` | `static string TruncateWithEllipsis(string? text, int maxLength, string ellipsis = "…")` | 超过 maxLength 时截断并加省略号；null/空返回空串；maxLength 为负抛出 |

### `HtmlTextHelp`  `static class`

HTML 转纯文本工具：按标签粗切（不做完整 DOM 解析），丢脚本 / 样式 / 注释、块级标签转换行、解码实体、逐行压缩空白。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `ToPlainText` | `static string ToPlainText(string? html)` | HTML → 纯文本（\n 分行）；null/空白返回空串 |
| `ExtractTitle` | `static string ExtractTitle(string? html)` | 抽取 `<title>` 内容，解码并压缩空白；无标签返回空串 |

### `NamingStyleHelp`  `static class`

标识符命名风格转换：将任意形式标识符拆词后一次性转换为 8 种常见风格。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `Convert` | `Result<NamingStyleResult> Convert(string? input)` | 转换为 camelCase/PascalCase/snake_case/kebab-case/SCREAMING_SNAKE/dot.case/Title Case/lowercase |

### `NamingStyleResult`  `sealed record`

命名风格转换结果：`CamelCase`、`PascalCase`、`SnakeCase`、`KebabCase`、`ScreamingSnake`、`DotCase`、`TitleCase`、`LowerCase`。空输入时所有字段为空字符串（`Empty` 静态属性）。

### `TextLinesHelp`  `static class`

多行文本处理工具：去重、排序、去空行、裁剪空白与行级统计。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `ComputeStats` | `static TextLineStats ComputeStats(string? text, bool applyTrim, bool removeEmpty, bool ignoreCase)` | 统计行数、非空行数、去重行数、单词数、字符数 |
| `Deduplicate` | `static Result<TextLinesOperationResult> Deduplicate(string? text, bool trimLines, bool ignoreEmptyLines, bool ignoreCase, bool keepOrder)` | 多行文本去重（可选保留首次出现顺序） |
| `SortAsc` | `static Result<TextLinesOperationResult> SortAsc(string? text, bool trimLines, bool ignoreEmptyLines, bool ignoreCase)` | 按字典序升序排序 |
| `SortDesc` | `static Result<TextLinesOperationResult> SortDesc(string? text, bool trimLines, bool ignoreEmptyLines, bool ignoreCase)` | 按字典序降序排序 |
| `RemoveEmptyLines` | `static Result<TextLinesOperationResult> RemoveEmptyLines(string? text, bool trimLines)` | 移除所有空白行 |
| `TrimOnly` | `static Result<TextLinesOperationResult> TrimOnly(string? text)` | 裁剪每行首尾空白（保留空行） |

### `TextLineStats`  `sealed record`

文本统计信息：`LineCount`、`NonEmptyLineCount`、`UniqueLineCount`、`WordCount`、`CharCount`、`CharNoWhitespaceCount`。

### `TextLinesOperationResult`  `sealed record`

多行文本操作结果：`Output`（处理后的文本）、`StatusMessage`（状态提示）。

### `TextNormalizeHelp`  `static class`

文本符号归一化工具：支持全角/半角字符与中英文标点之间的转换。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `NormalizeSymbolsToAscii` | `static string NormalizeSymbolsToAscii(string? text, bool removeSpaces)` | 归一化 OCR 文本：先 FormKC 归一化，再映射中文标点为英文标点；可选去除空格 |
| `NormalizeSymbolsToFullWidth` | `static string NormalizeSymbolsToFullWidth(string? text, bool convertSpaces)` | ASCII 可打印字符转全角，并将常见英文标点映射为中文标点；可选将空格转为全角空格 |

---

## TaskHelps

### `RetryHelp`  `static class`

按调用方谓词做指数退避重试。不判断何种异常可重试。

| 成员 | 签名 | 说明 |
|:---|:---|:---|
| `DefaultMaxAttempts` | `const int` | 默认最多尝试 3 次（含首次） |
| `DefaultBaseBackoffMs` | `const int` | 默认首次重试等待 1000ms |
| `DefaultMaxBackoffMs` | `const int` | 默认单次等待上限 8000ms |
| `WithBackoffAsync<T>` | `Task<T> WithBackoffAsync(Func<CancellationToken, Task<T>> action, Func<Exception, bool> shouldRetry, CancellationToken cancellationToken, int maxAttempts = 3, int baseBackoffMs = 1000, int maxBackoffMs = 8000)` | 失败且 shouldRetry 为真时按 2 的幂退避再跑；全部失败抛最后一次异常。可等待 |

---

## TimeHelps

### `TimestampConvertHelp`  `static class`

Unix 时间戳与日期时间互转工具：支持智能识别秒/毫秒、任意时区显示、ISO 8601 解析与相对时间提示。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `ParseUnix` | `Result<DateTimeOffset> ParseUnix(string? unixSeconds, string? unixMilliseconds, UnixFieldPrefer preferField)` | 解析 Unix 秒/毫秒时间戳（≥12 位按毫秒处理）；按 preferField 优先取值 |
| `ParseUnix` | `Result<DateTimeOffset> ParseUnix(string? unixSeconds, string? unixMilliseconds)` | 简化重载（优先秒字段） |
| `ParseFromTime` | `Result<DateTimeOffset> ParseFromTime(string? localTime, string? utcTime, string timeZoneId)` | 由时区时间/UTC 时间（支持 ISO 8601）解析 DateTimeOffset |
| `BuildDisplay` | `TimestampDisplayResult BuildDisplay(DateTimeOffset dto, string timeZoneId)` | 构建完整展示信息（秒/毫秒戳、本地/UTC 时间、ISO 8601、相对时间提示） |
| `FormatLocalTimeLabel` | `string FormatLocalTimeLabel(string timeZoneId)` | 生成时区展示标签（如 `China Standard Time (UTC+08:00)`） |
| `TryParseUnixToken` | `bool TryParseUnixToken(string text, out DateTimeOffset dto)` | 尝试解析单个时间戳文本（≥12 位按毫秒，否则按秒） |
| `TimeZoneOptions` | `static IReadOnlyList<TimestampTimeZoneOption> TimeZoneOptions` | 时区选项列表（常用时区置顶，其余按 UTC 偏移排序） |

### `TimestampTimeZoneOption`  `sealed record`

时区下拉选项：`Id`、`Label`。

### `TimestampDisplayResult`  `sealed record`

时间戳展示结果：`UnixSeconds`、`UnixMilliseconds`、`LocalTime`、`UtcTime`、`Iso8601Local`、`Iso8601Utc`、`RelativeHint`。

### `UnixFieldPrefer`  `enum`

时间戳解析优先字段：`Seconds`（优先秒）、`Milliseconds`（优先毫秒）。

### `ScheduleTimingHelp`  `static class`

定时触发时刻计算（依赖 Cronos）：固定间隔的锚定推进与 5 段标准 Cron 的下次出现查询。不收间隔下限，Cron 无法计算时返回 null。

| 成员 | 签名 | 说明 |
|:---|:---|:---|
| `ComputeNextInterval` | `static DateTimeOffset ComputeNextInterval(DateTimeOffset afterUtc, int intervalSeconds)` | 基准时刻 + 间隔；`intervalSeconds <= 0` 抛 `ArgumentOutOfRangeException` |
| `TryComputeNextCron` | `static DateTimeOffset? TryComputeNextCron(DateTimeOffset afterUtc, string cronExpression, string? timeZoneId)` | 5 段 Cron 在指定时区的下次出现（不含当前刻）；表达式无效或永无下次返回 null；时区空白 / `Local` / 未知回退本机 |
| `TryComputeInitialNextRun` | `static DateTimeOffset? TryComputeInitialNextRun(ScheduleKind kind, int intervalSeconds, string? cronExpression, string? timeZoneId, DateTimeOffset nowUtc)` | 初始下次时刻；Cron 无效或未知类型返回 null |
| `TryComputeNextAfterFire` | `static DateTimeOffset? TryComputeNextAfterFire(ScheduleKind kind, int intervalSeconds, string? cronExpression, string? timeZoneId, DateTimeOffset firedAtUtc, DateTimeOffset scheduledAtUtc)` | 触发后推进：间隔锚定计划时刻（落后过多从实际触发时刻重新起算）；Cron 按实际触发时刻取下一次，没有则返回 null |

### `ScheduleKind`  `enum`

触发方式：`Interval = 0`（固定间隔）、`Cron = 1`（5 段 Cron）。不含产品侧的间隔下限或失败回退。

---

## WindowsHelps

### `KeyboardInputHelp`  `static class`

Windows 键盘输入模拟：基于 Win32 `SendInput` 以 Unicode 直发键入文本。仅支持 Windows（`[SupportedOSPlatform("windows")]`）；非 Windows 抛出 `PlatformNotSupportedException`。

键入开始前会取消前台窗口 IME 组词并暂时分离输入法（结束后恢复），并松开仍按下的 Shift/Ctrl/Alt/Win，避免中文输入法或修饰键吞字。`SendInput` 经非托管指针按块写入，失败会短延迟重试。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `TypeTextAsync` | `static Task TypeTextAsync(string text, int startDelayMs = 3000, int charIntervalMs = 30, CancellationToken ct = default)` | 空串直接返回；延时后键入。`\n` → 回车、`\t` → Tab、`\r` 忽略。`charIntervalMs <= 0` 时整段连发（分块） |
| `TypeBatchAsync` | `static Task TypeBatchAsync(IReadOnlyList<string> items, int startDelayMs = 3000, int charIntervalMs = 30, int itemIntervalMs = 200, bool pressEnterAfterEach = true, CancellationToken ct = default)` | 依次键入多段。仅当首项非空时等待起始延时；项间停顿；默认每项后补回车。整批共用一次 IME 分离 |

---

## McpHelps

### `StdioMcpClient`  `sealed class : IAsyncDisposable`

stdio MCP（Model Context Protocol）客户端：spawn 子进程后按 JSON-RPC 顺序收发（内部信号量串行化，一请求一响应），支持 initialize 握手、`tools/list`、`tools/call`；释放时杀整棵进程树。JSON-RPC error、server 提前退出抛异常。请求 JSON 用手写 `Utf8JsonWriter` 发出。initialize 的 clientInfo 由构造参数传入，不读本库程序集。

构造：`StdioMcpClient(string serverName, McpServerOptions options, string clientName, string clientVersion)`。`clientName` / `clientVersion` 空白时抛 `ArgumentException`。

| 方法 | 签名 | 说明 |
|:---|:---|:---|
| `ListToolsAsync` | `Task<IReadOnlyList<McpToolInfo>> ListToolsAsync(CancellationToken cancellationToken = default)` | 惰性启动并调用 `tools/list`，返回该 server 的工具清单 |
| `CallToolAsync` | `Task<JsonElement> CallToolAsync(string toolName, JsonElement arguments, CancellationToken cancellationToken = default)` | 调用 `tools/call`，返回 result 的克隆（content 解析由调用方处理） |
| `DisposeAsync` | `ValueTask DisposeAsync()` | 停止进程并释放管道 |

### `McpServerOptions`  `sealed record`

Cursor 兼容的外部 MCP Server 启动配置：`Command`、`Args`、`WorkingDirectory`（可选）、`Enabled`（默认 true）。

### `McpConfiguration`  `sealed record`

Cursor 兼容的 MCP 配置文件：`McpServers`（server 名 → 启动配置字典）。

### `McpToolInfo`  `sealed record`

从 MCP Server 返回的工具描述：`ServerName`、`Name`、`Description`、`InputSchemaJson`。不含调用方的权限等级。
