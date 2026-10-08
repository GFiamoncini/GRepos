#!/bin/sh
# Instala o GRepos para o usuário atual, sem root: o executável em ~/.local/bin, o ícone
# e o atalho do menu em ~/.local/share. Uso:
#
#   packaging/linux/instalar.sh caminho/do/GRepos
#
# O executável é o arquivo único da release (GRepos-<versão>-linux-x64-standalone) ou o
# de um "dotnet publish ... -p:PublishSingleFile=true". Rodar de novo atualiza.
set -eu

exe=${1:?informe o caminho do executável do GRepos}
aqui=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
bin=${XDG_BIN_HOME:-$HOME/.local/bin}
dados=${XDG_DATA_HOME:-$HOME/.local/share}

install -Dm755 "$exe" "$bin/grepos"
install -Dm644 "$aqui/../../app/Assets/app.png" "$dados/icons/hicolor/128x128/apps/grepos.png"

mkdir -p "$dados/applications"
sed "s|^Exec=.*|Exec=$bin/grepos|" "$aqui/grepos.desktop" > "$dados/applications/grepos.desktop"

# não é erro faltar: o menu só demora um pouco mais para enxergar o atalho
update-desktop-database "$dados/applications" 2>/dev/null || true
gtk-update-icon-cache -q "$dados/icons/hicolor" 2>/dev/null || true

echo "Instalado em $bin/grepos"
