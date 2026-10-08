using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GRepos.Services;

/// <summary>
/// O que muda de um sistema para o outro e não é de nenhum serviço em particular: os
/// nomes que aparecem na tela e a busca de um comando no PATH.
/// </summary>
public static class Plataforma
{
    public static bool Windows => OperatingSystem.IsWindows();

    // ------------------------------------------------------------- textos

    public static string DicaAbrirPasta =>
        Windows ? "Abrir a pasta no Windows" : "Abrir a pasta no gerenciador de arquivos";

    public static string DicaPasta =>
        Windows ? "Abre a pasta no Explorer" : "Abre a pasta no gerenciador de arquivos";

    public static string DicaPastaDoRepositorio => Windows
        ? "Abre a pasta do repositório no Explorer"
        : "Abre a pasta do repositório no gerenciador de arquivos";

    public static string DicaTerminalEmbutido => Windows
        ? "Mostrar ou esconder o Git Bash embutido, na pasta do repositório"
        : "Mostrar ou esconder o terminal embutido, na pasta do repositório";

    public static string DicaTerminalNaPasta =>
        Windows ? "Abre o Git Bash na pasta do repositório" : "Abre o terminal na pasta do repositório";

    public static string DicaReiniciarTerminal => Windows
        ? "Encerra este Git Bash e abre outro na pasta do repositório"
        : "Encerra este shell e abre outro na pasta do repositório";

    public static string DicaTerminalEmJanela =>
        Windows ? "Abre o Git Bash numa janela separada" : "Abre o terminal do sistema numa janela separada";

    public static string DicaFecharTerminal => Windows
        ? "Encerra o Git Bash deste repositório e fecha o painel"
        : "Encerra o shell deste repositório e fecha o painel";

    /// <summary>Como chamar o terminal no meio de uma frase.</summary>
    public static string Terminal => Windows ? "Git Bash" : "Terminal";

    /// <summary>Onde o token fica, para completar "guardado …".</summary>
    public static string OndeFicaOToken => Windows ? "no Windows" : "pelo gerenciador de credenciais do git";

    public static string OutroUsuario => Windows ? "outro usuário do Windows" : "outro usuário do sistema";

    public static string ExemploDePasta => Windows ? @"D:\Projetos\..." : "~/Projetos/...";

    // --------------------------------------------------------------- PATH

    /// <summary>
    /// Caminho completo de um comando do PATH, ou null. <paramref name="existe"/> e
    /// <paramref name="path"/> só são trocados nos testes.
    /// </summary>
    public static string? NoPath(string comando, Func<string, bool>? existe = null, string? path = null)
    {
        existe ??= File.Exists;
        return PastasDoPath(path).Select(p => Path.Combine(p, comando)).FirstOrDefault(existe);
    }

    public static IEnumerable<string> PastasDoPath(string? path = null) =>
        (path ?? Environment.GetEnvironmentVariable("PATH") ?? "")
        .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
