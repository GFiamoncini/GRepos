using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GRepos.Services;

/// <summary>
/// Processo que acha que está num terminal de verdade: a gente lê e escreve o fluxo VT.
/// No Windows é o ConPTY; no Linux, um pty do kernel.
/// </summary>
public interface IPseudoTerminal : IDisposable
{
    /// <summary>Saída do processo, já com as sequências VT (UTF-8).</summary>
    Stream Saida { get; }

    /// <summary>Disparado (fora da thread de UI) quando o processo termina.</summary>
    event Action? Encerrou;

    void Escrever(byte[] dados);
    void Redimensionar(int colunas, int linhas);
}

public static class PseudoTerminal
{
    /// <param name="ambiente">Ajustes sobre o ambiente herdado; null remove a variável.</param>
    public static IPseudoTerminal Iniciar(string executavel, IReadOnlyList<string> argumentos, string pasta,
                                          int colunas, int linhas,
                                          IDictionary<string, string?>? ambiente = null)
    {
        if (!OperatingSystem.IsWindows())
            return PtyUnix.Iniciar(executavel, argumentos, pasta, colunas, linhas, ambiente);

        var linhaDeComando = string.Join(" ", new[] { $"\"{executavel}\"" }.Concat(argumentos));
        return ConPty.Iniciar(linhaDeComando, pasta, colunas, linhas, ambiente);
    }
}
