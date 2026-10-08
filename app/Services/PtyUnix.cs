using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace GRepos.Services;

/// <summary>
/// Processo rodando num pseudoterminal do Linux — o mesmo mecanismo de qualquer emulador
/// de terminal. Nós ficamos com a ponta mestre; o processo, com a escrava como terminal
/// de controle.
///
/// Não há <c>fork</c> aqui: bifurcar um processo .NET e seguir rodando código gerenciado
/// no filho é pedir problema. Quem cria o processo é o <see cref="Process"/> de sempre, e
/// um <c>sh</c> de uma linha liga a entrada e a saída na ponta escrava antes de o
/// <c>setsid</c> (util-linux) abrir a sessão nova e fazer dela o terminal de controle.
/// </summary>
public sealed class PtyUnix : IPseudoTerminal
{
    /// <summary>
    /// $1 é a ponta escrava; o resto, o comando. A escrava é aberta uma vez só e fica
    /// aberta até o fim: se ela fechasse no meio (um <c>stty &lt;"$pts"</c> avulso, por
    /// exemplo), o kernel avisaria a ponta mestre de que o terminal acabou, e a leitura
    /// terminaria antes de o shell nascer. O <c>-w</c> só importa se o setsid precisar
    /// bifurcar: sem ele o processo que observamos sairia na hora.
    /// </summary>
    private const string Lancador =
        "pts=$1; shift; exec <>\"$pts\" >&0 2>&0; stty iutf8 2>/dev/null; exec setsid -c -w \"$@\"";

    private readonly SafeFileHandle _mestre;
    private readonly Process _processo;
    private volatile bool _encerrado;

    public Stream Saida { get; }

    public event Action? Encerrou;

    private PtyUnix(SafeFileHandle mestre, Process processo)
    {
        _mestre = mestre;
        _processo = processo;
        Saida = new Leitura(mestre);

        processo.EnableRaisingEvents = true;
        processo.Exited += (_, _) =>
        {
            _encerrado = true;
            Encerrou?.Invoke();
        };
        // saiu antes de o evento ser ligado: o Exited ainda dispara, mas só com isto lido
        if (processo.HasExited) _encerrado = true;
    }

    public static PtyUnix Iniciar(string executavel, IReadOnlyList<string> argumentos, string pasta,
                                  int colunas, int linhas, IDictionary<string, string?>? ambiente = null)
    {
        // O_CLOEXEC: sem ele o filho herdaria a ponta mestre e o fim da saída nunca chegaria
        var fd = posix_openpt(O_RDWR | O_NOCTTY | O_CLOEXEC);
        if (fd < 0)
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Não foi possível criar o pseudoterminal");

        var mestre = new SafeFileHandle((IntPtr)fd, ownsHandle: true);
        try
        {
            if (grantpt(mestre) != 0 || unlockpt(mestre) != 0)
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Não foi possível liberar o pseudoterminal");

            var nome = new byte[256];
            if (ptsname_r(mestre, nome, (nuint)nome.Length) != 0)
                throw new Win32Exception(Marshal.GetLastPInvokeError(), "Não foi possível localizar o pseudoterminal");
            var escravo = System.Text.Encoding.UTF8.GetString(nome, 0, Array.IndexOf(nome, (byte)0));

            DefinirTamanho(mestre, colunas, linhas);

            var psi = new ProcessStartInfo("/bin/sh")
            {
                WorkingDirectory = pasta,
                UseShellExecute = false,
            };
            psi.ArgumentList.Add("-c");
            psi.ArgumentList.Add(Lancador);
            psi.ArgumentList.Add("grepos-pty");
            psi.ArgumentList.Add(escravo);
            psi.ArgumentList.Add(executavel);
            foreach (var a in argumentos) psi.ArgumentList.Add(a);

            if (ambiente is not null)
                foreach (var (variavel, valor) in ambiente)
                    if (valor is null) psi.Environment.Remove(variavel);
                    else psi.Environment[variavel] = valor;

            var processo = Process.Start(psi)
                           ?? throw new InvalidOperationException("Não foi possível iniciar " + executavel);
            return new PtyUnix(mestre, processo);
        }
        catch
        {
            mestre.Dispose();
            throw;
        }
    }

    public void Escrever(byte[] dados)
    {
        if (_encerrado) return;
        try
        {
            var enviado = 0;
            while (enviado < dados.Length)
            {
                var n = write(_mestre, ref dados[enviado], (nuint)(dados.Length - enviado));
                if (n > 0) enviado += (int)n;
                else if (Marshal.GetLastPInvokeError() != EINTR) return;
            }
        }
        catch (ObjectDisposedException)
        {
            // a sessão foi descartada entre a tecla e a escrita: nada a fazer
        }
    }

    public void Redimensionar(int colunas, int linhas)
    {
        if (colunas <= 0 || linhas <= 0) return;
        try { DefinirTamanho(_mestre, colunas, linhas); }
        catch (ObjectDisposedException) { }
    }

    public void Dispose()
    {
        // a leitura pendente segura a ponta mestre aberta até voltar, e ela só volta
        // quando ninguém mais tem a escrava: por isso derrubar a árvore toda, não só o shell
        if (!_encerrado)
        {
            try { _processo.Kill(entireProcessTree: true); }
            catch (Exception) { /* já saiu */ }
        }

        _mestre.Dispose();
        _processo.Dispose();
    }

    private static void DefinirTamanho(SafeFileHandle mestre, int colunas, int linhas)
    {
        var tamanho = new WinSize
        {
            Linhas = (ushort)Math.Clamp(linhas, 1, ushort.MaxValue),
            Colunas = (ushort)Math.Clamp(colunas, 1, ushort.MaxValue),
        };
        ioctl(mestre, TIOCSWINSZ, ref tamanho);
    }

    /// <summary>Só leitura, direto no descritor: fim da saída é 0, como num arquivo.</summary>
    private sealed class Leitura : Stream
    {
        private readonly SafeFileHandle _fd;

        public Leitura(SafeFileHandle fd) => _fd = fd;

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (count == 0) return 0;
            while (true)
            {
                var n = read(_fd, ref buffer[offset], (nuint)count);
                if (n >= 0) return (int)n;
                // EIO é como o kernel avisa que a ponta escrava fechou: o processo saiu
                if (Marshal.GetLastPInvokeError() != EINTR) return 0;
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    // ------------------------------------------------------------------- libc

    private const int O_RDWR = 0x2;
    private const int O_NOCTTY = 0x100;
    private const int O_CLOEXEC = 0x80000;
    private const int EINTR = 4;
    private const nuint TIOCSWINSZ = 0x5414;

    [StructLayout(LayoutKind.Sequential)]
    private struct WinSize
    {
        public ushort Linhas, Colunas, LarguraPx, AlturaPx;
    }

    [DllImport("libc", SetLastError = true)]
    private static extern int posix_openpt(int flags);

    [DllImport("libc", SetLastError = true)]
    private static extern int grantpt(SafeFileHandle fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int unlockpt(SafeFileHandle fd);

    [DllImport("libc", SetLastError = true)]
    private static extern int ptsname_r(SafeFileHandle fd, byte[] nome, nuint tamanho);

    [DllImport("libc", SetLastError = true)]
    private static extern int ioctl(SafeFileHandle fd, nuint pedido, ref WinSize tamanho);

    [DllImport("libc", SetLastError = true)]
    private static extern nint read(SafeFileHandle fd, ref byte buffer, nuint tamanho);

    [DllImport("libc", SetLastError = true)]
    private static extern nint write(SafeFileHandle fd, ref byte buffer, nuint tamanho);
}
