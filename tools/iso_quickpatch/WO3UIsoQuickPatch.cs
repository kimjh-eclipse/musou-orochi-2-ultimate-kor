using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

// 무쌍 오로치 2 얼티밋 (Warriors Orochi 3 Ultimate, PS3 일본판 BLJM61084) 한국어 빠른 패처.
//
// - 복호화 ISO: ISO9660 디렉터리로 EBOOT.BIN / LINKDATA.IDX / LINKDATA.BIN 의 extent 를 찾아
//   변경 구간만 제자리에 기록한다 (전체 ISO 복사 없음, 파일 크기 불변).
// - 폴더형 게임(RPCS3 dev_hdd0\disc\BLJM61084 또는 추출 폴더): 같은 구간을 USRDIR 의 세 파일에 직접 기록한다.
// - 어느 쪽이든 쓰기 전 원본 구간을 백업 파일에 저장하고, 쓰기 후 SHA-256 으로 최종 상태를 검증한다.
//   실패 시 백업으로 자동 복구한다.
internal static class WO3UIsoQuickPatch
{
    private const int SectorSize = 2048;
    private const string GameId = "BLJM61084";
    private const string GameTitle = "무쌍 오로치 2 얼티밋";
    private const string PatchResourceName = "WO3U_ISO_ranges.bin";
    private const string PackMagic = "WO3URNG1";
    private const int PackFormatVersion = 1;
    private const string BackupMagic = "WO3UBAK1";
    private const int BackupFormatVersion = 1;
    private const string BackupExtension = ".wo3u-backup";
    private const uint AttachParentProcess = 0xFFFFFFFF;
    private static readonly string[] TargetNames = { "EBOOT.BIN", "LINKDATA.IDX", "LINKDATA.BIN" };

    private static string versionText = "(팩 미로드)";

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(uint processId);

    // ------------------------------------------------------------------ 데이터 구조

    private sealed class PatchRange
    {
        public long LogicalOffset;
        public byte[] Data;
    }

    private sealed class PackFile
    {
        public string IsoPath;
        public string Name;
        public long Size;
        public byte[] SourceHash;
        public byte[] TargetHash;
        public List<PatchRange> Ranges = new List<PatchRange>();
    }

    private sealed class Pack
    {
        public string Version;
        public List<PackFile> Files = new List<PackFile>();
    }

    private sealed class Extent
    {
        public long Offset;
        public long Size;
    }

    private sealed class TargetFile
    {
        public PackFile Patch;
        public int StreamIndex;
        public List<Extent> Extents = new List<Extent>();
    }

    private sealed class Segment
    {
        public int StreamIndex;
        public long AbsoluteOffset;
        public byte[] Data;
        public int DataOffset;
        public int Length;
    }

    private enum FileState
    {
        Source,
        Target,
        Unknown
    }

    private enum TargetKind
    {
        Iso = 0,
        Folder = 1
    }

    // 패치 대상(ISO 하나 또는 폴더의 파일 3개)을 스트림 목록으로 추상화한다.
    private sealed class Target : IDisposable
    {
        public TargetKind Kind;
        public string Description;
        public List<FileStream> Streams = new List<FileStream>();
        public List<TargetFile> Files = new List<TargetFile>();

        public void Dispose()
        {
            foreach (FileStream stream in Streams)
                stream.Dispose();
            Streams.Clear();
        }
    }

    private sealed class Options
    {
        public string IsoPath;
        public string FolderPath;
        public string BackupPath;
        public bool Yes;
        public bool VerifyOnly;
        public bool Restore;
        public bool Pause;
    }

    private enum UiOperation
    {
        Verify,
        Patch,
        Restore
    }

    private sealed class UiWorkItem
    {
        public UiOperation Operation;
        public TargetKind Kind;
        public string TargetPath;
        public string BackupPath;
    }

    private sealed class UiResult
    {
        public int ExitCode;
        public Exception Error;
        public UiOperation Operation;
        public TargetKind Kind;
        public string TargetPath;
    }

    private sealed class UiTextWriter : TextWriter
    {
        private readonly Action<string> append;

        public UiTextWriter(Action<string> appendText)
        {
            append = appendText;
        }

        public override Encoding Encoding { get { return Encoding.UTF8; } }

        public override void Write(string value)
        {
            if (!String.IsNullOrEmpty(value))
                append(value);
        }

        public override void Write(char value)
        {
            append(value.ToString());
        }

        public override void WriteLine(string value)
        {
            append((value ?? String.Empty) + Environment.NewLine);
        }

        public override void WriteLine()
        {
            append(Environment.NewLine);
        }
    }

    // ------------------------------------------------------------------ GUI

    private sealed class MainForm : Form
    {
        private readonly TextBox isoPathBox;
        private readonly Button isoBrowseButton;
        private readonly TextBox isoBackupBox;
        private readonly Button isoBackupBrowseButton;
        private readonly TextBox folderPathBox;
        private readonly Button folderBrowseButton;
        private readonly TextBox folderBackupBox;
        private readonly Button folderBackupBrowseButton;
        private readonly CheckBox warningCheck;
        private readonly Button isoVerifyButton;
        private readonly Button isoPatchButton;
        private readonly Button isoRestoreButton;
        private readonly Button folderVerifyButton;
        private readonly Button folderPatchButton;
        private readonly Button folderRestoreButton;
        private readonly Button closeButton;
        private readonly RichTextBox logBox;
        private readonly ProgressBar progressBar;
        private readonly Label statusLabel;
        private readonly BackgroundWorker worker;
        private bool busyState;
        private bool isoBackupCustomized;
        private bool folderBackupCustomized;

        public MainForm(string initialIsoPath, string initialFolderPath)
        {
            Text = GameTitle + " 한국어 빠른 패처 " + versionText;
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            ClientSize = new Size(900, 900);
            Font = new Font("Segoe UI", 9F);
            BackColor = Color.FromArgb(242, 246, 250);
            AllowDrop = true;

            Panel titlePanel = new Panel();
            titlePanel.SetBounds(0, 0, 900, 78);
            titlePanel.BackColor = Color.FromArgb(86, 30, 30);
            Controls.Add(titlePanel);

            Label title = new Label();
            title.Text = GameTitle + " 한국어 빠른 패처";
            title.ForeColor = Color.White;
            title.Font = new Font("Segoe UI", 18F, FontStyle.Bold);
            title.AutoSize = true;
            title.Location = new Point(24, 13);
            titlePanel.Controls.Add(title);

            Label subtitle = new Label();
            subtitle.Text = "일본판 " + GameId + " 복호화 ISO 또는 RPCS3 폴더형 게임에 검증 후 변경 구간만 기록합니다.  " + versionText;
            subtitle.ForeColor = Color.FromArgb(242, 215, 205);
            subtitle.AutoSize = true;
            subtitle.Location = new Point(27, 51);
            titlePanel.Controls.Add(subtitle);

            int y = 92;
            AddSectionLabel("A. 복호화 ISO 파일 (.iso)", y);
            isoPathBox = AddPathBox(y + 22, initialIsoPath);
            isoBrowseButton = AddBrowseButton(y + 20, delegate { BrowseIso(); });
            AddSmallLabel("ISO 복구 백업 파일 (기존 백업이 있으면 그 파일을 선택)", y + 54);
            isoBackupBox = AddPathBox(y + 74, String.IsNullOrWhiteSpace(initialIsoPath) ? String.Empty :
                Path.GetFullPath(initialIsoPath) + BackupExtension);
            isoBackupBrowseButton = AddBrowseButton(y + 72, delegate { BrowseBackup(isoBackupBox, true); });
            isoBackupBox.TextChanged += delegate { if (isoBackupBox.Focused) isoBackupCustomized = true; };
            isoPathBox.TextChanged += delegate { UpdateDefaultIsoBackup(); };

            y = 212;
            AddSectionLabel("B. RPCS3 / 폴더형 게임 (게임 루트 · PS3_GAME · USRDIR · RPCS3 루트 자동 판별)", y);
            folderPathBox = AddPathBox(y + 22, initialFolderPath);
            folderBrowseButton = AddBrowseButton(y + 20, delegate { BrowseFolder(); });
            AddSmallLabel("폴더 복구 백업 파일", y + 54);
            folderBackupBox = AddPathBox(y + 74, String.Empty);
            folderBackupBrowseButton = AddBrowseButton(y + 72, delegate { BrowseBackup(folderBackupBox, false); });
            folderBackupBox.TextChanged += delegate { if (folderBackupBox.Focused) folderBackupCustomized = true; };
            folderPathBox.TextChanged += delegate { UpdateDefaultFolderBackup(); };
            UpdateDefaultFolderBackup();

            GroupBox warningGroup = new GroupBox();
            warningGroup.Text = "반드시 확인할 주의사항";
            warningGroup.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            warningGroup.ForeColor = Color.FromArgb(123, 72, 0);
            warningGroup.BackColor = Color.FromArgb(255, 248, 220);
            warningGroup.SetBounds(22, 332, 856, 200);
            Controls.Add(warningGroup);

            Label warnings = new Label();
            warnings.Font = new Font("Segoe UI", 9.5F, FontStyle.Regular);
            warnings.ForeColor = Color.FromArgb(70, 48, 10);
            warnings.AutoSize = false;
            warnings.SetBounds(18, 28, 820, 130);
            warnings.Text =
                "1. 일본판 " + GameId + " 의 복호화 ISO 또는 원본 폴더형 게임만 지원합니다. (영문판·다른 리전 불가)\r\n" +
                "2. 선택한 ISO 또는 USRDIR 의 EBOOT.BIN / LINKDATA.IDX / LINKDATA.BIN 자체를 수정합니다.\r\n" +
                "3. 작업 중 RPCS3 를 완전히 종료하고 ISO 마운트를 해제하세요. 실행 중이면 패처가 중단합니다.\r\n" +
                "4. 쓰기 전 원본 구간을 백업 파일(" + BackupExtension + ")에 저장하며, 이 파일로만 복구·재적용할 수 있습니다.\r\n" +
                "5. 패치 후 RPCS3 Save State 로 이어하지 마세요. 저장 상태는 패치 전 폰트·메모리를 되살릴 수 있습니다.\r\n" +
                "6. 세이브·savestate·PPU/SPU/셰이더 캐시는 건드리지 않습니다.";
            warningGroup.Controls.Add(warnings);

            warningCheck = new CheckBox();
            warningCheck.Text = "위 주의사항을 확인했으며, 선택한 ISO 또는 게임 파일이 직접 수정되는 것에 동의합니다.";
            warningCheck.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            warningCheck.AutoSize = true;
            warningCheck.Location = new Point(20, 164);
            warningGroup.Controls.Add(warningCheck);

            Label logLabel = new Label();
            logLabel.Text = "진행 로그";
            logLabel.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            logLabel.AutoSize = true;
            logLabel.Location = new Point(22, 545);
            Controls.Add(logLabel);

            logBox = new RichTextBox();
            logBox.SetBounds(22, 567, 856, 200);
            logBox.ReadOnly = true;
            logBox.BackColor = Color.FromArgb(22, 28, 34);
            logBox.ForeColor = Color.FromArgb(225, 235, 240);
            logBox.Font = new Font("Consolas", 9F);
            logBox.WordWrap = false;
            Controls.Add(logBox);

            isoVerifyButton = AddButton("ISO 상태 검사", 22, 783, 140, false, Color.Empty,
                delegate { StartOperation(UiOperation.Verify, TargetKind.Iso); });
            isoPatchButton = AddButton("ISO 에 한국어 패치 적용", 172, 783, 230, true, Color.FromArgb(34, 112, 166),
                delegate { StartOperation(UiOperation.Patch, TargetKind.Iso); });
            isoRestoreButton = AddButton("ISO 원본 복구", 412, 783, 140, false, Color.Empty,
                delegate { StartOperation(UiOperation.Restore, TargetKind.Iso); });

            folderVerifyButton = AddButton("폴더 게임 상태 검사", 22, 831, 140, false, Color.Empty,
                delegate { StartOperation(UiOperation.Verify, TargetKind.Folder); });
            folderPatchButton = AddButton("폴더 게임에 직접 패치", 172, 831, 230, true, Color.FromArgb(38, 130, 82),
                delegate { StartOperation(UiOperation.Patch, TargetKind.Folder); });
            folderRestoreButton = AddButton("폴더 게임 원본 복구", 412, 831, 140, false, Color.Empty,
                delegate { StartOperation(UiOperation.Restore, TargetKind.Folder); });

            closeButton = AddButton("닫기", 758, 831, 120, false, Color.Empty, delegate { Close(); });

            progressBar = new ProgressBar();
            progressBar.SetBounds(22, 877, 650, 16);
            Controls.Add(progressBar);

            statusLabel = new Label();
            statusLabel.Text = "대기 중";
            statusLabel.TextAlign = ContentAlignment.MiddleRight;
            statusLabel.SetBounds(685, 872, 193, 25);
            Controls.Add(statusLabel);

            worker = new BackgroundWorker();
            worker.DoWork += WorkerDoWork;
            worker.RunWorkerCompleted += WorkerCompleted;

            DragEnter += FormDragEnter;
            DragDrop += FormDragDrop;
            FormClosing += MainFormClosing;
        }

        private void AddSectionLabel(string text, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            label.AutoSize = true;
            label.Location = new Point(22, y);
            Controls.Add(label);
        }

        private void AddSmallLabel(string text, int y)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.ForeColor = Color.FromArgb(70, 80, 90);
            label.Location = new Point(22, y);
            Controls.Add(label);
        }

        private TextBox AddPathBox(int y, string text)
        {
            TextBox box = new TextBox();
            box.SetBounds(22, y, 750, 27);
            box.Text = text ?? String.Empty;
            Controls.Add(box);
            return box;
        }

        private Button AddBrowseButton(int y, EventHandler handler)
        {
            Button button = new Button();
            button.Text = "찾아보기...";
            button.SetBounds(782, y, 96, 30);
            button.Click += handler;
            Controls.Add(button);
            return button;
        }

        private Button AddButton(string text, int x, int y, int width, bool primary, Color color, EventHandler handler)
        {
            Button button = new Button();
            button.Text = text;
            button.SetBounds(x, y, width, 38);
            if (primary)
            {
                button.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
                button.BackColor = color;
                button.ForeColor = Color.White;
                button.FlatStyle = FlatStyle.Flat;
            }
            button.Click += handler;
            Controls.Add(button);
            return button;
        }

        private void BrowseIso()
        {
            using (OpenFileDialog dialog = new OpenFileDialog())
            {
                dialog.Title = "복호화된 일본판 " + GameTitle + " ISO 선택";
                dialog.Filter = "ISO 이미지 (*.iso)|*.iso|모든 파일 (*.*)|*.*";
                dialog.CheckFileExists = true;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    isoPathBox.Text = dialog.FileName;
                    UpdateDefaultIsoBackup();
                }
            }
        }

        private void BrowseFolder()
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "RPCS3 폴더, " + GameId + " 게임 폴더, PS3_GAME 또는 USRDIR 폴더를 선택하세요.";
                dialog.ShowNewFolderButton = false;
                string current = folderPathBox.Text.Trim().Trim('"');
                if (!String.IsNullOrWhiteSpace(current) && Directory.Exists(current))
                    dialog.SelectedPath = current;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                    folderPathBox.Text = dialog.SelectedPath;
            }
        }

        private void BrowseBackup(TextBox box, bool iso)
        {
            using (SaveFileDialog dialog = new SaveFileDialog())
            {
                dialog.Title = "복구 백업 파일 선택";
                dialog.Filter = "WO3U 복구 백업 (*" + BackupExtension + ")|*" + BackupExtension + "|모든 파일 (*.*)|*.*";
                dialog.AddExtension = false;
                dialog.OverwritePrompt = false;
                string current = box.Text.Trim().Trim('"');
                dialog.FileName = Path.GetFileName(current);
                string directory = String.IsNullOrWhiteSpace(current) ? null : Path.GetDirectoryName(current);
                if (!String.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
                    dialog.InitialDirectory = directory;
                if (dialog.ShowDialog(this) == DialogResult.OK)
                {
                    if (iso) isoBackupCustomized = true; else folderBackupCustomized = true;
                    box.Text = dialog.FileName;
                }
            }
        }

        private void UpdateDefaultIsoBackup()
        {
            if (isoBackupCustomized)
                return;
            string isoPath = isoPathBox.Text.Trim().Trim('"');
            isoBackupBox.Text = String.IsNullOrWhiteSpace(isoPath) ? String.Empty : isoPath + BackupExtension;
        }

        private void UpdateDefaultFolderBackup()
        {
            if (folderBackupCustomized)
                return;
            string folder = folderPathBox.Text.Trim().Trim('"');
            if (String.IsNullOrWhiteSpace(folder))
            {
                folderBackupBox.Text = String.Empty;
                return;
            }
            try
            {
                string usrdir = ResolveUsrDir(folder);
                folderBackupBox.Text = DefaultFolderBackupPath(usrdir);
            }
            catch
            {
                folderBackupBox.Text = String.Empty;
            }
        }

        private void FormDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void FormDragDrop(object sender, DragEventArgs e)
        {
            string[] files = e.Data.GetData(DataFormats.FileDrop) as string[];
            if (files == null || files.Length != 1)
                return;
            if (Directory.Exists(files[0]))
                folderPathBox.Text = files[0];
            else if (Path.GetExtension(files[0]).Equals(".iso", StringComparison.OrdinalIgnoreCase))
            {
                isoPathBox.Text = files[0];
                UpdateDefaultIsoBackup();
            }
        }

        private void StartOperation(UiOperation operation, TargetKind kind)
        {
            string targetPath;
            string backupPath;
            if (kind == TargetKind.Iso)
            {
                targetPath = isoPathBox.Text.Trim().Trim('"');
                if (!File.Exists(targetPath) ||
                    !Path.GetExtension(targetPath).Equals(".iso", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "유효한 ISO 파일을 선택하세요.", "ISO 확인",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                targetPath = Path.GetFullPath(targetPath);
                backupPath = isoBackupBox.Text.Trim().Trim('"');
            }
            else
            {
                try
                {
                    targetPath = ResolveUsrDir(folderPathBox.Text.Trim().Trim('"'));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "폴더형 게임 경로 확인",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                backupPath = folderBackupBox.Text.Trim().Trim('"');
            }

            if (String.IsNullOrWhiteSpace(backupPath))
            {
                MessageBox.Show(this, "복구 백업 파일의 저장 위치를 지정하세요.", "백업 경로 필요",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            backupPath = Path.GetFullPath(backupPath);
            if (operation != UiOperation.Verify)
            {
                string backupDirectory = Path.GetDirectoryName(backupPath);
                if (String.IsNullOrWhiteSpace(backupDirectory) || !Directory.Exists(backupDirectory))
                {
                    MessageBox.Show(this, "백업 파일을 저장할 폴더가 존재하지 않습니다.", "백업 경로 오류",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (kind == TargetKind.Iso && String.Equals(targetPath, backupPath, StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(this, "ISO 파일과 백업 파일은 서로 다른 경로여야 합니다.", "백업 경로 오류",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (operation == UiOperation.Restore && !File.Exists(backupPath))
                {
                    MessageBox.Show(this, "선택한 복구 백업 파일이 없습니다.", "백업 파일 확인",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                if (!warningCheck.Checked)
                {
                    MessageBox.Show(this, "패치 또는 복구 전에 주의사항 확인란을 체크하세요.", "주의사항 확인 필요",
                        MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            if (operation == UiOperation.Patch)
            {
                string what = kind == TargetKind.Iso ?
                    "선택한 ISO 파일 자체에 한국어 패치를 적용합니다.\r\n\r\n" + targetPath :
                    "다음 USRDIR 의 EBOOT.BIN / LINKDATA.IDX / LINKDATA.BIN 에 한국어 패치를 직접 적용합니다.\r\n\r\n" + targetPath;
                DialogResult answer = MessageBox.Show(this,
                    what + "\r\n\r\n복구 백업: " + backupPath + "\r\n\r\nRPCS3 가 완전히 종료되었습니까?",
                    "직접 패치 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2);
                if (answer != DialogResult.Yes)
                    return;
            }
            else if (operation == UiOperation.Restore)
            {
                DialogResult answer = MessageBox.Show(this,
                    "지정한 백업 파일로 패치 전 원본 상태로 복구합니다. 계속하시겠습니까?\r\n\r\n" + backupPath,
                    "원본 복구 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
                if (answer != DialogResult.Yes)
                    return;
            }

            logBox.Clear();
            SetBusy(true, operation == UiOperation.Verify ? "상태 검사 중..." :
                (operation == UiOperation.Patch ? "패치 적용 중..." : "원본 복구 중..."));
            worker.RunWorkerAsync(new UiWorkItem
            {
                Operation = operation,
                Kind = kind,
                TargetPath = targetPath,
                BackupPath = backupPath
            });
        }

        private void WorkerDoWork(object sender, DoWorkEventArgs e)
        {
            UiWorkItem work = (UiWorkItem)e.Argument;
            UiResult result = new UiResult();
            result.Operation = work.Operation;
            result.Kind = work.Kind;
            result.TargetPath = work.TargetPath;

            TextWriter originalOutput = Console.Out;
            try
            {
                Console.SetOut(new UiTextWriter(AppendLog));
                Options options = new Options();
                if (work.Kind == TargetKind.Iso)
                    options.IsoPath = work.TargetPath;
                else
                    options.FolderPath = work.TargetPath;
                options.BackupPath = work.BackupPath;
                options.Yes = true;
                options.VerifyOnly = work.Operation == UiOperation.Verify;
                options.Restore = work.Operation == UiOperation.Restore;
                options.Pause = false;
                result.ExitCode = Run(options);
            }
            catch (Exception ex)
            {
                result.ExitCode = 1;
                result.Error = ex;
                AppendLog(Environment.NewLine + "[실패] " + ex.Message + Environment.NewLine);
            }
            finally
            {
                Console.SetOut(originalOutput);
            }
            e.Result = result;
        }

        private void WorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (IsDisposed || Disposing)
                return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new RunWorkerCompletedEventHandler(WorkerCompleted), sender, e); }
                catch (InvalidOperationException) { }
                return;
            }

            SetBusy(false, "대기 중");
            UiResult result = e.Result as UiResult;
            if (result == null || result.Error != null || result.ExitCode != 0)
            {
                string message = result != null && result.Error != null ? result.Error.Message :
                    "검사 또는 작업이 성공적으로 끝나지 않았습니다. 진행 로그를 확인하세요.";
                MessageBox.Show(this, message, "작업 실패", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string where = result.Kind == TargetKind.Iso ? "ISO" : "폴더형 게임";
            if (result.Operation == UiOperation.Verify)
                MessageBox.Show(this, where + " 상태 검사가 완료되었습니다.\r\n자세한 상태는 진행 로그를 확인하세요.",
                    "검사 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else if (result.Operation == UiOperation.Patch)
                MessageBox.Show(this, where + " 에 한국어 패치 적용과 최종 해시 검증이 완료되었습니다.\r\n\r\n" +
                    result.TargetPath + "\r\n\r\nSave State 대신 게임 내부 세이브로 이어서 확인하세요.",
                    "패치 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
            else
                MessageBox.Show(this, where + " 원본 복구와 해시 검증이 완료되었습니다.",
                    "복구 완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void AppendLog(string text)
        {
            if (IsDisposed)
                return;
            if (InvokeRequired)
            {
                try { BeginInvoke(new Action<string>(AppendLog), text); } catch { }
                return;
            }
            logBox.AppendText(text);
            logBox.SelectionStart = logBox.TextLength;
            logBox.ScrollToCaret();
        }

        private void MainFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!busyState)
                return;
            e.Cancel = true;
            MessageBox.Show(this, "작업 중에는 창을 닫을 수 없습니다. 완료될 때까지 기다리세요.",
                "작업 진행 중", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private void SetBusy(bool busy, string status)
        {
            busyState = busy;
            Control[] controls =
            {
                isoPathBox, isoBrowseButton, isoBackupBox, isoBackupBrowseButton,
                folderPathBox, folderBrowseButton, folderBackupBox, folderBackupBrowseButton,
                warningCheck, isoVerifyButton, isoPatchButton, isoRestoreButton,
                folderVerifyButton, folderPatchButton, folderRestoreButton, closeButton
            };
            foreach (Control control in controls)
                control.Enabled = !busy;
            progressBar.Style = busy ? ProgressBarStyle.Marquee : ProgressBarStyle.Blocks;
            progressBar.MarqueeAnimationSpeed = busy ? 25 : 0;
            statusLabel.Text = status;
        }
    }

    // ------------------------------------------------------------------ 진입점

    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            versionText = LoadPack().Version;
        }
        catch (Exception ex)
        {
            versionText = "(팩 오류: " + ex.Message + ")";
        }

        bool commandLineMode = args.Any(delegate(string arg)
        {
            return arg.StartsWith("--", StringComparison.Ordinal);
        });

        if (!commandLineMode)
        {
            string initialIso = null;
            string initialFolder = null;
            if (args.Length > 0)
            {
                if (Directory.Exists(args[0]))
                    initialFolder = args[0];
                else
                    initialIso = args[0];
            }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(initialIso, initialFolder));
            return 0;
        }

        InitializeConsoleForCli();
        bool pauseRequested = !args.Any(delegate(string arg)
        {
            return arg.Equals("--no-pause", StringComparison.OrdinalIgnoreCase) ||
                arg.Equals("--headless", StringComparison.OrdinalIgnoreCase);
        });
        Options options = null;
        int result = 1;
        try
        {
            Console.OutputEncoding = new UTF8Encoding(false);
            Console.Title = GameTitle + " 한국어 빠른 패처 " + versionText;
            options = ParseOptions(args);
            result = Run(options);
        }
        catch (Exception ex)
        {
            SetConsoleColor(ConsoleColor.Red);
            Console.WriteLine();
            Console.WriteLine("[실패] " + ex.Message);
            ResetConsoleColor();
            result = 1;
        }
        finally
        {
            if ((options == null && pauseRequested) || (options != null && options.Pause))
            {
                Console.WriteLine();
                Console.Write("Enter 키를 누르면 종료합니다...");
                try { Console.ReadLine(); } catch { }
            }
        }
        return result;
    }

    private static void InitializeConsoleForCli()
    {
        try
        {
            AttachConsole(AttachParentProcess);
            Stream output = Console.OpenStandardOutput();
            if (output != null && output.CanWrite)
            {
                StreamWriter writer = new StreamWriter(output, new UTF8Encoding(false));
                writer.AutoFlush = true;
                Console.SetOut(writer);
            }
            Stream error = Console.OpenStandardError();
            if (error != null && error.CanWrite)
            {
                StreamWriter writer = new StreamWriter(error, new UTF8Encoding(false));
                writer.AutoFlush = true;
                Console.SetError(writer);
            }
        }
        catch
        {
        }
    }

    private static Options ParseOptions(string[] args)
    {
        Options options = new Options();
        options.Pause = true;
        for (int index = 0; index < args.Length; index++)
        {
            string arg = args[index].Trim();
            if (arg.Equals("--yes", StringComparison.OrdinalIgnoreCase))
                options.Yes = true;
            else if (arg.Equals("--verify-only", StringComparison.OrdinalIgnoreCase))
                options.VerifyOnly = true;
            else if (arg.Equals("--restore", StringComparison.OrdinalIgnoreCase))
                options.Restore = true;
            else if (arg.Equals("--no-pause", StringComparison.OrdinalIgnoreCase) ||
                     arg.Equals("--headless", StringComparison.OrdinalIgnoreCase))
                options.Pause = false;
            else if (arg.Equals("--backup", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                    throw new ArgumentException("--backup 다음에 백업 파일 경로를 지정하세요.");
                options.BackupPath = args[++index].Trim().Trim('"');
            }
            else if (arg.Equals("--iso", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                    throw new ArgumentException("--iso 다음에 ISO 경로를 지정하세요.");
                options.IsoPath = args[++index].Trim().Trim('"');
            }
            else if (arg.Equals("--folder", StringComparison.OrdinalIgnoreCase))
            {
                if (index + 1 >= args.Length)
                    throw new ArgumentException("--folder 다음에 게임 폴더 경로를 지정하세요.");
                options.FolderPath = args[++index].Trim().Trim('"');
            }
            else if (arg.StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("알 수 없는 옵션입니다: " + arg);
            else if (Directory.Exists(arg) && options.FolderPath == null)
                options.FolderPath = arg;
            else if (options.IsoPath == null)
                options.IsoPath = arg;
            else
                throw new ArgumentException("대상 경로는 하나만 지정할 수 있습니다.");
        }

        if (!String.IsNullOrWhiteSpace(options.IsoPath) && !String.IsNullOrWhiteSpace(options.FolderPath))
            throw new ArgumentException("--iso 와 --folder 는 동시에 지정할 수 없습니다.");
        if (String.IsNullOrWhiteSpace(options.IsoPath) && String.IsNullOrWhiteSpace(options.FolderPath))
            throw new ArgumentException(
                "사용법: WO3U_ISO_QuickPatch.exe [--iso <경로.iso> | --folder <게임 폴더>] [--backup <백업 파일>] [--verify-only | --restore] [--yes] [--no-pause]");

        if (!String.IsNullOrWhiteSpace(options.IsoPath))
            options.IsoPath = Path.GetFullPath(options.IsoPath);
        if (!String.IsNullOrWhiteSpace(options.FolderPath))
            options.FolderPath = ResolveUsrDir(options.FolderPath);
        if (!String.IsNullOrWhiteSpace(options.BackupPath))
            options.BackupPath = Path.GetFullPath(options.BackupPath);
        return options;
    }

    // ------------------------------------------------------------------ 폴더 경로 해석

    private static string ResolveUsrDir(string selectedPath)
    {
        if (String.IsNullOrWhiteSpace(selectedPath) || !Directory.Exists(selectedPath))
            throw new DirectoryNotFoundException(
                "RPCS3 폴더, " + GameId + " 게임 폴더, PS3_GAME 또는 USRDIR 폴더를 선택하세요.");

        string full = Path.GetFullPath(selectedPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string[] candidates =
        {
            full,
            Path.Combine(full, "USRDIR"),
            Path.Combine(full, "PS3_GAME", "USRDIR"),
            Path.Combine(full, "dev_hdd0", "disc", GameId, "PS3_GAME", "USRDIR"),
            Path.Combine(full, "dev_hdd0", "game", GameId, "USRDIR"),
            Path.Combine(full, "disc", GameId, "PS3_GAME", "USRDIR"),
            Path.Combine(full, "game", GameId, "USRDIR")
        };
        foreach (string candidate in candidates.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (Directory.Exists(candidate) && TargetNames.All(delegate(string name)
            {
                return File.Exists(Path.Combine(candidate, name));
            }))
                return Path.GetFullPath(candidate);
        }
        throw new DirectoryNotFoundException(
            "선택한 경로에서 " + GameId + " 의 EBOOT.BIN / LINKDATA.IDX / LINKDATA.BIN 을 찾지 못했습니다.\r\n\r\n" +
            "지원 예시:\r\n" +
            "  ...\\dev_hdd0\\disc\\" + GameId + "\\PS3_GAME\\USRDIR\r\n" +
            "  ...\\" + GameId + "\\PS3_GAME\r\n" +
            "  RPCS3 루트 폴더 (dev_hdd0\\disc\\" + GameId + " 자동 탐색)");
    }

    private static string DefaultFolderBackupPath(string usrdir)
    {
        // ...\<게임루트>\PS3_GAME\USRDIR → ...\<게임루트>.wo3u-backup (게임 폴더 바깥)
        DirectoryInfo dir = new DirectoryInfo(usrdir);
        DirectoryInfo gameRoot = dir.Parent != null && dir.Parent.Name.Equals("PS3_GAME", StringComparison.OrdinalIgnoreCase)
            ? dir.Parent.Parent : dir.Parent;
        if (gameRoot == null)
            return Path.Combine(usrdir, GameId + BackupExtension);
        return gameRoot.FullName.TrimEnd(Path.DirectorySeparatorChar) + BackupExtension;
    }

    // ------------------------------------------------------------------ 실행 본체

    private static int Run(Options options)
    {
        WriteHeader();
        if (Process.GetProcessesByName("rpcs3").Length != 0)
            throw new InvalidOperationException("RPCS3 가 실행 중입니다. 완전히 종료한 뒤 다시 실행하세요.");

        Pack pack = LoadPack();
        bool isoMode = !String.IsNullOrWhiteSpace(options.IsoPath);
        string backupPath;
        if (isoMode)
        {
            if (!File.Exists(options.IsoPath))
                throw new FileNotFoundException("ISO 파일이 없습니다.", options.IsoPath);
            if (!Path.GetExtension(options.IsoPath).Equals(".iso", StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("선택한 파일의 확장자가 .iso 가 아닙니다.");
            backupPath = String.IsNullOrWhiteSpace(options.BackupPath) ?
                options.IsoPath + BackupExtension : options.BackupPath;
            if (String.Equals(options.IsoPath, backupPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("ISO 파일과 백업 파일은 서로 다른 경로여야 합니다.");
            Console.WriteLine("대상 ISO: " + options.IsoPath);
            Console.WriteLine("방식: ISO 내부 변경 구간만 제자리 기록 (전체 ISO 복사 없음, 파일 크기 불변)");
        }
        else
        {
            backupPath = String.IsNullOrWhiteSpace(options.BackupPath) ?
                DefaultFolderBackupPath(options.FolderPath) : options.BackupPath;
            Console.WriteLine("대상 폴더: " + options.FolderPath);
            Console.WriteLine("방식: USRDIR 의 EBOOT.BIN / LINKDATA.IDX / LINKDATA.BIN 변경 구간만 제자리 기록");
        }
        backupPath = Path.GetFullPath(backupPath);
        string backupDirectory = Path.GetDirectoryName(backupPath);
        if (String.IsNullOrWhiteSpace(backupDirectory) || !Directory.Exists(backupDirectory))
            throw new DirectoryNotFoundException("백업 파일을 저장할 폴더가 없습니다: " + backupDirectory);
        Console.WriteLine("백업: " + backupPath);
        Console.WriteLine();

        bool writable = !options.VerifyOnly;
        using (Target target = isoMode ? OpenIso(options.IsoPath, pack, writable) : OpenFolder(options.FolderPath, pack, writable))
        {
            List<Segment> segments = BuildSegments(target);
            Dictionary<TargetFile, FileState> states;
            string detail;
            if (TryVerifyStatesFast(target, segments, backupPath, out states, out detail))
                WriteOk("기존 백업을 이용한 빠른 상태 검사 완료: " + detail);
            else
            {
                if (File.Exists(backupPath))
                    Console.WriteLine("[*] 빠른 검사 미사용: " + detail);
                Console.WriteLine("[*] 파일 전체 SHA-256 검사로 전환합니다.");
                states = VerifyStates(target);
            }

            if (options.VerifyOnly)
            {
                PrintOverallState(states);
                return states.Values.All(delegate(FileState s) { return s != FileState.Unknown; }) ? 0 : 2;
            }

            if (options.Restore)
                return RestoreMode(target, states, backupPath, options.Yes);

            if (states.Values.All(delegate(FileState s) { return s == FileState.Target; }))
            {
                WriteOk("이미 이 버전(" + pack.Version + ")의 한국어 패치가 적용된 상태입니다.");
                if (File.Exists(backupPath))
                {
                    Console.WriteLine("복구 백업: " + backupPath);
                    if (!options.Yes && AskYes("한국어 패치를 제거하고 원본으로 복구하시겠습니까? [Y/N]: "))
                        return RestoreMode(target, states, backupPath, true);
                }
                return 0;
            }

            if (states.Values.Any(delegate(FileState s) { return s != FileState.Source; }))
            {
                if (!File.Exists(backupPath))
                    throw new InvalidDataException(
                        "대상이 현재 버전의 원본도 완성본도 아니며 지정한 복구 백업도 없습니다. " +
                        "이전 패치 버전이라면 당시 생성한 백업 파일을 선택하세요.");
                SetConsoleColor(ConsoleColor.Yellow);
                Console.WriteLine("[!] 이전 패치 버전 또는 중단 상태를 감지했습니다.");
                Console.WriteLine("    지정한 백업으로 원본 상태를 복원한 뒤 현재 버전을 적용합니다.");
                ResetConsoleColor();
                Confirm(options.Yes, "복구 후 패치를 다시 적용하시겠습니까? [Y/N]: ");
                RestoreBackup(target, backupPath);
                states = VerifyStates(target);
                if (!states.Values.All(delegate(FileState s) { return s == FileState.Source; }))
                    throw new InvalidDataException("백업 복구 후에도 원본 해시가 일치하지 않습니다. 대상을 확인하세요.");
                WriteOk("중단 상태 복구 완료");
            }

            SetConsoleColor(ConsoleColor.Yellow);
            Console.WriteLine("주의: 이 작업은 선택한 " + (isoMode ? "ISO 파일" : "게임 파일") + " 자체를 수정합니다.");
            Console.WriteLine("원상복구 및 다음 버전 갱신용 백업은 지정한 경로에 보존됩니다.");
            ResetConsoleColor();
            Confirm(options.Yes, "계속하시겠습니까? [Y/N]: ");

            Console.WriteLine("기록 구간: {0:N0}개 / 실제 데이터 {1:N0} bytes", segments.Count,
                segments.Sum(delegate(Segment s) { return (long)s.Length; }));

            bool refreshing = File.Exists(backupPath);
            CreateBackup(target, backupPath, segments);
            WriteOk((refreshing ? "기존 복구 백업 갱신 완료: " : "원상복구 백업 생성 완료: ") + backupPath);

            try
            {
                ApplySegments(target, segments);
                states = VerifyStates(target);
                if (!states.Values.All(delegate(FileState s) { return s == FileState.Target; }))
                    throw new InvalidDataException("패치 후 최종 해시가 일치하지 않습니다.");
            }
            catch
            {
                SetConsoleColor(ConsoleColor.Yellow);
                Console.WriteLine("[!] 적용 실패. 백업으로 자동 복구합니다.");
                ResetConsoleColor();
                RestoreBackup(target, backupPath);
                states = VerifyStates(target);
                if (!states.Values.All(delegate(FileState s) { return s == FileState.Source; }))
                    throw new InvalidDataException(
                        "자동 복구 검증에 실패했습니다. 백업 파일을 삭제하지 말고 --restore 로 다시 복구하세요.");
                WriteOk("원본 자동 복구 완료");
                throw;
            }

            Console.WriteLine();
            WriteOk("한국어 패치 적용 및 최종 해시 검증 " + target.Files.Count + "/" + target.Files.Count + " 완료 (" + pack.Version + ")");
            Console.WriteLine("복구하려면 이 실행 파일을 다음 옵션으로 실행하세요:");
            Console.WriteLine("  WO3U_ISO_QuickPatch.exe --restore --backup \"" + backupPath + "\" " +
                (isoMode ? "--iso \"" + options.IsoPath + "\"" : "--folder \"" + options.FolderPath + "\""));
            Console.WriteLine("복구 백업은 삭제하지 않는 것을 권장합니다: " + backupPath);
            return 0;
        }
    }

    private static int RestoreMode(Target target, Dictionary<TargetFile, FileState> states, string backupPath, bool automaticYes)
    {
        if (states.Values.All(delegate(FileState s) { return s == FileState.Source; }))
        {
            WriteOk("이미 일본판 원본 상태입니다.");
            return 0;
        }
        if (!File.Exists(backupPath))
            throw new FileNotFoundException("복구 백업이 없습니다.", backupPath);

        Confirm(automaticYes, "한국어 패치를 제거하고 원본으로 복구하시겠습니까? [Y/N]: ");
        RestoreBackup(target, backupPath);
        Dictionary<TargetFile, FileState> after = VerifyStates(target);
        if (!after.Values.All(delegate(FileState s) { return s == FileState.Source; }))
            throw new InvalidDataException("복구 후 원본 해시 검증에 실패했습니다. 백업을 삭제하지 마세요.");
        WriteOk("원본 복구 및 해시 검증 " + target.Files.Count + "/" + target.Files.Count + " 완료");
        Console.WriteLine("백업은 재적용에 대비해 그대로 보존했습니다: " + backupPath);
        return 0;
    }

    private static void Confirm(bool automaticYes, string message)
    {
        if (automaticYes)
            return;
        if (!AskYes(message))
            throw new OperationCanceledException("사용자가 작업을 취소했습니다.");
    }

    private static bool AskYes(string message)
    {
        Console.Write(message);
        string answer = Console.ReadLine();
        return String.Equals(answer, "Y", StringComparison.OrdinalIgnoreCase);
    }

    private static void SetConsoleColor(ConsoleColor color)
    {
        try { Console.ForegroundColor = color; } catch { }
    }

    private static void ResetConsoleColor()
    {
        try { Console.ResetColor(); } catch { }
    }

    private static void WriteHeader()
    {
        SetConsoleColor(ConsoleColor.Cyan);
        Console.WriteLine("============================================================");
        Console.WriteLine(" " + GameTitle + " (" + GameId + ") 한국어 빠른 패처 " + versionText);
        Console.WriteLine("============================================================");
        ResetConsoleColor();
    }

    private static void WriteOk(string message)
    {
        SetConsoleColor(ConsoleColor.Green);
        Console.WriteLine("[OK] " + message);
        ResetConsoleColor();
    }

    // ------------------------------------------------------------------ 팩 로드

    private static Pack LoadPack()
    {
        Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(PatchResourceName);
        if (resource == null)
            throw new InvalidDataException("실행 파일 내부 패치 데이터를 찾을 수 없습니다.");

        using (resource)
        using (BinaryReader reader = new BinaryReader(resource, Encoding.UTF8))
        {
            string magic = Encoding.ASCII.GetString(ReadExactly(reader, 8));
            if (magic != PackMagic)
                throw new InvalidDataException("패치 데이터 형식이 올바르지 않습니다.");
            int format = reader.ReadInt32();
            if (format != PackFormatVersion)
                throw new InvalidDataException("지원하지 않는 패치 데이터 버전입니다.");
            int versionLength = reader.ReadUInt16();
            Pack pack = new Pack();
            pack.Version = Encoding.UTF8.GetString(ReadExactly(reader, versionLength));
            int fileCount = reader.ReadInt32();
            if (fileCount != TargetNames.Length)
                throw new InvalidDataException("패치 데이터의 파일 수가 예상과 다릅니다.");

            for (int fileIndex = 0; fileIndex < fileCount; fileIndex++)
            {
                PackFile file = new PackFile();
                int pathLength = reader.ReadUInt16();
                file.IsoPath = Encoding.UTF8.GetString(ReadExactly(reader, pathLength));
                file.Name = file.IsoPath.Substring(file.IsoPath.LastIndexOf('/') + 1);
                if (!TargetNames[fileIndex].Equals(file.Name, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("패치 데이터의 파일 순서가 예상과 다릅니다: " + file.Name);
                file.Size = checked((long)reader.ReadUInt64());
                file.SourceHash = ReadExactly(reader, 32);
                file.TargetHash = ReadExactly(reader, 32);
                int rangeCount = checked((int)reader.ReadUInt32());
                long previousEnd = -1;
                for (int rangeIndex = 0; rangeIndex < rangeCount; rangeIndex++)
                {
                    long offset = checked((long)reader.ReadUInt64());
                    int length = checked((int)reader.ReadUInt32());
                    if (offset < previousEnd || length <= 0 || offset + length > file.Size)
                        throw new InvalidDataException("패치 구간이 파일 범위를 벗어났거나 겹칩니다: " + file.IsoPath);
                    file.Ranges.Add(new PatchRange { LogicalOffset = offset, Data = ReadExactly(reader, length) });
                    previousEnd = offset + length;
                }
                pack.Files.Add(file);
            }
            if (resource.Position != resource.Length)
                throw new InvalidDataException("패치 데이터 끝에 알 수 없는 데이터가 있습니다.");
            return pack;
        }
    }

    private static byte[] ReadExactly(BinaryReader reader, int count)
    {
        byte[] data = reader.ReadBytes(count);
        if (data.Length != count)
            throw new EndOfStreamException("데이터를 읽는 중 파일 끝에 도달했습니다.");
        return data;
    }

    // ------------------------------------------------------------------ 대상 열기

    private static Target OpenIso(string isoPath, Pack pack, bool writable)
    {
        Target target = new Target();
        target.Kind = TargetKind.Iso;
        target.Description = isoPath;
        FileStream iso = new FileStream(isoPath, FileMode.Open,
            writable ? FileAccess.ReadWrite : FileAccess.Read,
            writable ? FileShare.None : FileShare.Read, 4 * 1024 * 1024, FileOptions.RandomAccess);
        target.Streams.Add(iso);
        try
        {
            Console.WriteLine("[*] ISO9660 구조 확인");
            IsoRecord root = FindRoot(iso);
            foreach (PackFile patch in pack.Files)
            {
                List<IsoRecord> records = FindIsoPath(iso, root, patch.IsoPath)
                    .Where(delegate(IsoRecord r) { return !r.IsDirectory; }).ToList();
                if (records.Count == 0)
                    throw new InvalidDataException("ISO 내부 파일을 찾을 수 없습니다: " + patch.IsoPath);
                TargetFile file = new TargetFile { Patch = patch, StreamIndex = 0 };
                foreach (IsoRecord record in records)
                {
                    if (record.FileUnitSize != 0 || record.InterleaveGap != 0)
                        throw new InvalidDataException("인터리브 ISO 파일은 지원하지 않습니다: " + patch.IsoPath);
                    file.Extents.Add(new Extent { Offset = checked((long)record.Extent * SectorSize), Size = record.Size });
                }
                long total = file.Extents.Sum(delegate(Extent e) { return e.Size; });
                if (total != patch.Size)
                    throw new InvalidDataException(String.Format(
                        "ISO 내부 파일 크기가 다릅니다: {0} (ISO {1:N0} / 패치 {2:N0})", patch.Name, total, patch.Size));
                if (file.Extents.Any(delegate(Extent e) { return e.Offset + e.Size > iso.Length; }))
                    throw new InvalidDataException("ISO extent 가 이미지 범위를 벗어났습니다: " + patch.IsoPath);
                target.Files.Add(file);
                Console.WriteLine("    {0,-13} {1,15:N0} bytes / extent {2} / 시작 0x{3:X}",
                    patch.Name, patch.Size, file.Extents.Count, file.Extents[0].Offset);
            }
            return target;
        }
        catch
        {
            target.Dispose();
            throw;
        }
    }

    private static Target OpenFolder(string usrdir, Pack pack, bool writable)
    {
        Target target = new Target();
        target.Kind = TargetKind.Folder;
        target.Description = usrdir;
        try
        {
            Console.WriteLine("[*] 폴더형 게임 파일 확인");
            foreach (PackFile patch in pack.Files)
            {
                string path = Path.Combine(usrdir, patch.Name);
                if (!File.Exists(path))
                    throw new FileNotFoundException("게임 파일이 없습니다.", path);
                FileStream stream = new FileStream(path, FileMode.Open,
                    writable ? FileAccess.ReadWrite : FileAccess.Read,
            writable ? FileShare.None : FileShare.Read, 4 * 1024 * 1024, FileOptions.RandomAccess);
                target.Streams.Add(stream);
                if (stream.Length != patch.Size)
                    throw new InvalidDataException(String.Format(
                        "파일 크기가 다릅니다: {0} (파일 {1:N0} / 패치 {2:N0})", patch.Name, stream.Length, patch.Size));
                TargetFile file = new TargetFile { Patch = patch, StreamIndex = target.Streams.Count - 1 };
                file.Extents.Add(new Extent { Offset = 0, Size = stream.Length });
                target.Files.Add(file);
                Console.WriteLine("    {0,-13} {1,15:N0} bytes", patch.Name, patch.Size);
            }
            return target;
        }
        catch
        {
            target.Dispose();
            throw;
        }
    }

    // ------------------------------------------------------------------ ISO9660

    private sealed class IsoRecord
    {
        public string Name;
        public uint Extent;
        public uint Size;
        public bool IsDirectory;
        public int FileUnitSize;
        public int InterleaveGap;
    }

    private static IsoRecord FindRoot(FileStream iso)
    {
        byte[] descriptor = new byte[SectorSize];
        for (int sector = 16; sector < 64; sector++)
        {
            if ((long)(sector + 1) * SectorSize > iso.Length)
                break;
            iso.Position = (long)sector * SectorSize;
            ReadFully(iso, descriptor, 0, descriptor.Length);
            if (Encoding.ASCII.GetString(descriptor, 1, 5) != "CD001")
                continue;
            if (descriptor[0] == 1)
            {
                IsoRecord root = ParseIsoRecord(descriptor, 156);
                if (root == null || !root.IsDirectory)
                    throw new InvalidDataException("ISO 루트 디렉터리를 읽을 수 없습니다.");
                return root;
            }
            if (descriptor[0] == 255)
                break;
        }
        throw new InvalidDataException(
            "표준 ISO9660 을 찾을 수 없습니다. 암호화된 PS3 ISO 는 먼저 복호화해야 합니다.");
    }

    private static List<IsoRecord> FindIsoPath(FileStream iso, IsoRecord root, string path)
    {
        string[] parts = path.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
        IsoRecord current = root;
        for (int index = 0; index < parts.Length; index++)
        {
            List<IsoRecord> matches = ReadDirectory(iso, current)
                .Where(delegate(IsoRecord r) { return r.Name.Equals(parts[index], StringComparison.OrdinalIgnoreCase); })
                .ToList();
            if (matches.Count == 0)
                throw new InvalidDataException("ISO 내부 경로가 없습니다: " + path + " (" + parts[index] + ")");
            if (index == parts.Length - 1)
                return matches;
            current = matches[0];
            if (!current.IsDirectory)
                throw new InvalidDataException("ISO 경로 중간 항목이 디렉터리가 아닙니다: " + parts[index]);
        }
        return new List<IsoRecord> { current };
    }

    private static List<IsoRecord> ReadDirectory(FileStream iso, IsoRecord directory)
    {
        if (!directory.IsDirectory || directory.Size > 64 * 1024 * 1024)
            throw new InvalidDataException("지원하지 않는 ISO 디렉터리입니다.");
        byte[] data = new byte[(int)directory.Size];
        iso.Position = checked((long)directory.Extent * SectorSize);
        ReadFully(iso, data, 0, data.Length);

        List<IsoRecord> records = new List<IsoRecord>();
        int position = 0;
        while (position < data.Length)
        {
            int length = data[position];
            if (length == 0)
            {
                position = ((position / SectorSize) + 1) * SectorSize;
                continue;
            }
            IsoRecord record = ParseIsoRecord(data, position);
            if (record != null && record.Name != "." && record.Name != "..")
                records.Add(record);
            position += length;
        }
        return records;
    }

    private static IsoRecord ParseIsoRecord(byte[] data, int offset)
    {
        int length = data[offset];
        if (length == 0)
            return null;
        if (offset < 0 || offset + length > data.Length || length < 34)
            throw new InvalidDataException("손상된 ISO 디렉터리 레코드입니다.");
        int nameLength = data[offset + 32];
        if (offset + 33 + nameLength > data.Length)
            throw new InvalidDataException("손상된 ISO 파일명 레코드입니다.");
        string name;
        if (nameLength == 1 && data[offset + 33] == 0)
            name = ".";
        else if (nameLength == 1 && data[offset + 33] == 1)
            name = "..";
        else
        {
            name = Encoding.ASCII.GetString(data, offset + 33, nameLength);
            int semicolon = name.IndexOf(';');
            if (semicolon >= 0)
                name = name.Substring(0, semicolon);
        }
        int flags = data[offset + 25];
        return new IsoRecord
        {
            Name = name,
            Extent = BitConverter.ToUInt32(data, offset + 2),
            Size = BitConverter.ToUInt32(data, offset + 10),
            IsDirectory = (flags & 2) != 0,
            FileUnitSize = data[offset + 26],
            InterleaveGap = data[offset + 27]
        };
    }

    // ------------------------------------------------------------------ 상태 검증

    private static Dictionary<TargetFile, FileState> VerifyStates(Target target)
    {
        Console.WriteLine("[*] 파일 SHA-256 검증");
        Dictionary<TargetFile, FileState> states = new Dictionary<TargetFile, FileState>();
        foreach (TargetFile file in target.Files)
        {
            byte[] hash = HashExtents(target.Streams[file.StreamIndex], file.Extents, file.Patch.Name);
            FileState state = EqualBytes(hash, file.Patch.SourceHash) ? FileState.Source :
                (EqualBytes(hash, file.Patch.TargetHash) ? FileState.Target : FileState.Unknown);
            states.Add(file, state);
            Console.WriteLine("    {0,-13} {1}  {2}", file.Patch.Name, ToHex(hash),
                state == FileState.Source ? "원본" : (state == FileState.Target ? "패치됨" : "불일치"));
        }
        return states;
    }

    private static void PrintOverallState(Dictionary<TargetFile, FileState> states)
    {
        if (states.Values.All(delegate(FileState s) { return s == FileState.Source; }))
            WriteOk("지원되는 일본판 원본 상태입니다. 빠른 패치를 적용할 수 있습니다.");
        else if (states.Values.All(delegate(FileState s) { return s == FileState.Target; }))
            WriteOk("한국어 패치가 정상 적용된 상태입니다.");
        else
        {
            SetConsoleColor(ConsoleColor.Red);
            Console.WriteLine("[불일치] 지원되지 않거나 중간 상태입니다. (다른 버전 패치·직접 수정 등)");
            ResetConsoleColor();
        }
    }

    private static byte[] HashExtents(FileStream stream, List<Extent> extents, string label)
    {
        long total = extents.Sum(delegate(Extent e) { return e.Size; });
        bool showProgress = total >= 512L * 1024 * 1024;
        long done = 0;
        int nextPercent = 25;
        using (SHA256 sha = SHA256.Create())
        {
            byte[] buffer = new byte[8 * 1024 * 1024];
            foreach (Extent extent in extents)
            {
                stream.Position = extent.Offset;
                long remaining = extent.Size;
                while (remaining > 0)
                {
                    int wanted = (int)Math.Min((long)buffer.Length, remaining);
                    int read = stream.Read(buffer, 0, wanted);
                    if (read <= 0)
                        throw new EndOfStreamException("해시 계산 중 파일 끝에 도달했습니다.");
                    sha.TransformBlock(buffer, 0, read, null, 0);
                    remaining -= read;
                    done += read;
                    if (showProgress && done * 100 / total >= nextPercent)
                    {
                        Console.WriteLine("    {0} {1}%", label, nextPercent);
                        nextPercent += 25;
                    }
                }
            }
            sha.TransformFinalBlock(new byte[0], 0, 0);
            return sha.Hash;
        }
    }

    // ------------------------------------------------------------------ 구간 계산

    private static List<Segment> BuildSegments(Target target)
    {
        List<Segment> segments = new List<Segment>();
        foreach (TargetFile file in target.Files)
        {
            foreach (PatchRange range in file.Patch.Ranges)
            {
                long logical = range.LogicalOffset;
                int dataOffset = 0;
                int remaining = range.Data.Length;
                long extentLogicalStart = 0;
                foreach (Extent extent in file.Extents)
                {
                    long extentLogicalEnd = extentLogicalStart + extent.Size;
                    if (logical >= extentLogicalEnd)
                    {
                        extentLogicalStart = extentLogicalEnd;
                        continue;
                    }
                    if (logical < extentLogicalStart)
                        throw new InvalidDataException("패치 구간 extent 변환에 실패했습니다.");
                    long inside = logical - extentLogicalStart;
                    int count = (int)Math.Min((long)remaining, extent.Size - inside);
                    segments.Add(new Segment
                    {
                        StreamIndex = file.StreamIndex,
                        AbsoluteOffset = extent.Offset + inside,
                        Data = range.Data,
                        DataOffset = dataOffset,
                        Length = count
                    });
                    logical += count;
                    dataOffset += count;
                    remaining -= count;
                    if (remaining == 0)
                        break;
                    extentLogicalStart = extentLogicalEnd;
                }
                if (remaining != 0)
                    throw new InvalidDataException("패치 구간이 extent 범위를 벗어났습니다.");
            }
        }
        segments.Sort(delegate(Segment left, Segment right)
        {
            int byStream = left.StreamIndex.CompareTo(right.StreamIndex);
            return byStream != 0 ? byStream : left.AbsoluteOffset.CompareTo(right.AbsoluteOffset);
        });
        for (int index = 1; index < segments.Count; index++)
        {
            Segment previous = segments[index - 1];
            if (previous.StreamIndex == segments[index].StreamIndex &&
                previous.AbsoluteOffset + previous.Length > segments[index].AbsoluteOffset)
                throw new InvalidDataException("겹치는 물리 패치 구간이 발견되었습니다.");
        }
        return segments;
    }

    // 기존 백업이 있으면 변경 구간만 비교해 원본/패치 상태를 빠르게 판정한다.
    private static bool TryVerifyStatesFast(
        Target target, List<Segment> expected, string backupPath,
        out Dictionary<TargetFile, FileState> states, out string detail)
    {
        states = null;
        detail = "기존 백업이 없습니다.";
        if (!File.Exists(backupPath))
            return false;

        List<Segment> originals;
        try
        {
            originals = ReadBackupSegments(backupPath, target);
        }
        catch (Exception ex)
        {
            detail = ex.Message;
            return false;
        }

        if (originals.Count != expected.Count)
        {
            detail = "백업 구간 수가 현재 패치와 다릅니다.";
            return false;
        }

        bool allSource = true;
        bool allTarget = true;
        byte[] current = null;
        for (int index = 0; index < expected.Count; index++)
        {
            Segment want = expected[index];
            Segment original = originals[index];
            if (original.StreamIndex != want.StreamIndex || original.AbsoluteOffset != want.AbsoluteOffset ||
                original.Length != want.Length)
            {
                detail = "백업 구간 배치가 현재 패치와 다릅니다.";
                return false;
            }
            if (current == null || current.Length < want.Length)
                current = new byte[want.Length];
            FileStream stream = target.Streams[want.StreamIndex];
            stream.Position = want.AbsoluteOffset;
            ReadFully(stream, current, 0, want.Length);

            bool sourceMatch = BytesEqual(current, 0, original.Data, original.DataOffset, want.Length);
            bool targetMatch = BytesEqual(current, 0, want.Data, want.DataOffset, want.Length);
            allSource = allSource && sourceMatch;
            allTarget = allTarget && targetMatch;
            if (!allSource && !allTarget)
            {
                detail = "변경 구간 상태가 원본이나 현재 패치와 완전히 일치하지 않습니다.";
                return false;
            }
        }

        FileState state;
        if (allSource && !allTarget)
            state = FileState.Source;
        else if (allTarget && !allSource)
            state = FileState.Target;
        else
        {
            detail = "변경 구간만으로 상태를 구분할 수 없습니다.";
            return false;
        }
        states = target.Files.ToDictionary(delegate(TargetFile f) { return f; }, delegate(TargetFile f) { return state; });
        detail = state == FileState.Source ? "일본판 원본 상태" : "현재 한국어 패치 상태";
        return true;
    }

    private static bool BytesEqual(byte[] left, int leftOffset, byte[] right, int rightOffset, int count)
    {
        if (left == null || right == null || leftOffset < 0 || rightOffset < 0 || count < 0 ||
            leftOffset + count > left.Length || rightOffset + count > right.Length)
            return false;
        for (int index = 0; index < count; index++)
            if (left[leftOffset + index] != right[rightOffset + index])
                return false;
        return true;
    }

    // ------------------------------------------------------------------ 백업 형식
    //
    // magic "WO3UBAK1" | i32 version | i32 kind | i32 streamCount | i64 length × streamCount
    // | i32 segmentCount | (i32 stream, i64 offset, i32 length, bytes) × segmentCount | 32B sha256(앞부분 전체)

    private static List<Segment> ReadBackupSegments(string backupPath, Target target)
    {
        using (FileStream input = new FileStream(backupPath, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, FileOptions.SequentialScan))
        using (BinaryReader reader = new BinaryReader(input, Encoding.UTF8))
        {
            string magic = Encoding.ASCII.GetString(ReadExactly(reader, 8));
            int version = reader.ReadInt32();
            int kind = reader.ReadInt32();
            int streamCount = reader.ReadInt32();
            if (magic != BackupMagic || version != BackupFormatVersion)
                throw new InvalidDataException("WO3U 복구 백업 형식이 아닙니다.");
            if (kind != (int)target.Kind || streamCount != target.Streams.Count)
                throw new InvalidDataException("복구 백업이 현재 대상(ISO/폴더)과 맞지 않습니다.");
            for (int index = 0; index < streamCount; index++)
            {
                long length = reader.ReadInt64();
                if (length != target.Streams[index].Length)
                    throw new InvalidDataException("복구 백업의 파일 크기가 현재 대상과 다릅니다.");
            }
            int count = reader.ReadInt32();
            if (count < 1 || count > 4000000)
                throw new InvalidDataException("복구 백업 구간 수가 올바르지 않습니다.");

            List<Segment> result = new List<Segment>(count);
            int previousStream = -1;
            long previousEnd = -1;
            for (int index = 0; index < count; index++)
            {
                int streamIndex = reader.ReadInt32();
                long offset = reader.ReadInt64();
                int length = reader.ReadInt32();
                if (streamIndex < 0 || streamIndex >= streamCount || offset < 0 || length <= 0 ||
                    length > 256 * 1024 * 1024 || offset + length > target.Streams[streamIndex].Length ||
                    streamIndex < previousStream || (streamIndex == previousStream && offset < previousEnd))
                    throw new InvalidDataException("복구 백업 구간이 올바르지 않습니다.");
                result.Add(new Segment
                {
                    StreamIndex = streamIndex,
                    AbsoluteOffset = offset,
                    Data = ReadExactly(reader, length),
                    DataOffset = 0,
                    Length = length
                });
                previousStream = streamIndex;
                previousEnd = offset + length;
            }
            if (input.Length - input.Position != 32)
                throw new InvalidDataException("복구 백업 무결성 정보가 올바르지 않습니다.");
            byte[] storedHash = ReadExactly(reader, 32);
            byte[] actualHash = ComputeHashPrefix(input, input.Length - 32);
            if (!BytesEqual(storedHash, 0, actualHash, 0, 32))
                throw new InvalidDataException("복구 백업 무결성 검사에 실패했습니다.");
            return result;
        }
    }

    private static byte[] ComputeHashPrefix(Stream input, long length)
    {
        input.Position = 0;
        using (SHA256 sha = SHA256.Create())
        {
            byte[] buffer = new byte[1024 * 1024];
            long remaining = length;
            while (remaining > 0)
            {
                int request = (int)Math.Min(buffer.Length, remaining);
                int read = input.Read(buffer, 0, request);
                if (read <= 0)
                    throw new EndOfStreamException("백업 무결성을 검사하는 중 파일 끝에 도달했습니다.");
                sha.TransformBlock(buffer, 0, read, null, 0);
                remaining -= read;
            }
            sha.TransformFinalBlock(new byte[0], 0, 0);
            return sha.Hash;
        }
    }

    private static void CreateBackup(Target target, string backupPath, List<Segment> segments)
    {
        string temporary = backupPath + ".tmp";
        if (File.Exists(temporary))
            File.Delete(temporary);

        using (FileStream output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
            1024 * 1024, FileOptions.SequentialScan))
        using (BinaryWriter writer = new BinaryWriter(output, Encoding.UTF8))
        {
            writer.Write(Encoding.ASCII.GetBytes(BackupMagic));
            writer.Write(BackupFormatVersion);
            writer.Write((int)target.Kind);
            writer.Write(target.Streams.Count);
            foreach (FileStream stream in target.Streams)
                writer.Write(stream.Length);
            writer.Write(segments.Count);
            foreach (Segment segment in segments)
            {
                byte[] original = new byte[segment.Length];
                FileStream stream = target.Streams[segment.StreamIndex];
                stream.Position = segment.AbsoluteOffset;
                ReadFully(stream, original, 0, original.Length);
                writer.Write(segment.StreamIndex);
                writer.Write(segment.AbsoluteOffset);
                writer.Write(original.Length);
                writer.Write(original);
            }
            writer.Flush();
            output.Flush(true);
        }

        byte[] integrityHash;
        using (FileStream input = new FileStream(temporary, FileMode.Open, FileAccess.Read, FileShare.Read,
            1024 * 1024, FileOptions.SequentialScan))
        using (SHA256 sha = SHA256.Create())
            integrityHash = sha.ComputeHash(input);
        using (FileStream output = new FileStream(temporary, FileMode.Append, FileAccess.Write, FileShare.None,
            4096, FileOptions.WriteThrough))
        {
            output.Write(integrityHash, 0, integrityHash.Length);
            output.Flush(true);
        }

        if (File.Exists(backupPath))
            File.Delete(backupPath);
        File.Move(temporary, backupPath);
    }

    private static void ApplySegments(Target target, List<Segment> segments)
    {
        Console.WriteLine("[*] 변경 구간 직접 기록");
        int done = 0;
        int nextPercent = 10;
        foreach (Segment segment in segments)
        {
            FileStream stream = target.Streams[segment.StreamIndex];
            stream.Position = segment.AbsoluteOffset;
            stream.Write(segment.Data, segment.DataOffset, segment.Length);
            done++;
            int percent = (int)((done * 100L) / segments.Count);
            if (percent >= nextPercent)
            {
                Console.WriteLine("    {0}%", percent);
                nextPercent += 10;
            }
        }
        foreach (FileStream stream in target.Streams)
            stream.Flush(true);
        WriteOk("변경 구간 기록 완료");
    }

    private static void RestoreBackup(Target target, string backupPath)
    {
        Console.WriteLine("[*] 원상복구 백업 적용");
        List<Segment> backupSegments = ReadBackupSegments(backupPath, target);
        foreach (Segment segment in backupSegments)
        {
            FileStream stream = target.Streams[segment.StreamIndex];
            stream.Position = segment.AbsoluteOffset;
            stream.Write(segment.Data, segment.DataOffset, segment.Length);
        }
        foreach (FileStream stream in target.Streams)
            stream.Flush(true);
        WriteOk("백업 데이터 기록 완료 (" + backupSegments.Count.ToString("N0") + " 구간)");
    }

    private static void ReadFully(Stream stream, byte[] buffer, int offset, int count)
    {
        while (count > 0)
        {
            int read = stream.Read(buffer, offset, count);
            if (read <= 0)
                throw new EndOfStreamException("읽는 중 파일 끝에 도달했습니다.");
            offset += read;
            count -= read;
        }
    }

    private static bool EqualBytes(byte[] left, byte[] right)
    {
        if (left == null || right == null || left.Length != right.Length)
            return false;
        int difference = 0;
        for (int index = 0; index < left.Length; index++)
            difference |= left[index] ^ right[index];
        return difference == 0;
    }

    private static string ToHex(byte[] data)
    {
        StringBuilder result = new StringBuilder(data.Length * 2);
        foreach (byte value in data)
            result.Append(value.ToString("X2"));
        return result.ToString();
    }
}
