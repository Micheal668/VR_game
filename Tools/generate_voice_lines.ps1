# 用 Windows 自带的俄语语音（OneCore：Irina 为地面飞控中心，Pavel 为指挥官）把 voice_lines.tsv 合成为 WAV。
# 需要 Windows 10/11 并安装俄语语音包（设置 → 时间和语言 → 语音）。用 Windows PowerShell 5.1 运行：
#   powershell -ExecutionPolicy Bypass -File Tools/generate_voice_lines.ps1
# 结果写入 Assets/_LunarEscape/Audio/Voice/<id>.wav（16 kHz 单声道）。无线电音色（带通、底噪、提示音）在游戏中实时添加。
# 本文件需保存为带 BOM 的 UTF-8，Windows PowerShell 5.1 才能正确读取中文注释。
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$table = Join-Path $PSScriptRoot 'voice_lines.tsv'
$output = Join-Path $root 'Assets/_LunarEscape/Audio/Voice'
New-Item -ItemType Directory -Force $output | Out-Null

Add-Type -AssemblyName System.Runtime.WindowsRuntime
$null = [Windows.Media.SpeechSynthesis.SpeechSynthesizer, Windows.Media.SpeechSynthesis, ContentType = WindowsRuntime]
$null = [Windows.Storage.Streams.DataReader, Windows.Storage.Streams, ContentType = WindowsRuntime]
$asTask = ([System.WindowsRuntimeSystemExtensions].GetMethods() | Where-Object {
    $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' })[0]
function Await($operation, [Type]$type) { $task = $asTask.MakeGenericMethod($type).Invoke($null, @($operation)); $task.Wait(-1) | Out-Null; $task.Result }

$voices = [Windows.Media.SpeechSynthesis.SpeechSynthesizer]::AllVoices
function Voice($name) {
    $voice = $voices | Where-Object { $_.DisplayName -like "*$name*" -and $_.Language -eq 'ru-RU' } | Select-Object -First 1
    if ($null -eq $voice) { throw "缺少俄语语音 $name：请在 Windows 设置中安装俄语语音包。" }
    $voice
}
# 说话人：语音、语速、音高。飞控中心语速稍快、干脆；指挥官声音略低、带点慵懒。
$speakers = @{
    cup = @{ Voice = (Voice 'Irina'); Rate = '+6%'; Pitch = '+0%' }
    cmd = @{ Voice = (Voice 'Pavel'); Rate = '+4%'; Pitch = '-8%' }
}

# 去掉首尾静音（各留 50 ms），峰值归一到 90%，写成标准 44 字节头的 16 位单声道 WAV。
function Write-TrimmedWav([byte[]]$wav, [string]$path) {
    $rate = [BitConverter]::ToUInt32($wav, 24)
    $offset = 12
    while ([Text.Encoding]::ASCII.GetString($wav, $offset, 4) -ne 'data') { $offset += 8 + [BitConverter]::ToUInt32($wav, $offset + 4) }
    $length = [Math]::Min([BitConverter]::ToUInt32($wav, $offset + 4), $wav.Length - $offset - 8)
    $count = [int]($length / 2)
    $samples = New-Object int16[] $count
    [Buffer]::BlockCopy($wav, $offset + 8, $samples, 0, $count * 2)
    $first = 0; while ($first -lt $count -and [Math]::Abs([int]$samples[$first]) -lt 300) { $first++ }
    $last = $count - 1; while ($last -gt $first -and [Math]::Abs([int]$samples[$last]) -lt 300) { $last-- }
    $pad = [int]($rate * 0.05)
    $first = [Math]::Max(0, $first - $pad); $last = [Math]::Min($count - 1, $last + $pad)
    $peak = 1; for ($i = $first; $i -le $last; $i++) { $peak = [Math]::Max($peak, [Math]::Abs([int]$samples[$i])) }
    $gain = 29490.0 / $peak
    $n = $last - $first + 1
    $out = New-Object IO.MemoryStream
    $w = New-Object IO.BinaryWriter($out)
    $w.Write([Text.Encoding]::ASCII.GetBytes('RIFF')); $w.Write([uint32](36 + $n * 2)); $w.Write([Text.Encoding]::ASCII.GetBytes('WAVEfmt '))
    $w.Write([uint32]16); $w.Write([uint16]1); $w.Write([uint16]1); $w.Write([uint32]$rate); $w.Write([uint32]($rate * 2)); $w.Write([uint16]2); $w.Write([uint16]16)
    $w.Write([Text.Encoding]::ASCII.GetBytes('data')); $w.Write([uint32]($n * 2))
    for ($i = $first; $i -le $last; $i++) { $w.Write([int16][Math]::Max(-32768, [Math]::Min(32767, [Math]::Round($samples[$i] * $gain)))) }
    $w.Flush()
    [IO.File]::WriteAllBytes($path, $out.ToArray())
}

$synth = New-Object Windows.Media.SpeechSynthesis.SpeechSynthesizer
$count = 0
foreach ($line in Get-Content -Encoding UTF8 $table) {
    if ($line.Trim() -eq '' -or $line.StartsWith('#')) { continue }
    $id, $who, $text = $line -split "`t", 3
    $speaker = $speakers[$who]
    if ($null -eq $speaker) { throw "未知说话人 '$who'（$id）" }
    $synth.Voice = $speaker.Voice
    $escaped = [System.Security.SecurityElement]::Escape($text)
    $ssml = "<speak version='1.0' xmlns='http://www.w3.org/2001/10/synthesis' xml:lang='ru-RU'><prosody rate='$($speaker.Rate)' pitch='$($speaker.Pitch)'>$escaped</prosody></speak>"
    $stream = Await ($synth.SynthesizeSsmlToStreamAsync($ssml)) ([Windows.Media.SpeechSynthesis.SpeechSynthesisStream])
    $size = [uint32]$stream.Size
    $reader = New-Object Windows.Storage.Streams.DataReader($stream.GetInputStreamAt(0))
    $null = Await ($reader.LoadAsync($size)) ([uint32])
    $bytes = New-Object byte[] $size
    $reader.ReadBytes($bytes)
    $reader.Dispose(); $stream.Dispose()
    Write-TrimmedWav $bytes (Join-Path $output "$id.wav")
    $count++
}
Write-Output "VOICE_LINES_GENERATED $count -> $output"
