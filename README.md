# GRepos

[![build](https://github.com/GFiamoncini/GRepos/actions/workflows/build.yml/badge.svg)](https://github.com/GFiamoncini/GRepos/actions/workflows/build.yml)

Cliente Git desktop para quem trabalha com **muitos repositórios ao mesmo tempo** —
substituto do SourceTree com foco em organização: a sidebar mostra todos os repositórios
agrupados, já com status (ahead/behind/sujo/conflito) de todos de uma vez, sem abrir uma
aba por repositório.

Traz o conceito de **par Origem × Destino**: dois repositórios do mesmo módulo em bancos
diferentes (por exemplo DBISAM e MySQL) ficam sob um único título na árvore e ganham uma aba
que mostra os dois lado a lado, com Fetch/Pull nos dois de uma vez.

Aplicação nativa em C# / .NET 8 com Avalonia, para **Windows e Linux**. Não precisa de
Node, Rust nem Build Tools: o SDK do .NET resolve tudo.

## Download

A cada versão marcada, o [GitHub Actions](.github/workflows/build.yml) publica quatro
executáveis na [página de releases](https://github.com/GFiamoncini/GRepos/releases), cada um
**um arquivo só**:

| Arquivo | Tamanho | Exige |
| --- | --- | --- |
| `GRepos-<versão>.exe` | ~25 MB | [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) instalado |
| `GRepos-<versão>-standalone.exe` | ~89 MB | nada — carrega o próprio .NET |
| `GRepos-<versão>-linux-x64` | ~25 MB | Linux, com o [.NET 8 Runtime](https://dotnet.microsoft.com/download/dotnet/8.0) instalado |
| `GRepos-<versão>-linux-x64-standalone` | ~87 MB | Linux — carrega o próprio .NET |

Push na `main` gera os mesmos quatro como artefato da execução, que expira em 30 dias.

No Linux o download chega sem permissão de execução: `chmod +x GRepos-*-linux-x64*` e
abra. Para ganhar atalho no menu, `packaging/linux/instalar.sh <arquivo baixado>` copia o
executável para `~/.local/bin/grepos` e registra o ícone — sem root. O app usa o `git` do
sistema e, no terminal embutido, o `setsid` do util-linux, que toda distribuição já traz.

**Atualização automática**: rodando o executável de arquivo único, o app consulta a última
release uma vez por dia e, se houver versão maior, acende um aviso na barra de status. Um
clique baixa o standalone do sistema em uso, confirma antes de aplicar, troca o executável e reabre —
o anterior é guardado como cópia e volta sozinho se a troca falhar. Dá para desligar o
aviso em *Preferências → Customização*.

## Comandos

```bash
dotnet run --project app      # abre o aplicativo
dotnet build                  # compila tudo
dotnet test                   # xunit: parser, grafo, workspace e as telas headless
dotnet publish app -c Release -r win-x64 --self-contained false -o dist   # gera dist/GRepos.exe
dotnet publish app -c Release -r linux-x64 --self-contained false -o dist # gera dist/GRepos
```

## No Linux

As funções são as mesmas; muda o que cada sistema tem de seu:

| | Windows | Linux |
| --- | --- | --- |
| Terminal embutido | `bash.exe` do Git for Windows, num ConPTY | o seu shell (`$SHELL`), num pty |
| Terminal em janela | Git Bash | o emulador instalado (`$TERMINAL`, o do ambiente gráfico ou o primeiro conhecido) — dá para fixar em *Preferências → Terminal* |
| Botão Local | Explorer | gerenciador de arquivos (`xdg-open`) |
| Diff externo | Beyond Compare, WinMerge, Meld… pela pasta de instalação | `bcompare`, `meld`, `kdiff3`, `code`… pelo PATH, ou o comando que você informar |
| Token do GitHub | Gerenciador de Credenciais do Windows | o helper do git: Git Credential Manager, senão o chaveiro (`libsecret`), senão `~/.git-credentials` |
| Workspace | `%APPDATA%\GRepos` | `~/.config/GRepos` |

## Como usar

1. **+ Repositório** — escolha a pasta; o nome vem do próprio repositório.
2. **+ Grupo** — crie grupos (por módulo, por cliente, por banco) e mova repositórios pelo
   botão ⚙ da barra superior. Grupos recolhem com um clique no título.
3. **Par Origem × Destino** — no ⚙ do repositório, preencha *Par* com a mesma chave nos dois
   repositórios (ex.: `Financeiro`) e marque o papel de cada um. Eles passam a aparecer sob
   um único título e a aba **Par** fica disponível.
4. **Alterações** — preparar/remover por arquivo ou **por bloco** (botão no cabeçalho de cada
   bloco do diff), descartar, commit, emendar.
5. **Histórico** — grafo de commits com raias coloridas, refs, detalhe do commit e diff por
   arquivo. Na barra, **Lado a lado** alterna entre diff dividido e unificado, e
   **Quebrar linha** quebra as linhas longas em vez de rolar na horizontal — útil em JSON
   e XML de uma linha só.
6. **Branches** — trocar, criar a partir do HEAD, ver o que rastreia o remoto e o que só
   existe aqui. O botão fica destacado quando você não está na `main`.
7. **Esteira** — quando o repositório tem GitHub Actions, mostra as últimas execuções em
   cartões e, para a escolhida, o passo a passo de cada job, com o passo que quebrou em
   destaque.

Preferências (⚙ da sidebar), separadas em **Customização** (tema claro/escuro, cor de
destaque, densidade das listas, aba inicial, intervalo de atualização automática, commits
carregados e os grupos) e **Autenticação** (usuário do GitHub, token e o gerenciador de
credenciais em uso).

## Onde ficam os dados

O workspace (grupos, repositórios, pares e preferências) fica em
`%APPDATA%\GRepos\workspace.json` — no Linux, `~/.config/GRepos/workspace.json`. Defina a variável de ambiente `GREPOS_HOME` para usar
outra pasta — útil para instalação portátil ou para testar sem mexer no workspace real.

## Credenciais

As operações de rede usam o `git` do sistema, que reaproveita o Git Credential Manager —
o GRepos não implementa autenticação própria.

O `workspace.json` guarda **apenas o nome de usuário** do GitHub; nenhum segredo é escrito
lá. O token informado em *Preferências → Autenticação* é entregue ao `git credential
approve`, ou seja, vai para onde o helper do git guarda — no Windows, o Gerenciador de
Credenciais, cifrado sob a sua conta. É a mesma proteção que o git, o VS Code e o GitHub
Desktop oferecem: protege contra outro usuário da máquina e contra quem leia o disco, mas
não contra um programa rodando como você — o próprio GRepos lê o token de volta com
`git credential fill`.

No Linux o git não vem com um gerenciador configurado. *Preferências → Autenticação*
mostra o que está em uso e oferece o melhor instalado: o Git Credential Manager, senão o
chaveiro do ambiente (`git-credential-libsecret`, pacote à parte na maioria das
distribuições), senão o `store` — que grava o token em `~/.git-credentials` **em texto
puro**, e o botão avisa disso antes de você escolher.

Uma exceção a conhecer: se você usar um **modelo de remoto com `{{token}}`** e aplicá-lo,
o `git remote set-url` grava a URL já expandida no `.git/config` daquele repositório, em
texto puro. Esse arquivo não é versionado (não vai para o GitHub), mas é a cópia menos
protegida — prefira um remoto comum e deixe a autenticação com o gerenciador.

## Arquitetura

```
app/
  Services/GitService.cs     chamadas ao git CLI e parsing (status v2, log, branches)
  Services/DiffParser.cs     parser de diff unificado e montagem de patch de bloco
  Services/GraphBuilder.cs   layout de raias do grafo de commits
  Services/WorkspaceStore.cs modelo persistido em JSON (gravação atômica)
  Services/GitHubService.cs  API do GitHub Actions e o token pelo credential manager
  ViewModels/                estado das telas (CommunityToolkit.Mvvm)
  Views/                     janela principal, abas e diálogos (Avalonia)
  Controls/GraphCell.cs      desenho das raias do grafo
tests/                       xunit: parser, grafo, workspace e montagem das telas
```

A camada Git usa o **git CLI** em vez de uma biblioteca: herda credenciais, SSH, hooks e
configuração do usuário sem reimplementar nada.

## Limitações conhecidas

- Sem merge, rebase interativo, cherry-pick, revert ou blame. Conflitos aparecem
  sinalizados e podem ser marcados como resolvidos (preparando o arquivo), mas a edição
  em si é feita fora.
- Sem realce de sintaxe no diff (só realce de adição/remoção).
- A aba **Par** compara os commits pelo assunto, não por conteúdo.
