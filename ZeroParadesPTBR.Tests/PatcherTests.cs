using System.Buffers.Binary;
using System.Text;
using Xunit;

namespace ZeroParadesPTBR.Tests;

public class PatcherTests
{
    [Fact]
    public void PoParser_ParsesMultilineAndEscapes()
    {
        var poContent = """
            #
            msgid ""
            msgstr ""
            "Content-Type: text/plain; charset=UTF-8\n"

            msgctxt "ui/hello"
            msgid "Hola\n\"mundo\""
            msgstr ""
            "Olá\n"
            "\"mundo\""

            msgctxt "ui/untranslated"
            msgid "Pendiente"
            msgstr ""
            """;

        var tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tmp, poContent, Encoding.UTF8);
            var result = PoParser.Load(tmp);

            Assert.Equal(2, result.TotalEntries);
            Assert.Equal(1, result.UntranslatedEntries);
            Assert.Equal("Olá\n\"mundo\"", result.Translations["ui/hello"]);
            Assert.Equal("Pendiente", result.Translations["ui/untranslated"]);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void CatalogPatcher_ZerosCrcAndUpdatesBundleSize()
    {
        var prefix = "\x00\x01\x02g5ibkj7vdwf2g67g_assets_all_9eb206f8e0ca7a9202649e6666b53149.bundle"u8.ToArray();
        var hashLen = new byte[4];
        BinaryPrimitives.WriteUInt32LittleEndian(hashLen, 32);
        var hashVal = "5e9cd0a1dc9c5206914ce41a2e2eb192"u8.ToArray();
        uint hashOff = (uint)(prefix.Length + hashLen.Length);

        var tail = new byte[18];
        BinaryPrimitives.WriteUInt32LittleEndian(tail.AsSpan(0, 4), 0x00011EC8);
        BinaryPrimitives.WriteUInt32LittleEndian(tail.AsSpan(4, 4), hashOff);
        BinaryPrimitives.WriteUInt32LittleEndian(tail.AsSpan(8, 4), 0x49F3C2AB);
        BinaryPrimitives.WriteUInt32LittleEndian(tail.AsSpan(12, 4), 29417910);

        byte[] raw = [.. prefix, .. hashLen, .. hashVal, .. tail];

        var tmpDir = Directory.CreateTempSubdirectory();
        try
        {
            var catPath = Path.Combine(tmpDir.FullName, "catalog.bin");
            File.WriteAllBytes(catPath, raw);

            var (records, sizeUpdated) = CatalogPatcher.PatchCatalogBin(
                catPath,
                "g5ibkj7vdwf2g67g_assets_all_9eb206f8e0ca7a9202649e6666b53149.bundle",
                29633176
            );

            Assert.Equal(1, records);
            Assert.True(sizeUpdated);
            Assert.Equal(raw, File.ReadAllBytes(catPath + ".bak"));

            var patched = File.ReadAllBytes(catPath);
            uint ptr = BinaryPrimitives.ReadUInt32LittleEndian(patched.AsSpan((int)hashOff + 36, 4));
            uint crc = BinaryPrimitives.ReadUInt32LittleEndian(patched.AsSpan((int)hashOff + 40, 4));
            uint size = BinaryPrimitives.ReadUInt32LittleEndian(patched.AsSpan((int)hashOff + 44, 4));

            Assert.Equal(hashOff, ptr);
            Assert.Equal(0u, crc);
            Assert.Equal(29633176u, size);
        }
        finally
        {
            tmpDir.Delete(recursive: true);
        }
    }

    [Fact]
    public void BundlePatcher_PatchesRealDirectorCutBundleInTempDir()
    {
        const string gameAa = @"E:\Arquivos de programas (x86)\Steam\steamapps\common\Zero Parades\ZeroParades_Data\StreamingAssets\aa";
        var srcBundle = Path.Combine(gameAa, "StandaloneWindows64", "g5ibkj7vdwf2g67g_assets_all_9eb206f8e0ca7a9202649e6666b53149.bundle.bak");
        var srcCatalog = Path.Combine(gameAa, "catalog.bin.bak");
        var poPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "release_ptbr", "my_translation.po"));

        if (!File.Exists(srcBundle) || !File.Exists(srcCatalog) || !File.Exists(poPath))
            return;

        var tmpDir = Directory.CreateTempSubdirectory();
        try
        {
            var win64 = Directory.CreateDirectory(Path.Combine(tmpDir.FullName, "StandaloneWindows64"));
            var dstBundle = Path.Combine(win64.FullName, "g5ibkj7vdwf2g67g_assets_all_9eb206f8e0ca7a9202649e6666b53149.bundle");
            var dstCatalog = Path.Combine(tmpDir.FullName, "catalog.bin");
            File.Copy(srcBundle, dstBundle);
            File.Copy(srcCatalog, dstCatalog);

            var summary = BundlePatcher.ApplyPatch(dstBundle, poPath);
            Assert.Equal(73708, summary.TotalKeys);
            Assert.Equal(0, summary.MissingKeys);
            Assert.True(File.Exists(dstBundle + ".bak"));
            Assert.True(File.Exists(dstCatalog + ".bak"));
        }
        finally
        {
            tmpDir.Delete(recursive: true);
        }
    }
}
