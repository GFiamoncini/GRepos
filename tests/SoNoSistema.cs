using System;
using Xunit;

namespace GRepos.Tests;

// Testes do que só existe num sistema (caminhos com letra de unidade, Git Bash, emulador
// de terminal): no outro aparecem como ignorados em vez de passar sem testar nada.

public sealed class FatoWindowsAttribute : FactAttribute
{
    public FatoWindowsAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "só no Windows";
    }
}

public sealed class TeoriaWindowsAttribute : TheoryAttribute
{
    public TeoriaWindowsAttribute()
    {
        if (!OperatingSystem.IsWindows()) Skip = "só no Windows";
    }
}

public sealed class FatoLinuxAttribute : FactAttribute
{
    public FatoLinuxAttribute()
    {
        if (!OperatingSystem.IsLinux()) Skip = "só no Linux";
    }
}
