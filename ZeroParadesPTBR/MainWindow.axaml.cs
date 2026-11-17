using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace ZeroParadesPTBR;

public partial class MainWindow : Window
{
    private string? _poPath;
    private bool _isBusy;

    public MainWindow()
    {
        InitializeComponent();
        Opened += OnWindowOpened;
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        _poPath = FindPoFile();
        if (_poPath is not null)
        {
            PoStatusText.Text = $"Tradução encontrada: {Path.GetFileName(_poPath)}";
            AppendLog($"Arquivo de tradução: {_poPath}");
        }
        else
        {
            PoStatusText.Text = "ERRO: my_translation.po não encontrado na pasta do instalador.";
            AppendLog("ERRO: coloque my_translation.po na mesma pasta do executável.");
        }

        var detected = SteamLocator.FindBundle();
        if (detected is not null)
        {
            BundlePathBox.Text = detected;
            AppendLog($"Jogo detectado automaticamente:\n  {detected}");
        }
        else
        {
            AppendLog("Não foi possível detectar o jogo automaticamente. Clique em 'Procurar...' para selecionar o arquivo *_assets_all_*.bundle ou a pasta do jogo.");
        }

        UpdateControls();
    }

    private async void OnBrowseClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Selecione o arquivo *_assets_all_*.bundle do Zero Parades",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Unity AssetBundle") { Patterns = ["*.bundle"] },
                FilePickerFileTypes.All
            ]
        });

        if (files.Count == 0) return;

        var selected = files[0].TryGetLocalPath();
        if (string.IsNullOrWhiteSpace(selected)) return;

        var resolved = SteamLocator.ResolveBundleFromPath(selected);
        if (resolved is null)
        {
            AppendLog("O arquivo selecionado não é um bundle *_assets_all_*.bundle válido.");
            return;
        }

        BundlePathBox.Text = resolved;
        AppendLog($"Bundle selecionado: {resolved}");
        UpdateControls();
    }

    private async void OnInstallClick(object? sender, RoutedEventArgs e)
    {
        var bundlePath = BundlePathBox.Text;
        if (string.IsNullOrWhiteSpace(bundlePath) || _poPath is null) return;

        SetBusy(true);
        try
        {
            var summary = await Task.Run(() => BundlePatcher.ApplyPatch(bundlePath, _poPath, AppendLog));
            AppendLog("");
            AppendLog($"Concluído! {summary.TotalKeys:N0} chaves processadas ({summary.OriginalSize / 1048576.0:F1} MB -> {summary.PatchedSize / 1048576.0:F1} MB).");
            AppendLog("Abra o jogo e selecione Deutsch/Alemão em Settings -> Language.");
        }
        catch (Exception ex)
        {
            AppendLog($"ERRO: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnRestoreClick(object? sender, RoutedEventArgs e)
    {
        var bundlePath = BundlePathBox.Text;
        if (string.IsNullOrWhiteSpace(bundlePath)) return;

        SetBusy(true);
        try
        {
            await Task.Run(() => BundlePatcher.RestoreBackup(bundlePath, AppendLog));
            AppendLog("Arquivos originais restaurados com sucesso.");
        }
        catch (Exception ex)
        {
            AppendLog($"ERRO ao restaurar: {ex.Message}");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;
        Progress.IsVisible = busy;
        Progress.IsIndeterminate = busy;
        UpdateControls();
    }

    private void UpdateControls()
    {
        var bundlePath = BundlePathBox.Text;
        bool hasBundle = !string.IsNullOrWhiteSpace(bundlePath) && File.Exists(bundlePath);
        bool hasBackup = hasBundle && File.Exists(bundlePath + ".bak");

        BackupStatusText.Text = hasBackup ? "Backup .bak disponível" : (hasBundle ? "Sem backup ainda" : "");
        BrowseButton.IsEnabled = !_isBusy;
        InstallButton.IsEnabled = !_isBusy && hasBundle && _poPath is not null;
        RestoreButton.IsEnabled = !_isBusy && hasBackup;
    }

    private void AppendLog(string message)
    {
        Dispatcher.UIThread.Post(() =>
        {
            LogBox.Text = string.IsNullOrEmpty(LogBox.Text)
                ? message
                : $"{LogBox.Text}\n{message}";
            LogBox.CaretIndex = LogBox.Text.Length;
        });
    }

    private static string? FindPoFile()
    {
        var baseDir = AppContext.BaseDirectory;
        var candidates = new[]
        {
            Path.Combine(baseDir, "my_translation.po"),
            Path.Combine(Directory.GetCurrentDirectory(), "my_translation.po"),
            Path.GetFullPath(Path.Combine(baseDir, "..", "..", "..", "..", "..", "release_ptbr", "my_translation.po"))
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
