using System.Collections.Generic;
using System.Linq;
using GRepos.Services;
using Xunit;

namespace GRepos.Tests;

/// <summary>
/// O lado Linux do botão Terminal: achar o emulador e o shell. A busca recebe o PATH e o
/// "existe" de fora, então roda igual em qualquer sistema.
/// </summary>
public class TerminalLinuxTests
{
    private const string Path = "/usr/local/bin:/usr/bin";

    private static System.Func<string, bool> Existem(params string[] arquivos)
    {
        var set = new HashSet<string>(arquivos);
        return set.Contains;
    }

    [FatoLinux]
    public void VazioUsaOEmuladorDaCasaDoAmbiente()
    {
        var existe = Existem("/usr/bin/gnome-terminal", "/usr/bin/konsole", "/usr/bin/xterm");
        Assert.Equal("/usr/bin/konsole", TerminalLinux.Localizar("", existe, Path, "", "KDE"));
        Assert.Equal("/usr/bin/gnome-terminal", TerminalLinux.Localizar("", existe, Path, "", "GNOME"));
        Assert.Null(TerminalLinux.Localizar("", Existem(), Path, "", "GNOME"));
    }

    [FatoLinux]
    public void VariavelTerminalVemAntesDosConhecidos()
    {
        var existe = Existem("/usr/bin/gnome-terminal", "/usr/local/bin/kitty");
        Assert.Equal("/usr/local/bin/kitty", TerminalLinux.Localizar("", existe, Path, "kitty", "GNOME"));
        // variável apontando para o que não existe não impede a busca
        Assert.Equal("/usr/bin/gnome-terminal", TerminalLinux.Localizar("", existe, Path, "sumiu", "GNOME"));
    }

    [FatoLinux]
    public void ConfiguradoValeComoComandoOuCaminho()
    {
        var existe = Existem("/usr/bin/gnome-terminal", "/opt/wez/wezterm", "/usr/bin/alacritty");
        Assert.Equal("/usr/bin/alacritty", TerminalLinux.Localizar(" alacritty ", existe, Path, "", ""));
        Assert.Equal("/opt/wez/wezterm", TerminalLinux.Localizar("/opt/wez/wezterm", existe, Path, "", ""));
    }

    [FatoLinux]
    public void ConfiguradoErradoNaoCaiNaBuscaAutomatica()
    {
        // há um terminal instalado, mas o usuário apontou para outro: avisar, não disfarçar
        var existe = Existem("/usr/bin/gnome-terminal");
        Assert.Null(TerminalLinux.Localizar("konsole", existe, Path, "", "GNOME"));
        Assert.Null(TerminalLinux.Localizar("/opt/nada/kitty", existe, Path, "", "GNOME"));
    }

    [Fact]
    public void CadaEmuladorRecebeAPastaDoJeitoDele()
    {
        const string pasta = "/home/eu/Meus Projetos/fin";
        Assert.Equal(new[] { "--working-directory=" + pasta },
            TerminalLinux.Argumentos("/usr/bin/gnome-terminal", pasta).ToArray());
        Assert.Equal(new[] { "--workdir", pasta }, TerminalLinux.Argumentos("/usr/bin/konsole", pasta).ToArray());
        Assert.Equal(new[] { "start", "--cwd", pasta }, TerminalLinux.Argumentos("/opt/wez/wezterm", pasta).ToArray());
        // desconhecido: sem argumento, abre no diretório de trabalho
        Assert.Empty(TerminalLinux.Argumentos("/usr/bin/meu-terminal", pasta));
    }

    [Fact]
    public void ShellEhODoUsuarioESoCaiNoBashSeEleSumiu()
    {
        Assert.Equal("/usr/bin/zsh", TerminalLinux.Shell(Existem("/usr/bin/zsh", "/bin/bash"), "/usr/bin/zsh"));
        Assert.Equal("/bin/bash", TerminalLinux.Shell(Existem("/bin/bash", "/bin/sh"), "/usr/bin/sumiu"));
        Assert.Equal("/bin/sh", TerminalLinux.Shell(Existem("/bin/sh"), ""));
        Assert.Null(TerminalLinux.Shell(Existem(), ""));
    }

    [Fact]
    public void HelperDeCredenciaisPrefereOQueGuardaComSeguranca()
    {
        Assert.Equal("manager", GitHubService.HelperDoLinux(
            Existem("/usr/bin/git-credential-manager", "/usr/libexec/git-core/git-credential-libsecret"), Path));
        Assert.Equal("libsecret", GitHubService.HelperDoLinux(
            Existem("/usr/libexec/git-core/git-credential-libsecret"), Path));
        // sem nenhum dos dois sobra o arquivo em texto puro, e o botão diz isso
        Assert.Equal("store", GitHubService.HelperDoLinux(Existem(), Path));
        Assert.Contains("texto puro", GitHubService.RotuloDoHelper("store", windows: false));
        Assert.Contains("Windows", GitHubService.RotuloDoHelper("manager", windows: true));
    }
}
