#!/usr/bin/env bash
# Unity Hub ile repo dışında oluşturulan URP şablon projesinden Packages/ ve
# ProjectSettings/ klasörlerini repodaki AnkaraBusSimulator/ içine kopyalar.
# Repodaki Assets/, .gitignore ve .gitattributes dosyalarına dokunmaz.
#
# Kullanım (repo kökünden):
#   tools/unity/kurulum.sh ~/UnityTemp/AnkaraBusSimulator
set -euo pipefail

if [ $# -ne 1 ]; then
    echo "Kullanım: $0 <geçici URP projesinin yolu>" >&2
    exit 1
fi

kaynak="${1%/}"
repo="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
hedef="$repo/AnkaraBusSimulator"

case "$(cd "$kaynak" && pwd)" in
    "$repo"*) echo "Geçici proje repo dışında olmalı: $kaynak" >&2; exit 1 ;;
esac

for klasor in Packages ProjectSettings; do
    if [ ! -d "$kaynak/$klasor" ]; then
        echo "$kaynak/$klasor bulunamadı. Proje Unity Hub'da en az bir kez açılıp kapatıldı mı?" >&2
        exit 1
    fi
    if [ -e "$hedef/$klasor" ]; then
        echo "$hedef/$klasor zaten var, üzerine yazılmadı." >&2
        exit 1
    fi
done

cp -R "$kaynak/Packages" "$hedef/Packages"
cp -R "$kaynak/ProjectSettings" "$hedef/ProjectSettings"

git lfs install

echo "Kopyalandı: $hedef/Packages, $hedef/ProjectSettings"
echo "Şimdi Unity Hub → Add → Add project from disk → $hedef"
echo "Unity açılınca: menü Ankara Bus → Proje Ayarlarını Uygula"
