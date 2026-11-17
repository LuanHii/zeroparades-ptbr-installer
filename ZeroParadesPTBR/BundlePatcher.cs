using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace ZeroParadesPTBR;

public sealed record PatchSummary(
    int TotalKeys,
    int MissingKeys,
    long OriginalSize,
    long PatchedSize,
    string CatalogMessage
);

public static class BundlePatcher
{
    private const string TemplateAssetPath = "Assets/FELDRuntime/Scriptables/Main/Localization/de/deLocalizationTable.asset";
    private const string TemplateAssetName = "deLocalizationTable";
    private const int TemplateLangCode = 57;

    public static PatchSummary ApplyPatch(string bundlePath, string poPath, Action<string>? log = null)
    {
        if (!File.Exists(bundlePath))
            throw new FileNotFoundException("Bundle não encontrado.", bundlePath);
        if (!File.Exists(poPath))
            throw new FileNotFoundException("Arquivo my_translation.po não encontrado.", poPath);

        log?.Invoke("Carregando traduções (.po)...");
        var po = PoParser.Load(poPath);
        log?.Invoke($"Traduções carregadas: {po.TotalEntries:N0} entradas ({po.UntranslatedEntries} sem tradução).");

        string backupPath = bundlePath + ".bak";
        if (!File.Exists(backupPath))
        {
            log?.Invoke("Criando backup do bundle original (.bak)...");
            File.Copy(bundlePath, backupPath);
        }

        long originalSize = new FileInfo(bundlePath).Length;
        string tempUnpacked = bundlePath + ".tmp_write";

        var manager = new AssetsManager();
        try
        {
            log?.Invoke("Abrindo AssetBundle...");
            var bundleInst = manager.LoadBundleFile(bundlePath, unpackIfPacked: true);
            var assetsInst = manager.LoadAssetsFileFromBundle(bundleInst, 0, loadDeps: false);

            var targetInfo = FindLocalizationTableAsset(manager, assetsInst)
                ?? throw new InvalidOperationException($"Asset '{TemplateAssetName}' não encontrado no bundle.");

            var baseField = manager.GetBaseField(assetsInst, targetInfo);

            baseField["m_Name"].AsString = TemplateAssetName;
            baseField["m_languageCode"].AsInt = TemplateLangCode;

            var keysArray = baseField["m_data"]["m_keys"]["Array"];
            var valuesArray = baseField["m_data"]["m_values"]["Array"];

            int keyCount = keysArray.Children.Count;
            if (keyCount != valuesArray.Children.Count)
                throw new InvalidDataException("Tabela de localização com contagem divergente de chaves/valores.");

            int missing = 0;
            for (int i = 0; i < keyCount; i++)
            {
                string key = keysArray.Children[i].AsString;
                if (po.Translations.TryGetValue(key, out var translated))
                {
                    valuesArray.Children[i].AsString = translated;
                }
                else
                {
                    missing++;
                }
            }

            if (missing > 0)
            {
                log?.Invoke($"Aviso: {missing} chave(s) novas do jogo mantidas no texto original.");
            }

            targetInfo.SetNewData(baseField);

            var dirInfo = bundleInst.file.BlockAndDirInfo.DirectoryInfos[0];
            dirInfo.SetNewData(assetsInst.file);

            log?.Invoke("Compactando bundle (LZ4)...");
            using (var uncompressedStream = new MemoryStream())
            using (var uncompressedWriter = new AssetsFileWriter(uncompressedStream))
            {
                bundleInst.file.Write(uncompressedWriter);
                uncompressedStream.Position = 0;

                var unpackedBundle = new AssetBundleFile();
                unpackedBundle.Read(new AssetsFileReader(uncompressedStream));

                using var outWriter = new AssetsFileWriter(tempUnpacked);
                unpackedBundle.Pack(outWriter, AssetBundleCompressionType.LZ4);
                unpackedBundle.Close();
            }

            manager.UnloadAll(unloadClassData: true);

            File.Move(tempUnpacked, bundlePath, overwrite: true);
            long patchedSize = new FileInfo(bundlePath).Length;

            log?.Invoke("Atualizando catálogo Addressables...");
            string catalogMsg = CatalogPatcher.PatchCatalog(bundlePath, (uint)patchedSize, log);
            log?.Invoke(catalogMsg);

            return new PatchSummary(keyCount, missing, originalSize, patchedSize, catalogMsg);
        }
        finally
        {
            manager.UnloadAll(unloadClassData: true);
            if (File.Exists(tempUnpacked))
            {
                try { File.Delete(tempUnpacked); } catch { }
            }
        }
    }

    public static void RestoreBackup(string bundlePath, Action<string>? log = null)
    {
        string backupPath = bundlePath + ".bak";
        if (!File.Exists(backupPath))
            throw new FileNotFoundException("Backup do bundle (.bak) não encontrado.", backupPath);

        File.Copy(backupPath, bundlePath, overwrite: true);
        log?.Invoke("Bundle original restaurado.");

        var aaDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(bundlePath)!, ".."));
        foreach (var catName in new[] { "catalog.bin", "catalog.json" })
        {
            var catPath = Path.Combine(aaDir, catName);
            var catBak = catPath + ".bak";
            if (File.Exists(catBak))
            {
                File.Copy(catBak, catPath, overwrite: true);
                log?.Invoke($"Catálogo restaurado: {catName}");
            }
        }
    }

    private static AssetFileInfo? FindLocalizationTableAsset(AssetsManager manager, AssetsFileInstance assetsInst)
    {
        // First try AssetBundle container lookup (AssetClassID.AssetBundle = 142)
        var abInfo = assetsInst.file.GetAssetsOfType(AssetClassID.AssetBundle).FirstOrDefault();
        if (abInfo is not null)
        {
            var abField = manager.GetBaseField(assetsInst, abInfo);
            var containerArray = abField["m_Container"]["Array"];
            foreach (var entry in containerArray.Children)
            {
                string path = entry["first"].AsString;
                if (string.Equals(path, TemplateAssetPath, StringComparison.OrdinalIgnoreCase))
                {
                    long pathId = entry["second"]["asset"]["m_PathID"].AsLong;
                    return assetsInst.file.GetAssetInfo(pathId);
                }
            }
        }

        // Fallback: scan MonoBehaviours for m_Name == deLocalizationTable
        foreach (var info in assetsInst.file.GetAssetsOfType(AssetClassID.MonoBehaviour))
        {
            var field = manager.GetBaseField(assetsInst, info);
            if (field["m_Name"].AsString == TemplateAssetName)
            {
                return info;
            }
        }

        return null;
    }
}
