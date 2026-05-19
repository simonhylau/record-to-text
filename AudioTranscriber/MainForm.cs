using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace AudioTranscriber
{
    public partial class MainForm : Form
    {
        // ── state ──────────────────────────────────────────────────────────
        private bool _isRecording = false;
        private CancellationTokenSource _timerCts;
        private readonly List<WaveInEvent> _micCaptures = new List<WaveInEvent>();
        private readonly List<WasapiLoopbackCapture> _loopbackCaptures = new List<WasapiLoopbackCapture>();
        private readonly List<string> _capturePaths = new List<string>();
        private readonly List<WaveFileWriter> _loopbackWriters = new List<WaveFileWriter>();
        private readonly List<WaveFileWriter> _micWriters = new List<WaveFileWriter>();
        private string _tempDir;

        private Image _imgRecord;
        private Image _imgStop;

        public MainForm()
        {
            InitializeComponent();
            LoadButtonImages();
        }

        private void LoadButtonImages()
        {
            string resourceDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resource");
            string recordPath = Path.Combine(resourceDir, "Record.png");
            string stopPath = Path.Combine(resourceDir, "Stop.png");

            var size = btnRecord.Size;
            if (File.Exists(recordPath)) _imgRecord = ScaleToFit(Image.FromFile(recordPath), size);
            if (File.Exists(stopPath)) _imgStop = ScaleToFit(Image.FromFile(stopPath), size);

            btnRecord.Image = _imgRecord;
        }

        private static Image ScaleToFit(Image source, System.Drawing.Size target)
        {
            // Scale to full button width, maintain aspect ratio, center vertically
            int scaledHeight = (int)((float)source.Height / source.Width * target.Width);
            int y = (target.Height - scaledHeight) / 2;

            var bmp = new System.Drawing.Bitmap(target.Width, target.Height);
            using (var g = System.Drawing.Graphics.FromImage(bmp))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.DrawImage(source, 0, y, target.Width, scaledHeight);
            }
            return bmp;
        }

        // ── button handler ─────────────────────────────────────────────────
        private async void btnRecord_Click(object sender, EventArgs e)
        {
            if (!_isRecording)
                await StartRecordingAsync();
            else
                await StopAndTranscribeAsync();
        }

        // ── start ──────────────────────────────────────────────────────────
        private async Task StartRecordingAsync()
        {
            _isRecording = true;
            btnRecord.Image = _imgStop;
            txtTranscript.Text = "";
            lblStatus.Text = "Recording...";
            lblTimer.Text = "00:00";

            _tempDir = Path.Combine(Path.GetTempPath(), "AudioTranscriber_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
            _capturePaths.Clear();

            // ── microphone devices ─────────────────────────────────────────
            int deviceCount = WaveInEvent.DeviceCount;
            for (int i = 0; i < deviceCount; i++)
            {
                string micPath = Path.Combine(_tempDir, $"mic_{i}.wav");
                _capturePaths.Add(micPath);
                var writer = new WaveFileWriter(micPath, new WaveFormat(44100, 1));
                _micWriters.Add(writer);
                var capture = new WaveInEvent { DeviceNumber = i, WaveFormat = new WaveFormat(44100, 1) };
                int idx = _micWriters.Count - 1;
                capture.DataAvailable += (s, ea) => _micWriters[idx]?.Write(ea.Buffer, 0, ea.BytesRecorded);
                _micCaptures.Add(capture);
                capture.StartRecording();
            }

            StartSystemOutputCapture();

            if (_micCaptures.Count == 0)
                lblStatus.Text = "No microphone found. Recording system audio only...";
            else
                lblStatus.Text = $"Recording {_micCaptures.Count} input device(s) and {_loopbackCaptures.Count} output device(s)...";

            // ── timer ──────────────────────────────────────────────────────
            _timerCts = new CancellationTokenSource();
            var ct = _timerCts.Token;
            var sw = Stopwatch.StartNew();
            _ = Task.Run(async () =>
            {
                while (!ct.IsCancellationRequested)
                {
                    await Task.Delay(500, ct).ContinueWith(_ => { });
                    var elapsed = sw.Elapsed;
                    Invoke((Action)(() => lblTimer.Text = elapsed.ToString(@"mm\:ss")));
                }
            });

            await Task.CompletedTask;
        }

        // ── stop + transcribe ──────────────────────────────────────────────
        private async Task StopAndTranscribeAsync()
        {
            _isRecording = false;
            btnRecord.Enabled = false;
            lblStatus.Text = "Stopping capture...";
            _timerCts?.Cancel();

            // stop captures
            foreach (var cap in _micCaptures) { try { cap.StopRecording(); } catch { } }
            foreach (var cap in _loopbackCaptures) { try { cap.StopRecording(); } catch { } }
            await Task.Delay(400);  // let writers flush
            foreach (var w in _micWriters) { try { w.Dispose(); } catch { } }
            _micWriters.Clear();
            foreach (var w in _loopbackWriters) { try { w.Dispose(); } catch { } }
            _loopbackWriters.Clear();
            foreach (var cap in _micCaptures) { try { cap.Dispose(); } catch { } }
            _micCaptures.Clear();
            foreach (var cap in _loopbackCaptures) { try { cap.Dispose(); } catch { } }
            _loopbackCaptures.Clear();

            lblStatus.Text = "Mixing audio...";
            string mixedWav = Path.Combine(_tempDir, "mixed.wav");
            await Task.Run(() => MixToWav(_capturePaths, mixedWav));

            lblStatus.Text = "Transcribing...";
            string transcript = await TranscribeAsync(mixedWav);

            txtTranscript.Text = transcript;
            lblStatus.Text = "Done.";
            btnRecord.Image = _imgRecord;
            btnRecord.Enabled = true;
        }

        private void StartSystemOutputCapture()
        {
            using (var enumerator = new MMDeviceEnumerator())
            {
                var endpoints = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
                int outputIndex = 0;

                foreach (var endpoint in endpoints)
                {
                    try
                    {
                        string loopPath = Path.Combine(_tempDir, $"output_{outputIndex++}.wav");
                        _capturePaths.Add(loopPath);

                        var capture = new WasapiLoopbackCapture(endpoint);
                        var writer = new WaveFileWriter(loopPath, capture.WaveFormat);
                        int writerIndex = _loopbackWriters.Count;

                        _loopbackWriters.Add(writer);
                        capture.DataAvailable += (s, ea) => _loopbackWriters[writerIndex]?.Write(ea.Buffer, 0, ea.BytesRecorded);
                        _loopbackCaptures.Add(capture);
                        capture.StartRecording();
                    }
                    catch
                    {
                    }
                }
            }
        }

        // ── audio mixing ───────────────────────────────────────────────────
        private void MixToWav(List<string> sources, string outputPath)
        {
            var providers = new List<ISampleProvider>();

            foreach (var src in sources)
            {
                if (!File.Exists(src) || new FileInfo(src).Length < 100) continue;
                try
                {
                    var reader = new WaveFileReader(src);
                    ISampleProvider sp = reader.ToSampleProvider();
                    if (reader.WaveFormat.SampleRate != 16000)
                        sp = new WdlResamplingSampleProvider(sp, 16000);
                    if (reader.WaveFormat.Channels > 1)
                        sp = sp.ToMono();
                    providers.Add(sp);
                }
                catch { }
            }

            if (providers.Count == 0)
                throw new InvalidOperationException("No audio data was captured.");

            ISampleProvider mix = providers.Count == 1
                ? providers[0]
                : new MixingSampleProvider(providers);

            WaveFileWriter.CreateWaveFile16(outputPath, mix);
        }

        // ── whisper-cli ────────────────────────────────────────────────────
        private const int ChunkSeconds = 30;

        private async Task<string> TranscribeAsync(string wavPath)
        {
            string exePath = GetWhisperPath();
            string modelPath = GetModelPath();
            if (exePath == null) return "[Error: whisper-cli not found]";
            if (modelPath == null) return "[Error: whisper model not found]";

            string chunksDir = Path.Combine(_tempDir, "chunks");
            Directory.CreateDirectory(chunksDir);

            List<string> chunks = await Task.Run(() => SplitWavToChunks(wavPath, chunksDir));

            if (chunks.Count == 0)
                return "[No audio data to transcribe]";

            // Limit concurrency to avoid saturating CPU/RAM
            int maxParallel = Math.Max(1, Environment.ProcessorCount / 2);
            var semaphore = new SemaphoreSlim(maxParallel);
            int done = 0;
            int total = chunks.Count;

            var tasks = new Task<string>[chunks.Count];
            for (int i = 0; i < chunks.Count; i++)
            {
                int idx = i;
                string chunkPath = chunks[i];
                tasks[i] = Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        string result = TranscribeChunk(chunkPath, exePath, modelPath);
                        int n = System.Threading.Interlocked.Increment(ref done);
                        Invoke((Action)(() => lblStatus.Text = $"Transcribing chunk {n}/{total}..."));
                        return result;
                    }
                    finally { semaphore.Release(); }
                });
            }

            string[] results = await Task.WhenAll(tasks);
            return string.Join(" ", results).Trim();
        }

        private List<string> SplitWavToChunks(string wavPath, string chunksDir)
        {
            var paths = new List<string>();
            using (var reader = new WaveFileReader(wavPath))
            {
                var fmt = reader.WaveFormat;
                int bytesPerChunk = fmt.AverageBytesPerSecond * ChunkSeconds;
                // Align to block boundary
                bytesPerChunk -= bytesPerChunk % fmt.BlockAlign;
                var buffer = new byte[bytesPerChunk];
                int chunkIndex = 0;
                int read;
                while ((read = reader.Read(buffer, 0, bytesPerChunk)) > 0)
                {
                    string chunkPath = Path.Combine(chunksDir, $"chunk_{chunkIndex++}.wav");
                    using (var writer = new WaveFileWriter(chunkPath, fmt))
                        writer.Write(buffer, 0, read);
                    paths.Add(chunkPath);
                }
            }
            return paths;
        }

        private string TranscribeChunk(string wavPath, string exePath, string modelPath)
        {
            string outBase = Path.ChangeExtension(wavPath, null);
            var psi = new ProcessStartInfo
            {
                FileName = exePath,
                Arguments = $"-m \"{modelPath}\" -f \"{wavPath}\" -l auto -otxt -of \"{outBase}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            string stderr = string.Empty;
            using (var p = Process.Start(psi))
            {
                stderr = p.StandardError.ReadToEnd();
                p.WaitForExit();
                if (p.ExitCode != 0)
                    return $"[whisper error: {stderr.Trim()}]";
            }
            string resultFile = outBase + ".txt";
            return File.Exists(resultFile) ? File.ReadAllText(resultFile).Trim() : $"[No output file. stderr: {stderr.Trim()}]";
        }

        private static readonly string CliDir =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "CLI");

        private string GetWhisperPath() =>
            Path.Combine(CliDir, "whisper-cli.exe");

        private string GetModelPath()
        {
            var modelsDir = Path.Combine(CliDir, "models");
            if (Directory.Exists(modelsDir))
            {
                foreach (var f in Directory.GetFiles(modelsDir, "ggml-base.en.bin", SearchOption.TopDirectoryOnly))
                    return f;
            }
            return null;
        }
    }
}
