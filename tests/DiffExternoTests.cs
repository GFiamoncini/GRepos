using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GRepos.Services;
using Xunit;

namespace GRepos.Tests;

/// <summary>
/// Ferramenta de diff externa: achar o executável, montar a linha de comando e entregar
/// as duas versões. A "ferramenta" dos testes é um .cmd (um script de shell no Linux)
/// que anota o que recebeu.
/// </summary>
public class DiffExternoTests
{
    private const string Raiz = @"C:\Program Files";
    private static Func<string, bool> Existem(params string[] arquivos) =>
        p => arquivos.Contains(p, StringComparer.OrdinalIgnoreCase);

    [FatoWindows]
    public void Pasta_configurada_acha_a_ferramenta_conhecida()
    {
        var exe = @"D:\Ferramentas\WinMerge\WinMergeU.exe";
        Assert.Equal(exe, DiffExterno.Localizar(@"D:\Ferramentas\WinMerge\", Existem(exe), Array.Empty<string>()));
        Assert.Equal(exe, DiffExterno.Localizar("\"" + exe + "\"", Existem(exe), Array.Empty<string>()));
    }

    [FatoWindows]
    public void Caminho_errado_nao_cai_na_busca_automatica()
    {
        var instalado = Raiz + @"\Beyond Compare 5\BCompare.exe";
        Assert.Null(DiffExterno.Localizar(@"D:\nada", Existem(instalado), new[] { Raiz }));
        Assert.Null(DiffExterno.Localizar(@"D:\nada\outro.exe", Existem(instalado), new[] { Raiz }));
    }

    [FatoWindows]
    public void Vazio_procura_nas_pastas_dos_instaladores()
    {
        var instalado = Raiz + @"\Beyond Compare 4\BCompare.exe";
        Assert.Equal(instalado, DiffExterno.Localizar("", Existem(instalado), new[] { Raiz }));
        Assert.Null(DiffExterno.Localizar("", Existem(), new[] { Raiz }));
    }

    [FatoWindows]
    public void Executavel_desconhecido_vale_com_os_argumentos_genericos()
    {
        var exe = @"D:\x\meudiff.exe";
        Assert.Equal(exe, DiffExterno.Localizar(exe, Existem(exe), Array.Empty<string>()));
        Assert.Equal("\"a b.pas\" \"c.pas\"", DiffExterno.Argumentos(exe, "", "a b.pas", "c.pas", "E", "D"));
    }

    [FatoLinux]
    public void No_linux_vale_o_comando_o_caminho_ou_o_que_o_path_tiver()
    {
        var raizes = new[] { "/usr/local/bin", "/usr/bin" };
        Func<string, bool> existe = p => p is "/usr/bin/meld" or "/usr/bin/kdiff3" or "/opt/bc/bcompare";

        // vazio: a primeira conhecida que o PATH tiver, na ordem de preferência
        Assert.Equal("/usr/bin/meld", DiffExterno.Localizar("", existe, raizes));
        Assert.Null(DiffExterno.Localizar("", _ => false, raizes));

        Assert.Equal("/usr/bin/kdiff3", DiffExterno.Localizar("kdiff3", existe, raizes));
        Assert.Equal("/opt/bc/bcompare", DiffExterno.Localizar("/opt/bc/bcompare", existe, raizes));
        Assert.Equal("/opt/bc/bcompare", DiffExterno.Localizar("/opt/bc/", existe, raizes));

        // apontou para o que não existe: avisar, não cair na busca automática
        Assert.Null(DiffExterno.Localizar("bcompare", existe, raizes));
        Assert.Null(DiffExterno.Localizar("/opt/nada", existe, raizes));
    }

    [FatoLinux]
    public void No_linux_os_argumentos_seguem_a_ferramenta()
    {
        Assert.Equal("-L \"a.pas (HEAD)\" -L \"a.pas (disco)\" \"l\" \"r\"",
            DiffExterno.Argumentos("/usr/bin/meld", null, "l", "r", "a.pas (HEAD)", "a.pas (disco)"));
        Assert.Equal("\"l\" \"r\" -lefttitle=\"E\" -righttitle=\"D\"",
            DiffExterno.Argumentos("/usr/bin/bcompare", "", "l", "r", "E", "D"));
        Assert.Equal("\"l\" \"r\"", DiffExterno.Argumentos("/usr/bin/meu-diff", null, "l", "r", "E", "D"));
        Assert.Equal("--wait --diff l r",
            DiffExterno.Argumentos("/usr/bin/meld", " --wait --diff $LOCAL $REMOTE ", "l", "r", "E", "D"));
    }

    [FatoWindows]
    public void Argumentos_padrao_seguem_a_ferramenta_e_o_modelo_do_usuario_manda()
    {
        Assert.Equal("\"l\" \"r\" /lefttitle=\"a.pas (HEAD)\" /righttitle=\"a.pas (disco)\"",
            DiffExterno.Argumentos(@"C:\bc\BCOMPARE.EXE", null, "l", "r", "a.pas (HEAD)", "a.pas (disco)"));
        Assert.Equal("--wait --diff l r",
            DiffExterno.Argumentos(@"C:\bc\BCompare.exe", " --wait --diff $LOCAL $REMOTE ", "l", "r", "E", "D"));
        // aspa no título fecharia o argumento antes da hora
        Assert.Equal("/dl \"um 'dois'\"", DiffExterno.Argumentos("x.exe", "/dl \"$LTITLE\"", "l", "r", "um \"dois\"", "D"));
    }

    [Theory]
    [InlineData("src/Unit1.pas", "HEAD", "Unit1.HEAD.pas")]
    [InlineData("src/Unit1.pas", "índice", "Unit1.índice.pas")]
    [InlineData("Makefile", "antes-de-abc1234", "Makefile.antes-de-abc1234")]
    [InlineData("a.txt", "", "a.versao.txt")]
    public void Temporario_mantem_a_extensao_e_leva_o_rotulo(string caminho, string rotulo, string esperado) =>
        Assert.Equal(esperado, DiffExterno.NomeTemporario(caminho, rotulo));

    // --------------------------------------------------------- repositório real

    private static Task<string> Git(string dir, params string[] args) => GitService.RunAsync(dir, args);

    private static void Limpar(string dir)
    {
        try
        {
            foreach (var f in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(dir, true);
        }
        catch (Exception) { /* pasta temporária */ }
    }

    private static async Task<string> NovoRepo()
    {
        var dir = Path.Combine(Path.GetTempPath(), "grepos-dif-" + Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        await Git(dir, "init", "-q", "-b", "main");
        await Git(dir, "config", "user.email", "t@t");
        await Git(dir, "config", "user.name", "Teste");
        await Git(dir, "config", "core.autocrlf", "true");
        return dir;
    }

    [Fact]
    public async Task Conteudo_bruto_vem_como_o_checkout_gravaria_e_null_se_nao_existe()
    {
        var dir = await NovoRepo();
        try
        {
            // ANSI de propósito: os fontes Delphi não são UTF-8, e os bytes têm de chegar iguais
            var bytes = Encoding.Latin1.GetBytes("ação\r\nfim\r\n");
            File.WriteAllBytes(Path.Combine(dir, "a.pas"), bytes);
            await Git(dir, "add", ".");
            await Git(dir, "commit", "-qm", "inicial");

            // no repositório fica LF; a comparação com o disco precisa do CRLF de volta
            Assert.Equal(bytes, await GitService.ConteudoBrutoAsync(dir, "HEAD:a.pas"));
            Assert.Equal(bytes, await GitService.ConteudoBrutoAsync(dir, ":a.pas"));
            Assert.Null(await GitService.ConteudoBrutoAsync(dir, "HEAD:nao-existe.pas"));
            Assert.Null(await GitService.ConteudoBrutoAsync(dir, "HEAD^:a.pas"));
        }
        finally { Limpar(dir); }
    }

    [Fact]
    public async Task Abrir_entrega_a_copia_do_commit_e_o_arquivo_real_do_disco()
    {
        var dir = await NovoRepo();
        var ferramenta = Path.Combine(dir, "..", Path.GetFileName(dir) + "-tool.cmd");
        var recebido = ferramenta + ".txt";
        try
        {
            File.WriteAllText(Path.Combine(dir, "a b.txt"), "um\r\n");
            await Git(dir, "add", ".");
            await Git(dir, "commit", "-qm", "inicial");
            File.WriteAllText(Path.Combine(dir, "a b.txt"), "dois\r\n");

            if (OperatingSystem.IsWindows())
                File.WriteAllText(ferramenta, "@echo off\r\n(echo %~1\r\necho %~2)> \"" + recebido + "\"\r\n");
            else
            {
                File.WriteAllText(ferramenta, "#!/bin/sh\nprintf '%s\\n' \"$1\" \"$2\" > '" + recebido + "'\n");
                File.SetUnixFileMode(ferramenta, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            }

            await DiffExterno.AbrirAsync(dir,
                VersaoDeArquivo.Em("HEAD", "a b.txt", "HEAD"), VersaoDeArquivo.NoDisco("a b.txt"),
                Path.GetFullPath(ferramenta), "\"$LOCAL\" \"$REMOTE\"");

            for (var i = 0; i < 100 && !File.Exists(recebido); i++) await Task.Delay(50);
            await Task.Delay(100);
            var linhas = File.ReadAllLines(recebido);

            Assert.EndsWith("a b.HEAD.txt", linhas[0]);
            Assert.Equal("um\r\n", File.ReadAllText(linhas[0]));
            Assert.True(File.GetAttributes(linhas[0]).HasFlag(FileAttributes.ReadOnly));
            // o lado do disco é o próprio arquivo: salvar na ferramenta altera o repositório
            Assert.Equal(Path.GetFullPath(Path.Combine(dir, "a b.txt")), linhas[1]);
        }
        finally
        {
            Limpar(dir);
            try { File.Delete(ferramenta); File.Delete(recebido); } catch (Exception) { /* temporário */ }
        }
    }

    [Fact]
    public async Task Sem_ferramenta_a_mensagem_aponta_para_as_preferencias()
    {
        var erro = await Assert.ThrowsAsync<FileNotFoundException>(() => DiffExterno.AbrirAsync(
            ".", VersaoDeArquivo.NoIndice("a.txt"), VersaoDeArquivo.NoDisco("a.txt"), @"Z:\nao\existe.exe", ""));
        Assert.Contains("Preferências", erro.Message);
    }
}
