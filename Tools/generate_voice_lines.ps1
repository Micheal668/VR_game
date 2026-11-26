# 用 Windows 自带的俄语语音（OneCore：Irina 为地面飞控中心，Pavel 为指挥官）把 voice_lines.tsv 合成为 WAV。
# 需要 Windows 10/11 并安装俄语语音包（设置 → 时间和语言 → 语音）。用 Windows PowerShell 5.1 运行：
#   powershell -ExecutionPolicy Bypass -File Tools/generate_voice_lines.ps1
# 结果写入 Assets/_LunarEscape/Audio/Voice/<id>.wav（16 kHz 单声道）。无线电音色（带通、底噪、提示音）在游戏中实时添加。
# 本文件需保存为带 BOM 的 UTF-8，Windows PowerShell 5.1 才能正确读取中文注释。
param([string]$OnlyPrefix = '')
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
    cup = @{ Voice = (Voice 'Irina'); Rate = '+0%'; Pitch = '+0%' }
    cmd = @{ Voice = (Voice 'Pavel'); Rate = '-2%'; Pitch = '-3%' }
}

# Keep a short breath at either end. Match the RMS of speech across takes and
# cap the loudest sample to leave headroom for the radio effect in Unity.
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Text;

public static class DialogueMaster
{
    public static void Write(byte[] wave, string path)
    {
        int rate = BitConverter.ToInt32(wave, 24);
        int offset = 12;
        while (offset + 8 <= wave.Length && Encoding.ASCII.GetString(wave, offset, 4) != "data")
        {
            int size = BitConverter.ToInt32(wave, offset + 4);
            offset += 8 + size + (size & 1);
        }
        if (offset + 8 > wave.Length || BitConverter.ToInt16(wave, 22) != 1 || BitConverter.ToInt16(wave, 34) != 16)
            throw new InvalidDataException("Expected mono 16-bit PCM speech.");
        int length = Math.Min(BitConverter.ToInt32(wave, offset + 4), wave.Length - offset - 8);
        short[] samples = new short[length / 2];
        Buffer.BlockCopy(wave, offset + 8, samples, 0, samples.Length * 2);
        int first = 0, last = samples.Length - 1;
        while (first <= last && Math.Abs((int)samples[first]) < 150) first++;
        while (last >= first && Math.Abs((int)samples[last]) < 150) last--;
        if (last <= first) throw new InvalidDataException("Empty speech take: " + path);
        first = Math.Max(0, first - rate / 15);
        last = Math.Min(samples.Length - 1, last + rate / 10);
        int peak = 0, voiced = 0;
        double energy = 0;
        for (int i = first; i <= last; ++i)
        {
            int value = Math.Abs((int)samples[i]);
            peak = Math.Max(peak, value);
            if (value >= 350) { energy += (double)value * value; voiced++; }
        }
        if (voiced == 0) throw new InvalidDataException("Inaudible speech take: " + path);
        double gain = Math.Min(28200.0 / peak, 5000.0 / Math.Sqrt(energy / voiced));
        int bytes = (last - first + 1) * 2;
        using (var writer = new BinaryWriter(File.Create(path)))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + bytes);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
            for (int i = first; i <= last; ++i)
            {
                double edge = Math.Min(1, Math.Min(i - first, last - i) / (rate * .006));
                writer.Write((short)Math.Round(samples[i] * gain * edge));
            }
        }
    }
}
'@

$synth = New-Object Windows.Media.SpeechSynthesis.SpeechSynthesizer
$count = 0
foreach ($line in Get-Content -Encoding UTF8 $table) {
    if ($line.Trim() -eq '' -or $line.StartsWith('#')) { continue }
    $id, $who, $text = $line -split "`t", 3
    if ($OnlyPrefix -ne '' -and -not $id.StartsWith($OnlyPrefix)) { continue }
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
    [DialogueMaster]::Write($bytes, (Join-Path $output "$id.wav"))
    $count++
}
Write-Output "VOICE_LINES_GENERATED $count -> $output"
