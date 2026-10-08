using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace GRepos.Services;

/// <summary>
/// O lado Linux do botão Terminal: o shell do usuário para o terminal embutido e o
/// emulador de terminal instalado para a janela separada. No Windows os dois saem da
/// instalação do Git (ver <see cref="GitBash"/>).
/// </summary>
public static class TerminalLinux
{
    /// <summary>
    /// Emuladores conhecidos e como cada um recebe a pasta inicial. Os que vivem num
    /// processo servidor (gnome-terminal, ptyxis, konsole) ignoram o diretório de
    /// trabalho de quem os chamou, por isso o argumento explícito.
    /// </summary>
    private static readonly (string Exe, string[] Argumentos)[] Emuladores =
    {
        ("ptyxis", new[] { "--new-window", "-d", "$DIR" }),
        ("gnome-terminal", new[] { "--working-directory=$DIR" }),
        ("kgx", new[] { "--working-directory=$DIR" }),
        ("konsole", new[] { "--workdir", "$DIR" }),
        ("xfce4-terminal", new[] { "--working-directory=$DIR" }),
        ("mate-terminal", new[] { "--working-directory=$DIR" }),
        ("tilix", new[] { "--working-directory=$DIR" }),
        ("terminator", new[] { "--working-directory=$DIR" }),
        ("lxterminal", new[] { "--working-directory=$DIR" }),
        ("qterminal", new[] { "-w", "$DIR" }),
        ("kitty", new[] { "--directory", "$DIR" }),
        ("alacritty", new[] { "--working-directory", "$DIR" }),
        ("wezterm", new[] { "start", "--cwd", "$DIR" }),
        ("ghostty", new[] { "--working-directory=$DIR" }),
        ("foot", new[] { "--working-directory=$DIR" }),
        ("x-terminal-emulator", Array.Empty<string>()),
        ("xterm", Array.Empty<string>()),
    };

    /// <summary>O emulador da casa de cada ambiente vem antes dos outros instalados.</summary>
    private static readonly (string Ambiente, string Exe)[] PorAmbiente =
    {
        ("KDE", "konsole"),
        ("XFCE", "xfce4-terminal"),
        ("MATE", "mate-terminal"),
        ("LXQT", "qterminal"),
        ("LXDE", "lxterminal"),
    };

    /// <summary>
    /// Emulador a abrir, ou null se não achou. O configurado pode ser o caminho do
    /// executável ou só o nome do comando; vazio, vale o <c>$TERMINAL</c> e depois os
    /// conhecidos. <paramref name="existe"/> e <paramref name="path"/> só são trocados
    /// nos testes.
    /// </summary>
    public static string? Localizar(string? configurado,
                                    Func<string, bool>? existe = null,
                                    string? path = null,
                                    string? terminalDoAmbiente = null,
                                    string? ambienteGrafico = null)
    {
        existe ??= File.Exists;

        var conf = (configurado ?? "").Trim().Trim('"');
        if (conf.Length > 0)
            // caminho informado manda: se não achar nada nele, é erro de configuração,
            // e cair na busca automática esconderia isso do usuário
            return Resolver(conf, existe, path);

        var preferido = (terminalDoAmbiente ?? Environment.GetEnvironmentVariable("TERMINAL") ?? "").Trim();
        if (preferido.Length > 0 && Resolver(preferido, existe, path) is { } doAmbiente)
            return doAmbiente;

        var desktop = ambienteGrafico ?? Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "";
        var daCasa = PorAmbiente
            .Where(a => desktop.Contains(a.Ambiente, StringComparison.OrdinalIgnoreCase))
            .Select(a => a.Exe);

        return daCasa.Concat(Emuladores.Select(e => e.Exe))
                     .Select(exe => Plataforma.NoPath(exe, existe, path))
                     .FirstOrDefault(achou => achou is not null);
    }

    private static string? Resolver(string comando, Func<string, bool> existe, string? path) =>
        comando.Contains('/')
            ? existe(comando) ? comando : null
            : Plataforma.NoPath(comando, existe, path);

    /// <summary>O shell do usuário, para o terminal embutido.</summary>
    public static string? Shell(Func<string, bool>? existe = null, string? shellDoAmbiente = null)
    {
        existe ??= File.Exists;
        return new[] { shellDoAmbiente ?? Environment.GetEnvironmentVariable("SHELL") ?? "", "/bin/bash", "/bin/sh" }
            .FirstOrDefault(s => s.Length > 0 && existe(s));
    }

    /// <summary>Argumentos para o emulador abrir em <paramref name="pasta"/>.</summary>
    public static IEnumerable<string> Argumentos(string exe, string pasta)
    {
        var nome = Path.GetFileName(exe);
        var conhecido = Emuladores.FirstOrDefault(e => e.Exe == nome);
        return (conhecido.Argumentos ?? Array.Empty<string>()).Select(a => a.Replace("$DIR", pasta));
    }

    public static void Abrir(string exe, string pasta)
    {
        var psi = new ProcessStartInfo(exe)
        {
            // os emuladores sem argumento de pasta abrem no diretório de trabalho
            WorkingDirectory = pasta,
            UseShellExecute = false,
        };
        foreach (var a in Argumentos(exe, pasta)) psi.ArgumentList.Add(a);
        Process.Start(psi)?.Dispose();
    }
}
