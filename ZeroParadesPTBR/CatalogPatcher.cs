using System.Buffers.Binary;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ZeroParadesPTBR;

public static partial class CatalogPatcher
{
    private static readonly byte[] BundleHashMarker = ".bundle\x20\x00\x00\x00"u8.ToArray();
    private static readonly byte[] CrcFieldUtf16 = Encoding.Unicode.GetBytes("\"m_Crc\":");

    [GeneratedRegex("\\\"m_Crc\\\":\\d+")]
    private static partial Regex CrcJsonRegex();

    public static string PatchCatalog(string bundlePath, uint newBundleSize, Action<string>? log = null)
    {
        var aaDir = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(bundlePath)!, ".."));
        var catalogBin = Path.Combine(aaDir, "catalog.bin");
        var catalogJson = Path.Combine(aaDir, "catalog.json");
        var bundleName = Path.GetFileName(bundlePath);

        if (File.Exists(catalogBin))
        {
            var (records, sizeUpdated) = PatchCatalogBin(catalogBin, bundleName, newBundleSize, log);
            return $"catalog.bin atualizado ({records} registro(s) CRC=0{(sizeUpdated ? ", tamanho atualizado" : "")}).";
        }

        if (File.Exists(catalogJson))
        {
            PatchCatalogJson(catalogJson, log);
            return "catalog.json atualizado (CRC=0).";
        }

        log?.Invoke($"Aviso: nenhum catalog.bin ou catalog.json encontrado em {aaDir}");
        return "Aviso: arquivo de catálogo não encontrado.";
    }

    public static (int PatchedRecords, bool SizeUpdated) PatchCatalogBin(
        string catalogBinPath,
        string? bundleName = null,
        uint? newBundleSize = null,
        Action<string>? log = null)
    {
        byte[] raw = File.ReadAllBytes(catalogBinPath);
        byte[] buf = (byte[])raw.Clone();
        byte[]? bundleNameBytes = bundleName is not null ? Encoding.UTF8.GetBytes(bundleName) : null;

        int patchedCount = 0;
        bool sizeUpdated = false;
        int pos = 0;

        while (true)
        {
            int idx = raw.AsSpan(pos).IndexOf(BundleHashMarker);
            if (idx == -1) break;
            idx += pos;

            int hashOff = idx + BundleHashMarker.Length;
            pos = hashOff;

            if (hashOff + 48 > raw.Length) continue;
            if (!IsHexAscii(raw.AsSpan(hashOff, 32))) continue;

            uint ptr = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(hashOff + 36, 4));
            if (ptr != (uint)hashOff) continue;

            uint crc = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(hashOff + 40, 4));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(raw.AsSpan(hashOff + 44, 4));

            if (crc != 0)
            {
                BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(hashOff + 40, 4), 0);
            }
            patchedCount++;

            if (bundleNameBytes is not null && newBundleSize is not null)
            {
                int searchStart = Math.Max(0, idx - 256);
                int searchLen = (idx + 7) - searchStart;
                if (raw.AsSpan(searchStart, searchLen).IndexOf(bundleNameBytes) >= 0)
                {
                    if (size != newBundleSize.Value)
                    {
                        BinaryPrimitives.WriteUInt32LittleEndian(buf.AsSpan(hashOff + 44, 4), newBundleSize.Value);
                    }
                    sizeUpdated = true;
                }
            }
        }

        if (patchedCount == 0)
        {
            log?.Invoke("Aviso: nenhum registro de CRC encontrado em catalog.bin.");
            return (0, false);
        }

        string backupPath = catalogBinPath + ".bak";
        if (!File.Exists(backupPath))
        {
            File.Copy(catalogBinPath, backupPath);
            log?.Invoke($"Backup do catálogo criado: {Path.GetFileName(backupPath)}");
        }

        if (!buf.AsSpan().SequenceEqual(raw))
        {
            File.WriteAllBytes(catalogBinPath, buf);
        }

        return (patchedCount, sizeUpdated);
    }

    public static void PatchCatalogJson(string catalogJsonPath, Action<string>? log = null)
    {
        var text = File.ReadAllText(catalogJsonPath, Encoding.UTF8);
        var node = JsonNode.Parse(text)?.AsObject()
            ?? throw new InvalidDataException("catalog.json inválido.");

        var extraBase64 = node["m_ExtraDataString"]?.GetValue<string>()
            ?? throw new InvalidDataException("m_ExtraDataString ausente em catalog.json.");

        byte[] extraBytes = Convert.FromBase64String(extraBase64);
        string extraUtf16 = Encoding.Unicode.GetString(extraBytes);
        string patched = CrcJsonRegex().Replace(extraUtf16, m =>
        {
            int digits = m.Length - "\"m_Crc\":".Length;
            return "\"m_Crc\":" + new string(' ', Math.Max(0, digits - 1)) + "0";
        });

        if (patched == extraUtf16)
        {
            log?.Invoke("Nenhum m_Crc encontrado em catalog.json.");
            return;
        }

        string backupPath = catalogJsonPath + ".bak";
        if (!File.Exists(backupPath))
        {
            File.Copy(catalogJsonPath, backupPath);
            log?.Invoke($"Backup do catálogo criado: {Path.GetFileName(backupPath)}");
        }

        node["m_ExtraDataString"] = Convert.ToBase64String(Encoding.Unicode.GetBytes(patched));
        File.WriteAllText(
            catalogJsonPath,
            node.ToJsonString(new JsonSerializerOptions { WriteIndented = false }),
            Encoding.UTF8
        );
    }

    private static bool IsHexAscii(ReadOnlySpan<byte> bytes)
    {
        foreach (byte b in bytes)
        {
            bool ok = (b >= '0' && b <= '9') || (b >= 'a' && b <= 'f') || (b >= 'A' && b <= 'F');
            if (!ok) return false;
        }
        return true;
    }
}
