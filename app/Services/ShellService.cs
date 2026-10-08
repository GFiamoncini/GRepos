using System;
using System.Diagnostics;
using System.IO;

namespace GRepos.Services;

/// <summary>Abre endereços e pastas no sistema — navegador padrão e gerenciador de arquivos.</summary>
public static class ShellService
{
    public static void AbrirUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;

        // só http(s): abrir qualquer esquema com ShellExecute é pedir problema
        if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
            !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Endereço inválido: " + url);

        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    public static void AbrirPasta(string caminho)
    {
        if (!Directory.Exists(caminho))
            throw new DirectoryNotFoundException("Pasta não encontrada: " + caminho);

        var cheia = Path.GetFullPath(caminho);
        if (!OperatingSystem.IsWindows())
        {
            // o .NET entrega ao xdg-open, que abre o gerenciador de arquivos do ambiente
            Process.Start(new ProcessStartInfo(cheia) { UseShellExecute = true })?.Dispose();
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{cheia}\"")
        {
            UseShellExecute = true,
        });
    }
}
